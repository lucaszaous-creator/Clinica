using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    private ObservacoesEnfermagemTablet Observacoes() => new(Guid.NewGuid(), svc.Hoje, horario.Id,
        [new(new(8, 0), "Primeira observação fictícia", false),
         new(new(8, 30), "Segunda observação fictícia", false),
         new(new(9, 0), "Intercorrência fictícia", true)]);

    [Theory]
    [InlineData(ModalidadeAtendimento.BsvApenas)]
    [InlineData(ModalidadeAtendimento.BsvComAcupuntura)]
    public async Task Observacoes_preservam_horarios_autoria_vinculo_e_guias_sem_duplicacao(ModalidadeAtendimento modalidade)
    {
        await PrepararBSV(); horario.ModalidadePrevista = modalidade; await db.SaveChangesAsync();
        await svc.SalvarAsync(sessao, horario.Id, (await Pedido()) with { ConcluirAoSalvar = true }, default);
        var fim = horario.FimAtendimentoEm;
        var guias = await db.Codigos.Select(g => g.Id).ToArrayAsync();
        await Enfermeira();
        var pedido = Observacoes();
        var salvo = await Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default);
        var repetido = await Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default);
        Assert.Equal(salvo.Ids, repetido.Ids);
        var registros = await db.EvolucoesEnfermagem.OrderBy(e => e.Hora).ToArrayAsync();
        Assert.Equal(3, registros.Length);
        Assert.Equal(pedido.Observacoes.Select(o => o.Hora), registros.Select(e => e.Hora));
        Assert.All(registros, e => { Assert.Equal(horario.Id, e.AgendamentoId); Assert.Equal(usuario.Id, e.AutorUsuarioId); Assert.Equal(pedido.Data, e.Data); });
        Assert.True(registros[2].Intercorrencia);
        Assert.Equal(fim, horario.FimAtendimentoEm);
        Assert.Equal(guias, await db.Codigos.Select(g => g.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Observacao_invalida_desfaz_todo_o_envio_inclusive_registro_anterior_valido()
    {
        await PrepararBSV(); await Enfermeira();
        var pedido = Observacoes();
        pedido.Observacoes[1] = pedido.Observacoes[1] with { Texto = "" };
        await Assert.ThrowsAnyAsync<Exception>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default));
        db.ChangeTracker.Clear();
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
        Assert.False(await db.Set<OperacaoClinicaTablet>().AnyAsync(o => o.Id == pedido.Idempotencia));
        Assert.Empty(await db.Codigos.ToListAsync());
    }

    [Fact]
    public async Task Observacoes_recusam_outro_paciente()
    {
        await PrepararBSV(); await Enfermeira();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, outro.PacienteId, Observacoes(), default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }

    [Fact]
    public async Task Observacoes_recusam_modalidade_nao_BSV()
    {
        await PrepararBSV(); await Enfermeira();
        horario.ModalidadePrevista = ModalidadeAtendimento.Consulta; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, Observacoes(), default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }

    [Fact]
    public async Task Observacoes_recusam_permissao_revogada()
    {
        await PrepararBSV(); await Enfermeira();
        usuario.PermissoesNegadas = Permissao.RegistrarEvolucaoEnfermagem; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, Observacoes(), default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task Observacoes_exigem_quantidade_limitada(int quantidade)
    {
        await PrepararBSV(); await Enfermeira();
        var pedido = Observacoes() with { Observacoes = Enumerable.Repeat(new ObservacaoEnfermagemTablet(new(8, 0), "Observação", false), quantidade).ToArray() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }

    [Fact]
    public async Task Nega_alergia_fica_na_evolucao_sem_criar_ou_apagar_alergias()
    {
        await PrepararBSV(); await Enfermeira();
        var alergia = new ProblemaPaciente { PacienteId = horario.PacienteId, Natureza = NaturezaProblema.Alergia, Descricao = "Alergia anterior fictícia" };
        db.ProblemasPaciente.Add(alergia); await db.SaveChangesAsync();
        var pedido = Observacoes() with { Observacoes = [new(new(8, 0), "Observação fictícia", false, NegaAlergia: true)] };
        await Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default);
        Assert.Equal("Observação fictícia\n\nAlergia: NEGA.", (await db.EvolucoesEnfermagem.SingleAsync()).Texto);
        db.ChangeTracker.Clear();
        var preservada = await db.ProblemasPaciente.SingleAsync();
        Assert.Equal(alergia.Id, preservada.Id); Assert.Equal(alergia.Descricao, preservada.Descricao);
        Assert.Equal(SituacaoProblema.Ativo, preservada.Situacao);
    }

    [Theory]
    [InlineData("Alergia fictícia", "Observação")]
    [InlineData(null, "")]
    public async Task Nega_nao_aceita_alergia_simultanea_nem_substitui_evolucao(string? alergia, string texto)
    {
        await PrepararBSV(); await Enfermeira();
        var pedido = Observacoes() with { Observacoes = [new(new(8, 0), texto, false, AlergiaObservada: alergia, NegaAlergia: true)] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Posto.RegistrarObservacoesEnfermagemAsync(sessao, horario.PacienteId, pedido, default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }
}
