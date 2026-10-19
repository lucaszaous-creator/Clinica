namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// O que a tela pede a quem a hospeda e que HTML não consegue fazer sozinho.
///
/// A tela não conhece o WPF: ela roda igual dentro do app (BlazorWebView) e na prévia do
/// navegador. Por isso salvar e escolher arquivo, copiar para a área de transferência e
/// navegar para a tela de outro módulo são pedidos ao hospedeiro, que já tem as rotinas
/// prontas (`ExportacaoCsv`, `ImpressaoPdf` e `NavegacaoSuite` do shell). Reescrevê-las aqui
/// seria a segunda definição de "como sai um arquivo desta clínica".
///
/// ⚠️ Confirmação, pergunta e aviso NÃO estão aqui: eles são HTML (<see cref="Dialogos"/>),
/// para o diálogo ter a mesma cara da tela que o abriu. Só o que o navegador não alcança —
/// o disco, a área de transferência, a navegação do shell — passa pelo hospedeiro.
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

    /// <summary>
    /// Pergunta onde salvar, grava e ABRE (PDF de recibo, de relatório). Null = deu certo
    /// ou a pessoa desistiu; a frase = falhou. Falha de ABRIR não se confunde com falha de
    /// GRAVAR: o arquivo já está no disco, e a frase diz onde.
    /// </summary>
    Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao);

    /// <summary>Pede um arquivo existente para LER (extrato OFX). Null = desistiu.</summary>
    Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo);

    /// <summary>Copia o texto. Falso quando a área de transferência não aceitou — nunca diga "copiado" sem ter copiado.</summary>
    Task<bool> CopiarAsync(string texto);

    /// <summary>
    /// Abre outra tela da suíte pela chave (`ChavesSuite`). Falso quando o destino não existe
    /// neste executável — e quem chama DIZ isso, porque botão que não navega em silêncio é o
    /// defeito da parcela 37.
    /// </summary>
    Task<bool> NavegarAsync(string chave);

    /// <summary>
    /// Volta para a tela de onde se veio (o histórico do shell). Falso = não há para onde
    /// voltar (ou o hospedeiro não tem histórico — a prévia, os testes); quem chama escolhe
    /// um destino. É o caminho de volta da ficha aberta por cima de outra tela.
    /// </summary>
    Task<bool> VoltarAsync() => Task.FromResult(false);

    /// <summary>
    /// Abre um endereço FORA do app (o wa.me da cobrança, o validador público): no app vai
    /// para o navegador padrão do Windows — a tela nunca navega para fora de si mesma.
    /// Falso quando não abriu; quem chama oferece copiar o endereço.
    /// </summary>
    Task<bool> AbrirEnderecoAsync(string url);

    /// <summary>
    /// Abre a FICHA do paciente (a do shell, a mesma da Recepção) por cima da tela, e volta
    /// a ela ao fechar — a pendência do faturamento leva à ficha sem perder o filtro do painel.
    /// Falso quando a ficha não existe neste executável (a prévia) — quem chama DIZ isso.
    /// A barreira (<c>VerFichaPaciente</c>) é de quem chama, como toda segunda barreira.
    /// </summary>
    Task<bool> AbrirFichaDoPacienteAsync(int pacienteId);

    /// <summary>
    /// Pergunta ONDE salvar sem gravar nada ainda — para o ato que gasta um número só
    /// acontecer depois de a pessoa escolher o destino (o lote TISS consome a sequência da
    /// ANS: desistir da janela não pode ter gasto o número). Null = desistiu.
    /// </summary>
    Task<string?> PerguntarOndeSalvarAsync(string nomeSugerido, string filtro, string extensao);

    /// <summary>Pergunta uma PASTA (vários arquivos de uma vez: um XML por operadora). Null = desistiu.</summary>
    Task<string?> PerguntarPastaAsync(string titulo);

    /// <summary>Grava no caminho já escolhido. Null = gravou; a frase = falhou (o arquivo não está lá).</summary>
    Task<string?> GravarArquivoAsync(string caminho, byte[] conteudo);
}

/// <summary>Arquivo escolhido pela pessoa: o nome (para mostrar) e o conteúdo.</summary>
public sealed record ArquivoEscolhido(string Nome, byte[] Conteudo);
