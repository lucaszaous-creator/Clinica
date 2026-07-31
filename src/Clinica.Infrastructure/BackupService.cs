using System.Data.Common;
using System.Text.Json;
using Clinica.Application;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>Quantas linhas uma tabela tinha no momento do backup.</summary>
public sealed record TabelaBackup(string Nome, int Linhas);

/// <summary>
/// O que o arquivo de backup contém, sem precisar abri-lo inteiro.
///
/// Existe para o backup poder ser CONFERIDO. Arquivo de 40 MB que ninguém sabe se
/// prestou é o mesmo que não ter backup — a clínica só descobre no dia em que precisa.
/// </summary>
public sealed record ManifestoBackup(
    DateTime GeradoEm,
    string Origem,
    int TotalTabelas,
    int TotalLinhas,
    IReadOnlyList<TabelaBackup> Tabelas)
{
    /// <summary>Base sem nenhuma linha: backup tecnicamente válido e praticamente inútil.</summary>
    public bool Vazio => TotalLinhas == 0;

    public TabelaBackup? Tabela(string nome)
        => Tabelas.FirstOrDefault(t => string.Equals(t.Nome, nome, StringComparison.OrdinalIgnoreCase));
}

/// <summary>O que a restauração de fato fez.</summary>
public sealed record ResultadoRestauracao(
    int TabelasRestauradas,
    int LinhasRestauradas,
    IReadOnlyList<string> Avisos)
{
    public bool TudoCerto => Avisos.Count == 0;
}

/// <summary>
/// Backup e restauração da base inteira (parcela 34).
///
/// Por que existe
/// --------------
/// O banco é remoto (Neon) e a clínica não tem cópia própria de nada. Existia um
/// `BackupLocal` — mas só dentro do faturamento, e ele salva **sete tabelas de
/// quarenta e nove**: pacientes, atendimentos, agendamentos, consultas, parâmetros,
/// convênios e configurações. Foi escrito quando o faturamento era o produto inteiro.
/// Prontuário, evolução, documento clínico, financeiro, pacotes, estoque, repasses,
/// acessos e auditoria **não estão em backup nenhum**, e a suíte inteira roda sem
/// nenhum.
///
/// E não havia restauração em lugar algum. **Backup que ninguém sabe restaurar é
/// falsa segurança**, que é pior que a ausência dele: a clínica acha que está coberta
/// e não está.
///
/// Três decisões
/// -------------
/// 1. **As tabelas saem do MODELO do EF, não de `information_schema`.** Assim o backup
///    é do que o aplicativo é dono — e não do que por acaso existe no banco —, funciona
///    igual no PostgreSQL e no SQLite dos testes, e o `__EFMigrationsHistory` fica de
///    fora de propósito: restaurá-lo faria o EF acreditar que migrations já aplicadas
///    não precisam rodar.
/// 2. **Os valores são lidos como TEXTO, pelo leitor cru.** É o mesmo motivo do backup
///    pré-migration do faturamento: o modelo em memória pode esperar colunas que o
///    banco ainda não tem, e o backup precisa funcionar justamente quando o schema está
///    fora de passo.
/// 3. **A restauração só entra em base VAZIA.** Restaurar por cima misturaria o de
///    ontem com o de hoje sem ninguém conseguir distinguir depois, e não tem volta.
///    Base com dados é recusada — quem quer trocar o conteúdo apaga a base antes, e
///    esse é um ato que merece ser consciente.
/// </summary>
public sealed class BackupService
{
    private readonly ClinicaDbContext _db;

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = false
    };

    public BackupService(ClinicaDbContext db) => _db = db;

    /// <summary>Nome de cada tabela do modelo, em ordem de dependência (pai antes de filho).</summary>
    private IReadOnlyList<string> TabelasEmOrdem()
    {
        var tipos = _db.Model.GetEntityTypes()
            .Where(t => t.GetTableName() is not null)
            .ToList();

        var nomePorTipo = tipos.ToDictionary(t => t, t => t.GetTableName()!);
        var visitado = new HashSet<string>();
        var ordem = new List<string>();

        void Visitar(Microsoft.EntityFrameworkCore.Metadata.IEntityType t, HashSet<string> caminho)
        {
            var nome = nomePorTipo[t];
            if (!visitado.Add(nome)) return;

            foreach (var fk in t.GetForeignKeys())
            {
                var pai = fk.PrincipalEntityType;
                if (!nomePorTipo.TryGetValue(pai, out var nomePai)) continue;
                // Auto-referência e ciclo: a linha filha entra depois, e o banco resolve
                // porque a FK é anulável nesses casos. Cortar aqui evita recursão infinita.
                if (nomePai == nome || !caminho.Add(nomePai)) continue;
                Visitar(pai, caminho);
                caminho.Remove(nomePai);
            }

            ordem.Add(nome);
        }

        foreach (var t in tipos)
            Visitar(t, []);

        return ordem;
    }

    private static string Aspas(string identificador)
        => "\"" + identificador.Replace("\"", "\"\"") + "\"";

    private async Task<DbConnection> AbrirAsync(CancellationToken ct)
    {
        var conexao = _db.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
            await conexao.OpenAsync(ct);
        return conexao;
    }

    // ==================================================================== gerar

    /// <summary>
    /// Escreve o backup completo em <paramref name="destino"/> e devolve o manifesto.
    ///
    /// O chamador decide onde gravar (pasta local, pendrive, nuvem) — o serviço não
    /// escolhe caminho, porque "onde fica a cópia" é decisão da clínica e não do código.
    /// </summary>
    public async Task<ManifestoBackup> GerarAsync(Stream destino, CancellationToken ct = default)
    {
        var conexao = await AbrirAsync(ct);
        var tabelas = TabelasEmOrdem();

        var dump = new Dictionary<string, List<Dictionary<string, string?>>>();
        var resumo = new List<TabelaBackup>();

        foreach (var tabela in tabelas)
        {
            var linhas = new List<Dictionary<string, string?>>();

            using var cmd = conexao.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {Aspas(tabela)}";

            using var leitor = await cmd.ExecuteReaderAsync(ct);
            while (await leitor.ReadAsync(ct))
            {
                var linha = new Dictionary<string, string?>();
                for (var i = 0; i < leitor.FieldCount; i++)
                    linha[leitor.GetName(i)] = leitor.IsDBNull(i)
                        ? null
                        : Converter(leitor.GetValue(i));
                linhas.Add(linha);
            }

            dump[tabela] = linhas;
            resumo.Add(new TabelaBackup(tabela, linhas.Count));
        }

        var manifesto = new ManifestoBackup(
            DateTime.Now,
            _db.Database.ProviderName ?? "?",
            resumo.Count,
            resumo.Sum(t => t.Linhas),
            resumo);

        await JsonSerializer.SerializeAsync(
            destino, new ArquivoBackup(manifesto, dump), Opcoes, ct);
        await destino.FlushAsync(ct);

        return manifesto;
    }

    /// <summary>
    /// Texto que volta a ser o mesmo valor na restauração.
    ///
    /// Data e hora saem em ISO com precisão: o formato da máquina transformaria
    /// 03/08 em 08/03 na hora de restaurar num computador com outra cultura, e a
    /// clínica só descobriria isso depois de já ter perdido a base.
    /// </summary>
    private static string Converter(object valor) => valor switch
    {
        DateTime d => d.ToString("O"),
        DateTimeOffset d => d.ToString("O"),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        TimeOnly t => t.ToString("HH:mm:ss.fffffff"),
        bool b => b ? "true" : "false",
        byte[] bytes => Convert.ToBase64String(bytes),
        decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
        double d => d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        float f => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        _ => valor.ToString() ?? string.Empty
    };

    private sealed record ArquivoBackup(
        ManifestoBackup Manifesto,
        Dictionary<string, List<Dictionary<string, string?>>> Tabelas);

    // ================================================================= conferir

    /// <summary>
    /// Lê o manifesto sem restaurar nada — é a conferência que faz o backup valer.
    ///
    /// Um arquivo que não abre, ou que abre e está vazio, precisa ser descoberto HOJE,
    /// e não no dia em que a base sumiu.
    /// </summary>
    public async Task<ManifestoBackup> ConferirAsync(Stream origem, CancellationToken ct = default)
    {
        var arquivo = await LerAsync(origem, ct);

        // O manifesto é gravado junto, mas quem manda é a CONTAGEM REAL das linhas:
        // manifesto e conteúdo podem divergir se o arquivo foi truncado no meio, e é
        // exatamente esse caso que a conferência existe para pegar.
        var real = arquivo.Tabelas
            .Select(t => new TabelaBackup(t.Key, t.Value.Count))
            .OrderBy(t => t.Nome)
            .ToList();

        return arquivo.Manifesto with
        {
            TotalTabelas = real.Count,
            TotalLinhas = real.Sum(t => t.Linhas),
            Tabelas = real
        };
    }

    private static async Task<ArquivoBackup> LerAsync(Stream origem, CancellationToken ct)
    {
        var arquivo = await JsonSerializer.DeserializeAsync<ArquivoBackup>(origem, Opcoes, ct)
            ?? throw new InvalidOperationException(
                "O arquivo não parece ser um backup deste sistema.");

        if (arquivo.Tabelas is null)
            throw new InvalidOperationException(
                "O arquivo está incompleto — não há tabela nenhuma dentro dele.");

        return arquivo;
    }

    // ================================================================ restaurar

    /// <summary>
    /// Restaura o backup numa base VAZIA.
    /// </summary>
    /// <remarks>
    /// Recusa base com dados, e isso não é excesso de zelo: restaurar por cima
    /// misturaria o de ontem com o de hoje sem ninguém conseguir separar depois, e não
    /// tem volta. Quem quer trocar o conteúdo apaga a base antes — um ato que merece
    /// ser consciente, feito por quem sabe o que está fazendo.
    ///
    /// A ordem de inserção é a de dependência (pai antes de filho), pela mesma razão
    /// que o banco tem chave estrangeira. Tabela do arquivo que não existe mais no
    /// modelo vira AVISO, não erro: um backup antigo continua restaurável, e o que ele
    /// tem a mais é informação — perder a restauração inteira por causa de uma tabela
    /// que foi removida numa versão posterior seria trocar o essencial pelo detalhe.
    /// </remarks>
    public async Task<ResultadoRestauracao> RestaurarAsync(
        Stream origem, bool confirmado, string? operador = null, CancellationToken ct = default)
    {
        if (!confirmado)
            throw new InvalidOperationException(
                "A restauração substitui a base inteira. Confirme explicitamente para continuar.");

        var arquivo = await LerAsync(origem, ct);
        var conexao = await AbrirAsync(ct);
        var ordem = TabelasEmOrdem();

        await RecusarSeTiverDadosAsync(conexao, ordem, ct);

        var avisos = new List<string>();
        var tabelasFeitas = 0;
        var linhasFeitas = 0;

        foreach (var tabela in ordem)
        {
            if (!arquivo.Tabelas.TryGetValue(tabela, out var linhas))
            {
                // Tabela nova, criada depois do backup: nasce vazia, como nasceria numa
                // instalação limpa. Dizer isso é melhor do que ficar calado.
                avisos.Add($"{tabela}: não existia no backup — restaurada vazia.");
                continue;
            }

            if (linhas.Count == 0) continue;

            foreach (var linha in linhas)
            {
                using var cmd = conexao.CreateCommand();
                var colunas = linha.Keys.ToList();

                cmd.CommandText =
                    $"INSERT INTO {Aspas(tabela)} " +
                    $"({string.Join(", ", colunas.Select(Aspas))}) VALUES " +
                    $"({string.Join(", ", colunas.Select((_, i) => "@p" + i))})";

                for (var i = 0; i < colunas.Count; i++)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@p" + i;
                    p.Value = (object?)linha[colunas[i]] ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }

                await cmd.ExecuteNonQueryAsync(ct);
                linhasFeitas++;
            }

            tabelasFeitas++;
        }

        foreach (var sobra in arquivo.Tabelas.Keys.Where(t => !ordem.Contains(t)))
            avisos.Add(
                $"{sobra}: existia no backup e não existe mais nesta versão — não foi restaurada.");

        await ReiniciarSequenciasAsync(conexao, ordem, ct);

        _db.Auditoria.Add(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "BaseRestaurada",
            Detalhe = $"Backup de {arquivo.Manifesto.GeradoEm:dd/MM/yyyy HH:mm} — "
                      + $"{tabelasFeitas} tabela(s), {linhasFeitas} linha(s)."
        });
        await _db.SaveChangesAsync(ct);

        return new ResultadoRestauracao(tabelasFeitas, linhasFeitas, avisos);
    }

    private async Task RecusarSeTiverDadosAsync(
        DbConnection conexao, IReadOnlyList<string> tabelas, CancellationToken ct)
    {
        foreach (var tabela in tabelas)
        {
            using var cmd = conexao.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {Aspas(tabela)}";

            var total = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct) ?? 0L);
            if (total > 0)
                throw new InvalidOperationException(
                    $"A base não está vazia ({tabela} tem {total} registro(s)). "
                    + "Restaurar por cima misturaria os dados sem volta — "
                    + "esvazie a base antes de restaurar.");
        }
    }

    /// <summary>
    /// Reposiciona as sequências de identidade depois da inserção com Id explícito.
    ///
    /// Sem isto o PostgreSQL continuaria contando do 1 e o próximo paciente cadastrado
    /// colidiria com um Id restaurado — a base voltaria e quebraria no primeiro
    /// cadastro, que é o pior momento possível para descobrir.
    ///
    /// É específico do PostgreSQL; no SQLite dos testes não há o que reposicionar, e a
    /// falha é engolida COM RASTRO em vez de derrubar uma restauração que já terminou.
    /// </summary>
    private async Task ReiniciarSequenciasAsync(
        DbConnection conexao, IReadOnlyList<string> tabelas, CancellationToken ct)
    {
        if (!_db.Database.IsNpgsql()) return;

        foreach (var tabela in tabelas)
        {
            try
            {
                using var cmd = conexao.CreateCommand();
                cmd.CommandText =
                    $"SELECT setval(pg_get_serial_sequence('{tabela.Replace("'", "''")}', 'Id'), " +
                    $"COALESCE((SELECT MAX(\"Id\") FROM {Aspas(tabela)}), 1), true) " +
                    $"WHERE pg_get_serial_sequence('{tabela.Replace("'", "''")}', 'Id') IS NOT NULL";
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                Diagnostico.Registrar(
                    $"Restauração — sequência de identidade de {tabela} não pôde ser reposicionada", ex);
            }
        }
    }
}
