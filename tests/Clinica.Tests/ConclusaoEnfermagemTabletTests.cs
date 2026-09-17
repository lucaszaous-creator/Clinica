using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    [Fact] public async Task Enfermagem_do_portal_grava_na_sessao_do_medico_sem_finalizar()
    {
        await Preparar();await Enfermeira();
        var pedido=new RegistroEnfermagemTablet(Guid.NewGuid(),svc.Hoje,new(8,0),"Observação fictícia",false,null,null,AgendamentoId:horario.Id);
        await Posto.RegistrarEnfermagemAsync(sessao,horario.PacienteId,pedido,default);
        await Posto.RegistrarEnfermagemAsync(sessao,horario.PacienteId,pedido,default);
        var e=await db.EvolucoesEnfermagem.SingleAsync();Assert.Equal(horario.Id,e.AgendamentoId);
        Assert.Null(horario.FimAtendimentoEm);Assert.Empty(await db.Atendimentos.ToListAsync());Assert.Empty(await db.Codigos.ToListAsync());
        var ficha=Json(await Posto.FichaAsync(sessao,horario.PacienteId,0,default));
        Assert.Equal(horario.Id,ficha.GetProperty("enfermagem")[0].GetProperty("agendamentoId").GetInt32());
    }

    [Fact] public async Task Enfermagem_nao_vincula_registro_a_paciente_diferente()
    {
        await Preparar();await Enfermeira();
        var pedido=new RegistroEnfermagemTablet(Guid.NewGuid(),svc.Hoje,new(8,0),"Observação fictícia",false,null,null,AgendamentoId:outro.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Posto.RegistrarEnfermagemAsync(sessao,horario.PacienteId,pedido,default));
        Assert.Empty(await db.EvolucoesEnfermagem.ToListAsync());
    }

    [Fact] public async Task Infusao_externa_no_portal_e_idempotente_e_nao_gera_guia()
    {
        await Preparar();await Enfermeira();
        db.Usuarios.Add(new UsuarioSistema {Nome="Médico B",Login="medico.b",Perfil=PerfilAcesso.Profissional,ProfissionalId=outro.ProfissionalId});await db.SaveChangesAsync();
        var pedido=new InfusaoExternaTablet(Guid.NewGuid(),new(horario.PacienteId,outro.ProfissionalId!.Value,null,svc.Hoje,new(8,0),"Execução fictícia","Orientação médica externa fictícia"));
        var a=await Posto.RegistrarInfusaoExternaAsync(sessao,horario.PacienteId,pedido,default);
        Assert.Equal(a,await Posto.RegistrarInfusaoExternaAsync(sessao,horario.PacienteId,pedido,default));
        var p=await db.PrescricoesInternas.SingleAsync();Assert.True(p.OrigemEnfermagem);Assert.True(p.AguardaValidacaoMedica);Assert.Equal(SituacaoPrescricao.Encerrada,p.Situacao);
        Assert.Null(horario.FimAtendimentoEm);Assert.Empty(await db.Atendimentos.ToListAsync());Assert.Empty(await db.Codigos.ToListAsync());
        var fila=Json(await Posto.FilaAsync(sessao,0,default));Assert.Single(fila.GetProperty("itens").EnumerateArray());
        var contexto=Json(await Posto.ContextoEnfermagemAsync(sessao,horario.PacienteId,svc.Hoje,default));
        Assert.Single(contexto.GetProperty("medicos").EnumerateArray());
        Assert.DoesNotContain("senha",contexto.ToString(),StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task Registro_substituido_nao_recebe_vinculo_tardio()
    {
        await Preparar();await Enfermeira();var e=await Enfermagem(null);e.AutorUsuarioId=usuario.Id;
        var novo=await Enfermagem(null);novo.RetificaEvolucaoId=e.Id;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new EvolucaoEnfermagemService(repo).VincularSessaoAsync(e.Id,horario.Id,usuario.Id,"Vínculo tardio"));
        Assert.Null(e.AgendamentoId);
    }

    [Theory][InlineData(null)][InlineData(true)]
    public async Task Conclusao_sem_resposta_ou_sem_enfermagem_vinculada_nao_grava_guias(bool? resposta)
    {
        await Preparar();var p=(await Pedido()) with {Finalizar=true,HouveEnfermagem=resposta};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.SalvarAsync(sessao,horario.Id,p,default));
        Assert.Null(horario.FimAtendimentoEm);Assert.Empty(await db.Atendimentos.ToListAsync());
        Assert.Empty(await db.Evolucoes.ToListAsync());
    }

    private async Task<EvolucaoEnfermagem> Enfermagem(int? vinculo)
    {
        var e=new EvolucaoEnfermagem {PacienteId=horario.PacienteId,AgendamentoId=vinculo,
            Data=svc.Hoje,Hora=new(8,0),Texto="Observação fictícia",AutorNome="Enfermagem teste",AutorConselho="COREN teste"};
        db.EvolucoesEnfermagem.Add(e);await db.SaveChangesAsync();return e;
    }

    [Fact] public async Task Evolucao_avulsa_ou_de_outra_sessao_nao_libera_conclusao()
    {
        await Preparar();await Enfermagem(null);await Enfermagem(outro.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new AgendaService(repo,new(repo)).ConferirEnfermagemParaConclusaoAsync(horario.Id,true));
        Assert.Null(horario.FimAtendimentoEm);
    }

    [Fact] public async Task Evolucao_vigente_libera_e_resposta_fica_auditada_sem_duplicar()
    {
        await Preparar();await Enfermagem(horario.Id);
        var p=(await Pedido()) with {Finalizar=true,HouveEnfermagem=true};
        await svc.SalvarAsync(sessao,horario.Id,p,default);await svc.SalvarAsync(sessao,horario.Id,p,default);
        Assert.True(horario.HouveAtendimentoEnfermagem);Assert.Equal(usuario.Id,horario.EnfermagemConferidaPorUsuarioId);
        Assert.NotNull(horario.FimAtendimentoEm);Assert.Single(await db.Atendimentos.ToListAsync());
    }

    [Fact] public async Task Cancelada_ou_substituida_nao_conta_como_evolucao_vigente()
    {
        await Preparar();var original=await Enfermagem(horario.Id);var retificada=await Enfermagem(horario.Id);
        retificada.RetificaEvolucaoId=original.Id;retificada.CanceladaEm=DateTime.Now;await db.SaveChangesAsync();
        Assert.False(await repo.TemEvolucaoEnfermagemVigenteNoHorarioAsync(horario.Id));
    }

    [Fact] public async Task Resposta_nao_nao_pode_contradizer_enfermagem_vigente()
    {
        await Preparar();await Enfermagem(horario.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new AgendaService(repo,new(repo)).ConferirEnfermagemParaConclusaoAsync(horario.Id,false));
    }

    [Fact] public async Task Perfil_enfermagem_com_permissoes_extras_nao_finaliza_medico()
    {
        await Preparar();usuario.Perfil=PerfilAcesso.Enfermagem;
        usuario.PermissoesExtras=Permissao.EditarProntuario|Permissao.LancarAtendimento;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new AgendaService(repo,new(repo)).ExigirConclusaoClinicaAsync(horario.Id,usuario.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Posto.IniciarAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),ModalidadeAtendimento.Consulta),default));
        Assert.Null(horario.FimAtendimentoEm);Assert.Empty(await db.Atendimentos.ToListAsync());
    }

    [Fact] public async Task Vinculo_tardio_preserva_fechamento_guias_e_datas_originais()
    {
        await Preparar();var p=(await Pedido()) with {Finalizar=true,HouveEnfermagem=false};
        await svc.SalvarAsync(sessao,horario.Id,p,default);
        var fim=horario.FimAtendimentoEm;var atendimento=horario.AtendimentoId;
        var guias=await db.Codigos.Select(g=>g.Id).ToArrayAsync();
        var e=await Enfermagem(null);e.AutorUsuarioId=usuario.Id;
        usuario.PermissoesExtras=Permissao.RegistrarEvolucaoEnfermagem;await db.SaveChangesAsync();
        var data=e.Data;var registrado=e.RegistradoEm;
        await new EvolucaoEnfermagemService(repo).VincularSessaoAsync(e.Id,horario.Id,usuario.Id,"Vínculo omitido na sessão original");
        Assert.Equal(horario.Id,e.AgendamentoId);Assert.Equal(fim,horario.FimAtendimentoEm);
        Assert.Equal(atendimento,horario.AtendimentoId);Assert.Equal(guias,await db.Codigos.Select(g=>g.Id).ToArrayAsync());
        Assert.Equal(data,e.Data);Assert.Equal(registrado,e.RegistradoEm);
        Assert.False(horario.HouveAtendimentoEnfermagem); // preserva a declaração original do médico; o vínculo tem auditoria própria
    }
}
