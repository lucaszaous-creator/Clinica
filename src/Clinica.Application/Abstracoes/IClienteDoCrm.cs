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

    /// <summary>
    /// POST …/{id}/unread — "Marcar como não lida" (o botão direito da lista, out/2026): a ÚLTIMA recebida volta a contar
    /// como não lida, só para quem pediu; abrir a conversa (/read) a lê de novo. 400 quando não há recebida.
    /// </summary>
    Task MarcarNaoLidaAsync(int usuarioId, string conversaId, CancellationToken ct = default);

    /// <summary>POST …/{id}/claim — a pessoa assume a conversa.</summary>
    Task<AppConversation> AssumirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default);

    /// <summary>
    /// POST …/{id}/transfer — para uma FILA (setor) a conversa passa na hora, sem responsável; para uma PESSOA ela
    /// continua com quem pediu e nasce o pedido (<see cref="AppConversation.PendingTransfer"/>), que passa quando a
    /// pessoa aceitar (<see cref="DecidirTransferenciaAsync"/>).
    /// </summary>
    Task<AppConversation> TransferirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default);

    /// <summary>
    /// POST /api/transfers/{pedidoId}/{acao} — <paramref name="acao"/> é <c>accept</c> ou <c>reject</c> (de quem recebe)
    /// ou <c>cancel</c> (de quem pediu). Recusar exige o motivo (3 a 500 letras). Devolve a conversa que o pedido moveu;
    /// repetir a mesma decisão (a resposta se perdeu) é inofensivo no CRM.
    /// </summary>
    Task<AppTransferDecided> DecidirTransferenciaAsync(int usuarioId, string pedidoId, string acao, AppTransferDecision decisao,
        CancellationToken ct = default);

    /// <summary>POST …/{id}/reopen — reabre uma conversa encerrada.</summary>
    Task<AppConversation> ReabrirAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default);

    /// <summary>POST …/{id}/reassign — devolve à fila (<c>AgentId</c> nulo).</summary>
    Task<AppConversation> DevolverAFilaAsync(int usuarioId, string conversaId, AppChange mudanca, CancellationToken ct = default);

    /// <summary>POST …/{id}/close — encerra com um resultado de <see cref="AppWorkspace.Outcomes"/>.</summary>
    Task<AppConversation> EncerrarAsync(int usuarioId, string conversaId, AppClose encerramento, CancellationToken ct = default);

    /// <summary>POST …/{id}/patient — liga a ficha que a RECEPÇÃO conferiu (o telefone não é identidade).</summary>
    Task<AppConversation> LigarFichaAsync(int usuarioId, string conversaId, AppPatientLink ficha, CancellationToken ct = default);

    /// <summary>
    /// POST …/{id}/attachments — sobe UM arquivo (multipart); o Id devolvido vai no <see cref="AppReply.AttachmentId"/>.
    /// <paramref name="gravacao"/>: é o áudio gravado na tela (o campo <c>recording=true</c> do formulário, como o CRM web
    /// manda): o CRM converte para OGG/Opus, o formato da nota de voz do WhatsApp, e recusa o que não for áudio.
    /// </summary>
    Task<AppAttachment> AnexarAsync(int usuarioId, string conversaId, Stream arquivo, string nome, string mime, bool gravacao = false,
        CancellationToken ct = default);

    /// <summary>
    /// O ARQUIVO de uma mensagem com mídia (foto, áudio, vídeo, documento), com o tipo e o nome que o CRM deu.
    /// A rota sai de <see cref="Servicos.MidiaDaConversa.Rota"/>: a RECEBIDA vem de
    /// <c>GET /api/conversations/{id}/media/{messageId}</c> (o CRM a busca no provedor na primeira vez) e a
    /// ENVIADA, de <c>GET /api/attachments/{attachmentId}</c>. Mensagem sem arquivo é <see cref="RecusaDoCrm"/>;
    /// mais de <see cref="Servicos.MidiaDaConversa.Teto"/> também (o CRM não guarda nada maior).
    /// </summary>
    Task<ArquivoDaMidia> MidiaAsync(int usuarioId, string conversaId, AppMessage mensagem, CancellationToken ct = default);

    /// <summary>GET /api/templates/{sender} — os modelos aprovados pela Meta para aquele remetente.</summary>
    Task<AppTemplate[]> ModelosAsync(int usuarioId, string remetente, CancellationToken ct = default);

    /// <summary>GET /api/workspace — as respostas rápidas e os resultados de encerrar.</summary>
    Task<AppWorkspace> AreaDeTrabalhoAsync(int usuarioId, CancellationToken ct = default);

    // ---------- O @SemDor das secretárias (24/10): a IA lê e redige; quem decide e envia é a secretária ----------

    /// <summary>GET /api/semdor/estado — se a IA está ligada e funcionando. <c>UltimaFalha</c> só traz o motivo para a gestão.</summary>
    Task<AppSemDorState> EstadoDoSemDorAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>
    /// POST …/{id}/semdor/sugestao — <c>Rascunho</c> nulo: o @SemDor lê a conversa e sugere a resposta; com texto: revisa
    /// ortografia e tom. Devolve o texto para o CAMPO: nada é enviado. Desligado = <see cref="RecusaDoCrm"/> 409; a IA
    /// sem responder = <see cref="CrmIndisponivel"/> com a frase do CRM (502). A IA leva até 30 s: o prazo é próprio.
    /// </summary>
    Task<AppSemDorSuggestion> SugerirComSemDorAsync(int usuarioId, string conversaId, AppSemDorRequest pedido, CancellationToken ct = default);

    /// <summary>POST …/{id}/semdor/resumo — o que o paciente quer, o que já foi feito e o que falta. As falhas, como na sugestão.</summary>
    Task<AppSemDorSummary> ResumirComSemDorAsync(int usuarioId, string conversaId, CancellationToken ct = default);

    /// <summary>
    /// POST /api/presence — o pulso de "estou aqui" (25/10, decisão da direção: quem usa só o app conta como
    /// conectado). Vale <see cref="AppPresence.LeaseSeconds"/> (120 s) e é o que a distribuição automática e o
    /// painel "responsável sem sinal" do CRM leem. Quem pulsa é o <see cref="Servicos.VigiaDoWhatsApp"/>, que PARA
    /// antes de o app sair (<c>EsquecerSessao</c>): pulso depois da saída entraria de novo no CRM.
    /// </summary>
    Task<AppPresence> PulsoDePresencaAsync(int usuarioId, CancellationToken ct = default);

    // ---------- A auditoria do atendimento (26/10): a gerente confere o atendimento das secretárias no app do Gerente ----------
    // Só a gestão (admin ou supervisor no CRM): para os outros, as quatro rotas são RecusaDoCrm 403 com a frase do CRM.

    /// <summary>GET /api/quality/painel?dias= — os números de cada secretária no período (<paramref name="dias"/>: 7 ou 30).</summary>
    Task<AppQualityPanel> PainelDaAuditoriaAsync(int usuarioId, int dias, CancellationToken ct = default);

    /// <summary>GET /api/quality/fila — as conversas a conferir (rascunho do @SemDor) e as já avaliadas, com o porquê de cada uma.</summary>
    Task<AppQualityItem[]> FilaDaAuditoriaAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>
    /// GET /api/quality/{id} — a conversa inteira, o <c>Fingerprint</c> que o salvar devolve, os cinco critérios, os achados
    /// do sistema, quem da equipe participou e o histórico de avaliações (a mais nova primeiro).
    /// </summary>
    Task<AppQualityDetail> DetalheDaAuditoriaAsync(int usuarioId, string conversaId, CancellationToken ct = default);

    /// <summary>
    /// POST /api/quality — guarda a avaliação da gerente como versão NOVA (nenhuma é sobrescrita) e devolve a versão gravada.
    /// <see cref="RecusaDoCrm.Conflito"/> (409): a conversa mudou depois do detalhe (o <c>Fingerprint</c>) ou outra versão foi
    /// salva antes (a <c>Version</c>) — releia o detalhe. 400: a frase da validação ("Preencha os cinco critérios…"), inline.
    /// </summary>
    Task<AppQualityReview> SalvarAuditoriaAsync(int usuarioId, AppQualitySave avaliacao, CancellationToken ct = default);

    /// <summary>
    /// GET /api/quality/evolucao?semanas= — os números de cada secretária semana a semana (<paramref name="semanas"/>: de 4 a 26;
    /// as segundas-feiras, a mais antiga primeiro, e a última é a semana corrente, parcial). A nota da semana é a média das
    /// avaliações FINAIS da gerente sobre as conversas encerradas nela — nula sem avaliação. 403 como as outras rotas.
    /// </summary>
    Task<AppQualityTrend> EvolucaoDaAuditoriaAsync(int usuarioId, int semanas, CancellationToken ct = default);

    // ---------- "Cara de WhatsApp", F2a (nov/2026 — semdor-crm docs/api-para-os-apps.md §15) ----------
    // CRM anterior a esta versão responde 404 nestas rotas (RecusaDoCrm 404): a tela esconde o recurso.

    /// <summary>
    /// POST …/{id}/react — a clínica reage a uma mensagem (emoji vazio tira). Uma reação da clínica por mensagem. Pela Meta só
    /// dentro da janela de 24 h, em mensagem de até 30 dias, que não seja reação nem importada do celular (409 com a frase).
    /// </summary>
    Task<AppConversation> ReagirAsync(int usuarioId, string conversaId, AppReact reacao, CancellationToken ct = default);

    /// <summary>GET /api/labels — as etiquetas da clínica (as quatro de partida enquanto a gestão não salvar nenhuma).</summary>
    Task<AppLabel[]> EtiquetasAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>POST /api/labels — cria ou muda uma etiqueta (só a gestão: 403 para os outros). Devolve a lista inteira.</summary>
    Task<AppLabel[]> SalvarEtiquetaAsync(int usuarioId, AppLabelSave etiqueta, CancellationToken ct = default);

    /// <summary>POST …/{id}/labels — as etiquetas da conversa, exatamente estas (até 10).</summary>
    Task<AppConversation> EtiquetarConversaAsync(int usuarioId, string conversaId, AppConversationLabels etiquetas, CancellationToken ct = default);

    /// <summary>POST …/{id}/messages/{mensagemId}/star — ★ marca ou desmarca a mensagem como importante (para toda a equipe).</summary>
    Task<AppConversation> EstrelarAsync(int usuarioId, string conversaId, string mensagemId, AppStar estrela, CancellationToken ct = default);

    /// <summary>GET /api/pins — as conversas que a pessoa fixou no topo, na ordem em que fixou, e o máximo (3).</summary>
    Task<AppPins> FixadasAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>POST …/{id}/pin — fixa ou desafixa a conversa no topo da lista da pessoa. 409 com a frase quando já tem o máximo.</summary>
    Task<AppPins> FixarAsync(int usuarioId, string conversaId, AppPin fixar, CancellationToken ct = default);

    /// <summary>
    /// POST …/{id}/typing — o pulso de "estou escrevendo" (vale 6 s: pulse a cada ~3 s enquanto a pessoa digita). Devolve quem MAIS
    /// da equipe escreve nesta conversa. Com <see cref="AppSession.PatientTyping"/>, o CRM também mostra "digitando…" ao paciente.
    /// </summary>
    Task<AppTyping> EscrevendoAsync(int usuarioId, string conversaId, CancellationToken ct = default);

    /// <summary>GET …/{id}/typing — quem da equipe (além da pessoa) está escrevendo agora.</summary>
    Task<AppTyping> QuemEscreveAsync(int usuarioId, string conversaId, CancellationToken ct = default);

    /// <summary>POST …/{id}/typing/stop — a pessoa parou de escrever (enviou ou apagou): some na hora para as colegas.</summary>
    Task ParouDeEscreverAsync(int usuarioId, string conversaId, CancellationToken ct = default);

    // ---------- "Cara de WhatsApp", F2b (semdor-crm §16): endereço, contato do médico, serviços e enviar mais tarde ----------

    /// <summary>GET /api/share — o que o clipe oferece: o endereço da clínica (nulo = não configurado), os contatos e os serviços.</summary>
    Task<AppShareOptions> OpcoesDoClipeAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>POST /api/share/location — o endereço da clínica (só a gestão; 403 para os outros).</summary>
    Task<AppShareOptions> SalvarEnderecoAsync(int usuarioId, AppLocation endereco, CancellationToken ct = default);

    /// <summary>POST /api/share/contacts — cria (Id vazio) ou muda um cartão de contato (só a gestão).</summary>
    Task<AppShareOptions> SalvarContatoAsync(int usuarioId, AppContactCard contato, CancellationToken ct = default);

    /// <summary>POST /api/share/services — cria (Id vazio) ou muda um serviço (só a gestão). Preço no texto é recusado (400).</summary>
    Task<AppShareOptions> SalvarServicoAsync(int usuarioId, AppService servico, CancellationToken ct = default);

    /// <summary>POST /api/share/services/{id}/image — a imagem do serviço (só a gestão). <paramref name="arquivo"/> nulo tira a imagem.</summary>
    Task<AppShareOptions> ImagemDoServicoAsync(int usuarioId, string servicoId, Stream? arquivo, string nome = "", string mime = "",
        CancellationToken ct = default);

    /// <summary>GET /api/share/services/{id}/image — a imagem do serviço, para o cartão na tela.</summary>
    Task<ArquivoDaMidia> VerImagemDoServicoAsync(int usuarioId, string servicoId, CancellationToken ct = default);

    /// <summary>
    /// POST …/{id}/share — envia o endereço, um contato ou um serviço, como resposta da pessoa (idempotente pelo <c>Id</c>,
    /// <c>Guid.NewGuid().ToString("D")</c>). Só na janela de 24 h (409 com a frase), como qualquer mensagem livre.
    /// </summary>
    Task<AppConversation> CompartilharAsync(int usuarioId, string conversaId, AppShare envio, CancellationToken ct = default);

    /// <summary>
    /// POST …/{id}/scheduled — "enviar mais tarde" (1 minuto a 30 dias). Fora da janela de 24 h só modelo: o 409 diz até que horas
    /// o texto livre ainda vale. A conversa devolvida traz <see cref="AppConversation.Scheduled"/>.
    /// </summary>
    Task<AppConversation> AgendarAsync(int usuarioId, string conversaId, AppSchedule agendamento, CancellationToken ct = default);

    /// <summary>POST …/{id}/scheduled/{agendamentoId}/cancel — só quem agendou ou a gestão (403); já enviada é 409.</summary>
    Task<AppConversation> CancelarAgendamentoAsync(int usuarioId, string conversaId, string agendamentoId, CancellationToken ct = default);

    /// <summary>Esquece a sessão do CRM da pessoa (o app chama no logout): a próxima chamada entra de novo.</summary>
    void EsquecerSessao(int usuarioId);
}

/// <summary>O arquivo de uma mensagem do WhatsApp: os bytes, o tipo (Content-Type) e o nome, quando o CRM o informa.</summary>
public sealed record ArquivoDaMidia(byte[] Conteudo, string Mime, string? Nome);

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
