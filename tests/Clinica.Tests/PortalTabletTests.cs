using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Clinica.Tests;

public sealed class PortalTabletTests : IDisposable
{
    private readonly SqliteConnection conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext db;
    private readonly ClinicaRepositorio repo;
    private readonly PortalTabletService svc;
    private readonly DocumentosClinicosPdfService pdf;
    private readonly Relogio relogio = new();
    private readonly FalharArquivo falha = new();
    private UsuarioSistema usuario = null!;
    private Paciente paciente = null!;
    public PortalTabletTests()
    {
        conn.Open();
        db=new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).AddInterceptors(falha).Options);
        db.Database.EnsureCreated();repo=new(db);pdf=new(repo);
        svc=new(db,repo,new(repo,new(repo),new(repo)),new(repo),pdf,new(repo),new([1,2]),relogio);
    }
    public void Dispose(){db.Dispose();conn.Dispose();}
    private async Task<(string Token,SessaoTablet Sessao)> Preparar(bool dois=false)
    {
        usuario=await new AcessoService(repo).CriarAsync("Enfermeira fictícia","tabletteste","TabletTeste#2026",PerfilAcesso.Enfermagem);
        paciente=new(){Nome="Paciente fictício",DataNascimento=new DateOnly(1980,1,15),Convenio=Convenio.UnimedPadrao};
        db.Pacientes.Add(paciente);
        var tcle=ModelosTermoBsv.Consentimento();tcle.Id=1;
        var bsv=ModelosTermoBsv.TermoDaSessao();bsv.Id=2;
        db.ModelosDocumento.AddRange(tcle,bsv);
        db.ExigenciasTermo.Add(new(){ModeloDocumentoId=2,Ativa=true,Modalidade=ModalidadeAtendimento.BsvApenas,SoValeNoDiaDoProcedimento=true});
        await db.SaveChangesAsync();
        var s=await svc.EntrarAsync(usuario,"tablet",null,default);
        await svc.PrepararAsync(s.Sessao,new(paciente.Id,dois?[1,2]:[1],paciente.DataNascimento.Value,"Documento com foto conferido"),default);
        return s;
    }
    private static EnviarRubrica Envio(ColetaTablet c,string alergia="Não")
    {
        var d=JsonSerializer.Deserialize<DocumentoTablet>(c.ConteudoJson,ContratoTablet.Json)!;
        return new(Guid.NewGuid(),c.ConteudoHash,d.Itens.ToDictionary(i=>i.Ordem,i=>(string?)(i.Codigo==RespostaDeclaracao.CodigoAlergiasTablet?alergia:"Sim")),alergia=="Sim"?"Relato fictício":null,Convert.ToBase64String(Png()),true);
    }

    [Fact] public async Task Dois_termos_sem_agendamento_arquivam_e_reabrem_os_mesmos_bytes()
    {
        var s=await Preparar(true);
        foreach(var c in await db.ColetasTablet.ToListAsync())
        {
            await svc.ReceberAsync(s.Sessao,c.Id,Envio(c),default);
            Assert.Null((await repo.ObterDocumentoAsync(c.DocumentoId))!.PacienteAssinadoEm);
            await svc.FinalizarAsync(c.Id,default);
            var via=await svc.AbrirViaAsync(c.DocumentoId,"tabletteste",default);
            Assert.StartsWith("%PDF",Encoding.ASCII.GetString(via));
            Assert.Equal(via,await pdf.GerarAsync(c.DocumentoId));
            var doc=(await repo.ObterDocumentoAsync(c.DocumentoId))!;
            Assert.Null(doc.AgendamentoId);Assert.Null(doc.EvolucaoId);Assert.Null(doc.ArquivoAssinadoId);
            Assert.True(AssinaturaDoPacienteService.ConteudoIntacto(doc));
            Assert.Equal("arquivado",c.Estado);
        }
        Assert.Equal(2,await db.ViasAssinadasPaciente.CountAsync());
    }

    [Theory][InlineData(null)][InlineData("")][InlineData(" ")][InlineData("Talvez")]
    public async Task Alergia_sem_resposta_valida_nao_recebe_rubrica(string? resposta)
    {
        var s=await Preparar();var c=await db.ColetasTablet.SingleAsync();var e=Envio(c);
        var d=JsonSerializer.Deserialize<DocumentoTablet>(c.ConteudoJson,ContratoTablet.Json)!;
        e.Respostas[d.Itens.Single(i=>i.Codigo==RespostaDeclaracao.CodigoAlergiasTablet).Ordem]=resposta;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.ReceberAsync(s.Sessao,c.Id,e,default));
        Assert.Equal("preparado",c.Estado);Assert.Null(c.TracoPng);
    }

    [Fact] public async Task Situacao_da_busca_e_agenda_confirma_arquivo_e_respeita_validade_diaria()
    {
        var s=await Preparar(true);
        static JsonElement Json(object valor)=>JsonSerializer.SerializeToElement(valor,ContratoTablet.Json);
        async Task<JsonElement[]> Buscar()=>Json(await svc.BuscarAsync("paciente FICTÍCIO",default))[0]
            .GetProperty("termos").EnumerateArray().ToArray();
        var iniciais=await Buscar();Assert.Equal(2,iniciais.Length);
        Assert.All(iniciais,t=>Assert.Equal("preparado",t.GetProperty("estado").GetString()));
        var coletas=await db.ColetasTablet.OrderBy(c=>c.DocumentoId).ToListAsync();
        await svc.ReceberAsync(s.Sessao,coletas[0].Id,Envio(coletas[0]),default);
        var recebida=(await Buscar()).Single(t=>t.GetProperty("modeloId").GetInt32()==1);
        Assert.Equal("recebido",recebida.GetProperty("estado").GetString());
        Assert.False(recebida.GetProperty("arquivado").GetBoolean());
        Assert.Equal(JsonValueKind.Null,recebida.GetProperty("assinadoEm").ValueKind);
        await svc.FinalizarAsync(coletas[0].Id,default);
        await svc.ReceberAsync(s.Sessao,coletas[1].Id,Envio(coletas[1]),default);
        await svc.FinalizarAsync(coletas[1].Id,default);
        Assert.All(await Buscar(),t=>{Assert.Equal("arquivado",t.GetProperty("estado").GetString());Assert.True(t.GetProperty("arquivado").GetBoolean());});
        Assert.Empty(await db.Agendamentos.ToListAsync());
        db.Agendamentos.Add(new(){PacienteId=paciente.Id,DataHora=svc.Hoje.ToDateTime(new TimeOnly(10,0))});
        await db.SaveChangesAsync();
        var dia=Json(await svc.DiaAsync(default)).GetProperty("pacientes")[0];
        Assert.Equal(paciente.Id,dia.GetProperty("pacienteId").GetInt32());
        Assert.All(dia.GetProperty("termos").EnumerateArray(),t=>Assert.Equal("arquivado",t.GetProperty("estado").GetString()));
        relogio.Adiantar(86400);
        var amanha=await Buscar();
        Assert.Equal("arquivado",amanha.Single(t=>!t.GetProperty("diario").GetBoolean()).GetProperty("estado").GetString());
        Assert.Equal("pendente",amanha.Single(t=>t.GetProperty("diario").GetBoolean()).GetProperty("estado").GetString());
    }

    [Fact] public async Task Alergia_ausente_nao_vira_nao()
    {
        var s=await Preparar();var c=await db.ColetasTablet.SingleAsync();var e=Envio(c);
        e.Respostas.Remove(e.Respostas.Keys.Max());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.ReceberAsync(s.Sessao,c.Id,e,default));
    }

    [Theory][InlineData("Sim",true)][InlineData("Não",false)]
    public async Task Alergia_explicita_e_preservada_com_alerta_correto(string resposta,bool alerta)
    {
        var s=await Preparar();var c=await db.ColetasTablet.SingleAsync();
        await svc.ReceberAsync(s.Sessao,c.Id,Envio(c,resposta),default);await svc.FinalizarAsync(c.Id,default);
        var doc=(await repo.ObterDocumentoAsync(c.DocumentoId))!;
        var item=doc.Itens.Single(i=>i.Codigo==RespostaDeclaracao.CodigoAlergiasTablet);
        Assert.Equal(resposta,item.Quantidade);Assert.Equal(alerta,RespostaDeclaracao.RequerAtencao(item));
    }

    [Fact] public async Task Reenvio_e_finalizacao_repetida_nao_duplicam()
    {
        var s=await Preparar();var c=await db.ColetasTablet.SingleAsync();var e=Envio(c);
        await svc.ReceberAsync(s.Sessao,c.Id,e,default);await svc.ReceberAsync(s.Sessao,c.Id,e,default);
        await svc.FinalizarAsync(c.Id,default);await svc.FinalizarAsync(c.Id,default);
        await svc.ReceberAsync(s.Sessao,c.Id,e,default);
        Assert.Equal(1,await db.ViasAssinadasPaciente.CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.ReceberAsync(s.Sessao,c.Id,e with{Idempotencia=Guid.NewGuid()},default));
    }

    [Fact] public async Task Recusa_e_recusa_clinica_e_nao_cancelamento()
    {
        var s=await Preparar();await svc.EncerrarAsync(s.Sessao,true,"Desejo conversar antes",default);
        var d=await db.DocumentosClinicos.SingleAsync();Assert.True(d.PacienteRecusou);Assert.Null(d.CanceladoEm);
        Assert.Null(d.PacienteAssinadoEm);Assert.Equal("encerrada",s.Sessao.Modo);
    }

    [Fact] public async Task Reentrada_encerra_abandonadas_preserva_recebida_e_libera_nova_coleta()
    {
        var s=await Preparar(true);var cs=await db.ColetasTablet.OrderBy(c=>c.DocumentoId).ToListAsync();
        await svc.ReceberAsync(s.Sessao,cs[0].Id,Envio(cs[0]),default);
        var nova=await svc.EntrarAsync(usuario,"tablet",s.Token,default);
        Assert.Equal("recebido",cs[0].Estado);Assert.Equal("encerrado",cs[1].Estado);Assert.Null(cs[1].ChaveAtiva);
        await svc.FinalizarAsync(cs[0].Id,default);
        await svc.PrepararAsync(nova.Sessao,new(paciente.Id,[2],paciente.DataNascimento!.Value,"Documento conferido"),default);
        Assert.Equal(3,await db.DocumentosClinicos.CountAsync());
    }

    [Fact] public async Task Modo_paciente_revogacao_e_dispositivo_barram_acesso()
    {
        var s=await Preparar();
        await Assert.ThrowsAsync<AcessoTabletBloqueado>(()=>svc.AutorizarAsync(s.Token,"tablet",true,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.AutorizarAsync(s.Token,"outro",false,default));
        usuario.Ativo=false;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.AutorizarAsync(s.Token,"tablet",false,default));
    }

    [Fact] public async Task Coleta_de_outra_sessao_e_conteudo_adulterado_sao_recusados()
    {
        var s=await Preparar();var c=await db.ColetasTablet.SingleAsync();
        var outra=await svc.EntrarAsync(usuario,"outro",null,default);outra.Sessao.Modo="paciente";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<AcessoTabletBloqueado>(()=>svc.ReceberAsync(outra.Sessao,c.Id,Envio(c),default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.ReceberAsync(s.Sessao,c.Id,Envio(c) with{ConteudoHash="alterado"},default));
    }

    [Fact] public async Task Expiracao_nao_apaga_submissao_recebida()
    {
        var s=await Preparar(true);var cs=await db.ColetasTablet.OrderBy(c=>c.DocumentoId).ToListAsync();
        await svc.ReceberAsync(s.Sessao,cs[0].Id,Envio(cs[0]),default);relogio.Adiantar(7200);
        await svc.ExpirarAsync(default);Assert.Equal("expirado",cs[1].Estado);
        await svc.FinalizarAsync(cs[0].Id,default);Assert.Equal("arquivado",cs[0].Estado);
    }

    [Fact] public async Task Via_imutavel_no_contexto_e_no_postgres()
    {
        var s=await Preparar();var c=await db.ColetasTablet.SingleAsync();
        await svc.ReceberAsync(s.Sessao,c.Id,Envio(c),default);await svc.FinalizarAsync(c.Id,default);
        var v=await db.ViasAssinadasPaciente.SingleAsync();v.Conteudo=[1,2,3];
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        Assert.Throws<InvalidOperationException>(()=>db.SaveChanges());db.ChangeTracker.Clear();
        if(BancoDosTestes.NoPostgres)
        {
            await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ViasAssinadasPaciente\" SET \"Sha256\"='alterado' WHERE \"DocumentoId\"={c.DocumentoId}"));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"DocumentosClinicos\" SET \"Titulo\"='alterado' WHERE \"Id\"={c.DocumentoId}"));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ViasAssinadasPaciente\" WHERE \"DocumentoId\"={c.DocumentoId}"));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ItensDocumento\" SET \"Quantidade\"='Não' WHERE \"DocumentoClinicoId\"={c.DocumentoId}"));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ItensDocumento\" WHERE \"DocumentoClinicoId\"={c.DocumentoId}"));
            await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"TracosAssinatura\" SET \"Largura\"=1 WHERE \"Id\"=(SELECT \"TracoAssinaturaId\" FROM \"DocumentosClinicos\" WHERE \"Id\"={c.DocumentoId})"));
        }
    }

    [Fact] public void Recusa_png_falso_branco_transparente_e_crc_invalido()
    {
        Assert.Throws<InvalidOperationException>(()=>TracoTablet.Validar(new byte[512]));
        Assert.Throws<InvalidOperationException>(()=>TracoTablet.Validar(Png(false)));
        var png=Png();png[^5]^=1;Assert.Throws<InvalidOperationException>(()=>TracoTablet.Validar(png));
        Assert.Equal((300,100),TracoTablet.Validar(Png()));
    }

    [Fact] public async Task Queda_ao_arquivar_reverte_conclusao_mas_preserva_submissao_e_permite_retomada()
    {
        var s=await Preparar();var c=await db.ColetasTablet.SingleAsync();var e=Envio(c);
        await svc.ReceberAsync(s.Sessao,c.Id,e,default);falha.Ativa=true;
        await Assert.ThrowsAsync<IOException>(()=>svc.FinalizarAsync(c.Id,default));
        db.ChangeTracker.Clear();var persistida=await db.ColetasTablet.SingleAsync();
        Assert.Equal("recebido",persistida.Estado);Assert.NotNull(persistida.TracoPng);
        Assert.Null((await db.DocumentosClinicos.SingleAsync()).PacienteAssinadoEm);
        Assert.Empty(await db.ViasAssinadasPaciente.ToListAsync());
        persistida.Estado="falha";persistida.Tentativas=5;await db.SaveChangesAsync();
        await svc.RetomarAsync(c.Id,"tabletteste",default);await svc.FinalizarAsync(c.Id,default);
        Assert.Equal("arquivado",persistida.Estado);Assert.Equal(e.Idempotencia,persistida.Idempotencia);
    }

    [Fact] public async Task Duas_requisicoes_com_estado_antigo_nao_sobrescrevem_a_primeira_rubrica()
    {
        var s=await Preparar();var c=await db.ColetasTablet.SingleAsync();
        using var outroDb=new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(conn).Options);
        var outroRepo=new ClinicaRepositorio(outroDb);
        var outroSvc=new PortalTabletService(outroDb,outroRepo,new(outroRepo,new(outroRepo),new(outroRepo)),new(outroRepo),new(outroRepo),new(outroRepo),new([1,2]),relogio);
        var antiga=await outroSvc.AutorizarAsync(s.Token,"tablet",false,default);
        await outroDb.ColetasTablet.SingleAsync();
        var primeiro=Envio(c);await svc.ReceberAsync(s.Sessao,c.Id,primeiro,default);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>outroSvc.ReceberAsync(antiga,c.Id,Envio(c,"Sim"),default));
        db.ChangeTracker.Clear();Assert.Equal(primeiro.Idempotencia,(await db.ColetasTablet.SingleAsync()).Idempotencia);
    }

    [Fact] public async Task Identidade_errada_e_segundo_tablet_nao_emitem_termos_duplicados()
    {
        var s=await Preparar();var outra=await svc.EntrarAsync(usuario,"outro",null,default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.PrepararAsync(outra.Sessao,new(paciente.Id,[1],new DateOnly(1981,1,15),"Documento conferido"),default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>svc.PrepararAsync(outra.Sessao,new(paciente.Id,[1],paciente.DataNascimento!.Value,"Documento conferido"),default));
        Assert.Equal(1,await db.DocumentosClinicos.CountAsync());Assert.Equal("equipe",outra.Sessao.Modo);
    }

    private sealed class FalharArquivo : SaveChangesInterceptor
    {
        public bool Ativa {get;set;}
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(Ativa && data.Context!.ChangeTracker.Entries<ViaAssinadaPaciente>().Any(e=>e.State==EntityState.Added))
            {Ativa=false;throw new IOException("Queda simulada antes do commit do arquivo");}
            return base.SavingChangesAsync(data,result,ct);
        }
    }

    internal static byte[] Png(bool traco=true)
    {
        using var output=new MemoryStream();output.Write(new byte[]{137,80,78,71,13,10,26,10});
        void Chunk(string tipo,byte[] bytes)
        {
            Span<byte> b=stackalloc byte[4];BinaryPrimitives.WriteInt32BigEndian(b,bytes.Length);output.Write(b);
            var all=Encoding.ASCII.GetBytes(tipo).Concat(bytes).ToArray();output.Write(all);uint crc=0xffffffff;
            foreach(var x in all){crc^=x;for(int n=0;n<8;n++)crc=(crc>>1)^(0xedb88320u & (uint)-(int)(crc&1));}
            BinaryPrimitives.WriteUInt32BigEndian(b,~crc);output.Write(b);
        }
        byte[] ihdr=new byte[13];BinaryPrimitives.WriteInt32BigEndian(ihdr,300);BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4),100);ihdr[8]=8;ihdr[9]=6;Chunk("IHDR",ihdr);
        using var raw=new MemoryStream();
        using(var z=new ZLibStream(raw,CompressionLevel.Optimal,true))
            for(int y=0;y<100;y++){z.WriteByte(0);for(int x=0;x<300;x++){byte color=(byte)(traco&&x>20&&x<270&&Math.Abs(y-(50+25*Math.Sin(x/17.0)))<3?20:255);z.Write(new[]{color,color,color,(byte)255});}}
        Chunk("IDAT",raw.ToArray());Chunk("IEND",[]);return output.ToArray();
    }
    private sealed class Relogio : TimeProvider
    {
        private DateTimeOffset agora=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>agora;
        public void Adiantar(int segundos)=>agora=agora.AddSeconds(segundos);
    }
}
