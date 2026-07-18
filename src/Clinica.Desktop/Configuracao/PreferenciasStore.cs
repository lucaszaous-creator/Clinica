using System.IO;
using System.Text.Json;

namespace Clinica.Desktop.Configuracao;

/// <summary>Preferências da clínica que não são regra de convênio (por máquina, JSON em %APPDATA%).</summary>
public sealed class PreferenciasLocais
{
    /// <summary>Dias antes do vencimento em que a consulta entra em alerta (painel/aviso).</summary>
    public int JanelaAlertaConsultaDias { get; set; } = 5;
}

public static class PreferenciasStore
{
    private static string Pasta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClinicaFaturamento");

    private static string Arquivo => Path.Combine(Pasta, "preferencias.json");

    public static PreferenciasLocais Carregar()
    {
        try
        {
            if (File.Exists(Arquivo))
                return JsonSerializer.Deserialize<PreferenciasLocais>(File.ReadAllText(Arquivo)) ?? new PreferenciasLocais();
        }
        catch
        {
            // Preferência corrompida não deve impedir o uso.
        }
        return new PreferenciasLocais();
    }

    public static void Salvar(PreferenciasLocais prefs)
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Arquivo, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
    }
}
