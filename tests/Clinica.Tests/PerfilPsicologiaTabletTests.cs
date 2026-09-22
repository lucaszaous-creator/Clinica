using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    [Fact]
    public async Task Psicologia_tem_agenda_clinica_sem_prescricao_e_sem_BSV()
    {
        await Preparar();
        Assert.Equal(5, (int)PerfilAcesso.Gerente);
        Assert.Equal(6, (int)PerfilAcesso.Psicologia);
        var profissional = await repo.ObterProfissionalAsync(outro.ProfissionalId!.Value);
        profissional!.EspecialidadeCodigo = "Psicologia";
        await repo.SalvarAsync();
        var psicologa = await new AcessoService(repo).CriarAsync("Psicóloga", "psicologa.teste",
            "TabletTeste#2026", PerfilAcesso.Psicologia, profissional.Id);
        Assert.True(psicologa.Pode(Permissao.EditarProntuario | Permissao.LancarAtendimento));
        Assert.False(psicologa.Pode(Permissao.Prescrever));
        psicologa.PermissoesExtras = Permissao.Prescrever | Permissao.ChecarPrescricao;
        Assert.False(psicologa.Pode(Permissao.Prescrever | Permissao.ChecarPrescricao));
        psicologa.DeveTrocarSenha = false;
        await repo.SalvarAsync();
        Assert.True(PoliticaAtendimentoTablet.PodeAtender(psicologa));

        var agenda = new AgendaService(repo, new(repo));
        var quando = svc.Hoje.AddDays(3).ToDateTime(new(10, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => agenda.AgendarAsync(
            horario.PacienteId, quando, ModalidadeAtendimento.BsvApenas, null,
            profissionalId: profissional.Id));
        var consulta = await agenda.AgendarAsync(horario.PacienteId, quando,
            ModalidadeAtendimento.Consulta, null, profissionalId: profissional.Id);
        Assert.Equal("Psicologia", consulta.EspecialidadeConsultaCodigo);
        var montado = await new AtendimentoService(repo).MontarAsync(horario.PacienteId,
            DateOnly.FromDateTime(quando), ModalidadeAtendimento.Consulta, null,
            especialidadeConsultaCodigo: consulta.EspecialidadeConsultaCodigo);
        var guia = Assert.Single(montado.Atendimento.Codigos);
        Assert.Equal(TipoCodigo.Consulta, guia.Tipo);
        Assert.Equal("Psicologia", guia.EspecialidadeCodigo);
        profissional.HabilitacoesAtendimentoJson = JsonSerializer.Serialize(new[]
        {
            new AtendimentoHabilitado(nameof(ModalidadeAtendimento.BsvApenas), null),
            new AtendimentoHabilitado(nameof(ModalidadeAtendimento.Consulta), "Psicologia")
        });
        await repo.SalvarAsync();
        Assert.False(profissional.Atende(nameof(ModalidadeAtendimento.BsvApenas)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => agenda.AgendarAsync(
            horario.PacienteId, quando.AddDays(1), ModalidadeAtendimento.BsvApenas, null,
            profissionalId: profissional.Id));
        var novaConsulta = await agenda.AgendarAsync(horario.PacienteId, quando.AddDays(1),
            ModalidadeAtendimento.Consulta, null, profissionalId: profissional.Id);
        Assert.Equal("Psicologia", novaConsulta.EspecialidadeConsultaCodigo);
    }
}
