using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Crm.Bridge;

/// <summary>
/// "Quem é o paciente desta conversa?" (out/2026). O telefone não é identidade: a mãe e o
/// filho dividem o celular, e o paciente escreve do número do marido. A ponte devolve as
/// FICHAS candidatas — pelo telefone e, se a recepção digitar, pelo nome ou documento — com
/// o que permite CONFERIR (nascimento, fim do documento, convênio) e o que vem pela frente
/// (próximos horários e a resposta ao lembrete). Quem decide qual ficha é a recepção.
/// </summary>
public sealed class ClinicPatientBridge(ClinicaDbContext db)
{
    public const int MaxCards = 8, MaxUpcoming = 3;
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task<ClinicPatientCard[]> Lookup(ClinicPatientLookup input, CancellationToken ct)
    {
        var phone = ClinicAgendaBridge.Phone(input.Phone);
        var query = (input.Query ?? "").Trim();
        var digits = new string(query.Where(char.IsAsciiDigit).ToArray());
        if (phone.Length == 0 && query.Length < 3) return [];

        // Telefone: a ficha guarda o número como foi digitado, então a comparação é pelo
        // número normalizado — a mesma regra da agenda (ClinicAgendaBridge.Phone).
        var byPhone = phone.Length == 0 ? [] : (await db.Pacientes.AsNoTracking().Where(p => p.Telefone != null)
            .Select(p => new { p.Id, p.Telefone }).ToListAsync(ct)).Where(p => ClinicAgendaBridge.Phone(p.Telefone) == phone).Select(p => p.Id).ToHashSet();
        var byQuery = new List<int>();
        if (query.Length >= 3)
        {
            var upper = query.ToUpperInvariant();
            var q = db.Pacientes.AsNoTracking().Where(p => p.Nome.ToUpper().Contains(upper));
            byQuery = await q.OrderBy(p => p.Nome).Select(p => p.Id).Take(MaxCards).ToListAsync(ct);
            if (digits.Length >= 6)
                byQuery.AddRange((await db.Pacientes.AsNoTracking().Where(p => p.Documento != null).Select(p => new { p.Id, p.Documento }).ToListAsync(ct))
                    .Where(p => new string(p.Documento!.Where(char.IsAsciiDigit).ToArray()).Contains(digits)).Select(p => p.Id).Take(MaxCards));
        }
        var ids = byPhone.Concat(byQuery).Distinct().Take(MaxCards).ToArray();
        if (ids.Length == 0) return [];

        var patients = await db.Pacientes.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Nome, p.Telefone, p.DataNascimento, p.Documento, p.Convenio, p.ConvenioCodigo }).ToListAsync(ct);
        var insurers = await db.Convenios.AsNoTracking().ToDictionaryAsync(c => c.Codigo, c => c.Nome, ct);
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone).DateTime;
        var upcoming = await db.Agendamentos.AsNoTracking().Include(a => a.Profissional)
            .Where(a => ids.Contains(a.PacienteId) && a.Status == StatusAgendamento.Agendado && a.DataHora >= now.AddHours(-2))
            .OrderBy(a => a.DataHora).ToListAsync(ct);
        var upcomingIds = upcoming.Select(a => a.Id).ToArray();
        var contacts = await db.Contatos.AsNoTracking()
            .Where(c => c.Tipo == TipoContato.ConfirmacaoSessao && c.AgendamentoId != null && upcomingIds.Contains(c.AgendamentoId.Value)).ToListAsync(ct);
        var sessions = await db.Atendimentos.AsNoTracking().Where(a => ids.Contains(a.PacienteId) && a.RealizadoEm != null && a.EstornadoEm == null)
            .Select(a => new { a.PacienteId, a.Data, a.Modalidade }).ToListAsync(ct);

        return patients.Select(p =>
        {
            var last = sessions.Where(s => s.PacienteId == p.Id).OrderByDescending(s => s.Data).FirstOrDefault();
            var doc = new string((p.Documento ?? "").Where(char.IsAsciiDigit).ToArray());
            return new ClinicPatientCard(p.Id, p.Nome, ClinicAgendaBridge.Phone(p.Telefone),
                // Data implausível (importação) não vira idade nem nascimento na tela.
                IdadeDoPaciente.Implausivel(p.DataNascimento, DateOnly.FromDateTime(now)) ? null : p.DataNascimento,
                p.ConvenioCodigo is { } code && insurers.TryGetValue(code, out var nome) ? nome : RotulosEnum.De(p.Convenio),
                doc.Length >= 3 ? doc[^3..] : "", byPhone.Contains(p.Id), last?.Data, last is null ? "" : RotulosEnum.De(last.Modalidade),
                upcoming.Where(a => a.PacienteId == p.Id).Take(MaxUpcoming).Select(a => new ClinicPatientVisit(a.Id,
                    new DateTimeOffset(DateTime.SpecifyKind(a.DataHora, DateTimeKind.Unspecified), Zone.GetUtcOffset(a.DataHora)),
                    RotulosEnum.De(a.ModalidadePrevista), a.Profissional?.Nome ?? "", "scheduled",
                    ClinicAgendaBridge.AnswerOf(contacts.FirstOrDefault(c => c.AgendamentoId == a.Id)))).ToArray());
        })
        // A ficha do telefone vem primeiro; dentro dela, quem tem horário marcado.
        .OrderByDescending(c => c.PhoneMatch).ThenBy(c => c.Upcoming.Length == 0).ThenBy(c => c.Name).ToArray();
    }
}
