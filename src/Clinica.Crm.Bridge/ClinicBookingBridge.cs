using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Crm.Bridge;

// A secretária marca, consulta a agenda e cancela PELO CRM, sem abrir o Clinica (out/2026).
// O CRM já tinha as telas e o protocolo (reserva de 5 min → confirmar com o paciente →
// recibo); faltava quem respondesse do lado da clínica. As regras continuam as do Clinica:
// marcar passa pelo `AgendaService.AgendarAsync` (quem vai atender é obrigatório, guia no
// agendamento, psicologia), cancelar pelo `CancelarAsync` (que suspende as guias).
//
// ⚠️ A reserva NÃO bloqueia a agenda do balcão — a agenda da casa avisa e não impede, e uma
// trava que só o CRM respeitasse seria meia trava. O que ela garante é que o CRM nunca grava
// por cima de um horário tomado: a confirmação reconfere a vaga dentro de uma transação com a
// tabela travada, como a remarcação. O estado da reserva mora na TRILHA (`Auditoria`), uma
// linha por mudança, e é por isso que uma resposta HTTP perdida se recupera com "status".
public sealed class ClinicBookingBridge(ClinicaDbContext db, AgendaService agenda, Clinica.Application.Abstracoes.IClinicaRepositorio repo)
{
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static readonly TimeSpan HoldFor = TimeSpan.FromMinutes(5);
    const string Acao = "CrmReservaV1";
    const string CancelAcao = "CrmCancelamentoEquipe";
    static DateTime Local(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, Zone).DateTime;
    static DateTime NowLocal() => Local(DateTimeOffset.UtcNow);
    static DateTimeOffset Offset(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Unspecified), Zone.GetUtcOffset(time));

    sealed record Reserva(string Id, string Phone, string Actor, BookingQuery Query, DateTimeOffset StartsAt,
        DateTimeOffset ExpiresAt, string State, BookingReceipt? Receipt);

    static string ValidPhone(string? phone) =>
        phone is { Length: >= 10 and <= 15 } && phone.All(char.IsAsciiDigit) ? phone : throw new InvalidOperationException("Telefone inválido.");

    async Task<Paciente[]> PatientsOf(string phone, CancellationToken ct)
    {
        // O telefone gravado aceita máscara; a comparação é pelos dígitos, como no snapshot.
        // Os quatro últimos dígitos são contíguos em toda máscara usada no cadastro.
        var tail = phone[^4..];
        var candidates = await db.Pacientes.AsNoTracking().Where(p => p.Telefone != null && p.Telefone.Contains(tail)).ToListAsync(ct);
        return candidates.Where(p => ClinicAgendaBridge.Phone(p.Telefone) == phone).OrderBy(p => p.Nome).ToArray();
    }

    public async Task<BookingCatalog> Catalog(BookingCatalogQuery input, CancellationToken ct)
    {
        var phone = ValidPhone(input.Phone);
        var patients = (await PatientsOf(phone, ct)).Select(p => new BookingPatient(p.Id, p.Nome)).ToArray();
        var professionals = (await db.Profissionais.AsNoTracking().Where(p => p.Ativo).OrderBy(p => p.Nome).ToListAsync(ct))
            .Select(p => new BookingProfessional(p.Id, p.Rotulo, p.DuracaoPadraoMinutos ?? Agendamento.DuracaoPadraoMinutos,
                p.DescricaoJornada ?? "", !p.JornadaDeclarada,
                p.HabilitacoesAtendimento?.Select(h => new BookingQualification(h.ModalidadeCodigo, h.EspecialidadeCodigo)).ToArray(),
                p.EspecialidadeCodigo)).ToArray();
        var rooms = await db.Salas.AsNoTracking().Where(s => s.Ativa).OrderBy(s => s.Ordem).ThenBy(s => s.Nome)
            .Select(s => new BookingRoom(s.Id, s.Nome, s.Capacidade)).ToArrayAsync(ct);
        var modalities = await db.Modalidades.AsNoTracking().Where(m => m.Ativo).OrderBy(m => m.Nome).ToListAsync(ct);
        var services = modalities.Count > 0
            ? modalities.Select(m => new BookingService(m.Codigo, m.Nome, m.Base == ModalidadeAtendimento.Consulta)).ToArray()
            : CatalogoModalidades.Ativas.Select(m => new BookingService(m.Codigo, m.Nome, m.Base == ModalidadeAtendimento.Consulta)).ToArray();
        var specialties = await db.Especialidades.AsNoTracking().Where(s => s.Ativo).OrderBy(s => s.Nome)
            .Select(s => new Specialty(s.Codigo, s.Nome, s.Ativo)).ToArrayAsync(ct);
        return new(DateTimeOffset.UtcNow, patients, professionals, rooms, services, specialties);
    }

    // A agenda como a secretária a lê no balcão: o que está ocupado, por quem e onde. O nome
    // dos OUTROS pacientes não sai — a conversa é com UM paciente, e a tela só precisa saber
    // que o horário está tomado. O horário do próprio contato vem identificado.
    public async Task<BookingCalendar> Calendar(BookingCalendarQuery input, CancellationToken ct)
    {
        var phone = ValidPhone(input.Phone);
        if (input.Days is < 1 or > 14) throw new InvalidOperationException("Consulte de 1 a 14 dias.");
        var from = input.From.ToDateTime(TimeOnly.MinValue); var until = from.AddDays(input.Days);
        var items = await db.Agendamentos.AsNoTracking().Include(a => a.Paciente)
            .Where(a => a.DataHora >= from && a.DataHora < until
                && (a.Status == StatusAgendamento.Agendado || a.Status == StatusAgendamento.Realizado)
                && (input.ProfessionalId == null || a.ProfissionalId == input.ProfessionalId)
                && (input.RoomId == null || a.SalaId == input.RoomId))
            .OrderBy(a => a.DataHora).Take(2000).ToListAsync(ct);
        return new(DateTimeOffset.UtcNow, items.Select(a => {
            var mine = ClinicAgendaBridge.Phone(a.Paciente?.Telefone) == phone;
            return new BookingCalendarItem(Offset(a.DataHora), Offset(a.DataHora.AddMinutes(a.DuracaoEfetiva)), a.ProfissionalId, a.SalaId,
                a.Status == StatusAgendamento.Realizado ? "done" : "busy", mine, mine ? a.Id : null);
        }).ToArray());
    }

    async Task<(Paciente patient, Profissional professional)> Validate(BookingQuery q, CancellationToken ct)
    {
        var phone = ValidPhone(q.Phone);
        if (q.Duration is < 5 or > 240 || string.IsNullOrWhiteSpace(q.ServiceCode) || q.Through < q.From
            || q.Through.DayNumber - q.From.DayNumber > BuscaDeVagas.DiasMaximos || q.Before <= q.After)
            throw new InvalidOperationException("Pedido de vagas inválido.");
        var patient = (await PatientsOf(phone, ct)).FirstOrDefault(p => p.Id == q.PatientId)
            ?? throw new InvalidOperationException("Paciente não pertence a este telefone.");
        var professional = await db.Profissionais.AsNoTracking().SingleOrDefaultAsync(p => p.Id == q.ProfessionalId && p.Ativo, ct)
            ?? throw new InvalidOperationException("Profissional inativo ou inexistente.");
        if (q.RoomId is { } room && !await db.Salas.AnyAsync(s => s.Id == room && s.Ativa, ct))
            throw new InvalidOperationException("Sala inativa ou inexistente.");
        return (patient, professional);
    }

    // A busca da agenda devolve as N PRIMEIRAS vagas; com filtro de período ("após 17h", "só
    // quinta") as primeiras dez quase nunca passam, e a secretária leria "nenhuma vaga" numa
    // semana livre. Aqui o cálculo é o mesmo (`BuscaDeVagas.Calcular`), sem o teto, e o
    // filtro entra antes do corte. Reservas ativas de OUTRAS conversas contam como ocupadas.
    async Task<DateTimeOffset[]> FreeSlots(BookingQuery q, CancellationToken ct, string? ignoreHold = null)
    {
        var start = q.From.ToDateTime(q.After); var now = NowLocal();
        if (start < now) start = now;
        var until = q.Through.ToDateTime(TimeOnly.MinValue).AddDays(1);
        if (until <= start) return [];
        var professional = await repo.ObterProfissionalAsync(q.ProfessionalId, ct) ?? throw new InvalidOperationException("Profissional não encontrado.");
        var room = q.RoomId is { } rid ? await repo.ObterSalaAsync(rid, ct) : null;
        var busy = await repo.AgendamentosQueSobrepoemAsync(start.Date, until, ct);
        var blocks = await repo.BloqueiosNoPeriodoAsync(start.Date, until, ct);
        var holds = (await ActiveHolds(ct)).Where(h => h.Id != ignoreHold && (h.Query.ProfessionalId == q.ProfessionalId || q.RoomId != null && h.Query.RoomId == q.RoomId)).ToArray();
        var days = q.Weekdays is { Length: > 0 } w ? w.ToHashSet() : null;
        return BuscaDeVagas.Calcular(start, q.Duration, professional, busy, blocks, quantidade: 2000,
                diasMaximos: Math.Max(1, (until - start.Date).Days), sala: room, pacienteId: q.PatientId)
            .Where(v => v.Inicio < until && TimeOnly.FromDateTime(v.Inicio) >= q.After && TimeOnly.FromDateTime(v.Inicio.AddMinutes(q.Duration)) <= q.Before
                && (days is null || days.Contains((int)v.Inicio.DayOfWeek))
                && !holds.Any(h => Local(h.StartsAt) < v.Inicio.AddMinutes(q.Duration) && v.Inicio < Local(h.StartsAt).AddMinutes(h.Query.Duration)))
            .Select(v => Offset(v.Inicio)).Take(80).ToArray();
    }
    async Task<Reserva[]> ActiveHolds(CancellationToken ct)
    {
        var since = NowLocal().Add(-HoldFor).AddMinutes(-1);
        var rows = await db.Auditoria.AsNoTracking().Where(e => e.Acao == Acao && e.DataHora >= since && e.Detalhe != null)
            .OrderBy(e => e.Id).Select(e => e.Detalhe!).ToListAsync(ct);
        return rows.Select(d => JsonSerializer.Deserialize<Reserva>(d[(d.IndexOf(':', "CRM-RESERVA-V1:".Length) + 1)..])!)
            .GroupBy(r => r.Id).Select(g => g.Last()).Where(r => r.State == "held" && r.ExpiresAt > DateTimeOffset.UtcNow).ToArray();
    }

    public async Task<BookingSlots> Slots(BookingQuery q, CancellationToken ct)
    {
        var (_, professional) = await Validate(q, ct);
        var slots = await FreeSlots(q, ct);
        return new(DateTimeOffset.UtcNow, $"Vagas de {q.Duration} min com {professional.Rotulo}, de {q.From:dd/MM} a {q.Through:dd/MM}, "
            + $"das {q.After:HH\\:mm} às {q.Before:HH\\:mm}.", !professional.JornadaDeclarada, slots);
    }

    async Task<Reserva?> Latest(string id, CancellationToken ct)
    {
        var prefix = "CRM-RESERVA-V1:" + id + ":";
        var row = await db.Auditoria.AsNoTracking().Where(e => e.Acao == Acao && e.Detalhe != null && e.Detalhe.StartsWith(prefix))
            .OrderByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        return row is null ? null : JsonSerializer.Deserialize<Reserva>(row.Detalhe![prefix.Length..]);
    }
    void Record(Reserva r, int? patient) => db.Auditoria.Add(new()
    {
        Acao = Acao, Operador = "CRM · " + r.Actor, PacienteId = patient, DataHora = NowLocal(),
        Detalhe = "CRM-RESERVA-V1:" + r.Id + ":" + JsonSerializer.Serialize(r),
    });
    static BookingReservation View(Reserva r) => new(r.Id,
        r.State == "held" && r.ExpiresAt <= DateTimeOffset.UtcNow ? "expired" : r.State, r.StartsAt, r.ExpiresAt, r.Receipt);

    public async Task<BookingReservation> Hold(BookingHold input, CancellationToken ct)
    {
        if (!Guid.TryParseExact(input.Id, "N", out _) || string.IsNullOrWhiteSpace(input.Actor) || input.Actor.Length > 74)
            throw new InvalidOperationException("Comando inválido.");
        if (await Latest(input.Id, ct) is { } prior)
        {
            if (prior.StartsAt != input.StartsAt || prior.Query != input.Query) throw new InvalidOperationException("Reserva já usada para outro horário.");
            return View(prior);
        }
        var (patient, _) = await Validate(input.Query, ct);
        // Uma reserva por paciente: duas abertas ao mesmo tempo são o começo de duas marcações.
        if ((await ActiveHolds(ct)).Any(h => h.Query.PatientId == input.Query.PatientId)) throw new InvalidOperationException("Este paciente já tem uma reserva em andamento.");
        if (!(await FreeSlots(input.Query, ct)).Contains(input.StartsAt)) throw new InvalidOperationException("A vaga deixou de estar disponível.");
        var r = new Reserva(input.Id, input.Query.Phone, input.Actor, input.Query, input.StartsAt, DateTimeOffset.UtcNow.Add(HoldFor), "held", null);
        Record(r, patient.Id); await db.SaveChangesAsync(ct); return View(r);
    }

    public async Task<BookingReservation> Status(BookingAction input, CancellationToken ct)
    {
        var r = await Latest(input.Id, ct) ?? throw new InvalidOperationException("Reserva não encontrada.");
        if (r.Phone != input.Phone) throw new InvalidOperationException("Reserva de outro contato.");
        return View(r);
    }

    public async Task<BookingReservation> Release(BookingAction input, CancellationToken ct)
    {
        var r = await Latest(input.Id, ct) ?? throw new InvalidOperationException("Reserva não encontrada.");
        if (r.Phone != input.Phone || r.Actor != input.Actor) throw new InvalidOperationException("Reserva de outro contato ou atendente.");
        if (r.State != "held") return View(r);
        var released = r with { State = "released" }; Record(released, r.Query.PatientId);
        await db.SaveChangesAsync(ct); return View(released);
    }

    public Task<BookingReservation> Confirm(BookingAction input, CancellationToken ct) => TransacaoDaPonte.Executar(db, () => ConfirmCore(input, ct), ct);
    async Task<BookingReservation> ConfirmCore(BookingAction input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(db.Database.IsNpgsql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Agendamentos\" IN SHARE ROW EXCLUSIVE MODE", ct);
        var r = await Latest(input.Id, ct) ?? throw new InvalidOperationException("Reserva não encontrada.");
        if (r.Phone != input.Phone || r.Actor != input.Actor) throw new InvalidOperationException("Reserva de outro contato ou atendente.");
        // A confirmação repetida devolve o MESMO recibo — é o que permite recuperar uma resposta perdida.
        if (r.Receipt is not null) return View(r);
        if (r.State != "held" || r.ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Reserva vencida ou liberada. Consulte vagas de novo.");
        var q = r.Query; await Validate(q, ct);
        var local = Local(r.StartsAt);
        var conflicts = await agenda.ConflitosAsync(local, q.Duration, q.ProfessionalId, q.RoomId, q.PatientId, null, ct);
        if (conflicts.Count > 0 || !(await FreeSlots(q, ct, ignoreHold: r.Id)).Contains(r.StartsAt))
            throw new InvalidOperationException("A vaga deixou de estar disponível.");
        var created = await agenda.AgendarAsync(q.PatientId, local, CatalogoModalidades.Base(q.ServiceCode),
            "Marcado pelo CRM com o paciente (" + r.Actor + ")", OrigemAgendamento.Manual, ct,
            modalidadeCodigo: q.ServiceCode, especialidadeConsultaCodigo: q.SpecialtyCode,
            profissionalId: q.ProfessionalId, salaId: q.RoomId, duracaoMinutos: q.Duration, operador: "CRM · " + r.Actor);
        var withPatient = await db.Agendamentos.Include(a => a.Paciente).SingleAsync(a => a.Id == created.Id, ct);
        var done = r with { State = "confirmed", Receipt = new(created.Id, q.PatientId, Offset(created.DataHora), ClinicAgendaBridge.Revision(withPatient)) };
        Record(done, q.PatientId);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return View(done);
    }

    // Cancelar pela equipe é ato da SECRETÁRIA, com motivo: o mesmo `CancelarAsync` do balcão,
    // que suspende as guias abertas. Idempotente pelo comando, como a remarcação.
    public Task<ClinicCancelResult> Cancel(ClinicCancel input, CancellationToken ct) => TransacaoDaPonte.Executar(db, () => CancelCore(input, ct), ct);
    async Task<ClinicCancelResult> CancelCore(ClinicCancel input, CancellationToken ct)
    {
        if (!Guid.TryParseExact(input.CommandId, "N", out _) || string.IsNullOrWhiteSpace(input.Actor) || input.Actor.Length > 74
            || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 300 || input.Revision is not { Length: 64 })
            throw new InvalidOperationException("Comando inválido.");
        var phone = ValidPhone(input.Phone);
        var prefix = "CRM-CANCEL-V1:" + input.CommandId + ":";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await db.Auditoria.AsNoTracking().FirstOrDefaultAsync(e => e.Acao == CancelAcao && e.Detalhe != null && e.Detalhe.StartsWith(prefix), ct);
        if (prior is not null)
        {
            var hit = prior.Detalhe![prefix.Length..].Split(':', 2);
            if (hit[0] != input.AppointmentId.ToString()) throw new InvalidOperationException("Comando já utilizado.");
            return new(input.AppointmentId, "cancelled", []);
        }
        var a = await db.Agendamentos.Include(x => x.Paciente).SingleOrDefaultAsync(x => x.Id == input.AppointmentId, ct)
            ?? throw new InvalidOperationException("Horário não encontrado.");
        if (a.PacienteId != input.PatientId || ClinicAgendaBridge.Phone(a.Paciente?.Telefone) != phone || ClinicAgendaBridge.Revision(a) != input.Revision
            || a.Status != StatusAgendamento.Agendado || a.ChegadaEm is not null || a.InicioAtendimentoEm is not null)
            throw new InvalidOperationException("Confira paciente e horário atualizado na clínica.");
        var warnings = await agenda.CancelarAsync(a.Id, "CRM · " + input.Actor + " · " + input.Reason.Trim(), ct);
        db.Auditoria.Add(new() { Acao = CancelAcao, Operador = "CRM · " + input.Actor, PacienteId = a.PacienteId, DataHora = NowLocal(),
            Detalhe = prefix + a.Id + ":" + input.Reason.Trim() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(a.Id, "cancelled", warnings.ToArray());
    }
}
