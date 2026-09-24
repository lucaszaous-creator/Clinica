using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clinica.Application.Modelos;

namespace Clinica.Desktop.Configuracao;

/// <summary>
/// Espelho local (JSON em %APPDATA%) da lista de baixas pendentes, atualizado sempre que
/// as pendências são carregadas. Se a internet ou o banco na nuvem caírem, a tela mais
/// importante do sistema — o que precisa ser faturado HOJE — continua visível, ainda que
/// somente leitura e com os dados da última sincronização.
/// </summary>
public static class PendenciasSnapshot
{
    public sealed record Snapshot(DateTime GeradoEm, IReadOnlyList<PendenciaCodigo> Pendencias);

    private static string Arquivo => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClinicaFaturamento", "pendencias-snapshot.json");

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Grava o snapshot (atômico: temporário + rename). Nunca lança.</summary>
    public static void Salvar(IReadOnlyList<PendenciaCodigo> pendencias)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
            var temp = Arquivo + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new Snapshot(DateTime.Now, pendencias), Opcoes));
            File.Move(temp, Arquivo, overwrite: true);
        }
        catch (Exception ex)
        {
            LogErros.Registrar("Snapshot local de pendências", ex);
        }
    }

    /// <summary>Último snapshot salvo, ou nulo se nunca houve / arquivo corrompido.</summary>
    public static Snapshot? Carregar()
    {
        try
        {
            if (!File.Exists(Arquivo)) return null;
            return JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(Arquivo), Opcoes);
        }
        catch (Exception ex)
        {
            LogErros.Registrar("Leitura do snapshot de pendências", ex);
            return null;
        }
    }
}
