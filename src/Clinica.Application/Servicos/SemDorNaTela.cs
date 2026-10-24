using Clinica.Application.Abstracoes;
using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>A etiqueta do @SemDor numa linha da lista: o assunto (sem o ✨, que a tela desenha), se pede atenção já e a dica.</summary>
public sealed record EtiquetaDoSemDor(string Texto, bool Atencao, string Dica);

/// <summary>O que o ✨ faz com o campo: sugerir a resposta (campo vazio) ou melhorar o texto escrito.</summary>
public enum PedidoAoSemDor { Sugerir, Revisar }

/// <summary>
/// A sugestão aplicada ao campo de escrever. <see cref="Aplicada"/> falso: o campo fica como está e
/// <see cref="Aviso"/> diz por quê. <see cref="Anterior"/>: o texto dela antes da revisão (o ↶); nulo quando
/// o campo estava vazio — não há o que desfazer.
/// </summary>
public sealed record SugestaoNoCampo(bool Aplicada, string Texto, string? Anterior, string Aviso);

/// <summary>
/// O @SEMDOR DAS SECRETÁRIAS NA TELA DE WHATSAPP DOS APPS (24/10/2026 — semdor-crm/docs/semdor-secretarias.md,
/// docs/whatsapp-nativo-implementacao.md §10). A IA mora no CRM (a chave fica num servidor só); aqui mora o que a
/// tela AFIRMA sobre ela, num lugar só e com teste (<c>SemDorNaTelaTests</c>):
/// <list type="bullet">
/// <item><b>Desligada, sai inteira</b>: sem botão ✨, sem "Resumo", sem etiqueta (<see cref="Ligado"/>). Estado que
/// não pôde ser lido conta como desligado — a tela funciona como antes.</item>
/// <item><b>Nada é enviado</b>: a sugestão vai para o CAMPO; quem revisa e envia é a secretária (regra 3 do CRM).
/// Se ela continuou escrevendo enquanto a IA pensava, o texto dela vence (<see cref="Aplicar"/>).</item>
/// <item><b>Falhando, ela escreve normalmente</b>: a frase é a do CRM (409 desligado, 502 não respondeu).</item>
/// </list>
/// </summary>
public static class SemDorNaTela
{
    /// <summary>O que o CRM manda no lugar do motivo da falha para quem não é da gestão.</summary>
    public const string MotivoSoDaGestao = "Fale com a gestão.";

    /// <summary>A frase quando a IA devolveu nada (ou o pedido falhou sem frase).</summary>
    public const string SemResposta = "O @SemDor não respondeu agora. Escreva normalmente ou tente de novo em instantes.";

    public const string DicaDoAssunto = "Assunto lido pelo @SemDor na última mensagem. Some quando a clínica responde.";
    public const string DicaDaAtencao = "O @SemDor leu a última mensagem como algo que pede atenção já: sintoma grave, reação ao tratamento ou reclamação.";

    /// <summary>"Leitura automática": o resumo é da IA, não da clínica.</summary>
    public const string RodapeDoResumo = "Leitura automática da conversa. Confira antes de agir.";

    /// <summary>Os botões e as etiquetas aparecem? Só com o CRM dizendo que a IA está ligada.</summary>
    public static bool Ligado(AppSemDorState? estado) => estado is { Ligada: true };

    /// <summary>
    /// A etiqueta da linha: só com a IA ligada, a conversa aberta e o paciente esperando a clínica (a última
    /// mensagem é dele). O CRM já zera quando a clínica responde; conferir de novo aqui cobre a linha que a
    /// tela atualizou na hora (<see cref="EtiquetaDepoisDaEscrita"/>) antes da próxima releitura.
    /// </summary>
    public static EtiquetaDoSemDor? Etiqueta(AppSemDorState? estado, AppConversationSummary c)
    {
        if (!Ligado(estado) || c.Semdor is not { } t || c.Closed || !string.Equals(c.PreviewDirection, "in", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(t.Rotulo))
            return null;
        return new EtiquetaDoSemDor(t.Rotulo.Trim(), t.Atencao, t.Atencao ? DicaDaAtencao : DicaDoAssunto);
    }

    /// <summary>A etiqueta depois de uma escrita da tela: respondeu ou encerrou, ela sai; nota interna não responde ao paciente, fica.</summary>
    public static AppSemDorTag? EtiquetaDepoisDaEscrita(AppSemDorTag? atual, AppConversation nova)
        => nova.Closed || nova.Messages.LastOrDefault()?.Direction != "in" ? null : atual;

    /// <summary>Campo vazio (ou só espaços) sugere; com texto, revisa.</summary>
    public static PedidoAoSemDor Pedido(string? rascunho) => string.IsNullOrWhiteSpace(rascunho) ? PedidoAoSemDor.Sugerir : PedidoAoSemDor.Revisar;

    /// <summary>O que vai no <see cref="AppSemDorRequest.Rascunho"/>: nulo para sugerir (é como o CRM entende "sugira"), o texto aparado para revisar.</summary>
    public static string? RascunhoDoPedido(string? rascunho) => string.IsNullOrWhiteSpace(rascunho) ? null : rascunho.Trim();

    /// <summary>O nome inteiro do ✨ (a dica curta e o leitor de tela): o botão só mostra o ícone, do tamanho dos vizinhos.</summary>
    public static string NomeDoBotao(string? rascunho, bool escrevendo)
        => escrevendo ? "O @SemDor está escrevendo…"
            : Pedido(rascunho) == PedidoAoSemDor.Revisar ? "Melhorar o texto com o @SemDor" : "Sugerir resposta com o @SemDor";

    /// <summary>A dica completa: o que acontece e que nada é enviado.</summary>
    public static string DicaDoBotao(string? rascunho, bool escrevendo)
        => escrevendo ? NomeDoBotao(rascunho, true)
            : NomeDoBotao(rascunho, false) + (Pedido(rascunho) == PedidoAoSemDor.Revisar
                ? ": corrige ortografia e tom, sem mudar o sentido. Você confere antes de enviar."
                : ": lê a conversa e escreve uma sugestão no campo. Você revisa antes de enviar.");

    /// <summary>
    /// Põe a sugestão no campo. <paramref name="rascunhoDoPedido"/> é o que estava no campo quando ela clicou;
    /// <paramref name="rascunhoAgora"/>, o que está agora. Se ela continuou escrevendo, nada muda (o texto dela vale
    /// mais que o da IA); se trocou para a nota interna, também não (a sugestão é para o paciente, não para a equipe).
    /// Revisão guarda o texto dela para o ↶.
    /// </summary>
    public static SugestaoNoCampo Aplicar(string? rascunhoDoPedido, string? rascunhoAgora, bool campoEmNota, AppSemDorSuggestion sugestao)
    {
        var agora = rascunhoAgora ?? "";
        if (campoEmNota)
            return new(false, agora, null, "O campo passou para a nota interna, então a sugestão do @SemDor não foi colocada.");
        if (agora.Trim() != (rascunhoDoPedido ?? "").Trim())
            return new(false, agora, null, "Você continuou escrevendo, então a sugestão do @SemDor não foi colocada.");
        var texto = (sugestao.Texto ?? "").Trim();
        if (texto.Length == 0) return new(false, agora, null, SemResposta);
        var revisao = Pedido(rascunhoDoPedido) == PedidoAoSemDor.Revisar;
        return new(true, texto, revisao ? agora : null,
            revisao ? "Texto revisado pelo @SemDor. Confira antes de enviar." : "Sugestão do @SemDor no campo. Revise antes de enviar.");
    }

    /// <summary>
    /// O ↶ aparece enquanto o campo ainda tem o texto que o @SemDor pôs. Depois que ela mexeu, voltar ao texto antigo
    /// apagaria o que ela acabou de escrever — então ele some.
    /// </summary>
    public static bool PodeDesfazer(string? textoDoSemDor, string? rascunhoAgora)
        => !string.IsNullOrWhiteSpace(textoDoSemDor) && (rascunhoAgora ?? "").Trim() == textoDoSemDor.Trim();

    /// <summary>A frase da falha: a do CRM quando ele disse (desligado, não respondeu, ainda não há conversa); senão, <see cref="SemResposta"/>.</summary>
    public static string Falha(Exception e)
        => e is RecusaDoCrm or CrmIndisponivel && !string.IsNullOrWhiteSpace(e.Message) ? e.Message.Trim() : SemResposta;

    /// <summary>As três linhas do resumo, com o rótulo de cada uma; campo vazio vira "—".</summary>
    public static IReadOnlyList<(string Rotulo, string Texto)> LinhasDoResumo(AppSemDorSummary r) =>
    [
        ("O paciente quer", Ou(r.Quer)),
        ("Já foi feito", Ou(r.Feito)),
        ("Falta", Ou(r.Falta)),
    ];

    /// <summary>
    /// O aviso da gestão: a IA ligada que parou de responder diz o porquê (o CRM só manda o motivo para a gestão;
    /// para os outros manda <see cref="MotivoSoDaGestao"/>, e aí não há aviso — eles veem a frase ao clicar).
    /// </summary>
    public static string? AvisoDaGestao(AppSemDorState? estado)
        => estado is { Ligada: true, Funcionando: false } e && !string.IsNullOrWhiteSpace(e.UltimaFalha) && e.UltimaFalha.Trim() != MotivoSoDaGestao
            ? "O @SemDor não está respondendo: " + e.UltimaFalha.Trim()
            : null;

    private static string Ou(string? texto) => string.IsNullOrWhiteSpace(texto) ? "—" : texto.Trim();
}
