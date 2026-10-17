using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Crm.Bridge;

/// <summary>
/// As três filas da tela "Relacionamento" do CRM, lidas do Clinica e nunca decididas lá.
///
/// - <b>absent</b>: quem não vem há 15 dias ou mais (até um ano) e não tem horário marcado.
///   Quem tem sessão futura não sumiu — já voltou, só ainda não veio.
/// - <b>new-bsv</b>: as duas leituras de "novo no BSV": o indicado pelo profissional
///   (acompanhamento <c>NovoBsv</c> aberto) que ainda não fez a 1ª sessão, e quem fez a
///   PRIMEIRA sessão de BSV nos últimos 30 dias (é a fase em que o paciente desiste).
/// - <b>renew</b>: consulta EMITIDA a renovar (<c>ARenovar</c>, a mesma regra da agenda do
///   balcão, pelo mesmo <see cref="ConsultaService"/> — nunca uma segunda definição).
/// </summary>
public sealed class ClinicRelationshipBridge(ClinicaDbContext db, ConsultaService consultas)
{
    public const int MinimumAbsentDays = 15, MaximumAbsentDays = 365, NewBsvWindowDays = 30;
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    static bool Bsv(ModalidadeAtendimento m) => m is ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura;

    public async Task<RelationshipSnapshot> Snapshot(CancellationToken ct)
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone);
        var today = DateOnly.FromDateTime(now.DateTime);
        var sessions = await db.Atendimentos.AsNoTracking().Where(a => a.RealizadoEm != null && a.EstornadoEm == null && a.Data <= today)
            .Select(a => new { a.PacienteId, a.Modalidade, a.Data }).ToListAsync(ct);
        var last = sessions.GroupBy(s => s.PacienteId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Data).First());
        var count = sessions.GroupBy(s => s.PacienteId).ToDictionary(g => g.Key, g => g.Count());
        var firstBsv = sessions.Where(s => Bsv(s.Modalidade)).GroupBy(s => s.PacienteId).ToDictionary(g => g.Key, g => g.Min(s => s.Data));
        var cases = await db.Acompanhamentos.AsNoTracking().Where(c => c.Tipo == TipoAcompanhamento.NovoBsv && c.EncerradoEm == null).ToListAsync(ct);
        var renew = (await consultas.ListarAsync(today, ct)).Where(s => s.ARenovar).ToList();
        var future = (await db.Agendamentos.AsNoTracking().Where(a => a.Status == StatusAgendamento.Agendado && a.DataHora >= now.DateTime)
            .Select(a => new { a.PacienteId, a.DataHora, a.ModalidadePrevista }).ToListAsync(ct))
            .GroupBy(a => a.PacienteId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.DataHora).ToList());
        // O nome da OPERADORA vem do cadastro (o catálogo em memória não é carregado nesta ponte);
        // sem código cadastrado, a família — nunca o nome de outra operadora.
        var insurers = await db.Convenios.AsNoTracking().ToDictionaryAsync(c => c.Codigo, c => c.Nome, ct);
        var professionals = await db.Profissionais.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Nome, ct);

        var absent = last.Where(p => today.DayNumber - p.Value.Data.DayNumber is >= MinimumAbsentDays and <= MaximumAbsentDays && !future.ContainsKey(p.Key)).Select(p => p.Key);
        var started = firstBsv.Where(p => today.DayNumber - p.Value.DayNumber <= NewBsvWindowDays).Select(p => p.Key);
        var ids = absent.Concat(started).Concat(cases.Select(c => c.PacienteId)).Concat(renew.Select(r => r.PacienteId)).Distinct().ToArray();
        var patients = await db.Pacientes.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Nome, p.Telefone, p.Convenio, p.ConvenioCodigo }).ToDictionaryAsync(p => p.Id, ct);
        var consent = (await new ClinicaRepositorio(db).PacientesComConsentimentoVigenteAsync(FinalidadeConsentimento.ComunicacaoEMarketing, ids, ct)).ToHashSet();
        var shared = (await db.Pacientes.AsNoTracking().Select(p => p.Telefone).ToListAsync(ct))
            .GroupBy(ClinicAgendaBridge.Phone).Where(g => g.Key.Length > 0 && g.Count() > 1).Select(g => g.Key).ToHashSet();

        var result = new List<RelationshipEntry>();
        void Add(string list, int pid, DateOnly reference, int days, string detail, string situation, DateTimeOffset? next = null)
        {
            if (!patients.TryGetValue(pid, out var p)) return;
            var phone = ClinicAgendaBridge.Phone(p.Telefone);
            var block = phone.Length == 0 ? "Telefone ausente ou inválido" : shared.Contains(phone) ? "Telefone compartilhado: conferir paciente" : "";
            next ??= future.TryGetValue(pid, out var f) ? Offset(f[0].DataHora) : null;
            result.Add(new(list, pid, p.Nome, phone, reference, days, detail, situation, consent.Contains(pid), block, next,
                p.ConvenioCodigo is { } codigo && insurers.TryGetValue(codigo, out var nomeConvenio) ? nomeConvenio : RotulosEnum.De(p.Convenio), count.GetValueOrDefault(pid)));
        }

        foreach (var pid in absent)
        {
            var s = last[pid];
            Add("absent", pid, s.Data, today.DayNumber - s.Data.DayNumber,
                $"Última sessão: {RotulosEnum.De(s.Modalidade)} em {s.Data:dd/MM/yyyy}", "Sem horário marcado");
        }
        var bsvDone = new HashSet<int>();
        foreach (var pid in started)
        {
            var first = firstBsv[pid]; bsvDone.Add(pid);
            var bsvCount = sessions.Count(s => s.PacienteId == pid && Bsv(s.Modalidade));
            var nextBsv = future.TryGetValue(pid, out var f) ? f.FirstOrDefault(a => Bsv(a.ModalidadePrevista)) : null;
            Add("new-bsv", pid, first, today.DayNumber - first.DayNumber, $"Começou o BSV em {first:dd/MM/yyyy} · {bsvCount} sessão(ões) de BSV",
                nextBsv is null ? "Sem próxima sessão de BSV marcada" : "Próxima sessão de BSV marcada", nextBsv is null ? null : Offset(nextBsv.DataHora));
        }
        foreach (var c in cases.Where(c => !bsvDone.Contains(c.PacienteId) && !sessions.Any(s => s.PacienteId == c.PacienteId && Bsv(s.Modalidade) && s.Data >= DateOnly.FromDateTime(c.ReferenciaEm))))
        {
            var reference = DateOnly.FromDateTime(c.ReferenciaEm);
            var nextBsv = future.TryGetValue(c.PacienteId, out var f) ? f.FirstOrDefault(a => Bsv(a.ModalidadePrevista)) : null;
            var by = c.ProfissionalId is { } id && professionals.TryGetValue(id, out var nome) ? " por " + nome : "";
            Add("new-bsv", c.PacienteId, reference, today.DayNumber - reference.DayNumber, $"Indicado para o BSV{by} em {reference:dd/MM/yyyy} · ainda não fez a 1ª sessão",
                nextBsv is not null ? "1ª sessão de BSV marcada" : RegrasAcompanhamento.Rotulo(c.Etapa), nextBsv is null ? null : Offset(nextBsv.DataHora));
        }
        foreach (var r in renew)
            Add("renew", r.PacienteId, r.Vencimento ?? today, r.DiasParaVencer ?? 0, r.AvisoRenovacao ?? "Consulta a renovar", r.Vencida ? "Consulta vencida" : "Vence em breve");

        return new(DateTimeOffset.UtcNow, result.OrderBy(e => e.List).ThenByDescending(e => e.List == "renew" ? -e.Days : e.Days).ThenBy(e => e.Name).ToArray());
    }

    static DateTimeOffset Offset(DateTime d) => new(DateTime.SpecifyKind(d, DateTimeKind.Unspecified), Zone.GetUtcOffset(d));
}
