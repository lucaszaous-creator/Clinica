using System.Text;
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
    [Fact] public async Task Estado_da_recepcao_reflete_pacote_e_conta_a_receber_sem_expor_valores()
    {
        await Preparar();await svc.SalvarAsync(sessao,horario.Id,(await Pedido()) with {Finalizar=true,HouveEnfermagem=false},default);
        db.Lancamentos.Add(new() {AtendimentoId=horario.AtendimentoId,PacienteId=horario.PacienteId,
            Tipo=TipoLancamento.Entrada,Status=StatusLancamento.Previsto,Valor=123.45m,Data=svc.Hoje,Descricao="Teste fictício"});
        await db.SaveChangesAsync();
        var f=Json(await svc.AbrirAsync(sessao,horario.Id,default)).GetProperty("faturamento");
        Assert.True(f.GetProperty("recepcaoDetalhe").GetProperty("contaAReceberRegistrada").GetBoolean());
        Assert.False(f.GetProperty("recepcaoDetalhe").GetProperty("recebimentoRegistrado").GetBoolean());
        Assert.DoesNotContain("123.45",f.ToString());
        Assert.Single(Json(await Posto.PendenciasAsync(sessao,0,default)).GetProperty("recepcao").EnumerateArray());
    }
    [Fact] public async Task Modelo_reutilizavel_nao_sobrescreve_modelo_existente()
    {
        await Preparar();var p=new NovoModeloDocumentoTablet(Guid.NewGuid(),"Modelo fictício",TipoDocumentoClinico.Receita,"Texto reutilizável");
        var r=await Posto.CriarModeloAsync(sessao,horario.PacienteId,p,default);
        Assert.Equal(r,await Posto.CriarModeloAsync(sessao,horario.PacienteId,p,default));
        Assert.Single(Json(await Posto.ModelosDocumentoAsync(sessao,default)).EnumerateArray());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Posto.CriarModeloAsync(sessao,horario.PacienteId,p with {Idempotencia=Guid.NewGuid(),Texto="Outro texto"},default));
        Assert.Equal("Texto reutilizável",(await db.ModelosDocumento.SingleAsync()).Corpo);
    }
    [Fact] public async Task Enfermagem_registra_sinais_e_retifica_sem_apagar_anterior_e_transcreve_exame()
    {
        await PrepararBSV();await Enfermeira();
        var data=DateOnly.FromDateTime(DateTime.Today);var hora=TimeOnly.FromDateTime(DateTime.Now);
        var p=new RegistroEnfermagemTablet(Guid.NewGuid(),data,hora,"Observação fictícia",false,new(120,80),null,AgendamentoId:horario.Id);
        var original=await Posto.RegistrarEnfermagemAsync(sessao,horario.PacienteId,p,default);
        Assert.Equal(original,await Posto.RegistrarEnfermagemAsync(sessao,horario.PacienteId,p,default));
        var r=await Posto.RegistrarEnfermagemAsync(sessao,horario.PacienteId,p with {Idempotencia=Guid.NewGuid(),Texto="Observação corrigida",RetificaId=original.Id,Motivo="Correção de teste"},default);
        var antigo=await db.EvolucoesEnfermagem.SingleAsync(e=>e.Id==original.Id);Assert.Equal("Observação fictícia",antigo.Texto);
        var novo=await db.EvolucoesEnfermagem.SingleAsync(e=>e.Id==r.Id);Assert.Equal(original.Id,novo.RetificaEvolucaoId);Assert.Equal(usuario.Nome,novo.AutorNome);Assert.Equal(120,novo.PressaoSistolica);
        await Posto.RegistrarExameAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),data,"Exame fictício","Não reagente",null,null,null,null),default);
        Assert.Single(await db.ResultadosExame.ToListAsync());
    }
    [Fact] public async Task Correcao_de_infusao_preserva_itens_e_original_e_assinatura_dupla_parcial_fica_na_fila()
    {
        await Preparar();var folha=await Folha(SituacaoPrescricao.Rascunho);
        var r=Json(await Posto.RascunhoAsync(sessao,horario.PacienteId,"infusao",folha.Id,default));
        var p=new RascunhoTablet(Guid.NewGuid(),r.GetProperty("versao").GetString()!,"Correção fictícia",null,"Cuidado",null,"Indicação",true,
            [new("Item A","Dose fictícia","Diluente","Volume",ViaAdministracao.Endovenosa,"Tempo",new TimeOnly(10,0),true,"Observação")]);
        var novo=await Posto.CorrigirRascunhoAsync(sessao,horario.PacienteId,"infusao",folha.Id,p,default);
        Assert.NotNull(folha.CanceladaEm);var atual=await repo.ObterPrescricaoInternaAsync(novo.Id);
        Assert.Equal("Dose fictícia",atual!.Itens.Single().Dose);Assert.True(atual.Itens.Single().SeNecessario);
        atual.Situacao=SituacaoPrescricao.Encerrada;atual.Assinaturas.Add(new() {Papel=PapelAssinatura.Executante,NomeAssinante="Enfermeira fictícia"});await db.SaveChangesAsync();
        await Enfermeira();var fila=Json(await Posto.FilaAsync(sessao,0,default));
        Assert.True(fila.GetProperty("itens")[0].GetProperty("registroSemAssinatura").GetBoolean());
    }
    [Fact] public async Task Presenca_confirmada_na_recepcao_permite_evoluir_e_concluir_sem_duplicar_guias()
    {
        await Preparar();
        await new AgendaService(repo,new(repo)).ConfirmarPresencaAsync(horario.Id,operador:usuario.Login);
        var id=horario.AtendimentoId;var quantidade=await db.Codigos.CountAsync();
        Assert.NotNull(id);Assert.Null(horario.FimAtendimentoEm);
        var r=Json(await svc.AbrirAsync(sessao,horario.Id,default));
        Assert.False(r.GetProperty("agendamento").GetProperty("finalizado").GetBoolean());
        await svc.SalvarAsync(sessao,horario.Id,(await Pedido()) with {Finalizar=true,HouveEnfermagem=false},default);
        Assert.Equal(id,horario.AtendimentoId);Assert.Equal(quantidade,await db.Codigos.CountAsync());
        Assert.NotNull(horario.FimAtendimentoEm);
    }
    [Fact] public async Task Protocolo_e_guias_permanecem_ao_reabrir_e_refletem_baixa_no_sistema()
    {
        await Preparar();var salvo=await svc.SalvarAsync(sessao,horario.Id,(await Pedido()) with {Finalizar=true,HouveEnfermagem=false},default);
        var codigo=await db.Codigos.FirstAsync();codigo.DarBaixa(svc.Hoje,"GUIA-FICTICIA-123",usuario.Login,null);await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var f=Json(await svc.AbrirAsync(sessao,horario.Id,default)).GetProperty("faturamento");
        Assert.Equal(salvo.AtendimentoId,f.GetProperty("atendimentoId").GetInt32());
        Assert.False(f.GetProperty("previa").GetBoolean());
        Assert.Contains(f.GetProperty("guias").EnumerateArray(),g=>g.GetProperty("numeroGuiaReal").GetString()=="GUIA-FICTICIA-123");
    }
    [Fact] public async Task Cadastro_indefinido_aparece_antes_de_concluir_sem_impedir_salvar_evolucao()
    {
        await Preparar();horario.Paciente!.ConvenioCodigo=ConvenioCadastro.CodigoADefinir;await db.SaveChangesAsync();
        var f=Json(await svc.AbrirAsync(sessao,horario.Id,default)).GetProperty("faturamento");
        Assert.False(f.GetProperty("podeConcluir").GetBoolean());
        await svc.SalvarAsync(sessao,horario.Id,await Pedido(),default);
        Assert.Single(await db.Evolucoes.ToListAsync());Assert.Empty(await db.Atendimentos.ToListAsync());
    }
    [Fact] public async Task Anamnese_grava_no_dominio_e_revisao_preserva_versao_anterior_e_rejeita_tela_antiga()
    {
        await Preparar();var f=Json(await Posto.FichaAsync(sessao,horario.PacienteId,0,default));
        var p=new AnamneseTablet(Guid.NewGuid(),f.GetProperty("versaoAnamnese").GetString()!,"Antecedente fictício",null,null,null,null,null,null);
        Assert.Equal(await Posto.SalvarAnamneseAsync(sessao,horario.PacienteId,p,default),await Posto.SalvarAnamneseAsync(sessao,horario.PacienteId,p,default));
        f=Json(await Posto.FichaAsync(sessao,horario.PacienteId,0,default));
        var correcao=p with {Idempotencia=Guid.NewGuid(),Versao=f.GetProperty("versaoAnamnese").GetString()!,AntecedentesPessoais="Histórico revisto",Motivo="Informação corrigida"};
        await Posto.SalvarAnamneseAsync(sessao,horario.PacienteId,correcao,default);
        Assert.Equal("Antecedente fictício",(await db.VersoesAnamnese.SingleAsync()).AntecedentesPessoais);
        Assert.Single(Json(await Posto.VersoesAnamneseAsync(sessao,horario.PacienteId,default)).EnumerateArray());
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(()=>Posto.SalvarAnamneseAsync(sessao,horario.PacienteId,correcao with {Idempotencia=Guid.NewGuid()},default));
    }
    [Fact] public async Task Alergia_gravada_no_portal_alimenta_conferencia_e_nao_pode_ser_editada_por_outro_paciente()
    {
        await Preparar();var r=await Posto.SalvarProblemaAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),0,null,NaturezaProblema.Alergia,"Alergia fictícia",null,null,null),default);
        Assert.Single((await new PrescricaoService(repo).ContextoAsync(horario.PacienteId)).Alergias);
        var f=Json(await Posto.FichaAsync(sessao,horario.PacienteId,0,default));var versao=f.GetProperty("problemas")[0].GetProperty("versao").GetString();
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(()=>Posto.SalvarProblemaAsync(sessao,outro.PacienteId,new(Guid.NewGuid(),r.Id,versao,NaturezaProblema.Alergia,"Alteração",null,null,null),default));
        await Enfermeira();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Posto.SalvarProblemaAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),0,null,NaturezaProblema.Diagnostico,"Teste",null,null,null),default));
    }
    [Fact] public async Task Medida_usa_validacao_do_catalogo_e_cancelamento_preserva_registro()
    {
        await Preparar();var tipo=MedidaClinicaService.Registraveis.First(t=>t.RotuloSegundoValor!=null);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Posto.RegistrarMedidaAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),svc.Hoje,tipo.Codigo,120,null,null),default));
        await db.Entry(sessao).ReloadAsync(); // Nova requisição relê a versão da sessão após rollback.
        var r=await Posto.RegistrarMedidaAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),svc.Hoje,tipo.Codigo,120,80,null),default);
        var f=Json(await Posto.FichaAsync(sessao,horario.PacienteId,0,default));
        await Posto.CancelarMedidaAsync(sessao,horario.PacienteId,r.Id,new(Guid.NewGuid(),f.GetProperty("medidas")[0].GetProperty("versao").GetString()!,"Aparelho incorreto"),default);
        Assert.NotNull((await db.MedidasClinicas.SingleAsync()).CanceladaEm);
    }
    [Fact] public async Task Anexo_tem_limite_tipo_recibo_e_isolamento_de_paciente_inclusive_na_enfermagem()
    {
        await Preparar();await Enfermeira();
        var p=new AnexoTablet(Guid.NewGuid(),svc.Hoje,"Laudo fictício","laudo.pdf","application/pdf",Encoding.ASCII.GetBytes("%PDF-1.7\n% teste ficticio"),null);
        var r=await Posto.AnexarAsync(sessao,horario.PacienteId,p,default);
        Assert.Equal(r,await Posto.AnexarAsync(sessao,horario.PacienteId,p,default));
        Assert.Equal(p.Conteudo,(await Posto.ConteudoAnexoAsync(sessao,horario.PacienteId,r.Id,default)).Conteudo);
        await Assert.ThrowsAsync<RecursoClinicoIndisponivel>(()=>Posto.ConteudoAnexoAsync(sessao,outro.PacienteId,r.Id,default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Posto.AnexarAsync(sessao,horario.PacienteId,p with {Idempotencia=Guid.NewGuid(),NomeArquivo="falso.png"},default));
        await db.Entry(sessao).ReloadAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Posto.AnexarAsync(sessao,horario.PacienteId,p with {Idempotencia=Guid.NewGuid(),Conteudo=new byte[PostoTabletService.LimiteAnexo+1]},default));
        Assert.Single(await db.AnexosPaciente.ToListAsync());
    }
    [Fact] public async Task Correcao_de_rascunho_preserva_original_itens_e_nao_altera_assinado()
    {
        await Preparar();var doc=await Posto.EmitirAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),"exame","Texto inicial"),default);
        var r=Json(await Posto.RascunhoAsync(sessao,horario.PacienteId,"documento",doc.Id,default));
        var p=new RascunhoTablet(Guid.NewGuid(),r.GetProperty("versao").GetString()!,"Correção fictícia","Texto corrigido",null,null,null,false,null);
        var novo=await Posto.CorrigirRascunhoAsync(sessao,horario.PacienteId,"documento",doc.Id,p,default);
        Assert.Equal(novo,await Posto.CorrigirRascunhoAsync(sessao,horario.PacienteId,"documento",doc.Id,p,default));
        var antigo=await repo.ObterDocumentoAsync(doc.Id);Assert.Equal("Texto inicial",antigo!.Corpo);Assert.NotNull(antigo.CanceladoEm);
        var atual=await repo.ObterDocumentoAsync(novo.Id);Assert.Equal("Texto corrigido",atual!.Corpo);Assert.Single(atual.Itens);
        atual.AssinadoEm=DateTime.Now;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(()=>Posto.CancelarRascunhoAsync(sessao,horario.PacienteId,"documento",novo.Id,new(Guid.NewGuid(),p.Versao,"Teste"),default));
        Assert.Null(atual.CanceladoEm);
    }
    [Fact] public async Task Pendencias_respeitam_profissional_e_modelos_retornam_apenas_tipos_permitidos()
    {
        await Preparar();await Posto.EmitirAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),"receita","Rascunho fictício"),default);
        var p=Json(await Posto.PendenciasAsync(sessao,0,default));
        Assert.Single(p.GetProperty("sessoes").EnumerateArray());Assert.Single(p.GetProperty("documentos").EnumerateArray());
        Assert.DoesNotContain("Paciente restrito",p.ToString());
        await Enfermeira();p=Json(await Posto.PendenciasAsync(sessao,0,default));
        Assert.Empty(p.GetProperty("sessoes").EnumerateArray());Assert.Empty(p.GetProperty("documentos").EnumerateArray());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Posto.ModelosDocumentoAsync(sessao,default));
    }
    [Fact] public async Task Fila_de_infusoes_separa_execucao_da_enfermagem_da_avaliacao_medica()
    {
        await Preparar();
        var rascunho=await Folha(SituacaoPrescricao.Rascunho);
        var assinada=await Folha(SituacaoPrescricao.Assinada);

        var pendencias=Json(await Posto.PendenciasAsync(sessao,0,default));
        Assert.False(pendencias.TryGetProperty("infusoes",out _));
        Assert.False(pendencias.TryGetProperty("totalInfusoes",out _));

        var medico=Json(await Posto.FilaAsync(sessao,0,default)).GetProperty("itens").EnumerateArray().ToArray();
        Assert.Equal(new[]{rascunho.Id},medico.Select(x=>x.GetProperty("id").GetInt32()));
        Assert.Equal("Rascunho",medico.Single().GetProperty("situacao").GetString());

        await Enfermeira();
        var enfermagem=Json(await Posto.FilaAsync(sessao,0,default)).GetProperty("itens").EnumerateArray().ToArray();
        Assert.Equal(new[]{assinada.Id},enfermagem.Select(x=>x.GetProperty("id").GetInt32()));
        Assert.Equal("Assinada",enfermagem.Single().GetProperty("situacao").GetString());
    }
}
