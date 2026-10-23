using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    private ConclusaoAutomaticaService Automatico => new(db, repo, new(repo, new(repo)), new(repo));

    private async Task<DateTime> PrepararPrazo(bool bsv = false)
    {
        if (bsv) await PrepararBSV(); else await Preparar();
        await svc.SalvarAsync(sessao, horario.Id, await Pedido(), default);
        var agora = ConclusaoAutomaticaService.Agora;
        horario.DataHora = agora.AddDays(-2);
        var e = await db.Evolucoes.SingleAsync();
        e.CriadoEm = agora.AddHours(-24); e.AtualizadoEm = null;
        await repo.SalvarConfiguracaoAsync(PoliticaConclusaoService.Chave,
            JsonSerializer.Serialize(PoliticaConclusao.Padrao with { Automatica = true, AtivadaEm = agora.AddDays(-3) }));
        await db.SaveChangesAsync();
        return agora;
    }

    [Fact] public async Task Politica_padrao_pergunta_somente_BSV_e_nao_ativa_automatico()
    {
        var p = await new PoliticaConclusaoService(repo).ObterAsync();
        Assert.False(p.Automatica); Assert.Equal(24, p.Horas);
        foreach (var modalidade in Enum.GetValues<ModalidadeAtendimento>())
            Assert.Equal(modalidade is ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura,
                p.ExigeEnfermagem(new() { ModalidadePrevista = modalidade }));
    }

    [Fact] public async Task Configuracao_exige_gerente_real_e_valida_prazo_e_modalidades()
    {
        await Preparar(); var p = new PoliticaConclusaoService(repo);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => p.SalvarAsync([], true, 24, usuario.Id));
        usuario.Perfil = PerfilAcesso.Gerente; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.SalvarAsync([], true, 0, usuario.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.SalvarAsync(["inexistente"], true, 24, usuario.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.SalvarAsync([nameof(ModalidadeAtendimento.Consulta)], true, 24, usuario.Id));
        await p.SalvarAsync([nameof(ModalidadeAtendimento.BsvComAcupuntura)], true, 24, usuario.Id);
        var salvo = await p.ObterAsync(); Assert.True(salvo.Automatica); Assert.NotNull(salvo.AtivadaEm);
        Assert.True(salvo.ExigeEnfermagem(new() { ModalidadePrevista = ModalidadeAtendimento.BsvComAcupuntura }));
        Assert.False(salvo.ExigeEnfermagem(new() { ModalidadePrevista = ModalidadeAtendimento.BsvApenas }));
        await p.SalvarAsync([], true, 48, usuario.Id); Assert.Equal(salvo.AtivadaEm, (await p.ObterAsync()).AtivadaEm);
        await p.SalvarAsync([], false, 48, usuario.Id); Assert.Null((await p.ObterAsync()).AtivadaEm);
    }

    [Fact] public async Task Modalidade_nao_configurada_finaliza_sem_pergunta_de_enfermagem()
    {
        await Preparar(); var p = (await Pedido()) with { Finalizar = true, HouveEnfermagem = null, Consumo = new([], true) };
        var resultado = await svc.SalvarAsync(sessao, horario.Id, p, default);
        Assert.True(resultado.Finalizado); Assert.True(resultado.Guias > 0);
        Assert.Null(horario.EnfermagemConferidaPorUsuarioId);
    }

    [Fact] public async Task Automatico_respeita_limite_e_repeticao_nao_duplica_guias_ou_autoria()
    {
        var agora = await PrepararPrazo();
        var autor = (await db.Evolucoes.SingleAsync()).CriadoPor;
        Assert.False(await Automatico.ConcluirSeVencidoAsync(horario.Id, agora.AddSeconds(-1)));
        Assert.True(await Automatico.ConcluirSeVencidoAsync(horario.Id, agora));
        var h = await db.Agendamentos.SingleAsync(a => a.Id == horario.Id);
        Assert.NotNull(h.FimAtendimentoEm); Assert.Equal(StatusAgendamento.Realizado, h.Status);
        var ids = await db.Codigos.OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync(); Assert.NotEmpty(ids);
        Assert.False(await Automatico.ConcluirSeVencidoAsync(horario.Id, agora.AddDays(1)));
        Assert.Equal(ids, await db.Codigos.OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync());
        var e = await db.Evolucoes.SingleAsync(); Assert.Equal(autor, e.CriadoPor);
        Assert.Equal(h.AtendimentoId, e.AtendimentoId); Assert.Single(await db.Atendimentos.ToListAsync());
    }

    // Em produção o AddClinica liga o EnableRetryOnFailure do Npgsql, e o EF recusa transação
    // aberta à mão sob estratégia que repete. O SQLite dos outros testes não tem estratégia e
    // passava com o defeito: em produção, toda conclusão automática estourava e as sessões
    // vencidas nunca fechavam (22/10/2026). Aqui o SQLite ganha uma estratégia que repete.
    sealed class RepeteComoONpgsql(Microsoft.EntityFrameworkCore.Storage.ExecutionStrategyDependencies d)
        : Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy(d, 3, TimeSpan.FromMilliseconds(10))
    {
        protected override bool ShouldRetryOn(Exception exception) => false;
    }

    [Fact] public async Task Automatico_conclui_sob_a_estrategia_que_repete_como_no_Postgres()
    {
        var agora = await PrepararPrazo();
        using var comRepeticao = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(connection, o => o.ExecutionStrategy(d => new RepeteComoONpgsql(d))).Options);
        var r = new ClinicaRepositorio(comRepeticao);
        var automatico = new ConclusaoAutomaticaService(comRepeticao, r, new(r, new(r)), new(r));
        Assert.True(await automatico.ConcluirSeVencidoAsync(horario.Id, agora));
        var h = await comRepeticao.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == horario.Id);
        Assert.NotNull(h.FimAtendimentoEm);
        Assert.NotEmpty(await comRepeticao.Codigos.ToListAsync());
    }

    // ===== O rascunho automático (out/2026, "como no Excel") =====

    /// <summary>Um horário de ontem, com a regra ligada há três dias e um rascunho NÃO SALVO alterado há <paramref name="horasAtras"/> h.</summary>
    private async Task<(DateTime Agora, RascunhoEvolucao Rascunho)> PrepararRascunho(double horasAtras, string texto = "Escrito e não salvo.")
    {
        await Preparar();
        var agora = ConclusaoAutomaticaService.Agora;
        horario.DataHora = agora.AddDays(-1);
        await repo.SalvarConfiguracaoAsync(PoliticaConclusaoService.Chave,
            JsonSerializer.Serialize(PoliticaConclusao.Padrao with { Automatica = true, AtivadaEm = agora.AddDays(-3) }));
        var folha = new Clinica.Application.Modelos.RascunhoDaSessao { Data = DateOnly.FromDateTime(horario.DataHora), TextoEvolucao = texto, EvaAntes = 7, EvaDepois = 2 };
        var r = new RascunhoEvolucao
        {
            PacienteId = horario.PacienteId, ProfissionalId = horario.ProfissionalId, AgendamentoId = horario.Id,
            Dia = DateOnly.FromDateTime(horario.DataHora),
            Conteudo = Clinica.Application.Modelos.ConteudoDoRascunho.De(folha).Serializar(),
            AtualizadoEm = agora.AddHours(-horasAtras), AtualizadoPor = usuario.Login
        };
        db.RascunhosEvolucao.Add(r);
        await db.SaveChangesAsync();
        return (agora, r);
    }

    [Fact] public async Task Texto_nao_salvo_e_gravado_e_a_sessao_encerrada_no_prazo_da_ultima_alteracao()
    {
        var (agora, r) = await PrepararRascunho(horasAtras: 25);
        Assert.Contains(r.Id, await Automatico.RascunhosVencidosAsync(agora));
        Assert.True(await Automatico.SalvarRascunhoVencidoAsync(r.Id, agora));

        var e = await db.Evolucoes.AsNoTracking().SingleAsync();
        Assert.Equal("Escrito e não salvo.", e.TextoEvolucao);
        Assert.Equal((horario.Id, usuario.Login), (e.AgendamentoId!.Value, e.CriadoPor));
        var h = await db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == horario.Id);
        Assert.NotNull(h.FimAtendimentoEm);
        Assert.NotEmpty(await db.Codigos.ToListAsync());
        var depois = await db.RascunhosEvolucao.AsNoTracking().SingleAsync();
        Assert.Equal((RascunhoEvolucao.SalvoPorPrazo, e.Id), (depois.Desfecho, depois.EvolucaoId!.Value));
        Assert.True(await db.Auditoria.AnyAsync(x => x.Acao == "RascunhoSalvoPorPrazo" && x.Operador == ConclusaoAutomaticaService.Operador));

        // Repetir não grava outra evolução nem outro atendimento.
        Assert.False(await Automatico.SalvarRascunhoVencidoAsync(r.Id, agora.AddHours(1)));
        Assert.Single(await db.Evolucoes.ToListAsync());
        Assert.Single(await db.Atendimentos.ToListAsync());
    }

    [Fact] public async Task Antes_do_prazo_o_rascunho_espera_e_a_sessao_nao_fecha_por_cima()
    {
        var (agora, r) = await PrepararRascunho(horasAtras: 2);
        Assert.Empty(await Automatico.RascunhosVencidosAsync(agora));
        Assert.False(await Automatico.SalvarRascunhoVencidoAsync(r.Id, agora));
        Assert.Empty(await db.Evolucoes.ToListAsync());
        Assert.Null((await db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == horario.Id)).FimAtendimentoEm);
    }

    [Fact] public async Task Rascunho_mais_velho_que_a_gravacao_encerra_sem_regravar()
    {
        var (agora, r) = await PrepararRascunho(horasAtras: 30);
        // Outra estação salvou DEPOIS da última alteração do rascunho.
        db.Evolucoes.Add(new Evolucao { PacienteId = horario.PacienteId, ProfissionalId = horario.ProfissionalId, AgendamentoId = horario.Id,
            Data = DateOnly.FromDateTime(horario.DataHora), TextoEvolucao = "O salvo, mais novo.", CriadoEm = agora.AddHours(-26) });
        await db.SaveChangesAsync();
        Assert.False(await Automatico.SalvarRascunhoVencidoAsync(r.Id, agora));
        Assert.Equal("O salvo, mais novo.", (await db.Evolucoes.AsNoTracking().SingleAsync()).TextoEvolucao);
        Assert.Equal(RascunhoEvolucao.Superado, (await db.RascunhosEvolucao.AsNoTracking().SingleAsync()).Desfecho);
    }

    [Fact] public async Task Rascunho_vencido_sob_a_estrategia_que_repete_como_no_Postgres()
    {
        var (agora, r) = await PrepararRascunho(horasAtras: 25);
        using var comRepeticao = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(connection, o => o.ExecutionStrategy(d => new RepeteComoONpgsql(d))).Options);
        var rr = new ClinicaRepositorio(comRepeticao);
        Assert.True(await new ConclusaoAutomaticaService(comRepeticao, rr, new(rr, new(rr)), new(rr)).SalvarRascunhoVencidoAsync(r.Id, agora));
        Assert.Single(await comRepeticao.Evolucoes.ToListAsync());
    }

    [Fact] public async Task BSV_vencido_sem_enfermagem_permanece_aberto_com_aviso_ate_vincular()
    {
        var agora = await PrepararPrazo(true);
        await Enfermagem(null); await Enfermagem(outro.Id);
        Assert.False(await Automatico.ConcluirSeVencidoAsync(horario.Id, agora));
        var pendencia = Assert.Single(await Automatico.PendenciasAsync(agora));
        Assert.Contains("falta evolução de enfermagem", pendencia.Motivo);
        Assert.Empty(await db.Codigos.ToListAsync()); Assert.Empty(await db.Atendimentos.ToListAsync());
        Assert.Null((await db.Agendamentos.SingleAsync(a => a.Id == horario.Id)).FimAtendimentoEm);
        await Enfermagem(horario.Id);
        Assert.True(await Automatico.ConcluirSeVencidoAsync(horario.Id, agora));
        Assert.Empty(await Automatico.PendenciasAsync(agora));
    }

    [Theory]
    [InlineData("desativada")][InlineData("historica")][InlineData("editada")]
    [InlineData("sem-evolucao")][InlineData("cancelada")][InlineData("ambiguo")][InlineData("outro-medico")]
    public async Task Automatico_nao_conclui_sem_condicoes(string caso)
    {
        var agora = await PrepararPrazo(); var e = await db.Evolucoes.SingleAsync();
        if (caso == "desativada") await repo.SalvarConfiguracaoAsync(PoliticaConclusaoService.Chave, JsonSerializer.Serialize(PoliticaConclusao.Padrao));
        if (caso == "historica") e.CriadoEm = agora.AddDays(-4);
        if (caso == "editada") e.AtualizadoEm = agora.AddHours(-1);
        if (caso == "sem-evolucao") db.Evolucoes.Remove(e);
        if (caso == "cancelada") horario.Status = StatusAgendamento.Cancelado;
        if (caso == "outro-medico") e.ProfissionalId = outro.ProfissionalId;
        if (caso == "ambiguo") db.Evolucoes.Add(new() { PacienteId = horario.PacienteId, AgendamentoId = horario.Id,
            ProfissionalId = horario.ProfissionalId, Data = e.Data, TextoEvolucao = "Outro registro", CriadoEm = e.CriadoEm });
        await db.SaveChangesAsync();
        Assert.False(await Automatico.ConcluirSeVencidoAsync(horario.Id, agora));
        Assert.Empty(await db.Atendimentos.ToListAsync()); Assert.Empty(await db.Codigos.ToListAsync());
    }

    [Fact] public async Task Automatico_nao_aceita_enfermagem_cancelada_nem_mapa_vazio()
    {
        var agora = await PrepararPrazo(true);
        var original = await Enfermagem(horario.Id); original.CanceladaEm = DateTime.Now; await db.SaveChangesAsync();
        Assert.False(await Automatico.ConcluirSeVencidoAsync(horario.Id, agora));
        await Enfermagem(horario.Id);
        var e = await db.Evolucoes.SingleAsync(); e.TextoEvolucao = " ";
        db.MapasCorporais.Add(new() { EvolucaoId = e.Id }); await db.SaveChangesAsync();
        Assert.False(await Automatico.ConcluirSeVencidoAsync(horario.Id, agora));
    }

    [Fact] public async Task Duas_estacoes_no_Postgres_nao_duplicam_a_conclusao_automatica()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var agora = await PrepararPrazo();
        async Task Tentar()
        {
            await using var contexto = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options);
            var repositorio = new ClinicaRepositorio(contexto);
            try { await new ConclusaoAutomaticaService(contexto, repositorio, new(repositorio, new(repositorio)), new(repositorio))
                    .ConcluirSeVencidoAsync(horario.Id, agora); }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "40001") { /* Reavaliada no próximo ciclo. */ }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "40001" }) { }
            catch (InvalidOperationException ex) when (ex.GetBaseException() is Npgsql.PostgresException { SqlState: "40001" }) { }
        }
        await Task.WhenAll(Tentar(), Tentar());
        db.ChangeTracker.Clear();
        Assert.Single(await db.Atendimentos.ToListAsync());
        var guias = await db.Codigos.CountAsync(); Assert.True(guias > 0);
        Assert.False(await Automatico.ConcluirSeVencidoAsync(horario.Id, agora));
        Assert.Equal(guias, await db.Codigos.CountAsync());
    }
}

