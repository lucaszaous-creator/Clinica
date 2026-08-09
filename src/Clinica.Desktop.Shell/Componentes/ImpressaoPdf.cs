using System.Diagnostics;
using System.IO;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Salvar um PDF em disco e abrir no leitor do Windows — o mesmo gesto em toda a suíte
/// (documentos clínicos na Recepção, recibo e orçamento no Financeiro).
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
    public static Task<string?> SalvarEAbrirAsync(byte[] pdf, string nomeSugerido)
        => SalvarEAbrirAsync(pdf, nomeSugerido, "PDF (*.pdf)|*.pdf", ".pdf");

    /// <summary>
    /// A mesma coisa para qualquer arquivo — a exportação CSV dos relatórios usa esta.
    /// O que se compartilha aqui não é o formato, é a REGRA: falha de abrir não pode ser
    /// confundida com falha de gerar, porque o arquivo já está gravado e mandar refazer
    /// seria mentir sobre o que aconteceu.
    /// </summary>
    public static async Task<string?> SalvarEAbrirAsync(
        byte[] conteudo, string nomeSugerido, string filtro, string extensao)
    {
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            FileName = nomeSugerido,
            Filter = filtro,
            DefaultExt = extensao
        };
        if (dialogo.ShowDialog() != true) return null;

        try
        {
            await File.WriteAllBytesAsync(dialogo.FileName, conteudo);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Suíte — arquivo não pôde ser gravado", ex);
            return $"Não foi possível salvar o arquivo: {ex.Message}";
        }

        try
        {
            Process.Start(new ProcessStartInfo(dialogo.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // Degradação com rastro: o arquivo está gravado, só o programa não abriu.
            Clinica.Application.Diagnostico.Registrar("Suíte — arquivo não pôde ser aberto", ex);
            return $"O arquivo foi salvo em {dialogo.FileName}, mas não foi possível abri-lo.";
        }

        return null;
    }

    /// <summary>
    /// Grava um arquivo escolhido pelo usuário e NÃO o abre. Devolve o caminho gravado,
    /// ou <c>null</c> se a pessoa desistiu do diálogo; erro vem como exceção.
    /// </summary>
    /// <remarks>
    /// Existe ao lado de <see cref="SalvarEAbrirAsync"/> porque nem todo arquivo é para
    /// LER. Um backup da base é um JSON de dezenas de megabytes: abri-lo no bloco de
    /// notas depois de gravar não ajuda ninguém e ainda dá a impressão de que algo saiu
    /// errado. Aqui o sucesso é o arquivo existir no lugar escolhido.
    /// </remarks>
    public static async Task<string?> SalvarAsync(
        Func<Stream, Task> escrever, string nomeSugerido, string filtro, string extensao)
    {
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            FileName = nomeSugerido,
            Filter = filtro,
            DefaultExt = extensao
        };
        if (dialogo.ShowDialog() != true) return null;

        // Grava em temporário e renomeia: interrupção no meio nunca deixa no lugar um
        // arquivo pela metade com cara de backup bom.
        var temporario = dialogo.FileName + ".parcial";

        await using (var saida = File.Create(temporario))
            await escrever(saida);

        File.Move(temporario, dialogo.FileName, overwrite: true);
        return dialogo.FileName;
    }

    /// <summary>Pede um arquivo já existente para leitura. <c>null</c> = desistiu.</summary>
    public static string? Escolher(string filtro, string titulo)
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog { Filter = filtro, Title = titulo };
        return dialogo.ShowDialog() == true ? dialogo.FileName : null;
    }

    /// <summary>
    /// Pede uma PASTA. <c>null</c> = desistiu.
    ///
    /// Usa o <c>OpenFolderDialog</c> do WPF (.NET 8), que é o seletor nativo do Windows —
    /// e não o antigo <c>FolderBrowserDialog</c> do WinForms, que obrigaria a suíte a
    /// referenciar WinForms inteiro para desenhar uma árvore de 1998.
    /// </summary>
    public static string? EscolherPasta(string titulo)
    {
        var dialogo = new Microsoft.Win32.OpenFolderDialog { Title = titulo };
        return dialogo.ShowDialog() == true ? dialogo.FolderName : null;
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
