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

    private static void LimparAntigos()
    {
        var arquivos = Directory.GetFiles(Pasta, "backup-*.json")
            .OrderByDescending(f => f)
            .Skip(ManterUltimos);
        foreach (var f in arquivos)
            try { File.Delete(f); } catch { /* backup antigo preso não é problema */ }
    }
}
