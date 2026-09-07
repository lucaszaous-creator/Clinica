using System.Runtime.CompilerServices;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Clinica.Tests;

/// <summary>
/// O interruptor que faz a MESMA suíte rodar contra o banco da clínica.
///
/// Os testes montam o banco com <c>new SqliteConnection("DataSource=:memory:")</c> +
/// <c>UseSqlite(conn)</c> + <c>EnsureCreated()</c> — em 147 arquivos, sem fixture comum.
/// O SQLite é a escolha certa para a velocidade da suíte, e é também a razão de três
/// famílias de defeito terem chegado à clínica com tudo verde: ele ignora o tamanho
/// declarado de uma coluna de texto (o 22001 do "Imprimir esta sessão"), não liga para
/// o <c>Kind</c> das datas (as seis colunas com fuso da parcela 52) e não tem
/// <c>xmin</c>. E nenhum teste executa uma MIGRATION: o schema do SQLite nasce do
/// modelo, por <c>EnsureCreated</c> — foi por aí que a FK para "UsuariosSistema"
/// (o nome da classe, não da tabela) derrubou a abertura do Consultório.
///
/// Com a variável de ambiente <c>CLINICA_TESTES_POSTGRES</c> apontando para um
/// Postgres (cadeia de conexão do banco administrativo, ex.
/// <c>Host=localhost;Username=postgres;Password=postgres;Database=postgres</c>), esta
/// sobrecarga de <c>UseSqlite</c> passa a:
///
///   1. criar UMA VEZ por processo um banco-modelo com TODAS as migrations aplicadas
///      (é isso que prova a migration escrita à mão, que o SQLite nunca executa);
///   2. dar a CADA teste um banco próprio, copiado do modelo
///      (<c>CREATE DATABASE … TEMPLATE …</c> — dezenas de milissegundos, sem re-migrar);
///   3. apagar o banco do teste quando a <see cref="SqliteConnection"/> dele é descartada
///      — é o único gancho comum a todos os arquivos, e os que não descartam são
///      limpos na abertura da rodada seguinte e na saída do processo.
///
/// ⚠️ Por que a sobrecarga tem o MESMO nome do método do EF, em vez de um helper
/// <c>BancoDeTeste.Abrir()</c> que os testes chamariam: helper é contrato que depende
/// de alguém lembrar, e o teste escrito daqui a seis meses com o padrão de sempre
/// ficaria fora da rede sem ninguém notar. Aqui ele entra sozinho. A resolução é
/// determinística: esta classe mora em <c>Clinica.Tests</c>, o namespace de todo
/// teste, e o C# procura métodos de extensão do namespace mais interno para fora —
/// a do EF, que chega pelo <c>using</c> do arquivo, só é considerada se esta não
/// servir. Sem a variável de ambiente ela delega para a do EF, byte a byte.
///
/// O <c>EnsureCreated()</c> que os testes chamam em seguida é inofensivo nos dois
/// mundos: no Postgres o banco copiado já tem as tabelas e ele não faz nada.
/// </summary>
public static class BancoDosTestes
{
    public const string VariavelDeAmbiente = "CLINICA_TESTES_POSTGRES";

    private const string Prefixo = "ct_";
    private static readonly string? CadeiaAdmin = Ler();
    private static readonly Lazy<string> Modelo = new(CriarModelo);
    private static int _sequencia;

    /// <summary>
    /// A MESMA conexão do SQLite é o MESMO banco no Postgres. Vários testes abrem um
    /// segundo <c>DbContext</c> sobre a conexão do fixture — o "escopo separado, como em
    /// produção" da parcela 68, o interceptor de lentidão da 74, o contador de consultas
    /// da retenção. No SQLite os dois contextos enxergam a mesma memória; aqui, sem esta
    /// tabela, cada chamada criaria um banco novo e o segundo contexto leria o vazio.
    /// </summary>
    private static readonly ConditionalWeakTable<SqliteConnection, string> BancoDaConexao = new();

    /// <summary>Verdadeiro quando a suíte está rodando contra o Postgres.</summary>
    public static bool NoPostgres => CadeiaAdmin is not null;

    public static DbContextOptionsBuilder<ClinicaDbContext> UseSqlite(
        this DbContextOptionsBuilder<ClinicaDbContext> builder, SqliteConnection conexao)
    {
        if (CadeiaAdmin is null)
            return SqliteDbContextOptionsBuilderExtensions.UseSqlite(builder, conexao);

        if (BancoDaConexao.TryGetValue(conexao, out var existente))
            return builder.UseNpgsql(ComBanco(existente));

        var nome = $"{Prefixo}{Environment.ProcessId}_{Interlocked.Increment(ref _sequencia)}";
        Admin($"CREATE DATABASE \"{nome}\" TEMPLATE \"{Modelo.Value}\"");
        BancoDaConexao.Add(conexao, nome);

        var cadeia = ComBanco(nome);
        // O único gancho comum aos 147 arquivos: quem descarta a conexão do SQLite
        // (todo `Dispose()` de fixture e todo `using var conn`) leva o banco junto.
        conexao.Disposed += (_, _) => Apagar(nome, cadeia);

        return builder.UseNpgsql(cadeia);
    }

    private static string? Ler()
    {
        var v = Environment.GetEnvironmentVariable(VariavelDeAmbiente);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static string CriarModelo()
    {
        // Sobras de uma rodada anterior interrompida (o processo morreu antes do
        // ProcessExit): apaga tudo o que tem o prefixo antes de começar.
        foreach (var sobra in Listar())
            Admin($"DROP DATABASE IF EXISTS \"{sobra}\" WITH (FORCE)");

        var nome = $"{Prefixo}modelo_{Environment.ProcessId}";
        Admin($"CREATE DATABASE \"{nome}\"");

        var cadeia = ComBanco(nome);
        using (var db = new ClinicaDbContext(
                   new DbContextOptionsBuilder<ClinicaDbContext>().UseNpgsql(cadeia).Options))
        {
            // As migrations, na ordem do ID — exatamente o que o MigrateAsync da
            // abertura do app faz na clínica.
            db.Database.Migrate();
        }

        // CREATE DATABASE … TEMPLATE recusa modelo com sessão aberta: devolve ao
        // Postgres as conexões que o EF deixou no pool.
        NpgsqlConnection.ClearPool(new NpgsqlConnection(cadeia));

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                foreach (var sobra in Listar())
                    Admin($"DROP DATABASE IF EXISTS \"{sobra}\" WITH (FORCE)");
            }
            catch
            {
                // Limpeza de saída: o próximo processo apaga o que sobrar.
            }
        };

        return nome;
    }

    private static void Apagar(string nome, string cadeia)
    {
        try
        {
            NpgsqlConnection.ClearPool(new NpgsqlConnection(cadeia));
            Admin($"DROP DATABASE IF EXISTS \"{nome}\" WITH (FORCE)");
        }
        catch
        {
            // O teste já terminou; o banco que sobrar cai na limpeza da próxima rodada.
        }
    }

    private static List<string> Listar()
    {
        using var conn = new NpgsqlConnection(CadeiaAdmin);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT datname FROM pg_database WHERE datname LIKE @p AND NOT datistemplate";
        cmd.Parameters.AddWithValue("p", Prefixo + "%");
        using var r = cmd.ExecuteReader();
        var lista = new List<string>();
        while (r.Read()) lista.Add(r.GetString(0));
        return lista;
    }

    private static void Admin(string sql)
    {
        using var conn = new NpgsqlConnection(CadeiaAdmin);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string ComBanco(string nome)
        => new NpgsqlConnectionStringBuilder(CadeiaAdmin) { Database = nome }.ConnectionString;
}
