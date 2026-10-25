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
/// RecordingEnabled: o CRM converte a gravação de voz (ffmpeg) — sem ele, o app não mostra o microfone.</summary>
public sealed record AppSession(string? AgentId, string Csrf, AppAgent[] Agents, AppInbox[] Inboxes, string[] Queues,
    bool AttachmentsEnabled, bool SendEnabled, bool WhatsappOff, AppSms? Sms = null, bool RecordingEnabled = false);

// ---------- A lista ----------
/// <summary>Como a conversa terminou (o último ServiceClose). Outcome é a chave de AppWorkspace.Outcomes.</summary>
public sealed record AppResolution(string Outcome, string Summary, string ActorId, DateTimeOffset At);
/// <summary>
/// Uma linha de GET /api/conversations. Channel: "whatsapp" | "sms". Unread: recebidas depois da
/// última leitura de QUEM pediu. Preview: o texto (ou o nome do arquivo) da última mensagem;
/// PreviewDirection "in" | "out" e PreviewStatus (o Status da mensagem) desenham o tick na lista.
/// </summary>
public sealed record AppConversationSummary(string Id, string Name, string Contact, string Channel, string Sender,
    string Queue, string? AgentId, bool Closed, int Version, DateTimeOffset UpdatedAt, DateTimeOffset LastIncoming,
    int? PatientId, string PatientName, int Unread, string Preview, bool AwaitingMenu, bool DeliveryAttention,
    AppResolution? Resolution, DateTimeOffset? ClosedAt, string PreviewDirection = "in", string PreviewStatus = "",
    AppSemDorTag? Semdor = null);
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
/// </summary>
public sealed record AppMessage(string Id, string Direction, string Text, string Type, string? AgentId, DateTimeOffset At,
    string Status, string? Error = null, string? AttachmentId = null, string? FileName = null,
    AppTemplateSelection? Template = null, string? ReplyToId = null, bool Sticker = false);
public sealed record AppNote(string Id, string Text, string AgentId, DateTimeOffset At);
/// <summary>A trilha da conversa (assumiu, transferiu, encerrou, ligou ficha…): vira os eventos da linha do tempo.</summary>
public sealed record AppAudit(DateTimeOffset At, string Actor, string Action);
/// <summary>GET /api/conversations/{id}, e a resposta de toda escrita sobre a conversa.</summary>
public sealed record AppConversation(string Id, string Name, string Contact, string Channel, string Sender, string Queue,
    string? AgentId, bool Closed, int Version, DateTimeOffset UpdatedAt, DateTimeOffset LastIncoming, int? PatientId,
    string PatientName, AppMessage[] Messages, AppNote[] Notes, AppAudit[] Audit, AppResolution[] Resolutions,
    DateTimeOffset? ClosedAt);

// ---------- Escritas (corpo dos POST; toda escrita leva X-CSRF-TOKEN e a Version da conversa) ----------
/// <summary>POST /api/conversations — Sender é um AppInbox.Sender (ou o remetente do SMS).</summary>
public sealed record AppNewConversation(string Sender, string Contact, string Name);
/// <summary>POST …/{id}/reply. Id: gerado pelo app, Guid no formato "D" (com hífens; "N" é recusado), torna o envio idempotente. Modelo: Template preenchido e Text vazio.
/// ContactRecordedByApp: o app já gravou este contato no Acompanhamento (o convite da retenção) — o CRM não manda o aviso dele à ponte, senão o mesmo envio conta duas tentativas.</summary>
public sealed record AppReply(int Version, string Id, string Text, string? AttachmentId = null,
    AppTemplateSelection? Template = null, string? ReplyToId = null, bool ContactRecordedByApp = false);
/// <summary>POST …/{id}/notes — nota interna; nunca vai ao paciente. Id: Guid no formato "D", como no AppReply.</summary>
public sealed record AppNoteRequest(int Version, string Id, string Text);
/// <summary>POST …/{id}/claim · /transfer · /reopen · /reassign (AgentId nulo = devolver à fila).</summary>
public sealed record AppChange(int Version, string? AgentId, string? Queue, string? Note = null);
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
