namespace Clinica.Crm.Contracts;

public sealed record Specialty(string Code, string Name, bool Active = true);
public sealed record ClinicAppointment(int Id, int PatientId, string Name, string Phone, DateTimeOffset StartsAt,
    string SpecialtyCode, string SpecialtyName, string Professional, string Status, string Revision, string Confirmation,
    string Answer = "", DateTimeOffset? AnsweredAt = null, DateTimeOffset? NotifiedAt = null, DateTimeOffset? CreatedAt = null);
public sealed record ClinicSnapshot(DateTimeOffset SyncedAt, Specialty[] Specialties, ClinicAppointment[] Appointments);
public sealed record ClinicDecision(string CommandId, int AppointmentId, string Revision, string Phone, string Decision, string MessageId);
public sealed record ClinicDecisionResult(string Status, string Description);
public sealed record ClinicSlotQuery(int AppointmentId, int PatientId, string Revision, string Phone, DateTimeOffset From);
public sealed record ClinicSlots(string Criterion, DateTimeOffset[] StartsAt);
public sealed record ClinicReschedule(string CommandId, int AppointmentId, int PatientId, string Revision, string Phone, DateTimeOffset StartsAt, string Actor);
public sealed record ClinicCancel(string CommandId, int AppointmentId, int PatientId, string Revision, string Phone, string Reason, string Actor);
public sealed record ClinicCancelResult(int AppointmentId, string Status, string[] Warnings);
public sealed record ClinicRescheduleResult(int AppointmentId, int PatientId, DateTimeOffset Previous, DateTimeOffset StartsAt, string Revision);
public sealed record RecallCandidate(string Key, int PatientId, string Name, string Phone, string Kind,
    string Modality, DateOnly Reference, int Days, int? CaseId, string Revision, bool Consent,
    string BlockReason, DateTimeOffset? LastContact, DateOnly? NextContact);
public sealed record RecallSnapshot(DateTimeOffset SyncedAt, RecallCandidate[] Candidates);
// Relacionamento: as filas que a secretária confere todo dia. Leitura pura; quem decide é o Clinica.
// List: "absent" (sem vir há N dias), "new-bsv" (indicado ou começou o BSV), "renew" (consulta a renovar).
public sealed record RelationshipEntry(string List, int PatientId, string Name, string Phone, DateOnly Reference, int Days,
    string Detail, string Situation, bool Consent, string Block, DateTimeOffset? NextAppointment, string? Insurance = null, int Sessions = 0);
public sealed record RelationshipSnapshot(DateTimeOffset SyncedAt, RelationshipEntry[] Entries);
public sealed record RecallNotice(string CommandId, int PatientId, int? CaseId, string MessageId, DateTimeOffset SentAt, string Channel = "whatsapp");

public sealed record ClinicDispatch(string Id, int PatientId, string Name, string Phone, string Source, string Actor,
    string State, string Detail, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string? Text = null, string? FileName = null, byte[]? File = null, bool Marketing = false, string Channel = "whatsapp", string? Email = null, string? Subject = null);
public sealed record ClinicDispatchClaim(string Consumer, int Protocol = 0);
public sealed record ClinicDispatchReport(string Consumer, string Status, string Detail);

public sealed record ClinicDispatchPermit(bool Granted, string State, string Detail);

public sealed record BookingPatient(int Id, string Name);
public sealed record BookingService(string Code, string Name, bool RequiresSpecialty);
public sealed record BookingProfessional(int Id, string Name, int Duration, string Schedule, bool AssumedSchedule,
    BookingQualification[]? Qualifications, string? SpecialtyCode);
public sealed record BookingQualification(string ServiceCode, string? SpecialtyCode);
public sealed record BookingRoom(int Id, string Name, int Capacity);
public sealed record BookingCatalog(DateTimeOffset ReadAt, BookingPatient[] Patients, BookingProfessional[] Professionals,
    BookingRoom[] Rooms, BookingService[] Services, Specialty[] Specialties);
public sealed record BookingCatalogQuery(string Phone);
public sealed record BookingQuery(string Phone, int PatientId, int ProfessionalId, int? RoomId, string ServiceCode,
    string? SpecialtyCode, int Duration, DateOnly From, DateOnly Through, TimeOnly After, TimeOnly Before, int[] Weekdays);
public sealed record BookingSlots(DateTimeOffset ReadAt, string Criterion, bool AssumedSchedule, DateTimeOffset[] Slots);
public sealed record BookingHold(string Id, BookingQuery Query, DateTimeOffset StartsAt, string Actor);
public sealed record BookingAction(string Id, string Phone, string Actor);
public sealed record BookingReceipt(int AppointmentId, int PatientId, DateTimeOffset StartsAt, string Revision);
public sealed record BookingReservation(string Id, string State, DateTimeOffset StartsAt, DateTimeOffset ExpiresAt,
    BookingReceipt? Receipt);
public sealed record BookingCalendarQuery(string Phone, DateOnly From, int Days, int? ProfessionalId, int? RoomId);
public sealed record BookingCalendarItem(DateTimeOffset StartsAt, DateTimeOffset EndsAt, int? ProfessionalId,
    int? RoomId, string State, bool ThisContact, int? AppointmentId);
public sealed record BookingCalendar(DateTimeOffset ReadAt, BookingCalendarItem[] Items);

// Acesso ao CRM pelo login do SISTEMA (out/2026): a ponte diz quem pode entrar e confere a senha.
public sealed record ClinicTeamMember(string Login, string Name, bool Admin);
public sealed record ClinicLogin(string Login, string Password);

// Quem é o paciente desta conversa (out/2026): o telefone pode ser de mais de uma ficha, e o
// paciente pode escrever de outro número. A ponte devolve as fichas candidatas com o que
// permite a recepção CONFERIR (nascimento, fim do documento, convênio) e o que vem pela frente.
public sealed record ClinicPatientLookup(string Phone, string? Query = null);
public sealed record ClinicPatientVisit(int AppointmentId, DateTimeOffset StartsAt, string Modality, string Professional,
    string Status, string Answer = "");
public sealed record ClinicPatientCard(int Id, string Name, string Phone, DateOnly? BirthDate, string Insurance,
    string DocumentEnd, bool PhoneMatch, DateOnly? LastSession, string LastModality, ClinicPatientVisit[] Upcoming);

// O dia do CRM para a direção (out/2026, caminho A): o CRM calcula, a ponte grava uma linha por
// dia no banco da clínica e o Gerente lê. Só contagens — nenhum dado de paciente. O dia de hoje
// chega várias vezes (parcial) e o de ontem mais uma (fechado); vale o GeneratedAt mais novo.
// MedianResponseSeconds nulo = não medido (nenhuma resposta humana no dia), nunca zero.
public sealed record CrmDailySummary(DateOnly Day, DateTimeOffset GeneratedAt,
    int ConversationsStarted, int ConversationsClosed, int ConversationsOpen,
    int MessagesIn, int MessagesOut, int? MedianResponseSeconds, int RepliesMeasured,
    int SmsSent, int SmsSegments, int SmsFailed,
    int ConfirmationsAsked, int ConfirmationsAnswered,
    int Resolved, int Scheduled, int NoResponse);

// O chat da equipe no CRM (out/2026): a secretária conversa com a equipe, pergunta ao Sistema
// SemDor e trata os pedidos à recepção sem sair do CRM. As conversas e os pedidos moram no banco
// da clínica (o mesmo chat dos apps); toda chamada leva o LOGIN de quem está no CRM, e a ponte
// confere que ele pode entrar (ClinicTeamBridge.PodeEntrar) antes de agir em nome dele.
public sealed record TeamChatAsk(string Login, string? Conversation = null, int? After = null, int? Before = null,
    DateTimeOffset? EditedSince = null, string? Text = null, int? MessageId = null);
public sealed record TeamChatItem(string Key, string Title, string Subtitle, int Unread, string? Preview, DateTimeOffset? LastAt,
    string Kind, int? PersonId, string Initials, string Presence, bool Active);
public sealed record TeamChatPanel(TeamChatItem[] Items, int Unread, int Online);
public sealed record TeamChatCardFigure(string Label, int Value, string Tone);
public sealed record TeamChatCardLine(string Time, string Title, string? Detail, string? Badge, string Tone, int? PatientId,
    int? AppointmentId, bool CanRequest);
public sealed record TeamChatCard(string Kind, string Title, string? Subtitle, TeamChatCardFigure[] Summary, TeamChatCardLine[] Lines,
    string[] Suggestions, string? Footer, int? RequestId, string? RequestState);
public sealed record TeamChatMessage(int Id, string AuthorName, string Initials, string Text, DateTimeOffset SentAt, DateTimeOffset? EditedAt,
    bool Mine, bool MentionsMe, bool FromSystem, string? PatientName, string? DocumentLabel, string? FileName, string? ReadBy,
    bool Truncated, TeamChatCard? Card);
public sealed record TeamChatConversation(string Key, string Title, string Subtitle, TeamChatMessage[] Messages, int LastReadBefore,
    string[] Typing, bool HasOlder, DateTimeOffset ReadAt, bool ReadOnly, bool MoreNewer = false);
public sealed record TeamChatNews(int Id, string Conversation, string Title, string Author, string Preview, DateTimeOffset At, string Sound);
public sealed record TeamChatPulse(int Last, TeamChatNews[] News);
public sealed record TeamChatHit(int MessageId, string Conversation, string Title, string Author, DateTimeOffset At, string Excerpt);
public sealed record TeamRequest(int Id, string Kind, string KindLabel, int? PatientId, string PatientName, int? AppointmentId,
    DateTimeOffset? AppointmentAt, string Detail, string RequestedBy, DateTimeOffset RequestedAt, string State, string StateLabel,
    string Owner, DateTimeOffset? TakenAt, string Outcome, DateTimeOffset? ClosedAt, bool CanHandle, bool Mine);
public sealed record TeamRequestAct(string Login, int RequestId, string Action, string? Outcome = null);
