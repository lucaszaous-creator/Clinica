using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Crm.Bridge;

// v1 preserves patient, professional, room, duration and modality; the model cannot call these endpoints.
public sealed class ClinicSchedulingBridge(ClinicaDbContext db, AgendaService agenda, BuscaDeVagasService vacancies)
{
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    static DateTime Local(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, Zone).DateTime;
    static DateTimeOffset Offset(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Unspecified), Zone.GetUtcOffset(time));
    async Task<Agendamento> Appointment(int id, int patient, string revision, string phone, CancellationToken ct) {
        if (id <= 0 || patient <= 0 || revision?.Length != 64 || phone is null || phone.Length is < 10 or > 15 || !phone.All(char.IsAsciiDigit))
            throw new InvalidOperationException("Identificação inválida.");
        var a = await db.Agendamentos.Include(a => a.Paciente).Include(a => a.Profissional).Include(a => a.Sala).SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException("Horário não encontrado.");
        if (a.PacienteId != patient || ClinicAgendaBridge.Phone(a.Paciente?.Telefone) != phone || ClinicAgendaBridge.Revision(a) != revision
            || a.Status != StatusAgendamento.Agendado || a.DataHora <= Local(DateTimeOffset.UtcNow)
            || a.ChegadaEm is not null || a.InicioAtendimentoEm is not null || a.AtendimentoId is not null || a.Profissional?.Ativo != true || a.Sala?.Ativa == false)
            throw new InvalidOperationException("Confira paciente e horário atualizado na clínica.");
        return a;
    }
    static void ValidateTime(DateTimeOffset time) {
        if (time <= DateTimeOffset.UtcNow || time > DateTimeOffset.UtcNow.AddDays(31) || time.Second != 0 || time.Millisecond != 0)
            throw new InvalidOperationException("Use um horário futuro nos próximos 31 dias.");
    }
    public async Task<ClinicSlots> Slots(ClinicSlotQuery input, CancellationToken ct) {
        ValidateTime(input.From);
        var a = await Appointment(input.AppointmentId, input.PatientId, input.Revision, input.Phone, ct);
        var result = await vacancies.ProximasComRecursosAsync(a.ProfissionalId!.Value, Local(input.From), a.DuracaoEfetiva, a.SalaId, a.PacienteId, ct);
        return new(result.Criterio, result.Vagas.Select(v => Offset(v.Inicio)).Where(t => t <= DateTimeOffset.UtcNow.AddDays(31)).ToArray());
    }
    public async Task<ClinicRescheduleResult> Reschedule(ClinicReschedule input, CancellationToken ct) {
        if (!Guid.TryParseExact(input.CommandId, "N", out _) || string.IsNullOrWhiteSpace(input.Actor) || input.Actor.Length > 74)
            throw new InvalidOperationException("Comando inválido.");
        var prefix = "CRM-AGENDA-V1:" + input.CommandId + ":";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        await using var tx = await db.Database.BeginTransactionAsync(db.Database.IsNpgsql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, ct);
        // Serialize bridge submissions and block concurrent appointment writes during read/revalidation/write.
        // The clinic may explicitly override conflicts later; this bridge never overrides one.
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"Agendamentos\" IN SHARE ROW EXCLUSIVE MODE", ct);
        var prior = await db.Auditoria.AsNoTracking().SingleOrDefaultAsync(e => e.Acao == "CrmRemarcacaoV1" && e.Detalhe != null && e.Detalhe.StartsWith(prefix), ct);
        if (prior is not null) {
            var marker = prefix + hash + ":";
            if (!prior.Detalhe!.StartsWith(marker, StringComparison.Ordinal)) throw new InvalidOperationException("Comando já utilizado para outra alteração.");
            return JsonSerializer.Deserialize<ClinicRescheduleResult>(prior.Detalhe[marker.Length..])!;
        }
        ValidateTime(input.StartsAt);
        var a = await Appointment(input.AppointmentId, input.PatientId, input.Revision, input.Phone, ct);
        if (Local(input.StartsAt) == a.DataHora) throw new InvalidOperationException("Escolha um novo horário.");
        var conflicts = await agenda.ConflitosAsync(Local(input.StartsAt), a.DuracaoEfetiva, a.ProfissionalId, a.SalaId, a.PacienteId, a.Id, ct);
        if (conflicts.Count > 0) throw new InvalidOperationException("A vaga deixou de estar disponível. Consulte novamente.");
        // Recheck the proposed slot against the actual search rules, including opening hours when no schedule was declared.
        var available = await vacancies.ProximasComRecursosAsync(a.ProfissionalId!.Value, Local(input.StartsAt), a.DuracaoEfetiva, a.SalaId, a.PacienteId, ct);
        if (!available.Vagas.Any(v => v.Inicio == Local(input.StartsAt))) throw new InvalidOperationException("Horário fora das vagas da clínica.");
        var previous = Offset(a.DataHora);
        await agenda.RemarcarAsync(a.Id, Local(input.StartsAt), a.Observacoes, operador: "CRM · " + input.Actor, ct: ct);
        var contact = await db.Contatos.SingleOrDefaultAsync(c => c.Tipo == TipoContato.ConfirmacaoSessao && c.AgendamentoId == a.Id, ct);
        if (contact is not null) {
            db.Auditoria.Add(new() { Acao = "CrmConfirmacaoReaberta", Operador = "CRM · " + input.Actor, PacienteId = a.PacienteId,
                DataHora = Local(DateTimeOffset.UtcNow), Detalhe = JsonSerializer.Serialize(new { appointmentId = a.Id, contact.Referencia, contact.Status, contact.EnviadoEm, contact.RespondidoEm }) });
            // Preserve the prior comment; move delivery/response timestamps to the audit before reopening.
            contact.Referencia = DateOnly.FromDateTime(a.DataHora); contact.Status = StatusContato.Pendente;
            contact.EnviadoEm = null; contact.EnviadoPor = null; contact.RespondidoEm = null;
        }
        var result = new ClinicRescheduleResult(a.Id, a.PacienteId, previous, Offset(a.DataHora), ClinicAgendaBridge.Revision(a));
        // Receipt and clinical changes commit together, permitting recovery after a lost HTTP response.
        db.Auditoria.Add(new() { Acao = "CrmRemarcacaoV1", Operador = "CRM · " + input.Actor, PacienteId = a.PacienteId,
            DataHora = Local(DateTimeOffset.UtcNow), Detalhe = prefix + hash + ":" + JsonSerializer.Serialize(result) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
    }
}
