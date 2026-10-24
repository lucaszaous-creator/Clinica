using Clinica.Crm.Contracts;

namespace Clinica.Application.Abstracoes;

/// <summary>
/// A API do CRM vista pelo app da clínica (out/2026, PR 1 do WhatsApp nativo na Recepção —
/// docs/whatsapp-nativo-implementacao.md; a metade do CRM em semdor-crm/docs/api-para-os-apps.md).
/// Um método por rota que a tela nativa usa, com os records de <c>ContratosDoCrm.cs</c>.
///
/// A sessão no CRM é de QUEM ENTROU NO APP (<paramref name="usuarioId"/> de toda chamada é o
/// <c>SessaoUsuario.Atual</c>): duas pessoas no mesmo PC do balcão são duas sessões, e o CRM
/// aplica a cada uma o acesso dela. A entrada é pelo bilhete (<see cref="IEntradaNoCrmService"/>)
/// e é invisível: quem chama nunca vê "entre de novo".
///
/// Falhas, sempre tipadas — a tela mostra um estado para cada uma:
/// <list type="bullet">
/// <item><see cref="CrmNaoConfigurado"/>: a Gerência não informou o endereço do CRM ("ainda não foi ligado").</item>
/// <item><see cref="CrmIndisponivel"/>: fora do ar, demorou, a rede caiu ou o CRM não aceitou o acesso
/// (<c>FalhaDeLeitura</c>, nunca lista vazia). <see cref="CrmNaoConfigurado"/> deriva dela: o
/// compilador obriga o <c>catch</c> do não configurado a vir antes.</item>
/// <item><see cref="RecusaDoCrm"/>: o CRM disse não, com a frase para a tela (inline);
/// <see cref="RecusaDoCrm.Conflito"/> = alguém mexeu antes, releia.</item>
/// </list>
/// Cancelar o <see cref="CancellationToken"/> devolve <see cref="OperationCanceledException"/>, como sempre.
/// </summary>
public interface IClienteDoCrm
{
    /// <summary>Há endereço do CRM configurado? Lê a configuração na hora (é o que a tela pergunta ao abrir).</summary>
    Task<bool> ConfiguradoAsync(CancellationToken ct = default);

    /// <summary>GET /api/session — quem a pessoa é no CRM, as caixas de saída, as filas e o que está ligado.</summary>
    Task<AppSession> SessaoAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>GET /api/conversations — a lista inteira que a pessoa pode ver.</summary>
    Task<AppConversationSummary[]> ListarConversasAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>
    /// GET /api/conversations?desde= — só o que mudou depois de <paramref name="desde"/> (a releitura de 7 s;
    /// passe o <c>Now</c> da resposta anterior). O CRM devolve com 5 s de margem antes de <paramref name="desde"/>:
    /// a mesma conversa pode vir de novo, e quem consome JUNTA POR <c>Id</c>. ⚠️ Não traz a mudança de
    /// <c>Unread</c> feita em outro aparelho nem a conversa que saiu da visão da pessoa (transferida para fora
    /// do que ela vê): a tela relê a lista inteira (<see cref="ListarConversasAsync"/>) ao abrir e a cada poucos minutos.
    /// </summary>
    Task<AppConversationsSince> ListarDesdeAsync(int usuarioId, DateTimeOffset desde, CancellationToken ct = default);

    /// <summary>GET /api/conversations/{id} — a conversa inteira: mensagens, notas e trilha.</summary>
    Task<AppConversation> AbrirConversaAsync(int usuarioId, string conversaId, CancellationToken ct = default);

    /// <summary>POST /api/conversations — começa uma conversa com um contato.</summary>
    Task<AppConversation> NovaConversaAsync(int usuarioId, AppNewConversation pedido, CancellationToken ct = default);

    /// <summary>
    /// POST …/{id}/reply — envia ao paciente (texto, anexo ou modelo). O <c>Id</c> torna o envio idempotente e é
    /// <c>Guid.NewGuid().ToString("D")</c> (com hífens; o CRM recusa o "N" com 400, apesar do comentário do contrato).
    /// </summary>
    Task<AppConversation> ResponderAsync(int usuarioId, string conversaId, AppReply resposta, CancellationToken ct = default);

    /// <summary>POST …/{id}/notes — nota interna; nunca vai ao paciente. O <c>Id</c> é <c>Guid.NewGuid().ToString("D")</c>, como no envio.</summary>
    Task<AppConversation> AnotarAsync(int usuarioId, string conversaId, AppNoteRequest nota, CancellationToken ct = default);

    /// <summary>POST …/{id}/read — marca como vistas as recebidas até <c>IncomingCount</c> (o CRM responde 200 sem corpo).</summary>
    Task MarcarLidaAsync(int usuarioId, string conversaId, AppReadMarker marcador, CancellationToken ct = default);

    /// <summary>POST …/{id}/claim — a pessoa assume a conversa.</summary>
    Task<AppConversation> AssumirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default);

    /// <summary>POST …/{id}/transfer — passa a conversa a outra pessoa ou fila.</summary>
    Task<AppConversation> TransferirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default);

    /// <summary>POST …/{id}/reopen — reabre uma conversa encerrada.</summary>
    Task<AppConversation> ReabrirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default);

    /// <summary>POST …/{id}/reassign — devolve à fila (<c>AgentId</c> nulo).</summary>
    Task<AppConversation> DevolverAFilaAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default);

    /// <summary>POST …/{id}/close — encerra com um resultado de <see cref="AppWorkspace.Outcomes"/>.</summary>
    Task<AppConversation> EncerrarAsync(int usuarioId, string conversaId, AppClose encerramento, CancellationToken ct = default);

    /// <summary>POST …/{id}/patient — liga a ficha que a RECEPÇÃO conferiu (o telefone não é identidade).</summary>
    Task<AppConversation> LigarFichaAsync(int usuarioId, string conversaId, AppPatientLink ficha, CancellationToken ct = default);

    /// <summary>POST …/{id}/attachments — sobe UM arquivo (multipart); o Id devolvido vai no <see cref="AppReply.AttachmentId"/>.</summary>
    Task<AppAttachment> AnexarAsync(int usuarioId, string conversaId, Stream arquivo, string nome, string mime, CancellationToken ct = default);

    /// <summary>GET /api/templates/{sender} — os modelos aprovados pela Meta para aquele remetente.</summary>
    Task<AppTemplate[]> ModelosAsync(int usuarioId, string remetente, CancellationToken ct = default);

    /// <summary>GET /api/workspace — as respostas rápidas e os resultados de encerrar.</summary>
    Task<AppWorkspace> AreaDeTrabalhoAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>Esquece a sessão do CRM da pessoa (o app chama no logout): a próxima chamada entra de novo.</summary>
    void EsquecerSessao(int usuarioId);
}

/// <summary>
/// O CRM recusou o pedido (4xx com <c>{ "error": "…" }</c>): <see cref="Exception.Message"/> é a
/// frase em pt-BR para a tela mostrar inline. É uma recusa de regra, como as da Application — por
/// isso deriva de <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class RecusaDoCrm(string mensagem, int status) : InvalidOperationException(mensagem)
{
    /// <summary>O código HTTP da recusa.</summary>
    public int Status { get; } = status;

    /// <summary>409: alguém mexeu na conversa antes (a <c>Version</c> mudou). A tela relê e avisa.</summary>
    public bool Conflito => Status == 409;
}

/// <summary>
/// O CRM não pôde responder: fora do ar (5xx), demorou, a rede caiu, a resposta veio num formato
/// que este app não conhece, ou ele não aceitou o acesso da pessoa. A tela mostra
/// <c>FalhaDeLeitura</c> com a frase — nunca uma lista vazia.
/// </summary>
public class CrmIndisponivel : Exception
{
    /// <summary>O bilhete foi recusado, ou a sessão nova também tomou 401: não é a rede, é o acesso.</summary>
    public const string AcessoRecusado = "O CRM não aceitou o acesso. Fale com a direção.";

    public CrmIndisponivel(string mensagem) : base(mensagem) { }
    public CrmIndisponivel(string mensagem, Exception? interna) : base(mensagem, interna) { }
}

/// <summary>Sem endereço do CRM nas Configurações: a tela mostra "ainda não foi ligado", não "fora do ar".</summary>
public sealed class CrmNaoConfigurado() : CrmIndisponivel(Frase)
{
    public const string Frase = "O WhatsApp da clínica ainda não foi ligado aqui. A Gerência informa o endereço do CRM em Configurações.";
}
