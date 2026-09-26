using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public partial class SegundaAssinaturaExecucaoTests
{
    [Fact] public async Task Medico_devolve_enfermagem_retifica_e_assinaturas_antigas_permanecem()
    {
        var c=await CenarioAsync();
        var medico=new UsuarioSistema {Nome="Médica",Login="medica",Perfil=PerfilAcesso.Profissional,ProfissionalId=c.ProfissionalMedicaId};
        _db.Usuarios.Add(medico);await _db.SaveChangesAsync();
        var ontem=DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var dados=new RegistroInfusaoExterna(c.PacienteId,c.ProfissionalMedicaId,null,
            ontem,new(9,30),"Infusão realizada","Orientação externa",
            DataPrescricao:ontem,HoraPrescricao:new(8,15));
        var original=await _prescricoes.RegistrarExecucaoExternaAsync(dados,c.UsuarioEnfermeiraId);
        await _orquestra.AssinarExecucaoAsync(original.Id,ECpfDeTeste("Enfermagem",CpfEnfermeira),c.UsuarioEnfermeiraId);
        var pdfOriginal=(await _orquestra.FolhaAsync(original.Id,FolhaPrescricao.RegistroExecucao)).Pdf;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>_prescricoes.DevolverInfusaoExternaAsync(
            original.Id,c.UsuarioEnfermeiraId,"Rever horário"));
        await _prescricoes.DevolverInfusaoExternaAsync(original.Id,medico.Id,"Rever o horário da execução");
        original.SituacaoParaExibicao.Should().Be("Devolvida");
        original.MotivoDevolucao.Should().Be("Rever o horário da execução");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>_orquestra.AssinarPrescricaoAsync(
            original.Id,ECpfDeTeste("Médica",CpfMedica),usuarioId:medico.Id));

        var revisada=await _prescricoes.RegistrarExecucaoExternaAsync(dados with {
            Hora=new TimeOnly(9,45),RetificaPrescricaoId=original.Id
        },c.UsuarioEnfermeiraId);
        revisada.RetificaPrescricaoId.Should().Be(original.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>_prescricoes.RegistrarExecucaoExternaAsync(
            dados with {RetificaPrescricaoId=original.Id},c.UsuarioEnfermeiraId));
        (await _orquestra.FolhaAsync(original.Id,FolhaPrescricao.RegistroExecucao)).Pdf.Should().Equal(pdfOriginal);
        await _orquestra.AssinarExecucaoAsync(revisada.Id,ECpfDeTeste("Enfermagem",CpfEnfermeira),c.UsuarioEnfermeiraId);
        await _orquestra.AssinarPrescricaoAsync(revisada.Id,ECpfDeTeste("Médica",CpfMedica),usuarioId:medico.Id);
        _assinador.ConferirTodas((await _orquestra.FolhaAsync(revisada.Id,FolhaPrescricao.Prescricao)).Pdf)
            .Should().HaveCount(2).And.OnlyContain(a=>a.Conferida);
    }

    [Fact] public async Task Infusao_externa_assina_enfermagem_primeiro_e_medico_valida_mesmo_pdf()
    {
        var c=await CenarioAsync();
        var medico=new UsuarioSistema {Nome="Médica",Login="medica",Perfil=PerfilAcesso.Profissional,ProfissionalId=c.ProfissionalMedicaId};
        _db.Usuarios.Add(medico);await _db.SaveChangesAsync();
        var p=await _prescricoes.RegistrarExecucaoExternaAsync(new(c.PacienteId,c.ProfissionalMedicaId,null,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),new(9,30),"Infusão de teste","Orientação externa descrita pela executante",
            DataPrescricao:DateOnly.FromDateTime(DateTime.Today.AddDays(-2)),HoraPrescricao:new(8,15)),c.UsuarioEnfermeiraId);
        p.Data.Should().Be(DateOnly.FromDateTime(DateTime.Today.AddDays(-2)));
        p.Hora.Should().Be(new TimeOnly(8,15));
        p.Itens.Single().ChecagemVigente!.DataRealizacao.Should().Be(DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
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
        await Assert.ThrowsAsync<InvalidOperationException>(()=>_prescricoes.CorrigirHorariosInfusaoExternaAsync(
            p.Id,c.UsuarioEnfermeiraId,DateOnly.FromDateTime(DateTime.Today),new(8,15),
            DateOnly.FromDateTime(DateTime.Today),new(9,30),"Correção após assinatura"));
        await _prescricoes.CancelarAsync(p.Id,"Registro lançado para paciente incorreto", "enfermeira");
        p.Cancelada.Should().BeTrue();
        p.Assinaturas.Should().HaveCount(2);
        Despejar("infusao-externa-duas-assinaturas.pdf",final.Pdf);
    }
}
