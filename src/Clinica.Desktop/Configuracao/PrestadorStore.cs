using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clinica.Application.Modelos;

namespace Clinica.Desktop.Configuracao;

/// <summary>
/// LEGADO: arquivo JSON local em %APPDATA% usado antes da configuração global no banco.
/// Mantido apenas para a importação única na abertura (App.OnStartup); a fonte da
/// verdade agora é ParametrosService.ObterPrestadorAsync (tabela Configuracoes).
/// </summary>
public static class PrestadorStore
{
    private static string Pasta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClinicaFaturamento");

    private static string Arquivo => Path.Combine(Pasta, "prestador.json");

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static DadosPrestador Carregar()
    {
        try
        {
            if (File.Exists(Arquivo))
                return JsonSerializer.Deserialize<DadosPrestador>(File.ReadAllText(Arquivo), Opcoes) ?? new DadosPrestador();
        }
        catch
        {
            // Config corrompida não deve impedir o uso.
        }
        return new DadosPrestador();
    }

    public static void Salvar(DadosPrestador dados)
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Arquivo, JsonSerializer.Serialize(dados, Opcoes));
    }
}
