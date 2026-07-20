using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Configuracao;

/// <summary>
/// Backup local automático: uma vez por dia, na abertura, grava um espelho de todas as
/// tabelas em JSON em %APPDATA%\ClinicaFaturamento\backups (mantém os últimos 14).
/// O banco fica na nuvem (Neon); este é o plano B da clínica se a conta sumir ou a
/// internet cair de vez — os dados do dia anterior estão sempre na máquina.
/// </summary>
public static class BackupLocal
{
    private const int ManterUltimos = 14;

    private static string Pasta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClinicaFaturamento", "backups");

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Executa o backup do dia se ainda não existir. Nunca lança (roda em segundo plano).</summary>
    public static async Task ExecutarSeNecessarioAsync(IServiceProvider servicos)
    {
        try
        {
            Directory.CreateDirectory(Pasta);
            var arquivoHoje = Path.Combine(Pasta, $"backup-{DateTime.Today:yyyy-MM-dd}.json");
            if (File.Exists(arquivoHoje)) return; // já feito hoje

            using var scope = servicos.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();

            var dados = new
            {
                GeradoEm = DateTime.Now,
                Pacientes = await db.Pacientes.AsNoTracking().ToListAsync(),
                Atendimentos = await db.Atendimentos.AsNoTracking().Include(a => a.Codigos).ToListAsync(),
                Agendamentos = await db.Agendamentos.AsNoTracking().ToListAsync(),
                Consultas = await db.Consultas.AsNoTracking().ToListAsync(),
                Parametros = await db.Parametros.AsNoTracking().ToListAsync(),
                Convenios = await db.Convenios.AsNoTracking().ToListAsync(),
                Configuracoes = await db.Configuracoes.AsNoTracking().ToListAsync()
            };

            // Grava em temporário e renomeia: nunca fica um backup pela metade.
            var temp = arquivoHoje + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(dados, Opcoes));
            File.Move(temp, arquivoHoje, overwrite: true);

            LimparAntigos();
        }
        catch (Exception ex)
        {
            LogErros.Registrar("Backup local diário", ex);
        }
    }

    /// <summary>
    /// Backup ANTES de aplicar migrations pendentes — se a migration falhar no meio,
    /// os dados de ontem estão na máquina. Lê as tabelas via SQL cru (SELECT *), e não
    /// pelo modelo do EF: o modelo novo pode esperar colunas que só existirão DEPOIS
    /// da migration, e o backup precisa funcionar justamente com o schema antigo.
    /// Nunca lança (uma falha aqui não pode impedir o app de abrir).
    /// </summary>
    public static async Task ExecutarPreMigracaoAsync(ClinicaDbContext db)
    {
        try
        {
            Directory.CreateDirectory(Pasta);
            var arquivo = Path.Combine(Pasta, $"backup-pre-migracao-{DateTime.Now:yyyy-MM-dd-HHmmss}.json");

            var conexao = db.Database.GetDbConnection();
            if (conexao.State != System.Data.ConnectionState.Open)
                await conexao.OpenAsync();

            var tabelas = new List<string>();
            using (var cmd = conexao.CreateCommand())
            {
                cmd.CommandText = "SELECT table_name FROM information_schema.tables " +
                                  "WHERE table_schema = 'public' AND table_type = 'BASE TABLE'";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    tabelas.Add(reader.GetString(0));
            }

            var dump = new Dictionary<string, List<Dictionary<string, object?>>>();
            foreach (var tabela in tabelas)
            {
                var linhas = new List<Dictionary<string, object?>>();
                using var cmd = conexao.CreateCommand();
                cmd.CommandText = $"SELECT * FROM \"{tabela.Replace("\"", "\"\"")}\"";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var linha = new Dictionary<string, object?>();
                    for (var i = 0; i < reader.FieldCount; i++)
                        linha[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
                    linhas.Add(linha);
                }
                dump[tabela] = linhas;
            }

            var temp = arquivo + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(new { GeradoEm = DateTime.Now, Tabelas = dump }, Opcoes));
            File.Move(temp, arquivo, overwrite: true);
        }
        catch (Exception ex)
        {
            LogErros.Registrar("Backup pré-migration", ex);
        }
    }

    private static void LimparAntigos()
    {
        var arquivos = Directory.GetFiles(Pasta, "backup-*.json")
            .OrderByDescending(f => f)
            .Skip(ManterUltimos);
        foreach (var f in arquivos)
            try { File.Delete(f); } catch { /* backup antigo preso não é problema */ }
    }
}
