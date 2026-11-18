using System.Data;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
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

    public const string Operador = "sistema:conclusao-automatica";

    public async Task<int[]> CandidatosAsync(DateTime agora, int depois = 0, CancellationToken ct = default)
    {
        var regra = await politica.ObterAsync(ct);
        if (!regra.Automatica) return [];
        var limite = agora.AddHours(-regra.Horas);
        return await Abertos().AsNoTracking().Where(a => a.Id > depois && a.DataHora <= agora
            && db.Evolucoes.Any(e => e.AgendamentoId == a.Id && e.CanceladaEm == null
                && (e.AtualizadoEm ?? e.CriadoEm) >= regra.AtivadaEm && (e.AtualizadoEm ?? e.CriadoEm) <= limite)
            // Texto escrito e não salvo ainda vale: a sessão com rascunho aberto espera o prazo
            // DELE (rascunho salvo e sessão encerrada juntos), e não fecha por cima com o velho.
            && !db.RascunhosEvolucao.Any(r => r.AgendamentoId == a.Id && r.EncerradoEm == null))
            .OrderBy(a => a.Id).Select(a => a.Id).Take(100).ToArrayAsync(ct);
    }

    private async Task<string?> ImpedimentoAsync(Agendamento a, PoliticaConclusao regra, DateTime agora, CancellationToken ct,
        bool gravadaAgoraPorPrazo = false)
    {
        if (await db.RascunhosEvolucao.AnyAsync(r => r.AgendamentoId == a.Id && r.EncerradoEm == null, ct))
            return "Há texto escrito e não salvo: ele é salvo e a sessão encerrada no prazo, contado da última alteração.";
        var evolucoes = await db.Evolucoes.AsNoTracking().Where(e => e.AgendamentoId == a.Id && e.CanceladaEm == null).ToListAsync(ct);
        if (evolucoes.Count != 1) return "Confira o vínculo: a sessão precisa de uma evolução médica vigente e inequívoca.";
        var e = evolucoes[0];
        if (e.ProfissionalId is null || e.ProfissionalId != a.ProfissionalId || e.PacienteId != a.PacienteId
            || (!ProntuarioService.TemRegistro(e) && !await db.MapasCorporais.AnyAsync(m => m.EvolucaoId == e.Id
                && (m.Pontos.Any() || m.Observacoes != null && m.Observacoes.Trim() != ""), ct)))
            return "Confira a evolução e o médico responsável antes de concluir.";
        var salva = e.AtualizadoEm ?? e.CriadoEm;
        // Gravada AGORA pelo prazo do rascunho: o prazo já venceu, contado da última alteração.
        if (!gravadaAgoraPorPrazo && (salva < regra.AtivadaEm || salva.AddHours(regra.Horas) > agora))
            return "O prazo conta a partir da última gravação da evolução médica, após ativar a regra.";
        if (regra.ExigeEnfermagem(a) && !await repo.TemEvolucaoEnfermagemVigenteNoHorarioAsync(a.Id, ct))
            return "Prazo vencido: falta evolução de enfermagem vinculada. Registrar no atendimento de enfermagem; a sessão permanece aberta.";
        return null;
    }

    // A transação roda DENTRO da estratégia de execução: o AddClinica liga o EnableRetryOnFailure
    // do Npgsql, e o EF recusa transação aberta à mão sob estratégia que repete. Sem isto, no
    // Postgres TODA conclusão automática estourava ("does not support user-initiated
    // transactions"), ia ao log e era reavaliada a cada minuto, para sempre — as sessões
    // vencidas desde set/2026 nunca fecharam (achado em 22/10/2026). A releitura logo abaixo
    // (ChangeTracker.Clear) é o que torna a repetição segura.
    public Task<bool> ConcluirSeVencidoAsync(int id, DateTime agora, CancellationToken ct = default)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(c => ConcluirNaTransacaoAsync(id, agora, c), ct);

    private async Task<bool> ConcluirNaTransacaoAsync(int id, DateTime agora, CancellationToken ct)
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
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria { Operador = Operador,
            Acao = "ConclusaoAutomaticaPorPrazo", PacienteId = a.PacienteId,
            Detalhe = $"Sessão {id}; prazo de {regra.Horas} horas após evolução salva. Encerramento administrativo automático; não representa nova avaliação ou assinatura médica." }, ct);
        // Mesmo núcleo da conclusão manual: mantém atendimento original e códigos existentes.
        await agenda.ConcluirAtendimentoClinicoAsync(id, Operador, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    // ==================== Conclusão automática POR PROFISSIONAL (nov/2026) ====================
    //
    // Pedido da gerência: a médica que não usa o sistema. Ninguém conclui as sessões dela, então
    // as guias não nascem e o faturamento não dá baixa. Com o prazo no cadastro do profissional
    // (Profissional.ConcluirAutomaticamenteHoras), o horário com CHEGADA registrada se conclui
    // sozinho N horas depois do horário marcado, pelo MESMO núcleo do "Concluir". Não depende da
    // política de conclusão por prazo acima (essa conta da evolução salva; esta, do horário) e não
    // escreve prontuário. A regra e as frases: Application/Modelos/ConclusaoPorProfissional.

    /// <summary>
    /// Os horários vencidos dos profissionais com a regra ligada, ainda em aberto. <paramref name="comChegada"/>:
    /// os que se concluem (chegada registrada) ou os que viram aviso (sem chegada). A conta do prazo
    /// (horário + N horas, N por profissional) é feita aqui na memória, sobre um recorte pequeno: o banco
    /// filtra estado, chegada, profissional ligado e uma janela de dias.
    /// </summary>
    private async Task<List<(Agendamento Horario, Profissional Profissional, int Horas)>> VencidosPorProfissionalAsync(
        DateTime agora, bool comChegada, int depois, CancellationToken ct)
    {
        var linhas = await ConsultaAbertosDosProfissionaisComPrazo(agora, comChegada, depois).ToListAsync(ct);
        return linhas
            .Select(a => (Horario: a, Profissional: a.Profissional!, Horas: ConclusaoPorProfissional.Horas(a.Profissional)))
            .Where(l => l.Horas is { } h && ConclusaoPorProfissional.Venceu(l.Horario.DataHora, h, agora))
            .Select(l => (l.Horario, l.Profissional, l.Horas!.Value))
            .ToList();
    }

    /// <summary>O recorte do banco (estado, chegada, profissional com a regra, janela de dias) — público para a rede de tradução.</summary>
    public IQueryable<Agendamento> ConsultaAbertosDosProfissionaisComPrazo(DateTime agora, bool comChegada, int depois)
    {
        var desde = agora.AddDays(-ConclusaoPorProfissional.DiasDoAvisoSemChegada);
        var ultimoPossivel = agora.AddHours(-1);
        var abertos = db.Agendamentos.AsNoTracking().Include(a => a.Profissional)
            .Where(a => a.Id > depois && a.Status == StatusAgendamento.Agendado
                && a.DataHora >= desde && a.DataHora <= ultimoPossivel
                && a.Profissional != null && a.Profissional.ConcluirAutomaticamenteHoras > 0);
        abertos = comChegada ? abertos.Where(a => a.ChegadaEm != null) : abertos.Where(a => a.ChegadaEm == null);
        return abertos.OrderBy(a => a.Id).Take(500);
    }

    /// <summary>Os horários com chegada e prazo vencido — os que <see cref="ConcluirPorProfissionalAsync"/> conclui.</summary>
    public async Task<int[]> CandidatosPorProfissionalAsync(DateTime agora, int depois = 0, CancellationToken ct = default)
        => (await VencidosPorProfissionalAsync(agora, comChegada: true, depois, ct)).Select(l => l.Horario.Id).Take(100).ToArray();

    /// <summary>
    /// Conclui UM horário pela regra do profissional, se ele ainda está em aberto, com chegada e vencido.
    /// Mesma transação serializável e mesmo bloqueio do horário que a conclusão por prazo (as duas, o
    /// médico e a outra estação disputam o mesmo horário): rodar duas vezes, ou em dois lugares ao mesmo
    /// tempo, conclui uma vez só — a releitura dentro da transação vê o Realizado da primeira.
    /// </summary>
    public Task<bool> ConcluirPorProfissionalAsync(int id, DateTime agora, CancellationToken ct = default)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(c => ConcluirPorProfissionalNaTransacaoAsync(id, agora, c), ct);

    private async Task<bool> ConcluirPorProfissionalNaTransacaoAsync(int id, DateTime agora, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20260916, {id})", ct);
        db.ChangeTracker.Clear();
        var a = await db.Agendamentos.Include(x => x.Profissional).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (a is not { Status: StatusAgendamento.Agendado, ChegadaEm: not null, Profissional: { } profissional }
            || ConclusaoPorProfissional.Horas(profissional) is not { } horas
            || !ConclusaoPorProfissional.Venceu(a.DataHora, horas, agora))
            return false;

        var operador = ConclusaoPorProfissional.Operador(profissional, horas);
        a.ConcluidoAutomaticamenteEm = agora;
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria { Operador = operador,
            Acao = "ConclusaoAutomaticaPorProfissional", PacienteId = a.PacienteId,
            Detalhe = ConclusaoPorProfissional.Detalhe(a, horas, agora) }, ct);
        // O MESMO núcleo do botão Concluir: atendimento, códigos (as guias pendentes de baixa),
        // particular, pacote e a trilha "PresencaConfirmada" — num SaveChanges só, com o carimbo acima.
        await agenda.ConcluirAtendimentoClinicoAsync(id, operador, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>
    /// Os horários SEM chegada que passaram do prazo, por profissional — o aviso do sino da recepção
    /// ("3 horários sem chegada na agenda de Dra. Ana: confirme falta ou presença"). Dos últimos
    /// <see cref="ConclusaoPorProfissional.DiasDoAvisoSemChegada"/> dias.
    /// </summary>
    public async Task<IReadOnlyList<HorariosSemChegada>> SemChegadaAsync(DateTime agora, CancellationToken ct = default)
        => (await VencidosPorProfissionalAsync(agora, comChegada: false, 0, ct))
            .GroupBy(l => l.Profissional.Id)
            .Select(g =>
            {
                var p = g.First().Profissional;
                var nome = string.IsNullOrWhiteSpace(p.NomeCurto) ? p.Nome : p.NomeCurto!;
                return new HorariosSemChegada(p.Id, nome, g.Count(), g.Min(l => l.Horario.DataHora),
                    ConclusaoPorProfissional.ChaveSemChegada(p.Id, g.Max(l => l.Horario.Id), g.Count()));
            })
            .OrderBy(h => h.MaisAntigo)
            .ToList();

    /// <summary>Os rascunhos abertos cuja última alteração passou do prazo (depois de ativar a regra).</summary>
    public async Task<int[]> RascunhosVencidosAsync(DateTime agora, int depois = 0, CancellationToken ct = default)
    {
        var regra = await politica.ObterAsync(ct);
        if (!regra.Automatica) return [];
        var limite = agora.AddHours(-regra.Horas);
        return await db.RascunhosEvolucao.AsNoTracking()
            .Where(r => r.Id > depois && r.EncerradoEm == null && r.AtualizadoEm >= regra.AtivadaEm && r.AtualizadoEm <= limite)
            .OrderBy(r => r.Id).Select(r => r.Id).Take(100).ToArrayAsync(ct);
    }

    /// <summary>
    /// O RASCUNHO VENCIDO (out/2026, "como no Excel"): o médico escreveu e não salvou. Passado o
    /// prazo da última alteração, o sistema GRAVA a evolução em nome de quem escreveu (o mesmo
    /// <c>ProntuarioService.SalvarAsync</c> do Salvar, que versiona) e, havendo horário em
    /// aberto sem impedimento, ENCERRA a sessão e gera as guias — na mesma transação, com a
    /// auditoria dizendo que foi o sistema. Rascunho sem conteúdo encerra sem gravar nada.
    /// </summary>
    public Task<bool> SalvarRascunhoVencidoAsync(int rascunhoId, DateTime agora, CancellationToken ct = default)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(c => SalvarRascunhoNaTransacaoAsync(rascunhoId, agora, c), ct);

    private async Task<bool> SalvarRascunhoNaTransacaoAsync(int rascunhoId, DateTime agora, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(20261022, {rascunhoId})", ct);
        db.ChangeTracker.Clear();
        var regra = await politica.ObterAsync(ct);
        if (!regra.Automatica) return false;
        var r = await db.RascunhosEvolucao.SingleOrDefaultAsync(x => x.Id == rascunhoId, ct);
        if (r is null || r.EncerradoEm is not null || r.AtualizadoEm < regra.AtivadaEm || r.AtualizadoEm.AddHours(regra.Horas) > agora)
            return false;

        var horario = r.AgendamentoId is { } idHorario ? await db.Agendamentos.SingleOrDefaultAsync(a => a.Id == idHorario, ct) : null;
        var conteudo = ConteudoDoRascunho.Ler(r.Conteudo);
        var definicoes = await new CampoPersonalizadoService(repo).DaSessaoAsync(horario?.ModalidadeCodigo, ct);
        var dados = conteudo?.ParaEvolucao(r.EvolucaoId ?? 0, r.PacienteId, r.ProfissionalId, r.AgendamentoId, definicoes);
        r.EncerradoEm = agora;
        // Salvo DEPOIS da última alteração (outra estação, o tablet, um Salvar cujo encerramento
        // do rascunho falhou): o prontuário já tem o mais novo, e regravar o velho o desdiria.
        var vigentes = db.Evolucoes.AsNoTracking().Where(e => e.CanceladaEm == null);
        vigentes = r.EvolucaoId is { } continua ? vigentes.Where(e => e.Id == continua)
            : r.AgendamentoId is { } doHorario ? vigentes.Where(e => e.AgendamentoId == doHorario)
            : vigentes.Where(e => false);
        var alteradoEm = r.AtualizadoEm;
        var gravadaDepois = await vigentes.AnyAsync(e => (e.AtualizadoEm ?? e.CriadoEm) >= alteradoEm, ct);
        if (gravadaDepois || dados is null || !ProntuarioService.TemRegistro(dados))
        {
            r.Desfecho = gravadaDepois ? RascunhoEvolucao.Superado : RascunhoEvolucao.SemConteudo;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return false;
        }

        r.Desfecho = RascunhoEvolucao.SalvoPorPrazo;
        var salva = await new ProntuarioService(repo).SalvarAsync(dados, operador: r.AtualizadoPor ?? Operador, ct: ct);
        r.EvolucaoId = salva.Id;
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria { Operador = Operador, Acao = "RascunhoSalvoPorPrazo",
            PacienteId = r.PacienteId,
            Detalhe = $"Evolução {salva.Id} gravada do rascunho {r.Id}, escrito por {r.AtualizadoPor ?? "?"} e não salvo; "
                      + $"última alteração em {r.AtualizadoEm:dd/MM/yyyy HH:mm}, prazo de {regra.Horas} horas. "
                      + "Ato do sistema; não representa nova avaliação ou assinatura médica." }, ct);
        await db.SaveChangesAsync(ct);

        // Com horário em aberto e sem impedimento (médico, enfermagem do BSV), encerra junto.
        if (horario is { } h && h.DataHora <= agora && await Abertos().AnyAsync(a => a.Id == h.Id, ct)
            && await ImpedimentoAsync(h, regra, agora, ct, gravadaAgoraPorPrazo: true) is null)
        {
            await repo.RegistrarAuditoriaAsync(new EventoAuditoria { Operador = Operador,
                Acao = "ConclusaoAutomaticaPorPrazo", PacienteId = h.PacienteId,
                Detalhe = $"Sessão {h.Id}; prazo de {regra.Horas} horas após a última alteração do texto não salvo. "
                          + "Encerramento administrativo automático; não representa nova avaliação ou assinatura médica." }, ct);
            await agenda.ConcluirAtendimentoClinicoAsync(h.Id, Operador, ct);
        }
        await tx.CommitAsync(ct);
        return true;
    }

    private IQueryable<Agendamento> Pendentes(PoliticaConclusao regra, DateTime agora, int? id)
    {
        var limite = agora.AddHours(-regra.Horas);
        return Abertos().AsNoTracking()
            .Where(a => (id == null || a.Id == id) && a.DataHora <= agora
                && db.Evolucoes.Any(e => e.AgendamentoId == a.Id && e.CanceladaEm == null
                    && (e.AtualizadoEm ?? e.CriadoEm) >= regra.AtivadaEm && (e.AtualizadoEm ?? e.CriadoEm) <= limite)
                && !db.RascunhosEvolucao.Any(r => r.AgendamentoId == a.Id && r.EncerradoEm == null))
            .OrderBy(a => a.DataHora).Take(100);
    }

    /// <summary>
    /// Quantas pendências <see cref="PendenciasAsync"/> listaria — a mesma consulta, contada no banco.
    /// O aviso de fundo (a cada minuto, em todo posto) usa só o número, e montar a lista custava
    /// uma a três consultas POR LINHA para escrever um motivo que ninguém lia ali (out/2026).
    /// </summary>
    public async Task<int> ContarPendenciasAsync(DateTime agora, CancellationToken ct = default)
    {
        var regra = await politica.ObterAsync(ct);
        if (!regra.Automatica) return 0;
        return await Pendentes(regra, agora, null).CountAsync(ct);
    }

    public async Task<IReadOnlyList<PendenciaConclusao>> PendenciasAsync(DateTime agora, int? id = null, CancellationToken ct = default)
    {
        var regra = await politica.ObterAsync(ct);
        if (!regra.Automatica) return [];
        var lista = await Pendentes(regra, agora, id).Include(a => a.Paciente).Include(a => a.Profissional).ToListAsync(ct);
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
                // 1) O texto escrito e não salvo, vencido: grava e encerra ("como no Excel").
                // Em try PRÓPRIO: falhar aqui (o papel restrito do portal sem o grant da tabela
                // nova, uma base ainda sem a migration) nunca pode impedir a etapa 2 — foi
                // exatamente assim, calada, que a conclusão ficou parada de set a out/2026.
                try
                {
                    int rascunhoDepois = 0;
                    while (!ct.IsCancellationRequested)
                    {
                        int[] rascunhos;
                        using (var scope = scopes.CreateScope())
                            rascunhos = await scope.ServiceProvider.GetRequiredService<ConclusaoAutomaticaService>().RascunhosVencidosAsync(Agora, rascunhoDepois, ct);
                        if (rascunhos.Length == 0) break;
                        foreach (var id in rascunhos)
                        {
                            try
                            {
                                using var scope = scopes.CreateScope();
                                await scope.ServiceProvider.GetRequiredService<ConclusaoAutomaticaService>().SalvarRascunhoVencidoAsync(id, Agora, ct);
                            }
                            catch (Exception ex) when (!ct.IsCancellationRequested)
                            { Clinica.Application.Diagnostico.Registrar($"Rascunho {id} vencido não pôde ser salvo; será reavaliado", ex); }
                        }
                        rascunhoDepois = rascunhos[^1];
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                { Clinica.Application.Diagnostico.Registrar("Rascunhos vencidos indisponíveis; a conclusão das sessões segue", ex); }

                // 2) As sessões com evolução salva e prazo vencido.
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
                // 3) A regra do PROFISSIONAL (nov/2026): horário com chegada, N horas depois.
                // Em try próprio, pela mesma razão da etapa 1: falhar aqui não para as outras.
                try
                {
                    int profDepois = 0;
                    while (!ct.IsCancellationRequested)
                    {
                        int[] ids;
                        using (var scope = scopes.CreateScope())
                            ids = await scope.ServiceProvider.GetRequiredService<ConclusaoAutomaticaService>().CandidatosPorProfissionalAsync(Agora, profDepois, ct);
                        if (ids.Length == 0) break;
                        foreach (var id in ids)
                        {
                            try
                            {
                                using var scope = scopes.CreateScope();
                                await scope.ServiceProvider.GetRequiredService<ConclusaoAutomaticaService>().ConcluirPorProfissionalAsync(id, Agora, ct);
                            }
                            catch (Exception ex) when (!ct.IsCancellationRequested)
                            { Clinica.Application.Diagnostico.Registrar($"Conclusão automática do profissional pendente no horário {id}; será reavaliada", ex); }
                        }
                        profDepois = ids[^1];
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                { Clinica.Application.Diagnostico.Registrar("Conclusão automática por profissional indisponível; as outras etapas seguem", ex); }

                if (avisar is not null)
                {
                    using var scope = scopes.CreateScope();
                    var quantidade = await scope.ServiceProvider.GetRequiredService<ConclusaoAutomaticaService>().ContarPendenciasAsync(Agora, ct);
                    if (quantidade > 0 && (quantidade != quantidadeAnterior || Agora - ultimoAviso >= TimeSpan.FromHours(1)))
                    { avisar(quantidade); ultimoAviso = Agora; }
                    quantidadeAnterior = quantidade;
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
