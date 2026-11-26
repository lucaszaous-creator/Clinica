using Clinica.Application.Modelos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Crm.Bridge;

/// <summary>
/// A reserva de horário SEM FICHA pela conversa do CRM (nov/2026). A regra mora no Clinica (ReservaDeHorarioService,
/// a mesma dos apps); a ponte só traduz o contrato e confere que a reserva é DESTE telefone. Virar paciente é no
/// balcão (app), não aqui — a ponte não cria ficha.
/// </summary>
public sealed class ClinicReservationBridge(ClinicaDbContext db)
{
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    static DateTime Local(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, Zone).DateTime;
    static DateTimeOffset Offset(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Unspecified), Zone.GetUtcOffset(time));

    static string Phone(string? phone)
        => FichaPeloTelefone.Normalizar(phone) is { Length: >= 10 and <= 15 } p ? p : throw new InvalidOperationException("Telefone inválido.");
    static string Actor(string? actor)
        => actor is { Length: > 0 and <= 74 } a ? "CRM · " + a : throw new InvalidOperationException("Atendente inválida.");

    public static string Status(SituacaoDaReserva s) => s switch
    {
        SituacaoDaReserva.Reservada => "reserved",
        SituacaoDaReserva.Desmarcada => "cancelled",
        SituacaoDaReserva.NaoCompareceu => "no-show",
        _ => "converted"
    };

    static ClinicReservation View(ReservaDeHorario r) => new(r.Id, r.Nome, r.Telefone, Offset(r.DataHora), r.DuracaoMinutos,
        r.ProfissionalId, r.Profissional?.Rotulo ?? "", r.ModalidadeCodigo, r.EspecialidadeConsultaCodigo, r.Particular, r.ConvenioNome,
        Status(r.Situacao), r.AgendamentoId, r.Versao.ToString(System.Globalization.CultureInfo.InvariantCulture), Reminder(r));

    /// <summary>A resposta ao lembrete, em código do contrato (as palavras ficam com quem mostra).</summary>
    public static string Reminder(ReservaDeHorario r) => r.RespostaDoLembrete switch
    {
        ReservaDeHorario.RespostasDoLembrete.Confirmou => "confirmed",
        ReservaDeHorario.RespostasDoLembrete.PediuCancelar => "cancel-requested",
        ReservaDeHorario.RespostasDoLembrete.PediuRemarcar => "reschedule-requested",
        _ => r.LembreteEnviadoEm is null ? "" : "sent"
    };

    /// <summary>As reservas do telefone da conversa (todas as situações: é o histórico dela).</summary>
    public async Task<ClinicReservation[]> List(ClinicReservationQuery q, CancellationToken ct)
        => (await ReservaDeHorarioService.Sobre(db).DoTelefoneAsync(Phone(q.Phone), "CRM", ct)).Select(View).ToArray();

    /// <summary>
    /// Reserva. Idempotente: a MESMA reserva (telefone, profissional, horário) ainda reservada volta como está — uma
    /// resposta perdida no caminho não segura o horário duas vezes.
    /// </summary>
    public async Task<ClinicReservation> Create(ClinicReservationRequest r, CancellationToken ct)
    {
        var phone = Phone(r.Phone); var actor = Actor(r.Actor); var inicio = Local(r.StartsAt);
        var mesma = await db.ReservasDeHorario.AsNoTracking().Include(x => x.Profissional)
            .FirstOrDefaultAsync(x => x.Telefone == phone && x.ProfissionalId == r.ProfessionalId && x.DataHora == inicio
                && x.Situacao == SituacaoDaReserva.Reservada, ct);
        if (mesma is not null) return View(mesma);
        var feita = await ReservaDeHorarioService.Sobre(db).ReservarAsync(new NovaReserva(r.Name, phone, r.ConversationId,
            r.ProfessionalId, r.RoomId, inicio, r.Duration, r.ServiceCode, r.SpecialtyCode, r.Private, r.Insurance), actor, ct);
        return View(feita);
    }

    public async Task<ClinicReservation> Cancel(ClinicReservationCancel c, CancellationToken ct)
    {
        await DesteTelefone(c.Id, c.Phone, c.Revision, ct);
        return View(await ReservaDeHorarioService.Sobre(db).DesmarcarAsync(c.Id, c.Reason, Actor(c.Actor), ct));
    }

    public async Task<ClinicReservation> Reschedule(ClinicReservationReschedule c, CancellationToken ct)
    {
        await DesteTelefone(c.Id, c.Phone, c.Revision, ct);
        return View(await ReservaDeHorarioService.Sobre(db).RemarcarAsync(c.Id, Local(c.StartsAt), Actor(c.Actor), ct));
    }

    /// <summary>A reserva é do telefone da conversa e ninguém mexeu nela desde a leitura (a revisão bate).</summary>
    async Task DesteTelefone(int id, string? phone, string? revision, CancellationToken ct)
    {
        var p = Phone(phone);
        var r = await db.ReservasDeHorario.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || r.Telefone != p) throw new InvalidOperationException("Reserva não encontrada para este telefone.");
        if (revision != r.Versao.ToString(System.Globalization.CultureInfo.InvariantCulture))
            throw new InvalidOperationException("A reserva mudou. Atualize a conversa e confira antes de mexer.");
    }
}
