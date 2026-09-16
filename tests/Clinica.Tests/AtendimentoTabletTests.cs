using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class AtendimentoTabletTests : IDisposable
{
    private readonly SqliteConnection connection=new("Data Source=:memory:");
    private readonly ClinicaDbContext db;
    private readonly ClinicaRepositorio repo;
    private readonly AtendimentoTabletService svc;
    private readonly PortalTabletService portal;
    private readonly Relogio tempo=new();
    private UsuarioSistema usuario=null!;
    private SessaoTablet sessao=null!;
    private Agendamento horario=null!,outro=null!;
    public AtendimentoTabletTests()
    {
        connection.Open();db=new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();repo=new(db);
        var prontuario=new ProntuarioService(repo);var prescricoes=new PrescricaoService(repo);
        var documentos=new DocumentoClinicoService(repo,prontuario,new(repo));
        portal=new(db,repo,documentos,new(repo),new(repo),new(repo),new([1,2],true),tempo);
        svc=new(db,repo,prontuario,new(repo,new(repo)),documentos,new(repo,prescricoes),prescricoes,tempo);
    }
    public void Dispose(){db.Dispose();connection.Dispose();}
    private async Task Preparar()
    {
        var a=new Profissional {Nome="Profissional A",Ativo=true};var b=new Profissional {Nome="Profissional B",Ativo=true};
        db.Profissionais.AddRange(a,b);await db.SaveChangesAsync();
        usuario=await new AcessoService(repo).CriarAsync("A","medica.teste","TabletTeste#2026",PerfilAcesso.Profissional);
        usuario.ProfissionalId=a.Id;usuario.DeveTrocarSenha=false;
        horario=new() {Paciente=new Paciente {Nome="Paciente autorizado",Convenio=Convenio.UnimedPadrao},ProfissionalId=a.Id,
            DataHora=svc.Hoje.ToDateTime(new TimeOnly(9,0)),ModalidadePrevista=ModalidadeAtendimento.AcupunturaComEletro};
        outro=new() {Paciente=new Paciente {Nome="Paciente restrito",Convenio=Convenio.UnimedPadrao},ProfissionalId=b.Id,
            DataHora=horario.DataHora,ModalidadePrevista=horario.ModalidadePrevista};
        db.Agendamentos.AddRange(horario,outro);await db.SaveChangesAsync();
        sessao=(await portal.EntrarAsync(usuario,"aparelho",null,default)).Sessao;
    }
    private async Task<SalvarAtendimentoTablet> Pedido(string texto="Evolução fictícia")
    {
        var json=JsonSerializer.SerializeToElement(await svc.AbrirAsync(sessao,horario.Id,default),ContratoTablet.Json);
        var dto=json.GetProperty("evolucao").Deserialize<EvolucaoClinicaTablet>(ContratoTablet.Json)!;
        return new(Guid.NewGuid(),dto with {TextoEvolucao=texto});
    }
    [Fact] public async Task Agenda_e_modelos_nao_expoem_outro_profissional()
    {
        await Preparar();
        db.ModelosEvolucao.AddRange(new ModeloEvolucao {Nome="Modelo permitido",ProfissionalId=usuario.ProfissionalId,TextoEvolucao="A"},
            new ModeloEvolucao {Nome="Modelo restrito",ProfissionalId=outro.ProfissionalId,TextoEvolucao="B"});await db.SaveChangesAsync();
        var agenda=JsonSerializer.Serialize(await svc.DiaAsync(sessao,null,default));
        Assert.Contains("Paciente autorizado",agenda);Assert.DoesNotContain("Paciente restrito",agenda);
        var registro=JsonSerializer.Serialize(await svc.AbrirAsync(sessao,horario.Id,default));
        Assert.Contains("Modelo permitido",registro);Assert.DoesNotContain("Modelo restrito",registro);
    }
    [Fact] public async Task Trocar_id_na_url_nao_abre_nem_grava_outro_paciente()
    {
        await Preparar();
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(()=>svc.AbrirAsync(sessao,outro.Id,default));
        var pedido=await Pedido();
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(()=>svc.SalvarAsync(sessao,outro.Id,pedido,default));
        Assert.Empty(await db.Evolucoes.ToListAsync());
    }
    [Theory][InlineData("paciente")][InlineData("revogada")]
    public async Task Modo_paciente_ou_revogado_nao_abre_clinico(string modo)
    {await Preparar();sessao.Modo=modo;await db.SaveChangesAsync();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.AbrirAsync(sessao,horario.Id,default));}
    [Fact] public async Task Permissao_retirada_e_profissional_inativo_valem_sem_novo_login()
    {
        await Preparar();usuario.PermissoesNegadas=Permissao.VerProntuario;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.DiaAsync(sessao,null,default));
        usuario.PermissoesNegadas=0;usuario.Profissional!.Ativo=false;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.DiaAsync(sessao,null,default));
    }
    [Fact] public async Task Senha_alterada_invalida_sessao()
    {await Preparar();usuario.SenhaHash="senha-alterada";await db.SaveChangesAsync();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.DiaAsync(sessao,null,default));}
    [Fact] public async Task Quinze_minutos_sem_atividade_exigem_entrada()
    {await Preparar();tempo.Avancar(TimeSpan.FromMinutes(16));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.DiaAsync(sessao,null,default));}
    [Fact] public async Task Duplo_envio_salva_uma_evolucao_e_um_mapa()
    {
        await Preparar();var pedido=await Pedido();pedido=pedido with {Evolucao=pedido.Evolucao with {Mapa=new([new(FaceCorpo.Frente,.4,.6,"IG4",TecnicaPonto.Agulha)],"Mapa fictício")}};
        var a=await svc.SalvarAsync(sessao,horario.Id,pedido,default);var b=await svc.SalvarAsync(sessao,horario.Id,pedido,default);
        Assert.Equal(ContratoTablet.Serializar(a),ContratoTablet.Serializar(b));Assert.Single(await db.Evolucoes.ToListAsync());Assert.Single(await db.MapasCorporais.ToListAsync());
        Assert.Single(await db.PontosMapa.ToListAsync());Assert.Single(await db.Set<OperacaoClinicaTablet>().ToListAsync());
    }
    [Fact] public async Task Mesmo_recibo_com_texto_diferente_e_recusado()
    {
        await Preparar();var p=await Pedido();await svc.SalvarAsync(sessao,horario.Id,p,default);
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(()=>svc.SalvarAsync(sessao,horario.Id,p with {Evolucao=p.Evolucao with {TextoEvolucao="Outro texto"}},default));
    }
    [Fact] public async Task Edicao_antiga_nao_sobrescreve_registro_recente()
    {
        await Preparar();var p=await Pedido();await svc.SalvarAsync(sessao,horario.Id,p,default);
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(()=>svc.SalvarAsync(sessao,horario.Id,p with {Idempotencia=Guid.NewGuid()},default));
    }
    [Fact] public async Task Finalizacao_amarra_evolucao_atendimento_e_guias_sem_duplicar()
    {
        await Preparar();var p=(await Pedido()) with {Finalizar=true};
        var result=await svc.SalvarAsync(sessao,horario.Id,p,default);
        Assert.True(result.Finalizado);Assert.True(result.Guias>0);Assert.Equal(StatusAgendamento.Realizado,horario.Status);
        Assert.NotNull(horario.FimAtendimentoEm);Assert.Equal(horario.AtendimentoId,(await db.Evolucoes.SingleAsync()).AtendimentoId);
        await svc.SalvarAsync(sessao,horario.Id,p,default);Assert.Single(await db.Atendimentos.ToListAsync());
    }
    [Fact] public async Task Infusao_livre_tem_padrao_e_recibo_sem_duplicacao()
    {
        await Preparar();var p=new EmitirDocumentoTablet(Guid.NewGuid(),"infusao","Texto livre fictício\nSegunda linha");
        var a=await svc.EmitirAsync(sessao,horario.Id,p,default);var b=await svc.EmitirAsync(sessao,horario.Id,p,default);
        Assert.Equal(a,b);var folha=await db.PrescricoesInternas.Include(x=>x.Itens).SingleAsync();
        Assert.Equal("SF 0,9%",folha.Itens.Single().Diluente);Assert.Equal("1h",folha.Itens.Single().TempoInfusao);
        Assert.Equal(p.Texto,folha.Itens.Single().Descricao);Assert.Equal(SituacaoPrescricao.Rascunho,folha.Situacao);
    }
    [Fact] public async Task Permissao_de_prescrever_e_reavaliada_na_emissao()
    {
        await Preparar();usuario.PermissoesNegadas=Permissao.Prescrever;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.EmitirAsync(sessao,horario.Id,new(Guid.NewGuid(),"receita","Texto"),default));
        Assert.Empty(await db.DocumentosClinicos.ToListAsync());
    }
    [Fact] public async Task Copia_expoe_posologia_e_preserva_via_sem_transformar_em_endovenosa()
    {
        await Preparar();
        await svc.EmitirAsync(sessao,horario.Id,new(Guid.NewGuid(),"receita","Corpo fictício"),default);
        var receita=await db.DocumentosClinicos.Include(d=>d.Itens).SingleAsync();
        receita.Itens.Add(new ItemDocumento {Descricao="Item fictício",Detalhe="Posologia original",Quantidade="2 unidades"});
        await svc.EmitirAsync(sessao,horario.Id,new(Guid.NewGuid(),"infusao","Item de teste",Via:ViaAdministracao.Subcutanea),default);
        var item=await db.ItensPrescricaoInterna.SingleAsync();
        Assert.Equal(ViaAdministracao.Subcutanea,item.Via);
        item.Dose="Dose de teste";item.HoraPrevista=new(10,30);item.SeNecessario=true;item.Observacoes="Cuidado original";
        item.SuspensoEm=DateTime.Now;item.MotivoSuspensao="Suspensão de teste";await db.SaveChangesAsync();
        var json=JsonSerializer.SerializeToElement(await svc.AbrirAsync(sessao,horario.Id,default),ContratoTablet.Json);
        var linha=json.GetProperty("documentos")[0].GetProperty("itens")[0];
        Assert.Equal("Posologia original",linha.GetProperty("detalhe").GetString());
        Assert.Equal("2 unidades",linha.GetProperty("quantidade").GetString());
        var infusao=json.GetProperty("infusoes")[0].GetProperty("itens")[0];
        Assert.Equal("Subcutanea",infusao.GetProperty("via").GetString());Assert.True(infusao.GetProperty("seNecessario").GetBoolean());
        Assert.Equal("Dose de teste",infusao.GetProperty("dose").GetString());
        Assert.Equal("10:30:00",infusao.GetProperty("horaPrevista").GetString());
        Assert.Equal("Cuidado original",infusao.GetProperty("observacoes").GetString());
        Assert.Equal("Suspensão de teste",infusao.GetProperty("motivoSuspensao").GetString());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.EmitirAsync(sessao,horario.Id,new(Guid.NewGuid(),"infusao","Texto",Via:(ViaAdministracao)99),default));
    }
    [Fact] public async Task Mapa_e_documento_de_outro_paciente_nao_sao_acessiveis()
    {
        await Preparar();var e=new Evolucao {PacienteId=outro.PacienteId,ProfissionalId=outro.ProfissionalId,Data=svc.Hoje,TextoEvolucao="Restrito"};
        db.Evolucoes.Add(e);var d=new DocumentoClinico {PacienteId=outro.PacienteId,ProfissionalId=outro.ProfissionalId,Tipo=TipoDocumentoClinico.Receita,Corpo="Restrito",Numero="X",CodigoVerificacao="RESTRITO"};
        db.DocumentosClinicos.Add(d);await db.SaveChangesAsync();
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(()=>svc.CopiarMapaAsync(sessao,horario.Id,e.Id,default));
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(()=>svc.ExigirDocumentoAsync(sessao,horario.Id,"documento",d.Id,false,default));
    }
    [Fact] public async Task Sem_permissao_de_lancar_pode_salvar_mas_nao_concluir()
    {
        await Preparar();usuario.PermissoesNegadas=Permissao.LancarAtendimento;await db.SaveChangesAsync();
        var p=await Pedido();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.SalvarAsync(sessao,horario.Id,p with {Finalizar=true},default));
        // A tentativa seguinte representa outra requisição, com a versão persistida da sessão.
        await db.Entry(sessao).ReloadAsync();
        await svc.SalvarAsync(sessao,horario.Id,p,default);Assert.Empty(await db.Atendimentos.ToListAsync());
    }
    [Fact] public async Task Campos_longos_respeitam_o_banco_sem_truncar_prescricoes()
    {
        await Preparar();var p=await Pedido();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.SalvarAsync(sessao,horario.Id,p with {Evolucao=p.Evolucao with {QueixaPrincipal=new string('Q',1001)}},default));
        await db.Entry(sessao).ReloadAsync();
        var texto=new string('T',12000);
        await svc.EmitirAsync(sessao,horario.Id,new(Guid.NewGuid(),"exame",texto),default);
        var doc=await db.DocumentosClinicos.Include(d=>d.Itens).SingleAsync();
        Assert.Equal(texto,doc.Corpo);Assert.True(doc.Itens.Single().Descricao.Length<=300);
    }
    [Fact] public async Task Modelo_de_pontos_fica_no_paciente_e_nao_se_duplica()
    {
        await Preparar();var p=new SalvarModeloMapaTablet(Guid.NewGuid(),"Sessão habitual",new([new(FaceCorpo.Costas,.3,.4,"Ponto",TecnicaPonto.Ventosa)],"Observação"));
        await svc.SalvarModeloMapaAsync(sessao,horario.Id,p,default);await svc.SalvarModeloMapaAsync(sessao,horario.Id,p,default);
        var m=await db.ProtocolosCorporais.Include(p=>p.Pontos).SingleAsync();Assert.Equal(horario.PacienteId,m.PacienteId);Assert.Single(m.Pontos);
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(()=>svc.SalvarModeloMapaAsync(sessao,outro.Id,p with {Idempotencia=Guid.NewGuid()},default));
    }
    [Fact] public async Task Evolucoes_ambiguas_nao_sao_escolhidas_automaticamente()
    {
        await Preparar();
        db.Evolucoes.AddRange(Enumerable.Range(0,2).Select(_=>new Evolucao {PacienteId=horario.PacienteId,AgendamentoId=horario.Id,ProfissionalId=usuario.ProfissionalId,Data=svc.Hoje,TextoEvolucao="Fictícia"}));await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(()=>svc.AbrirAsync(sessao,horario.Id,default));
    }
    [Fact] public void Safeid_amarrado_a_sessao_state_unico_e_expiracao()
    {
        var store=new AutorizacoesSafeIdTablet(tempo);var a=store.Criar("sessao-A",1,2,"documento","hash",false);
        Assert.Throws<RecursoClinicoIndisponivel>(()=>store.Obter(a.Id,"sessao-B"));
        Assert.Throws<RecursoClinicoIndisponivel>(()=>store.Receber(new string('0',64),"codigo",null));
        Assert.Equal(a.Id,store.Receber(a.Estado,"codigo",null));
        Assert.Throws<RecursoClinicoIndisponivel>(()=>store.Receber(a.Estado,"codigo",null));
        store.Obter(a.Id,"sessao-A",true);
        Assert.Throws<ConflitoClinicoTablet>(()=>store.Obter(a.Id,"sessao-A",true));
        store.Concluir(a,true);Assert.Null(a.Codigo);
        tempo.Avancar(TimeSpan.FromMinutes(6));Assert.Throws<RecursoClinicoIndisponivel>(()=>store.Obter(a.Id,"sessao-A"));
    }
    private sealed class Relogio : TimeProvider
    {private DateTimeOffset agora=new(2026,9,16,14,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>agora;public void Avancar(TimeSpan t)=>agora+=t;}
}
