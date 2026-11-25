namespace Clinica.Crm.Contracts;

// A API DO CRM PARA OS APPS DA CLÍNICA (out/2026 — docs/api-para-os-apps.md; no Clinica,
// docs/whatsapp-nativo-implementacao.md). A tela nativa do WhatsApp da Recepção fala com /api/*
// por estes records. O MESMO arquivo nos dois repositórios: aqui src/Clinica.Crm/ApiParaOsApps.cs,
// no Clinica src/Clinica.Application/Modelos/Crm/ContratosDoCrm.cs. Cada repositório tem um teste que
// confere o SHA-256 do seu arquivo contra a MESMA constante: mudar um lado obriga a trocar a constante,
// e o PR do outro repositório traz o arquivo e a constante iguais.
// Regras: campo novo entra no FIM do record, com valor padrão; nunca renomear nem remover (o app
// em produção quebraria em runtime). O CRM serializa em camelCase (JsonSerializerDefaults.Web) e o
// app lê com as mesmas opções. Erro é sempre AppError ({ "error": "frase em pt-BR" }).

/// <summary>Erro de qualquer rota: a frase que a tela mostra, em pt-BR, sem pilha.</summary>
public sealed record AppError(string Error);

// ---------- Sessão e entrada ----------
/// <summary>POST /api/login/ticket — o mesmo JSON do ClinicTicketLogin (o app não enxerga o ClinicContracts.cs da ponte).</summary>
public sealed record AppTicketLogin(string Ticket);
public sealed record AppAgent(string Id, string Name);
public sealed record AppInbox(string Sender, string Name);
public sealed record AppSms(bool Enabled, string Provider);
/// <summary>GET /api/session. Anônimo: só Csrf (e o cookie antiforgery). Logado: AgentId e o resto. O Csrf MUDA a cada login.
/// RecordingEnabled: o CRM converte a gravação de voz (ffmpeg) — sem ele, o app não mostra o microfone.
/// RemindersEnabled: o pedido de confirmação automático da agenda está ligado (Automação → Agenda e lembretes) — sem ele,
/// a mensagem ao paciente não promete lembrete nenhum.
/// PatientTyping: o CRM mostra "digitando…" ao paciente enquanto a equipe escreve (CRM_WA_TYPING_INDICATOR; §15). Ligado, o
/// pulso de POST …/typing também MARCA COMO LIDA a última mensagem do paciente (é como a Meta faz o indicador).</summary>
public sealed record AppSession(string? AgentId, string Csrf, AppAgent[] Agents, AppInbox[] Inboxes, string[] Queues,
    bool AttachmentsEnabled, bool SendEnabled, bool WhatsappOff, AppSms? Sms = null, bool RecordingEnabled = false,
    bool RemindersEnabled = false, bool PatientTyping = false, bool TranscriptionEnabled = false);

// ---------- A lista ----------
/// <summary>Como a conversa terminou (o último ServiceClose). Outcome é a chave de AppWorkspace.Outcomes.</summary>
public sealed record AppResolution(string Outcome, string Summary, string ActorId, DateTimeOffset At);
/// <summary>
/// Uma linha de GET /api/conversations. Channel: "whatsapp" | "sms". Unread: recebidas depois da
/// última leitura de QUEM pediu. Preview: o texto (ou o nome do arquivo) da última mensagem;
/// PreviewDirection "in" | "out" e PreviewStatus (o Status da mensagem) desenham o tick na lista.
/// Labels: ids de AppLabel (GET /api/labels) — o filtro por etiqueta é da tela. Pinned: QUEM pediu fixou esta conversa (§15).
/// </summary>
public sealed record AppConversationSummary(string Id, string Name, string Contact, string Channel, string Sender,
    string Queue, string? AgentId, bool Closed, int Version, DateTimeOffset UpdatedAt, DateTimeOffset LastIncoming,
    int? PatientId, string PatientName, int Unread, string Preview, bool AwaitingMenu, bool DeliveryAttention,
    AppResolution? Resolution, DateTimeOffset? ClosedAt, string PreviewDirection = "in", string PreviewStatus = "",
    AppSemDorTag? Semdor = null, AppPendingTransfer? PendingTransfer = null, string[]? Labels = null, bool Pinned = false);
/// <summary>A etiqueta do @SemDor na linha: o assunto da mensagem nova de quem espera a clínica. Nula quando a IA
/// está desligada, quando a clínica já respondeu ou a conversa encerrou. Atencao: sintoma grave, reação ao tratamento ou reclamação.</summary>
public sealed record AppSemDorTag(string Tema, string Rotulo, bool Atencao);
/// <summary>GET /api/conversations?desde=ISO: só o que mudou depois de "desde", os ids que fecharam, e o "agora" para a próxima.</summary>
public sealed record AppConversationsSince(AppConversationSummary[] Conversations, string[] Closed, DateTimeOffset Now);

// ---------- A conversa ----------
public sealed record AppTemplateSelection(string Name, string Language, string[] Parameters, string[]? ButtonPayloads = null);
/// <summary>
/// Direction "in" | "out". Status: "received" (entrada) e, na saída, "sending" | "accepted" | "simulated" |
/// "uncertain" (envio não confirmado) | "delivered" | "seen" (lida) | "failed" (Error diz por quê). Type "TEXT" | "IMAGE" | "DOCUMENT" | "AUDIO" | "VIDEO" | "TEMPLATE" |
/// "INTERACTIVE". AgentId nulo na saída = mensagem automática (lembrete, recall, menu).
/// Sticker: figurinha do WhatsApp — Type continua "IMAGE" (o app antigo a mostra como foto), e o novo a desenha pequena e sem moldura.
/// Animated (24/11/2026): figurinha animada ou GIF — a tela toca em laço, mudo e sem controles (só vem quando a Meta marca). ReplyToId na
/// RECEBIDA: a mensagem que o paciente citou (o Id dela nesta conversa; se a citada é de antes do CRM, o wamid, que a tela não acha e ignora).
/// Reactions: as reações a esta mensagem (no máximo uma do paciente e uma da clínica). ReactionTo: esta mensagem É a reação
/// do paciente (texto "Reagiu com 👍") e aponta o Id da reagida — a tela nova a esconde e põe o emoji no balão; a antiga mostra
/// o texto. Starred: ★ importante, para todos da conversa (§15).
/// </summary>
public sealed record AppMessage(string Id, string Direction, string Text, string Type, string? AgentId, DateTimeOffset At,
    string Status, string? Error = null, string? AttachmentId = null, string? FileName = null,
    AppTemplateSelection? Template = null, string? ReplyToId = null, bool Sticker = false,
    AppReaction[]? Reactions = null, string? ReactionTo = null, bool Starred = false,
    AppLocation? Location = null, AppSharedContact? SharedContact = null, string? Transcript = null, string? RetryOf = null,
    bool Animated = false);
/// <summary>Uma reação. FromPatient: do paciente; senão, da clínica, e AgentId diz quem da equipe reagiu.</summary>
public sealed record AppReaction(string Emoji, string? AgentId, bool FromPatient, DateTimeOffset At);
public sealed record AppNote(string Id, string Text, string AgentId, DateTimeOffset At);
/// <summary>A trilha da conversa (assumiu, transferiu, encerrou, ligou ficha…): vira os eventos da linha do tempo.</summary>
public sealed record AppAudit(DateTimeOffset At, string Actor, string Action);
/// <summary>GET /api/conversations/{id}, e a resposta de toda escrita sobre a conversa.
/// Labels: ids de AppLabel. Typing: quem da equipe está escrevendo agora (ids de AppAgent; inclui quem pediu — a tela o tira).</summary>
public sealed record AppConversation(string Id, string Name, string Contact, string Channel, string Sender, string Queue,
    string? AgentId, bool Closed, int Version, DateTimeOffset UpdatedAt, DateTimeOffset LastIncoming, int? PatientId,
    string PatientName, AppMessage[] Messages, AppNote[] Notes, AppAudit[] Audit, AppResolution[] Resolutions,
    DateTimeOffset? ClosedAt, AppPendingTransfer? PendingTransfer = null, string[]? Labels = null, string[]? Typing = null,
    AppScheduledMessage[]? Scheduled = null, AppTimelineItem[]? Timeline = null);
/// <summary>
/// Um item da LINHA DO TEMPO da conversa (24/11/2026): <see cref="AppConversation.Timeline"/> já vem na ordem certa —
/// mensagens, notas e eventos intercalados pelo horário real do fato (UTC), com desempate estável. Kind: "message" (Ref =
/// AppMessage.Id), "note" (Ref = AppNote.Id) ou "event" (Ref = a posição do evento em AppConversation.Audit: "0", "1"…).
/// A tela desenha NESTA ordem; nula num CRM antigo (a tela cai na ordem de cada lista).
/// </summary>
public sealed record AppTimelineItem(string Kind, string Ref, DateTimeOffset At);
/// <summary>
/// O pedido de transferência para uma PESSOA que espera o aceite (o setor passa na hora e nunca fica pendente). Nulo
/// quando não há. From: quem pediu; To: quem recebe (ids de AppAgent); Note: o contexto para quem recebe. Quem recebe
/// aceita ou recusa, e quem pediu cancela, por POST /api/transfers/{Id}/accept · reject · cancel.
/// </summary>
public sealed record AppPendingTransfer(string Id, string From, string To, string Note, DateTimeOffset At);

// ---------- Escritas (corpo dos POST; toda escrita leva X-CSRF-TOKEN e a Version da conversa) ----------
/// <summary>POST /api/conversations — Sender é um AppInbox.Sender (ou o remetente do SMS).</summary>
public sealed record AppNewConversation(string Sender, string Contact, string Name);
/// <summary>POST …/{id}/reply. Id: gerado pelo app, Guid no formato "D" (com hífens; "N" é recusado), torna o envio idempotente. Modelo: Template preenchido e Text vazio.
/// ContactRecordedByApp: o app já gravou este contato no Acompanhamento (o convite da retenção) — o CRM não manda o aviso dele à ponte, senão o mesmo envio conta duas tentativas.</summary>
public sealed record AppReply(int Version, string Id, string Text, string? AttachmentId = null,
    AppTemplateSelection? Template = null, string? ReplyToId = null, bool ContactRecordedByApp = false);
/// <summary>POST …/{id}/notes — nota interna; nunca vai ao paciente. Id: Guid no formato "D", como no AppReply.</summary>
public sealed record AppNoteRequest(int Version, string Id, string Text);
/// <summary>POST …/{id}/claim · /transfer · /reopen · /reassign (AgentId nulo = devolver à fila). No /transfer, com AgentId
/// a conversa NÃO muda de mãos: vira o pedido AppPendingTransfer, que passa quando a pessoa aceitar; sem AgentId vai à
/// fila Queue na hora, sem responsável.</summary>
public sealed record AppChange(int Version, string? AgentId, string? Queue, string? Note = null);
/// <summary>POST /api/transfers/{id}/accept · reject · cancel. Reason: obrigatório ao recusar (3 a 500 letras); opcional nos outros.</summary>
public sealed record AppTransferDecision(string? Reason = null);
/// <summary>A resposta da decisão: a conversa que o pedido moveu (o app a relê por GET /api/conversations/{id}).</summary>
public sealed record AppTransferDecided(string ConversationId);
/// <summary>POST …/{id}/close — Outcome é uma chave de AppWorkspace.Outcomes.</summary>
public sealed record AppClose(int Version, string Outcome, string Summary, string? NextStep = null, DateTimeOffset? DueAt = null);
/// <summary>POST …/{id}/patient — a ficha que a RECEPÇÃO conferiu (regra 5: o telefone não é identidade).</summary>
public sealed record AppPatientLink(int? PatientId, string Name = "", int? Version = null);
/// <summary>POST …/{id}/read — quantas recebidas a pessoa já viu.</summary>
public sealed record AppReadMarker(int IncomingCount);
/// <summary>Resposta de POST …/{id}/attachments (multipart, um arquivo até 16 MB): o Id vai no AppReply.AttachmentId.</summary>
public sealed record AppAttachment(string Id, string ConversationId, string Name, string Mime, string Type, long Size);

// ---------- Catálogos ----------
/// <summary>GET /api/templates/{sender}: os modelos aprovados pela Meta. Parameters = quantos {{n}} o Text tem.</summary>
public sealed record AppTemplate(string Name, string Language, string Status, string Text, int Parameters,
    bool Supported, string Reason, string[] Buttons, string Category);
public sealed record AppQuickReply(string Id, string Title, string Text, bool Archived = false);
/// <summary>A parte de GET /api/workspace que o app usa: as respostas rápidas e os resultados de encerrar (chave → rótulo).</summary>
public sealed record AppWorkspace(AppQuickReply[] QuickReplies, Dictionary<string, string> Outcomes);

// ---------- O @SemDor das secretárias (só lê e redige; quem decide e envia é a secretária — regra 3 do CRM) ----------
/// <summary>POST …/{id}/semdor/sugestao — Rascunho nulo ou vazio: sugere a resposta; com texto: revisa ortografia e tom.</summary>
public sealed record AppSemDorRequest(string? Rascunho);
/// <summary>Resposta da sugestão: o texto que vai para o CAMPO (nunca é enviado sozinho). Modo "sugestao" | "revisao".</summary>
public sealed record AppSemDorSuggestion(string Texto, string Modo);
/// <summary>POST …/{id}/semdor/resumo (corpo vazio): o que o paciente quer, o que já foi feito e o que falta.</summary>
public sealed record AppSemDorSummary(string Quer, string Feito, string Falta);
/// <summary>GET /api/semdor/estado. Ligada falso: os botões e as etiquetas somem. UltimaFalha: o motivo só para a gestão
/// (para os outros, "Fale com a gestão." ou vazio). 409 (desligado) e 502 (não respondeu) trazem a frase em AppError.</summary>
public sealed record AppSemDorState(bool Ligada, bool Funcionando, DateTimeOffset? UltimoSucessoEm, DateTimeOffset? UltimaFalhaEm, string UltimaFalha);

// ---------- A presença do app (25/10: quem usa só o app conta como conectado no CRM) ----------
/// <summary>POST /api/presence (corpo "{}", com o X-CSRF-TOKEN): o pulso de "estou aqui" de quem está logado. Connected verdadeiro e
/// LeaseSeconds = por quanto tempo o pulso vale (120 s): o app pulsa antes disso enquanto a pessoa está logada. 401 = a sessão acabou.</summary>
public sealed record AppPresence(bool Connected, int LeaseSeconds);

// ---------- A auditoria do atendimento (26/10: a gerente confere o atendimento das secretárias; o @SemDor pré-avalia) ----------
// Só a gestão (perfil admin ou supervisor no CRM): 403 com AppError para os outros. A nota que vale é sempre a da gerente;
// o rascunho do @SemDor é uma AppQualityReview com ReviewerId "semdor" e State "draft".
/// <summary>Um dos cinco critérios da auditoria. Guide: a pergunta que a gerente responde ao dar a nota.</summary>
public sealed record AppQualityCriterion(string Id, string Title, string Guide);
/// <summary>Os números de uma secretária no período (GET /api/quality/painel). FirstReplyMedianMinutes: minutos ÚTEIS até a primeira
/// resposta (seg–sex 7h–19h, sáb 7h–13h), nulo sem resposta medida. RepliedOnTime: quantas das RepliesMeasured saíram em até 15 min
/// úteis. WaitingNow: conversas dela esperando resposta agora. Resolved/NoResponse: encerramentos do período pelo resultado.
/// Transfers: transferências que ela pediu. Reviewed/AverageScore: avaliações FINAIS da gerente (nota 0–100). ToReview: rascunhos
/// do @SemDor esperando a gerente.</summary>
public sealed record AppQualityAgent(string AgentId, string Name, int Conversations, double? FirstReplyMedianMinutes, int RepliedOnTime,
    int RepliesMeasured, int WaitingNow, int Resolved, int NoResponse, int Transfers, int Reviewed, decimal? AverageScore, int ToReview);
/// <summary>GET /api/quality/painel?dias=7|30 — o painel por secretária. SemDorEnabled: o @SemDor está pré-avaliando a amostra.</summary>
public sealed record AppQualityPanel(DateTimeOffset From, DateTimeOffset To, AppQualityAgent[] Agents, bool SemDorEnabled);
/// <summary>Uma linha de GET /api/quality/fila. Reason: por que entrou ("Reclamação do paciente", "Espera longa", "Sorteio da semana"…).
/// State: "para-conferir" (rascunho do @SemDor) | "avaliada" (a gerente concluiu). SuggestedScore: a nota do rascunho; Score: a da
/// gerente. Stale: a conversa mudou depois da última avaliação.</summary>
public sealed record AppQualityItem(string ConversationId, string SubjectId, string SubjectName, string PatientName, DateTimeOffset At,
    string Reason, string State, decimal? SuggestedScore, decimal? Score, bool Stale);
/// <summary>A nota de um critério: Score -1 = não avaliável, 0 = não atendeu, 1 = em parte, 2 = atendeu. MessageIds: as mensagens
/// que provam a nota (ids de AppMessage).</summary>
public sealed record AppQualityAnswer(string Criterion, int? Score, string Reason, string[] MessageIds);
/// <summary>Uma avaliação guardada (cada versão fica: o rascunho do @SemDor e a da gerente). Score 0–100 só na final.
/// Outcome: "resolved" | "followup" | "inconclusive". ActionDone: o plano de ação foi cumprido.</summary>
public sealed record AppQualityReview(string SubjectId, int Version, string State, string ReviewerId, DateTimeOffset ReviewedAt,
    AppQualityAnswer[] Answers, string Outcome, string Feedback, string Action, DateTimeOffset? DueAt, decimal? Score, bool ActionDone = false);
/// <summary>O que o sistema achou na conversa sem IA (espera, reclamação, anexo, falha de envio). Level "attention" | "info".</summary>
public sealed record AppQualityFinding(string Code, string Level, string Title, string Description, string[] MessageIds);
/// <summary>GET /api/quality/{conversationId}: a conversa inteira, o Fingerprint que o POST devolve, os critérios, os achados,
/// quem da equipe participou (Subjects: ids de AppAgent) e o histórico de avaliações (a mais nova primeiro).</summary>
public sealed record AppQualityDetail(AppConversation Conversation, string Fingerprint, AppQualityCriterion[] Criteria,
    AppQualityFinding[] Findings, string[] Subjects, AppQualityReview[] History);
/// <summary>POST /api/quality — a avaliação da gerente. Version: a da última avaliação desta secretária nesta conversa (0 se nenhuma);
/// Fingerprint: o do detalhe (409 se a conversa mudou). State "final" exige os cinco critérios justificados. Resposta: AppQualityReview.</summary>
public sealed record AppQualitySave(string ConversationId, string SubjectId, int Version, string Fingerprint, string State,
    AppQualityAnswer[] Answers, string Outcome, string Feedback, string Action, DateTimeOffset? DueAt, bool ActionDone = false);
/// <summary>Uma semana de uma secretária (GET /api/quality/evolucao). WeekStart: a segunda-feira (data de Brasília). Conversations:
/// conversas em que ela escreveu; FirstReplyMedianMinutes, RepliedOnTime e RepliesMeasured: como no AppQualityAgent. AverageScore:
/// média das avaliações FINAIS da gerente sobre as conversas encerradas na semana (nulo sem avaliação); Reviewed: quantas.</summary>
public sealed record AppQualityWeek(DateOnly WeekStart, int Conversations, double? FirstReplyMedianMinutes, int RepliedOnTime,
    int RepliesMeasured, decimal? AverageScore, int Reviewed);
public sealed record AppQualityTrendAgent(string AgentId, string Name, AppQualityWeek[] Weeks);
/// <summary>GET /api/quality/evolucao?semanas=8 (de 4 a 26): Weeks são as segundas-feiras, a mais antiga primeiro; a última é a
/// semana corrente, ainda parcial. Cada secretária traz uma AppQualityWeek por semana, na mesma ordem.</summary>
public sealed record AppQualityTrend(DateOnly[] Weeks, AppQualityTrendAgent[] Agents);

// ---------- "CRM com cara de WhatsApp" (nov/2026, F2a — docs/api-para-os-apps.md §15) ----------
/// <summary>POST …/{id}/react — a clínica reage a uma mensagem (API oficial da Meta). Emoji vazio tira a reação. Só na janela de 24 h,
/// em mensagem de até 30 dias, que não seja reação nem importada do celular. Resposta: AppConversation.</summary>
public sealed record AppReact(string MessageId, string Emoji);
/// <summary>Uma etiqueta da clínica (GET /api/labels). Archived: some da escolha, mas continua nas conversas que já a têm.</summary>
public sealed record AppLabel(string Id, string Name, bool Archived, int Version);
/// <summary>POST /api/labels — só a gestão. Id nulo cria; Version: a da etiqueta (0 para criar). Resposta: a lista inteira (AppLabel[]).</summary>
public sealed record AppLabelSave(string? Id, string Name, bool Archived, int Version);
/// <summary>POST …/{id}/labels — as etiquetas da conversa, EXATAMENTE estas (ids de AppLabel; até 10). Resposta: AppConversation.</summary>
public sealed record AppConversationLabels(string[] Labels);
/// <summary>POST …/{id}/messages/{messageId}/star — ★ marca ou desmarca a mensagem como importante. Resposta: AppConversation.</summary>
public sealed record AppStar(bool Starred);
/// <summary>POST …/{id}/pin — fixa ou desafixa a conversa no topo da lista de QUEM pede (até Max). 409 com a frase quando já tem Max.</summary>
public sealed record AppPin(bool Pinned);
/// <summary>GET /api/pins e a resposta do /pin: as conversas fixadas por quem pediu, na ordem em que fixou.</summary>
public sealed record AppPins(string[] ConversationIds, int Max);
/// <summary>POST …/{id}/typing (corpo "{}") — o pulso de "estou escrevendo" (vale 6 s; pulse a cada ~3 s enquanto digita) e
/// GET …/{id}/typing: Agents = quem MAIS da equipe escreve agora (ids de AppAgent, sem quem pediu). POST …/typing/stop esquece o pulso.</summary>
public sealed record AppTyping(string[] Agents);

// ---------- "CRM com cara de WhatsApp" (nov/2026, F2b — docs/api-para-os-apps.md §16) ----------
/// <summary>O endereço da clínica (mensagem de localização da Meta). Também vem em AppMessage.Location da mensagem enviada (Type "LOCATION").</summary>
public sealed record AppLocation(string Name, string Address, double Latitude, double Longitude);
/// <summary>O cartão de contato de um profissional. Phone: com DDI, só dígitos. Role: o que ele faz ("Acupuntura").</summary>
public sealed record AppContactCard(string Id, string Name, string Phone, string Role, bool Archived, int Version);
/// <summary>O contato enviado numa mensagem (AppMessage.SharedContact, Type "CONTACTS").</summary>
public sealed record AppSharedContact(string Name, string Phone, string Role);
/// <summary>Um serviço da clínica em cartão: título, texto e imagem opcional (GET /api/share/services/{Id}/image). Sem preço.</summary>
public sealed record AppService(string Id, string Title, string Text, string? ImageId, bool Archived, int Version);
/// <summary>GET /api/share — o que o clipe oferece: o endereço (nulo = não configurado), os contatos e os serviços.
/// POST /api/share/location · /contacts · /services (só a gestão) devolvem o mesmo objeto.</summary>
public sealed record AppShareOptions(AppLocation? Location, AppContactCard[] Contacts, AppService[] Services);
/// <summary>POST …/{id}/share — envia o endereço (Kind "location"), um contato ("contact", ItemId) ou um serviço ("service", ItemId).
/// Id: Guid "D", torna o envio idempotente como no AppReply. Só na janela de 24 h. Resposta: AppConversation.</summary>
public sealed record AppShare(int Version, string Id, string Kind, string? ItemId = null);
/// <summary>POST …/{id}/scheduled — "enviar mais tarde": texto, anexo ou modelo, de 1 minuto a 30 dias à frente. Fora da janela de
/// 24 h só modelo (409 diz até que horas o texto livre ainda vale). Id: Guid "D". Resposta: AppConversation (com Scheduled).
/// POST …/{id}/scheduled/{Id}/cancel — só quem agendou ou a gestão.</summary>
public sealed record AppSchedule(string Id, string Text, DateTimeOffset At, string? AttachmentId = null, AppTemplateSelection? Template = null);
/// <summary>Uma mensagem agendada. State: "agendada" | "enviando" | "enviada" (MessageId = a AppMessage criada) | "cancelada" |
/// "falhou" (Error diz por quê, em pt-BR).</summary>
public sealed record AppScheduledMessage(string Id, string AgentId, string Text, DateTimeOffset At, string State, DateTimeOffset CreatedAt,
    string? AttachmentId = null, AppTemplateSelection? Template = null, string? MessageId = null, string? Error = null);

// ---------- "CRM com cara de WhatsApp" (nov/2026, F2c — docs/api-para-os-apps.md §17) ----------
// AppSession.TranscriptionEnabled: o botão "Transcrever" só aparece ligado (hoje DESLIGADO: a IA do @SemDor não ouve áudio).
// AppMessage.Transcript: o texto do áudio; AppMessage.RetryOf: esta mensagem é o "Tentar de novo" da original (a tela tira o "!" dela).
/// <summary>POST …/{id}/semdor/opcoes (corpo vazio) — de 2 a 3 respostas curtas para as pílulas; a escolhida vai para o CAMPO.
/// 409 desligado ou sem fala do paciente · 502 não respondeu (as frases como na sugestão).</summary>
public sealed record AppSemDorOptions(string[] Options);
/// <summary>POST …/{id}/messages/{messageId}/retry — "Tentar de novo": envio NOVO (Id: Guid "D" novo) com o mesmo conteúdo da que
/// falhou (ou ficou incerta há mais de 10 min). Version: a da conversa. Resposta: AppConversation.</summary>
public sealed record AppRetry(int Version, string Id);
/// <summary>GET /api/greeting e POST (só a gestão) — a saudação do menu, a primeira mensagem ao paciente que escreve, e se o menu
/// está ligado. Version: a da configuração (409 quando outra pessoa salvou antes).</summary>
public sealed record AppGreeting(bool Enabled, string Text, int Version);
