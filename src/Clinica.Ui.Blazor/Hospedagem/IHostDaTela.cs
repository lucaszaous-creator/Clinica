namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que a tela pede a quem a hospeda e que HTML não consegue fazer sozinho.
///
/// A tela não conhece o WPF: ela roda igual dentro do app (BlazorWebView) e na prévia do
/// navegador. Por isso salvar arquivo — que no app é a janela "Salvar como" do Windows e
/// o CSV no padrão do Excel em português — é pedido ao hospedeiro, que já tem a rotina
/// pronta (`ExportacaoCsv` e `ImpressaoPdf` do shell). Reescrevê-la aqui seria a segunda
/// definição de "como sai um CSV desta clínica".
/// </summary>
public interface IHostDaTela
{
    /// <summary>
    /// Grava a tabela como CSV. Devolve null quando deu certo (ou quando a pessoa desistiu
    /// da janela de salvar) e a frase do erro quando falhou.
    /// </summary>
    Task<string?> ExportarCsvAsync(
        IReadOnlyList<string> cabecalho,
        IReadOnlyList<IReadOnlyList<string>> linhas,
        string nomeArquivo);
}
