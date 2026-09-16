using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    private PostoTabletService Posto => new(db,repo,svc,new(repo,new(repo),new(repo)),new(repo),
        new(repo,()=>svc.Hoje.ToDateTime(new TimeOnly(18,0))));
    private static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value,ContratoTablet.Json);
    private async Task Enfermeira()
    {
        usuario.Perfil=PerfilAcesso.Enfermagem;usuario.Profissional!.RegistroConselho="COREN-RJ 123456";
        await db.SaveChangesAsync();
    }
    private async Task<PrescricaoInterna> Folha(SituacaoPrescricao situacao=SituacaoPrescricao.Assinada)
    {
        var f=new PrescricaoInterna {PacienteId=horario.PacienteId,ProfissionalId=horario.ProfissionalId,Data=svc.Hoje,
            Numero=Guid.NewGuid().ToString("N")[..20],CodigoVerificacao=Guid.NewGuid().ToString("N")[..20],Situacao=situacao,
            Itens=[new ItemPrescricaoInterna {Descricao="Item fictício para teste",Ordem=1,Via=ViaAdministracao.Endovenosa}]};
        db.PrescricoesInternas.Add(f);await db.SaveChangesAsync();return f;
    }
    [Fact] public async Task Enfermeira_le_ficha_e_fila_mas_nao_emite_nem_edita_atendimento_medico()
    {
        await Preparar();await Enfermeira();
        Assert.True(PoliticaAtendimentoTablet.PodeUsarPosto(usuario));Assert.False(PoliticaAtendimentoTablet.PodeAtender(usuario));
        var f=Json(await Posto.FichaAsync(sessao,horario.PacienteId,0,default));
        Assert.True(f.GetProperty("podeExecutar").GetBoolean());Assert.False(f.GetProperty("podePrescrever").GetBoolean());
        await Posto.FilaAsync(sessao,0,default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.AbrirAsync(sessao,horario.Id,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Posto.EmitirAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),"receita","Teste"),default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Posto.IniciarAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),ModalidadeAtendimento.Consulta),default));
        Assert.Empty(await db.DocumentosClinicos.ToListAsync());
    }
    [Theory][InlineData(PerfilAcesso.Recepcao)][InlineData(PerfilAcesso.Faturista)]
    public async Task Perfil_administrativo_nao_ganha_acesso_clinico_pelo_vinculo(PerfilAcesso perfil)
    {
        await Preparar();usuario.Perfil=perfil;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Posto.BuscarAsync(sessao,"Paciente",default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Posto.FichaAsync(sessao,horario.PacienteId,0,default));
    }
    [Fact] public async Task Documento_avulso_repetido_nao_cria_sessao_ou_guia()
    {
        await Preparar();var p=new EmitirDocumentoTablet(Guid.NewGuid(),"receita","Texto livre\nSem endereço obrigatório");
        var a=await Posto.EmitirAsync(sessao,horario.PacienteId,p,default);
        Assert.Equal(a,await Posto.EmitirAsync(sessao,horario.PacienteId,p,default));
        var d=await db.DocumentosClinicos.SingleAsync();Assert.Null(d.AgendamentoId);Assert.Null(d.EvolucaoId);
        Assert.Equal(2,await db.Agendamentos.CountAsync());Assert.Empty(await db.Atendimentos.ToListAsync());
        var r=await db.Set<OperacaoClinicaTablet>().SingleAsync();Assert.Null(r.AgendamentoId);Assert.Equal(horario.PacienteId,r.PacienteId);
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(()=>Posto.EmitirAsync(sessao,outro.PacienteId,p,default));
    }
    [Theory][InlineData("comparecimento")][InlineData("relatorio")][InlineData("anamnese")]
    public async Task Ficha_emite_documentos_adicionais_sem_agendamento(string tipo)
    {
        await Preparar();await Posto.EmitirAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),tipo,"Texto clínico fictício"),default);
        Assert.Null((await db.DocumentosClinicos.SingleAsync()).AgendamentoId);
    }
    [Fact] public async Task Atender_agora_reaproveita_horario_original()
    {
        await Preparar();var p=new IniciarAvulsoTablet(Guid.NewGuid(),ModalidadeAtendimento.Consulta);
        var r=await Posto.IniciarAsync(sessao,horario.PacienteId,p,default);Assert.True(r.Existente);Assert.Equal(horario.Id,r.AgendamentoId);
        Assert.Equal(r,await Posto.IniciarAsync(sessao,horario.PacienteId,p,default));Assert.Equal(2,await db.Agendamentos.CountAsync());
        Assert.Equal(ModalidadeAtendimento.AcupunturaComEletro,horario.ModalidadePrevista);
    }
    [Fact] public async Task Atender_agora_nao_adivinha_entre_duas_sessoes_abertas()
    {
        await Preparar();db.Agendamentos.Add(new(){PacienteId=horario.PacienteId,ProfissionalId=usuario.ProfissionalId,DataHora=horario.DataHora.AddHours(2)});await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(()=>Posto.IniciarAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),ModalidadeAtendimento.Consulta),default));
        Assert.Equal(3,await db.Agendamentos.CountAsync());
    }
    [Fact] public async Task Encaixe_novo_tem_autoria_horario_local_e_recibo_unico()
    {
        await Preparar();var p=new IniciarAvulsoTablet(Guid.NewGuid(),ModalidadeAtendimento.Consulta);
        var r=await Posto.IniciarAsync(sessao,outro.PacienteId,p,default);Assert.False(r.Existente);
        Assert.Equal(r,await Posto.IniciarAsync(sessao,outro.PacienteId,p,default));
        var a=await db.Agendamentos.SingleAsync(a=>a.Id==r.AgendamentoId);Assert.Equal(usuario.ProfissionalId,a.ProfissionalId);
        Assert.Equal(svc.Hoje,DateOnly.FromDateTime(a.DataHora));Assert.Equal(a.DataHora,a.InicioAtendimentoEm);Assert.Null(a.FimAtendimentoEm);
        Assert.Equal(StatusAgendamento.Agendado,a.Status);Assert.Equal(3,await db.Agendamentos.CountAsync());
    }
    [Fact] public async Task Ficha_tem_historico_com_paginacao_sem_segredos_de_armazenamento()
    {
        await Preparar();db.Evolucoes.AddRange(Enumerable.Range(1,26).Select(i=>new Evolucao {PacienteId=horario.PacienteId,Data=svc.Hoje.AddDays(-i),TextoEvolucao="Histórico "+i}));await db.SaveChangesAsync();
        var a=Json(await Posto.FichaAsync(sessao,horario.PacienteId,0,default));Assert.Equal(25,a.GetProperty("evolucoes").GetArrayLength());Assert.True(a.GetProperty("mais").GetBoolean());
        var b=Json(await Posto.FichaAsync(sessao,horario.PacienteId,1,default));Assert.Single(b.GetProperty("evolucoes").EnumerateArray());
        Assert.DoesNotContain("senhaHash",a.GetRawText());Assert.DoesNotContain("caminho",a.GetRawText());
        var evolucaoId=(await db.Evolucoes.FirstAsync()).Id;
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(()=>Posto.MapaAsync(sessao,outro.PacienteId,evolucaoId,default));
    }
    [Fact] public async Task Fila_so_oferece_prescricoes_assinadas_e_revalida_revogacao()
    {
        await Preparar();await Enfermeira();var assinada=await Folha();await Folha(SituacaoPrescricao.Rascunho);
        var cancelada=await Folha();cancelada.CanceladaEm=DateTime.Now;await db.SaveChangesAsync();
        var f=Json(await Posto.FilaAsync(sessao,0,default));Assert.Single(f.GetProperty("itens").EnumerateArray());Assert.Equal(assinada.Id,f.GetProperty("itens")[0].GetProperty("id").GetInt32());
        usuario.PermissoesNegadas=Permissao.ChecarPrescricao;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Posto.InfusaoAsync(sessao,assinada.Id,default));
    }
    [Fact] public async Task Execucao_tem_autoria_retificacao_e_fechamento_sem_duplicar()
    {
        await Preparar();await Enfermeira();var f=await Folha();
        var p=new ChecarInfusaoTablet(Guid.NewGuid(),f.Itens[0].Id,PostoTabletService.Versao(f),SituacaoChecagem.Realizado,new(10,0),ConfirmouAlergia:true);
        await Posto.ChecarAsync(sessao,f.Id,p,default);await Posto.ChecarAsync(sessao,f.Id,p,default);
        var c=Assert.Single(f.Itens[0].Checagens);Assert.Equal(usuario.Id,c.ExecutanteUsuarioId);Assert.Equal("COREN-RJ 123456",c.ExecutanteConselho);
        var corrigir=p with {Idempotencia=Guid.NewGuid(),Versao=PostoTabletService.Versao(f),Hora=new(10,15),MotivoRetificacao="Correção do horário informado"};
        await Posto.ChecarAsync(sessao,f.Id,corrigir,default);Assert.Equal(2,f.Itens[0].Checagens.Count);Assert.Equal(c.Id,f.Itens[0].ChecagemVigente!.RetificaChecagemId);
        var encerrar=new EncerrarInfusaoTablet(Guid.NewGuid(),PostoTabletService.Versao(f));
        await Posto.EncerrarAsync(sessao,f.Id,encerrar,default);await Posto.EncerrarAsync(sessao,f.Id,encerrar,default);
        Assert.Equal(SituacaoPrescricao.Encerrada,f.Situacao);Assert.NotNull(f.EncerradaEm);Assert.Null(f.AssinaturaDaExecucao);
    }
    [Theory][InlineData("versao")][InlineData("item")][InlineData("futuro")][InlineData("motivo")][InlineData("conselho")][InlineData("rascunho")]
    public async Task Execucao_recusa_dados_inseguros_sem_checar(string falha)
    {
        await Preparar();await Enfermeira();var f=await Folha(falha=="rascunho"?SituacaoPrescricao.Rascunho:SituacaoPrescricao.Assinada);
        if(falha=="conselho"){usuario.Profissional!.RegistroConselho=null;await db.SaveChangesAsync();}
        var p=new ChecarInfusaoTablet(Guid.NewGuid(),falha=="item"?999999:f.Itens[0].Id,falha=="versao"?"antiga":PostoTabletService.Versao(f),
            falha=="motivo"?SituacaoChecagem.NaoRealizado:SituacaoChecagem.Realizado,falha=="futuro"?new(23,59):new(10,0),ConfirmouAlergia:true);
        await Assert.ThrowsAnyAsync<Exception>(()=>Posto.ChecarAsync(sessao,f.Id,p,default));Assert.Empty(await db.ChecagensPrescricao.ToListAsync());
    }
}
