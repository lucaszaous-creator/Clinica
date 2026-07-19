using System.IO;

namespace Clinica.Desktop.Configuracao;

/// <summary>
/// Log de erros em arquivo para suporte em produção:
/// %APPDATA%\ClinicaFaturamento\logs\erros-AAAA-MM.log (um arquivo por mês).
/// Nunca lança — um log que falha não pode derrubar o app.
/// </summary>
public static class LogErros
{
    private static readonly object Trava = new();

    private static string Pasta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClinicaFaturamento", "logs");

    public static void Registrar(string contexto, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Pasta);
            var arquivo = Path.Combine(Pasta, $"erros-{DateTime.Now:yyyy-MM}.log");
            var linha = $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {contexto}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}";
            lock (Trava)
                File.AppendAllText(arquivo, linha);
        }
        catch
        {
            // Sem disco/permissão: segue sem log.
        }
    }
}
