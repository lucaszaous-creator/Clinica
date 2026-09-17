using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public partial class SegundaAssinaturaExecucaoTests
{
    [Fact] public async Task Infusao_externa_assina_enfermagem_primeiro_e_medico_valida_mesmo_pdf()
    {
        var c=await CenarioAsync();
        var medico=new UsuarioSistema {Nome="Médica",Login="medica",Perfil=PerfilAcesso.Profissional,ProfissionalId=c.ProfissionalMedicaId};
        _db.Usuarios.Add(medico);await _db.SaveChangesAsync();
        var p=await _prescricoes.RegistrarExecucaoExternaAsync(new(c.PacienteId,c.ProfissionalMedicaId,null,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),new(9,30),"Infusão de teste","Orientação externa descrita pela executante"),c.UsuarioEnfermeiraId);
        p.AguardaValidacaoMedica.Should().BeTrue();
        (await _db.Atendimentos.CountAsync()).Should().Be(0);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>_orquestra.AssinarPrescricaoAsync(p.Id,ECpfDeTeste("Médica",CpfMedica),usuarioId:medico.Id));
        await _orquestra.AssinarExecucaoAsync(p.Id,ECpfDeTeste("Enfermagem",CpfEnfermeira),c.UsuarioEnfermeiraId);
        var parcial=await _orquestra.FolhaAsync(p.Id,FolhaPrescricao.RegistroExecucao);
        _assinador.ConferirTodas(parcial.Pdf).Should().HaveCount(1).And.OnlyContain(a=>a.Conferida);
        p.AguardaValidacaoMedica.Should().BeTrue();
        await _orquestra.AssinarPrescricaoAsync(p.Id,ECpfDeTeste("Médica",CpfMedica),usuarioId:medico.Id);
        var final=await _orquestra.FolhaAsync(p.Id,FolhaPrescricao.Prescricao);
        _assinador.ConferirTodas(final.Pdf).Should().HaveCount(2).And.OnlyContain(a=>a.Conferida);
        final.Pdf.Take(parcial.Pdf.Length).Should().Equal(parcial.Pdf);
        (await _orquestra.FolhaAsync(p.Id,FolhaPrescricao.RegistroExecucao)).Pdf.Should().Equal(final.Pdf);
        p.Situacao.Should().Be(SituacaoPrescricao.Encerrada);
        (await _db.Atendimentos.CountAsync()).Should().Be(0);
        Despejar("infusao-externa-duas-assinaturas.pdf",final.Pdf);
    }
}
