using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    [Fact]
    public async Task Gerente_sem_profissional_conclui_sessao_escrita_preservando_medico_e_guias()
    {
        await Preparar();
        await svc.SalvarAsync(sessao, horario.Id, await Pedido(), default);
        var evolucao = await db.Evolucoes.SingleAsync();
        var medico = evolucao.ProfissionalId;
        var autor = evolucao.CriadoPor;
        usuario.Perfil = PerfilAcesso.Gerente;
        usuario.ProfissionalId = null;
        usuario.Profissional = null;
        await db.SaveChangesAsync();
        Assert.True(ConclusaoClinica.Permitida(usuario.Perfil, usuario.Efetivas, null));
        var agenda = new AgendaService(repo, new(repo));
        await agenda.ConcluirAtendimentoClinicoAsync(horario.Id, usuario.Login, usuarioId: usuario.Id, houveEnfermagem: false);
        var guias = await db.Codigos.Select(g => g.Id).ToArrayAsync();
        await agenda.ConcluirAtendimentoClinicoAsync(horario.Id, usuario.Login, usuarioId: usuario.Id, houveEnfermagem: false);
        Assert.NotNull(horario.FimAtendimentoEm);
        Assert.Equal(StatusAgendamento.Realizado, horario.Status);
        Assert.Equal(medico, horario.ProfissionalId);
        Assert.Equal(medico, evolucao.ProfissionalId);
        Assert.Equal(autor, evolucao.CriadoPor);
        Assert.Equal(usuario.Id, horario.EnfermagemConferidaPorUsuarioId);
        Assert.NotEmpty(guias);
        Assert.Equal(guias, await db.Codigos.Select(g => g.Id).ToArrayAsync());
        Assert.Single(await db.Atendimentos.ToListAsync());
    }

    [Fact]
    public async Task Salvar_como_gerente_sem_vinculo_nao_apaga_profissional_da_evolucao()
    {
        await Preparar();
        await svc.SalvarAsync(sessao, horario.Id, await Pedido(), default);
        var original = await db.Evolucoes.SingleAsync();
        var medico = original.ProfissionalId;
        var autor = original.CriadoPor;
        await new ProntuarioService(repo).SalvarAsync(new Evolucao {
            Id=original.Id, PacienteId=original.PacienteId, Data=original.Data,
            TextoEvolucao=original.TextoEvolucao, ProfissionalId=null
        }, "gerente");
        Assert.Equal(medico, original.ProfissionalId);
        Assert.Equal(autor, original.CriadoPor);
    }

    [Fact]
    public async Task Gerente_desativado_nao_conclui_e_medico_nao_conclui_atendimento_de_outro()
    {
        await Preparar();
        var agenda = new AgendaService(repo, new(repo));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => agenda.ExigirConclusaoClinicaAsync(outro.Id, usuario.Id));
        usuario.Perfil=PerfilAcesso.Gerente; usuario.Ativo=false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => agenda.ExigirConclusaoClinicaAsync(horario.Id, usuario.Id));
    }
}
