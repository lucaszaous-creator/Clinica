using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Clinica.Tests;
public sealed partial class AtendimentoTabletTests
{
    [Theory]
    [InlineData(ModalidadeAtendimento.BsvApenas)]
    [InlineData(ModalidadeAtendimento.BsvComAcupuntura)]
    public async Task Salvar_medico_gera_guia_agora_e_enfermagem_registra_depois_sem_duplicar(ModalidadeAtendimento modalidade)
    {
        await PrepararBSV();
        horario.ModalidadePrevista = modalidade;
        await db.SaveChangesAsync();
        var pedido = (await Pedido()) with { ConcluirAoSalvar = true };
        var salvo = await svc.SalvarAsync(sessao, horario.Id, pedido, default);
        Assert.True(salvo.Finalizado);
        Assert.True(salvo.Guias > 0);
        Assert.NotNull(horario.FimAtendimentoEm);
        Assert.Null(horario.HouveAtendimentoEnfermagem);
        var repetido = await svc.SalvarAsync(sessao, horario.Id, pedido, default);
        Assert.Equal(salvo.AtendimentoId, repetido.AtendimentoId);
        Assert.Equal(salvo.EvolucaoId, repetido.EvolucaoId);
        Assert.Equal(salvo.Guias, repetido.Guias);
        Assert.True(repetido.Finalizado);
        var guias = await db.Codigos.Select(c => c.Id).ToArrayAsync();
        var agenda = new AgendaService(repo, new(repo));
        Assert.NotNull(await agenda.PendenciaEnfermagemAsync(horario.Id));
        await Enfermeira();
        await Posto.RegistrarEnfermagemAsync(sessao, horario.PacienteId,
            new RegistroEnfermagemTablet(Guid.NewGuid(), svc.Hoje, new(8,0), "Registro tardio fictício", false, null, null, AgendamentoId: horario.Id), default);
        Assert.Null(await agenda.PendenciaEnfermagemAsync(horario.Id));
        Assert.Equal(guias, await db.Codigos.Select(c => c.Id).ToArrayAsync());
        Assert.Single(await db.Atendimentos.ToListAsync());
    }
    [Fact]
    public async Task Conclusao_com_registro_posterior_nao_libera_enfermagem_nem_outro_medico()
    {
        await PrepararBSV();
        var agenda = new AgendaService(repo, new(repo));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => agenda.ConcluirAtendimentoClinicoAsync(outro.Id, "teste", usuarioId: usuario.Id, permitirEnfermagemPosterior: true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => agenda.ConcluirAtendimentoClinicoAsync(horario.Id, "teste", permitirEnfermagemPosterior: true));
        await Enfermeira();
        usuario.PermissoesExtras |= Permissao.EditarProntuario | Permissao.LancarAtendimento;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => agenda.ConcluirAtendimentoClinicoAsync(horario.Id, "teste", usuarioId: usuario.Id, permitirEnfermagemPosterior: true));
        Assert.Empty(await db.Codigos.ToListAsync());
    }
    [Fact]
    public async Task Gerente_conclui_com_enfermagem_posterior_preservando_autoria_medica()
    {
        await PrepararBSV();
        await svc.SalvarAsync(sessao, horario.Id, await Pedido(), default);
        var evolucao = await db.Evolucoes.SingleAsync();
        var autor = evolucao.CriadoPor;
        var medico = evolucao.ProfissionalId;
        usuario.Perfil = PerfilAcesso.Gerente;
        usuario.ProfissionalId = null; usuario.Profissional = null;
        await db.SaveChangesAsync();
        var agenda = new AgendaService(repo, new(repo));
        await agenda.ConcluirAtendimentoClinicoAsync(horario.Id, usuario.Login,
            usuarioId: usuario.Id, permitirEnfermagemPosterior: true);
        Assert.NotNull(horario.FimAtendimentoEm);
        Assert.NotEmpty(await db.Codigos.ToListAsync());
        Assert.Equal(autor, evolucao.CriadoPor);
        Assert.Equal(medico, evolucao.ProfissionalId);
        Assert.Null(horario.HouveAtendimentoEnfermagem);
        Assert.NotNull(await agenda.PendenciaEnfermagemAsync(horario.Id));
    }
    [Fact]
    public void Cadastro_distingue_medico_de_enfermagem_sem_alterar_identidade_dos_perfis()
    {
        Assert.Equal("Médico", PerfisAcesso.Rotular(PerfilAcesso.Profissional));
        Assert.Equal("Enfermagem", PerfisAcesso.Rotular(PerfilAcesso.Enfermagem));
        Assert.False((PerfisAcesso.Padrao(PerfilAcesso.Enfermagem) & Permissao.Prescrever) != 0);
        Assert.True((PerfisAcesso.Padrao(PerfilAcesso.Enfermagem) & Permissao.RegistrarEvolucaoEnfermagem) != 0);
    }
}
