using System.Data;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Infrastructure;

public sealed record PendenciaConclusao(int AgendamentoId, string Paciente, string Profissional, DateTime Data, string Motivo);

/// <summary>Conclusão por prazo é um ato do sistema, nunca uma assinatura ou declaração médica.</summary>
public sealed class ConclusaoAutomaticaService(ClinicaDbContext db, IClinicaRepositorio repo,
    AgendaService agenda, PoliticaConclusaoService politica)
{
    public static DateTime Agora => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").DateTime;
    private IQueryable<Agendamento> Abertos() => db.Agendamentos.Where(a =>
        a.Status == StatusAgendamento.Agendado || a.Status == StatusAgendamento.Realizado && a.FimAtendimentoEm == null);

    public async Task<int[]> CandidatosAsync(DateTime agora, int depois = 0, CancellationToken ct = default)
    {
        var regra = await politica.ObterAsync(ct);
        if (!regra.Automatica) return [];
        var limite = agora.AddHours(-regra.Horas);
        return await Abertos().AsNoTracking().Where(a => a.Id > depois && a.DataHora <= agora
            && db.Evolucoes.Any(e => e.AgendamentoId == a.Id && e.CanceladaEm == null
                && (e.AtualizadoEm ?? e.CriadoEm) >= regra.AtivadaEm && (e.AtualizadoEm ?? e.CriadoEm) <= limite))
            .OrderBy(a => a.Id).Select(a => a.Id).Take(100).ToArrayAsync(ct);
    }

    private async Task<string?> ImpedimentoAsync(Agendamento a, PoliticaConclusao regra, DateTime agora, CancellationToken ct)
    {
        var evolucoes = await db.Evolucoes.AsNoTracking().Where(e => e.AgendamentoId == a.Id && e.CanceladaEm == null).ToListAsync(ct);
        if (evolucoes.Count != 1) return "Confira o vínculo: a sessão precisa de uma evolução médica vigente e inequívoca.";
        var e = evolucoes[0];
        if (e.ProfissionalId is null || e.ProfissionalId != a.ProfissionalId || e.PacienteId != a.PacienteId
            || (!ProntuarioService.TemRegistro(e) && !await db.MapasCorporais.AnyAsync(m => m.EvolucaoId == e.Id
                && (m.Pontos.Any() || m.Observacoes != null && m.Observacoes.Trim() != ""), ct)))
            return "Confira a evolução e o médico responsável antes de concluir.";
        var salva = e.AtualizadoEm ?? e.CriadoEm;
        if (salva < regra.AtivadaEm || salva.AddHours(regra.Horas) > agora)
            return "O prazo conta a partir da última gravação da evolução médica, após ativar a regra.";
        if (regra.ExigeEnfermagem(a) && !await repo.TemEvolucaoEnfermagemVigenteNoHorarioAsync(a.Id, ct))
            return "Prazo vencido: falta evolução de enfermagem vinculada. Registrar no atendimento de enfermagem; a sessão permanece aberta.";
        return null;
    }

    public async Task<bool> ConcluirSeVencidoAsync(int id, DateTime agora, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260916, {id})", ct);
        // Releitura dentro da transação: outra estação ou o médico pode ter acabado de concluir.
        db.ChangeTracker.Clear();
        var regra = await politica.ObterAsync(ct);
        if (!regra.Automatica) return false;
        var a = await Abertos().SingleOrDefaultAsync(a => a.Id == id, ct);
        if (a is null || a.DataHora > agora || await ImpedimentoAsync(a, regra, agora, ct) is not null) return false;
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria { Operador = "sistema:conclusao-automatica",
            Acao = "ConclusaoAutomaticaPorPrazo", PacienteId = a.PacienteId,
            Detalhe = $"Sessão {id}; prazo de {regra.Horas} horas após evolução salva. Encerramento administrativo automático; não representa nova avaliação ou assinatura médica." }, ct);
        // Mesmo núcleo da conclusão manual: mantém atendimento original e códigos existentes.
        await agenda.ConcluirAtendimentoClinicoAsync(id, "sistema:conclusao-automatica", ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<PendenciaConclusao>> PendenciasAsync(DateTime agora, int? id = null, CancellationToken ct = default)
    {
        var regra = await politica.ObterAsync(ct);
        if (!regra.Automatica) return [];
        var limite = agora.AddHours(-regra.Horas);
        var lista = await Abertos().AsNoTracking().Include(a => a.Paciente).Include(a => a.Profissional)
            .Where(a => (id == null || a.Id == id) && a.DataHora <= agora
                && db.Evolucoes.Any(e => e.AgendamentoId == a.Id && e.CanceladaEm == null
                    && (e.AtualizadoEm ?? e.CriadoEm) >= regra.AtivadaEm && (e.AtualizadoEm ?? e.CriadoEm) <= limite))
            .OrderBy(a => a.DataHora).Take(100).ToListAsync(ct);
        var resultado = new List<PendenciaConclusao>();
        foreach (var a in lista)
            resultado.Add(new(a.Id, a.Paciente?.Nome ?? "", a.Profissional?.Nome ?? "", a.DataHora,
                await ImpedimentoAsync(a, regra, agora, ct) ?? "Prazo vencido: aguardando a próxima verificação automática."));
        return resultado;
    }

    public static async Task AcompanharAsync(IServiceScopeFactory scopes, CancellationToken ct, Action<int>? avisar = null)
    {
        var ultimoAviso = DateTime.MinValue;
        var quantidadeAnterior = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                int depois = 0;
                while (!ct.IsCancellationRequested)
                {
                    int[] ids;
                    using (var scope = scopes.CreateScope())
                        ids = await scope.ServiceProvider.GetRequiredService<ConclusaoAutomaticaService>().CandidatosAsync(Agora, depois, ct);
                    if (ids.Length == 0) break;
                    foreach (var id in ids)
                    {
                        try
                        {
                            using var scope = scopes.CreateScope();
                            await scope.ServiceProvider.GetRequiredService<ConclusaoAutomaticaService>().ConcluirSeVencidoAsync(id, Agora, ct);
                        }
                        catch (Exception ex) when (!ct.IsCancellationRequested)
                        { Clinica.Application.Diagnostico.Registrar($"Conclusão automática pendente na sessão {id}; será reavaliada", ex); }
                    }
                    depois = ids[^1];
                }
                if (avisar is not null)
                {
                    using var scope = scopes.CreateScope();
                    var pendencias = await scope.ServiceProvider.GetRequiredService<ConclusaoAutomaticaService>().PendenciasAsync(Agora, ct: ct);
                    if (pendencias.Count > 0 && (pendencias.Count != quantidadeAnterior || Agora - ultimoAviso >= TimeSpan.FromHours(1)))
                    { avisar(pendencias.Count); ultimoAviso = Agora; }
                    quantidadeAnterior = pendencias.Count;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            { Clinica.Application.Diagnostico.Registrar("Verificação automática de sessões indisponível", ex); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
        }
    }
}
