using System.Diagnostics;
using System.IO;

namespace Clinica.Recepcao.Servicos;

/// <summary>
/// Salvar um PDF em disco e abrir no leitor do Windows — o mesmo gesto em toda a
/// Recepção (documentos clínicos hoje, o que vier depois amanhã).
///
/// Existe para que a falha de ABRIR não seja confundida com a falha de GERAR: o arquivo
/// já está gravado quando o leitor não abre, e dizer "não foi possível gerar" mandaria
/// a secretária refazer um documento que existe. Por isso as duas etapas devolvem
/// mensagens diferentes.
/// </summary>
public static class ImpressaoPdf
{
    /// <summary>
    /// Pergunta onde salvar, grava e abre. Devolve null quando deu tudo certo (ou
    /// quando o usuário desistiu do diálogo) e a mensagem de erro quando não deu.
    /// </summary>
    public static async Task<string?> SalvarEAbrirAsync(byte[] pdf, string nomeSugerido)
    {
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            FileName = nomeSugerido,
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf"
        };
        if (dialogo.ShowDialog() != true) return null;

        try
        {
            await File.WriteAllBytesAsync(dialogo.FileName, pdf);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Recepção — PDF não pôde ser gravado", ex);
            return $"Não foi possível salvar o arquivo: {ex.Message}";
        }

        try
        {
            Process.Start(new ProcessStartInfo(dialogo.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // Degradação com rastro: o documento está gravado, só o leitor não abriu.
            Clinica.Application.Diagnostico.Registrar("Recepção — PDF não pôde ser aberto", ex);
            return $"O documento foi salvo em {dialogo.FileName}, mas o leitor de PDF não abriu.";
        }

        return null;
    }

    /// <summary>Nome de arquivo sem os caracteres que o Windows recusa.</summary>
    public static string NomeSeguro(string bruto)
    {
        var limpo = bruto.Trim();
        foreach (var proibido in Path.GetInvalidFileNameChars())
            limpo = limpo.Replace(proibido, '-');
        return limpo;
    }
}
