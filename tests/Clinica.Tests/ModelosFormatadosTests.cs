using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed partial class AtendimentoTabletTests
{
    [Fact]
    public async Task Postgres_papel_de_modelos_cria_le_atualiza_e_substitui_itens()
    {
        if(!BancoDosTestes.NoPostgres)return;
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN IF NOT EXISTS(SELECT FROM pg_roles WHERE rolname='modelos_qa') THEN CREATE ROLE modelos_qa NOLOGIN; END IF; END $$;
            GRANT USAGE ON SCHEMA public TO modelos_qa;
            GRANT SELECT,INSERT,UPDATE ON "ModelosDocumento" TO modelos_qa;
            GRANT SELECT,INSERT,UPDATE,DELETE ON "ItensModelo" TO modelos_qa;
            GRANT SELECT,INSERT ON "Auditoria" TO modelos_qa;
            GRANT USAGE,SELECT ON ALL SEQUENCES IN SCHEMA public TO modelos_qa;
            SET ROLE modelos_qa;
            """);
        try {
            var servico=new Clinica.Application.Servicos.DocumentoClinicoService(repo,new(repo),new(repo));
            var modelo=await servico.SalvarModeloAsync(new() {Nome="Modelo de teste de permissão",Tipo=TipoDocumentoClinico.Receita,Corpo="Primeira versão",Itens=[new(){Descricao="Item inicial"}]});
            db.ChangeTracker.Clear();
            await servico.SalvarModeloAsync(new() {Id=modelo.Id,Nome=modelo.Nome,Tipo=modelo.Tipo,Corpo="Segunda versão",Itens=[new(){Descricao="Item atualizado"}]});
            db.ChangeTracker.Clear();
            var reaberto=await db.ModelosDocumento.Include(m=>m.Itens).SingleAsync();
            Assert.Equal("Segunda versão",reaberto.Corpo);Assert.Equal("Item atualizado",Assert.Single(reaberto.Itens).Descricao);
        } finally {await db.Database.ExecuteSqlRawAsync("RESET ROLE");}
    }
    [Theory]
    [InlineData(TipoDocumentoClinico.Receita)]
    [InlineData(TipoDocumentoClinico.Atestado)]
    [InlineData(TipoDocumentoClinico.PedidoExame)]
    public async Task Modelo_formatado_cria_reabre_atualiza_e_nao_duplica(TipoDocumentoClinico tipo)
    {
        await Preparar();
        var formato=TextoFormatado.Guardar([new("Texto "),new("fictício",true,true)]);
        var pedido=new NovoModeloDocumentoTablet(Guid.NewGuid(),"Modelo de teste",tipo,"Texto fictício",formato);
        var salvo=await Posto.CriarModeloAsync(sessao,horario.PacienteId,pedido,default);
        Assert.Equal(salvo,await Posto.CriarModeloAsync(sessao,horario.PacienteId,pedido,default));
        db.ChangeTracker.Clear();
        var modelo=Json(await Posto.ModelosDocumentoAsync(sessao,default)).EnumerateArray().Single();
        Assert.Equal(formato,modelo.GetProperty("corpoFormatado").GetString());
        var versao=modelo.GetProperty("versao").GetString();
        var alterado=pedido with {Idempotencia=Guid.NewGuid(),Id=salvo.Id,Versao=versao,Texto="Revisado",CorpoFormatado=TextoFormatado.Guardar([new("Revisado",false,true)])};
        await Posto.CriarModeloAsync(sessao,horario.PacienteId,alterado,default);
        db.ChangeTracker.Clear();
        var reaberto=await db.ModelosDocumento.AsNoTracking().SingleAsync();
        Assert.Equal("Revisado",reaberto.Corpo);Assert.Equal(alterado.CorpoFormatado,reaberto.CorpoFormatado);
        await Assert.ThrowsAsync<ConflitoClinicoTablet>(()=>Posto.CriarModeloAsync(sessao,horario.PacienteId,alterado with {Idempotencia=Guid.NewGuid()},default));
    }
    [Fact]
    public async Task Busca_de_modelos_inclui_o_modelo_apos_os_primeiros_duzentos()
    {
        await Preparar();db.ModelosDocumento.AddRange(Enumerable.Range(1,205).Select(i=>new ModeloDocumento {Nome=$"Modelo {i:000}",Tipo=TipoDocumentoClinico.Receita,Corpo="Texto",Ativo=true}));await db.SaveChangesAsync();
        var todos=Json(await Posto.ModelosDocumentoAsync(sessao,default));
        Assert.Equal(205,todos.GetArrayLength());Assert.Contains(todos.EnumerateArray(),m=>m.GetProperty("nome").GetString()=="Modelo 205");
    }
    [Fact]
    public async Task Modelo_de_infusao_preserva_configuracao_e_formatacao_sem_dados_do_paciente()
    {
        await Preparar();var estilo=TextoFormatado.Guardar([new("Item fictício",true)]);
        var config=new ModeloInfusao("Indicação","Cuidados",[new("Item fictício",estilo,"Dose fictícia","SF 0,9%","100 ml",ViaAdministracao.Endovenosa,"1h",true)]).Guardar();
        await Posto.CriarModeloAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),"Infusão fictícia",TipoDocumentoClinico.Receita,"Item fictício",estilo,ParaInfusao:true,ConfiguracaoInfusao:config),default);
        db.ChangeTracker.Clear();var modelo=await db.ModelosDocumento.AsNoTracking().SingleAsync();
        Assert.True(modelo.ParaInfusao);var reaberto=ModeloInfusao.Ler(modelo.ConfiguracaoInfusao);
        Assert.Equal("100 ml",reaberto.Itens[0].Volume);Assert.Equal(estilo,reaberto.Itens[0].DescricaoFormatada);Assert.True(reaberto.Itens[0].SeNecessario);
        Assert.DoesNotContain("paciente",config,StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Formato_antigo_nao_altera_texto_editado_e_espacos_nas_pontas_sao_normalizados()
    {
        var json=TextoFormatado.Guardar([new("  texto\r\n",true)]);
        Assert.True(TextoFormatado.Ler("texto",json).Single().Negrito);
        Assert.False(TextoFormatado.Ler("alterado",json).Single().Negrito);
        Assert.Equal("alterado",TextoFormatado.Ler("alterado",json).Single().Texto);
        Assert.Null(TextoFormatado.Normalizar("texto","<script>erro</script>"));
    }
    [Fact]
    public async Task Pdf_de_documento_e_infusao_embute_fontes_negrito_e_italico()
    {
        await Preparar();var estilo=TextoFormatado.Guardar([new("Regular "),new("negrito ",true),new("italico",false,true)]);
        foreach(var tipo in new[]{"receita","infusao"}) {
            var salvo=await Posto.EmitirAsync(sessao,horario.PacienteId,new(Guid.NewGuid(),tipo,"Regular negrito italico",CorpoFormatado:estilo),default);
            var bytes=tipo=="receita"?await new Clinica.Application.Servicos.DocumentosClinicosPdfService(repo).GerarAsync(salvo.Id,null)
                :await new Clinica.Application.Servicos.PrescricaoInternaPdfService(repo,new(repo)).GerarPrescricaoAsync(salvo.Id);
            using var pdf=PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(bytes),PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            var fontes=pdf.Internals.GetAllObjects().OfType<PdfSharp.Pdf.PdfDictionary>()
                .Select(d=>d.Elements.GetName("/BaseFont")).Where(n=>!string.IsNullOrEmpty(n)).ToArray();
            Assert.Contains(fontes,f=>f.Contains("Bold",StringComparison.OrdinalIgnoreCase));
            Assert.Contains(fontes,f=>f.Contains("Italic",StringComparison.OrdinalIgnoreCase));
        }
    }
}
