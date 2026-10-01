using System.Formats.Asn1;
using System.Net;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Clinica.Application.Assinatura.SafeID;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using Clinica.Application.Abstracoes;
using Clinica.Application.Assinatura;
using Clinica.Application.Servicos;
using Clinica.Assinaturas.Api;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class SafeIdAutomaticoHttpTests
{
    [Theory]
    [InlineData("sucesso")]
    [InlineData("aba-fechada")]
    [InlineData("recusado")]
    [InlineData("expirado")]
    [InlineData("cpf-diferente")]
    [InlineData("sessao-revogada")]
    [InlineData("conteudo-alterado")]
    [InlineData("falha-remota")]
    [InlineData("falha-banco")]
    [InlineData("resposta-commit-perdida")]
    public async Task Retorno_conclui_sem_segundo_clique_e_preserva_contrato(string caso)
    {
        var pasta=Path.Combine(Path.GetTempPath(),"safeid-automatico-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await File.WriteAllTextAsync(Path.Combine(pasta,"index.html"),"<!doctype html><title>Teste fictício</title>");
        using var medico=Certificado("Médica fictícia","12345678909");
        using var enfermeira=Certificado("Enfermeira fictícia","98765432100");
        var psc=new PscFicticio(caso=="cpf-diferente"?medico:enfermeira,caso);
        var relogio=new Relogio();
        await using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>
        {
            b.UseEnvironment("Development");b.UseSetting("Portal:Demo","true");
            b.UseSetting("ConnectionStrings:Clinica","");b.UseSetting("Portal:Interface",pasta);
            b.UseSetting("Portal:BancoDemo",Path.Combine(pasta,"demo.db"));
            b.ConfigureServices(s=>{
                s.AddSingleton<TimeProvider>(relogio);
                s.RemoveAll<DbContextOptions<ClinicaDbContext>>();
                s.AddDbContext<ClinicaDbContext>(o=>o.UseSqlite("Data Source="+Path.Combine(pasta,"demo.db")).AddInterceptors(new FalhaCommit(psc,caso)));
                s.AddScoped(_=>new AssinaturaDigitalService(exigirCadeiaConfiavel:false));
                s.AddHttpClient("SafeIdTablet").ConfigurePrimaryHttpMessageHandler(()=>psc);
            });
        });
        using var client=factory.CreateClient(new(){AllowAutoRedirect=false});
        async Task<JsonNode> Get(string path)=>JsonNode.Parse(await client.GetStringAsync(path))!;
        try
        {
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(string?)(await Get("/api/sessao"))["csrf"]);
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/entrar",new {login="enfermagem.demo",senha="TabletDemo#2026"})).StatusCode);
            int id=0,idOutra=0;byte[] original=[];int arquivosAntes;
            using(var scope=factory.Services.CreateScope())
            {
                var sp=scope.ServiceProvider;var db=sp.GetRequiredService<ClinicaDbContext>();
                var med=await db.Usuarios.Include(u=>u.Profissional).SingleAsync(u=>u.Login=="medica.demo");
                var enf=await db.Usuarios.Include(u=>u.Profissional).SingleAsync(u=>u.Login=="enfermagem.demo");
                med.Profissional!.Cpf="12345678909";enf.Profissional!.Cpf="98765432100";
                await db.SaveChangesAsync();
                var paciente=await db.Pacientes.FirstAsync();
                var prescricoes=sp.GetRequiredService<PrescricaoInternaService>();
                for(var indice=0;indice<(caso=="sucesso"?2:1);indice++)
                {
                var p=await prescricoes.CriarAsync(paciente.Id,med.ProfissionalId!.Value);
                await prescricoes.SalvarRascunhoAsync(p.Id,"Teste fictício",null,[new ItemPrescricaoInterna {Descricao="Item fictício",Dose="1 mL"}],exigeAssinaturaEletronicaDaExecucao:true);
                var assinatura=sp.GetRequiredService<AssinaturaDePrescricaoService>();
                await assinatura.AssinarPrescricaoAsync(p.Id,CertificadoIcpBrasil.Ler(medico));
                p=(await sp.GetRequiredService<IClinicaRepositorio>().ObterPrescricaoInternaAsync(p.Id))!;
                var checagens=sp.GetRequiredService<ChecagemPrescricaoService>();
                var executante=new IdentificacaoExecutante(enf.Id,enf.Nome,enf.Profissional.RegistroConselho);
                // Data passada: 10:37 de hoje seria uma administração futura ao rodar
                // de madrugada. A regra clínica permanece ativa durante o teste HTTP.
                await checagens.ChecarAsync(p.Itens.Single().Id,SituacaoChecagem.Realizado,new TimeOnly(10,37),executante,
                    dataRealizacao:DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));
                await checagens.EncerrarAsync(p.Id,executante);
                if(indice==0){id=p.Id;original=(await assinatura.FolhaAsync(id,FolhaPrescricao.Prescricao)).Pdf;}
                else idOutra=p.Id;
                }
                arquivosAntes=await db.ArquivosAssinados.CountAsync();
            }
            // Somente a configuração do servidor isolado de teste. Todas as chamadas
            // de provedor são interceptadas pelo handler, que rejeita destinos extras.
            var config=factory.Services.GetRequiredService<IConfiguration>();
            config["Portal:Demo"]="false";config["Portal:SafeId:Habilitado"]="true";
            config["Portal:SafeId:ConclusaoAutomatica"]="true";config["Portal:SafeId:ClientId"]="cliente-ficticio";
            config["Portal:SafeId:ClientSecret"]="segredo-ficticio";
            config["Portal:SafeId:Retorno"]="https://homologacao.clinicasemdormacae.com.br/safeid/retorno";
            var caminho=$"/api/clinico/atendimentos/0/execucao/{id}/safeid";
            var semRevisao=await client.PostAsJsonAsync(caminho,new {confirmouAlergia=false,concluirAutomaticamente=true});
            Assert.Equal(HttpStatusCode.BadRequest,semRevisao.StatusCode);
            var inicio=await client.PostAsJsonAsync(caminho,new {confirmouAlergia=true,concluirAutomaticamente=true});
            Assert.Equal(HttpStatusCode.OK,inicio.StatusCode);
            var pedido=JsonNode.Parse(await inicio.Content.ReadAsStringAsync())!;
            var operacao=Guid.Parse((string)pedido["id"]!);
            var pendencia=await Get(caminho+"/pendencia");
            Assert.Equal("aguardando",(string?)pendencia["situacao"]);Assert.True((bool)pendencia["podeRetomar"]!);
            Assert.StartsWith("PRE ",(string?)pendencia["documento"]);
            Assert.DoesNotContain("PRE PRE ",(string?)pendencia["documento"]);
            var retomada=await client.PostAsJsonAsync(caminho,new {confirmouAlergia=true,concluirAutomaticamente=true});
            Assert.Equal(HttpStatusCode.OK,retomada.StatusCode);
            Assert.Equal(pedido.ToJsonString(),JsonNode.Parse(await retomada.Content.ReadAsStringAsync())!.ToJsonString());
            Assert.Equal(1,psc.TokensAplicacao);
            if(caso=="sucesso")
            {
                var outroCaminho=$"/api/clinico/atendimentos/0/execucao/{idOutra}/safeid";
                Assert.Null((await Get(outroCaminho+"/pendencia"))["situacao"]);
                var outra=await client.PostAsJsonAsync(outroCaminho,new {confirmouAlergia=true,concluirAutomaticamente=true});
                Assert.Equal(HttpStatusCode.OK,outra.StatusCode);
                Assert.NotEqual((string?)pedido["id"],(string?)JsonNode.Parse(await outra.Content.ReadAsStringAsync())!["id"]);
                Assert.Equal("aguardando",(string?)(await Get(caminho+"/pendencia"))["situacao"]);
            }
            var url=new Uri((string)pedido["url"]!);var q=System.Web.HttpUtility.ParseQueryString(url.Query);
            Assert.Equal("pscsafeweb.safewebpss.com.br",url.Host);
            Assert.Equal(new[]{"client_id","code_challenge","code_challenge_method","lifetime","login_hint","redirect_uri","response_type","scope","state"},q.AllKeys.Order().ToArray());
            Assert.Equal("S256",q["code_challenge_method"]);Assert.Equal("98765432100",q["login_hint"]);
            Assert.Equal("code",q["response_type"]);
            Assert.Equal(EscopoSafeID.ParaAto(2).Escopo,q["scope"]);
            Assert.Equal(EscopoSafeID.ParaAto(2).DuracaoSegundos.ToString(),q["lifetime"]);
            Assert.Equal(config["Portal:SafeId:Retorno"],q["redirect_uri"]);
            psc.Desafio=q["code_challenge"]!;psc.Retorno=q["redirect_uri"]!;
            using var retorno=factory.CreateClient(new(){AllowAutoRedirect=false,HandleCookies=false});
            Assert.Equal(HttpStatusCode.NotFound,(await retorno.GetAsync("/safeid/retorno?state="+new string('F',64)+"&code=ficticio")).StatusCode);
            Assert.Equal(0,psc.Assinaturas);
            if(caso=="expirado")
            {
                relogio.Avancar(TimeSpan.FromMinutes(6));
                Assert.Null((await Get(caminho+"/pendencia"))["situacao"]);
            }
            if(caso is "sessao-revogada" or "conteudo-alterado")
            {
                using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
                if(caso=="sessao-revogada")foreach(var sessao in await db.SessoesTablet.ToListAsync())sessao.Modo="revogada";
                else (await db.ChecagensPrescricao.SingleAsync(c=>c.Item!.PrescricaoInternaId==id)).HoraRealizacao=new TimeOnly(10,42);
                await db.SaveChangesAsync();
                if(caso=="conteudo-alterado")
                    Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(caminho,new {confirmouAlergia=true,concluirAutomaticamente=true})).StatusCode);
            }
            var callback="/safeid/retorno?state="+q["state"]+(caso=="recusado"?"&error=access_denied":"&code=codigo-ficticio");
            if(caso=="aba-fechada")
            {
                using var interromper=new CancellationTokenSource();
                var requisicao=retorno.GetAsync(callback,interromper.Token);
                await psc.Chegou.Task.WaitAsync(TimeSpan.FromSeconds(25));
                interromper.Cancel();psc.Liberar.TrySetResult();
                try { await requisicao; } catch(OperationCanceledException) { }
                // Espera determinística pelo processamento, não por uma resposta do browser.
                for(var n=0;n<100;n++)
                {
                    using var scope=factory.Services.CreateScope();
                    if(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().OperacoesAssinaturaTablet.AnyAsync(x=>x.Id==operacao&&x.Situacao=="concluido"))break;
                    await Task.Delay(50);
                }
            }
            else Assert.Equal(caso=="expirado"?HttpStatusCode.NotFound:HttpStatusCode.Redirect,(await retorno.GetAsync(callback)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await retorno.GetAsync(callback)).StatusCode);
            if(caso is "falha-banco" or "resposta-commit-perdida")Assert.True(psc.FalhaCommitInjetada);
            using(var scope=factory.Services.CreateScope())
            {
                var sp=scope.ServiceProvider;var db=sp.GetRequiredService<ClinicaDbContext>();
                var op=await db.OperacoesAssinaturaTablet.SingleAsync(x=>x.Id==operacao);
                var p=(await sp.GetRequiredService<IClinicaRepositorio>().ObterPrescricaoInternaAsync(id))!;
                if(caso is "sucesso" or "aba-fechada" or "resposta-commit-perdida")
                {
                    Assert.Equal("concluido",op.Situacao);Assert.Null(op.ChaveAtiva);Assert.Equal(1,psc.Assinaturas);
                    Assert.NotNull(p.AssinaturaDaExecucao?.ArquivoId);
                    var pdf=(await sp.GetRequiredService<AssinaturaDePrescricaoService>().FolhaAsync(id,FolhaPrescricao.Prescricao)).Pdf;
                    Assert.True(pdf.AsSpan(0,original.Length).SequenceEqual(original));
                    var conferidas=sp.GetRequiredService<AssinaturaDigitalService>().ConferirTodas(pdf);
                    Assert.Equal(2,conferidas.Count);Assert.All(conferidas,c=>Assert.True(c.Integra&&c.Conferida,c.Frase));
                    Assert.Equal(arquivosAntes+1,await db.ArquivosAssinados.CountAsync());
                    var status=await Get("/api/clinico/safeid/"+operacao);
                    Assert.Equal("concluido",(string?)status["situacao"]);Assert.True((bool)status["conclusaoAutomatica"]!);
                }
                else
                {
                    Assert.Null(p.AssinaturaDaExecucao);Assert.Equal(arquivosAntes,await db.ArquivosAssinados.CountAsync());
                    Assert.Equal(caso is "falha-remota" or "falha-banco"?"verificar":caso=="expirado"?"aguardando":"falha",op.Situacao);
                    Assert.Equal(caso is "falha-remota" or "falha-banco"?1:0,psc.Assinaturas);
                    if(caso is "falha-remota" or "falha-banco")
                    {
                        var bloqueada=await Get(caminho+"/pendencia");
                        Assert.Equal("verificar",(string?)bloqueada["situacao"]);Assert.False((bool)bloqueada["podeRetomar"]!);
                        Assert.NotNull(op.ChaveAtiva);
                        // Simula perda da memória do processo: o bloqueio fica no banco.
                        var memoriaNova=new AutorizacoesSafeIdTablet(relogio);
                        var nova=memoriaNova.Criar(op.SessaoId,0,id,"execucao","hash",true,true);
                        await Assert.ThrowsAsync<Clinica.Application.Tablet.ConflitoClinicoTablet>(()=>sp.GetRequiredService<RegistroAssinaturaTablet>().CriarAsync(nova,default));
                    }
                }
            }
        }
        finally { await factory.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(pasta,true); }
    }

    private sealed class FalhaCommit(PscFicticio psc,string caso) : DbTransactionInterceptor
    {
        private bool falhou;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,TransactionEventData eventData,InterceptionResult result,CancellationToken ct=default)
        {
            if(!falhou && psc.Assinaturas>0 && caso=="falha-banco") {falhou=true;psc.FalhaCommitInjetada=true;throw new IOException("Falha fictícia antes do commit");}
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction,TransactionEndEventData eventData,CancellationToken ct=default)
        {
            if(!falhou && psc.Assinaturas>0 && caso=="resposta-commit-perdida") {falhou=true;psc.FalhaCommitInjetada=true;throw new IOException("Falha fictícia depois do commit");}
            return Task.CompletedTask;
        }
    }
    private sealed class Relogio : TimeProvider
    {
        private TimeSpan deslocamento;public override DateTimeOffset GetUtcNow()=>DateTimeOffset.UtcNow+deslocamento;
        public void Avancar(TimeSpan t)=>deslocamento+=t;
    }
    private sealed class PscFicticio(X509Certificate2 cert,string caso) : HttpMessageHandler
    {
        public string Desafio="",Retorno="";public int Assinaturas,TokensAplicacao;public bool FalhaCommitInjetada;
        public TaskCompletionSource Chegou=new(TaskCreationOptions.RunContinuationsAsynchronously),Liberar=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
        {
            Assert.Equal("pscsafeweb.safewebpss.com.br",req.RequestUri!.Host);
            var path=req.RequestUri.AbsolutePath;object resposta;
            if(path.EndsWith("client_token")){TokensAplicacao++;resposta=new {access_token="aplicacao-ficticia",expires_in=300};}
            else if(path.EndsWith("/token"))
            {
                var body=System.Web.HttpUtility.ParseQueryString(await req.Content!.ReadAsStringAsync(ct));
                Assert.Equal("codigo-ficticio",body["code"]);Assert.Equal(Retorno,body["redirect_uri"]);
                Assert.Equal("authorization_code",body["grant_type"]);
                Assert.Equal(Desafio,Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(body["code_verifier"]!))).TrimEnd('=').Replace('+','-').Replace('/','_'));
                resposta=new {access_token="assinatura-ficticia",expires_in=300};
            }
            else if(path.EndsWith("certificate-discovery"))
            {
                Assert.Equal("?from_authorization=true",req.RequestUri.Query);
                resposta=new {certificates=new[]{new {alias="ficticio",certificate=cert.ExportCertificatePem()}}};
            }
            else if(path.EndsWith("/signature"))
            {
                Interlocked.Increment(ref Assinaturas);Chegou.TrySetResult();
                if(caso=="aba-fechada")await Liberar.Task.WaitAsync(ct);
                if(caso=="falha-remota")throw new HttpRequestException("Resposta fictícia perdida após consumo");
                var body=JsonNode.Parse(await req.Content!.ReadAsStringAsync(ct))!;
                var hash=body["hashes"]![0]!;Assert.Equal("CMS",(string?)hash["signature_format"]);
                Assert.Equal("2.16.840.1.101.3.4.2.1",(string?)hash["hash_algorithm"]);
                var digest=Convert.FromBase64String((string)hash["hash"]!);Assert.Equal(32,digest.Length);
                resposta=new {signatures=new[]{new {id=(string)hash["id"]!,raw_signature=Convert.ToBase64String(Cms(digest,cert))}}};
            }
            else throw new InvalidOperationException("Endpoint externo inesperado no teste: "+path);
            return new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(resposta)};
        }
    }
    private static X509Certificate2 Certificado(string nome,string cpf)
    {
        using var rsa=RSA.Create(2048);
        var pedido=new CertificateRequest("CN="+nome+", OU=Teste, C=BR",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.NonRepudiation,true));
        var w=new AsnWriter(AsnEncodingRules.DER);var tag=new Asn1Tag(TagClass.ContextSpecific,0);
        using(w.PushSequence())using(w.PushSequence(tag)){w.WriteObjectIdentifier(CertificadoIcpBrasil.OidPessoaFisica);using(w.PushSequence(tag))w.WriteOctetString(Encoding.ASCII.GetBytes("14031978"+cpf+"12345678901"+"123456".PadLeft(15,'0')+"SSPSP "));}
        pedido.CertificateExtensions.Add(new X509Extension("2.5.29.17",w.Encode(),false));
        using var c=pedido.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1),DateTimeOffset.Now.AddYears(1));
        return new X509Certificate2(c.Export(X509ContentType.Pfx));
    }
    // PSC fictício: CMS destacado válido a partir do hash, sem receber o PDF.
    private static byte[] Cms(byte[] hash,X509Certificate2 cert)
    {
        const string data="1.2.840.113549.1.7.1",sha="2.16.840.1.101.3.4.2.1";
        var attrs=new AsnWriter(AsnEncodingRules.DER);
        using(attrs.PushSetOf()){
            using(attrs.PushSequence()){attrs.WriteObjectIdentifier("1.2.840.113549.1.9.3");using(attrs.PushSetOf())attrs.WriteObjectIdentifier(data);}
            using(attrs.PushSequence()){attrs.WriteObjectIdentifier("1.2.840.113549.1.9.4");using(attrs.PushSetOf())attrs.WriteOctetString(hash);}
        }
        var atributos=attrs.Encode();var assinatura=cert.GetRSAPrivateKey()!.SignData(atributos,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        var w=new AsnWriter(AsnEncodingRules.DER);var tag=new Asn1Tag(TagClass.ContextSpecific,0);
        void Alg(string oid){using(w.PushSequence()){w.WriteObjectIdentifier(oid);w.WriteNull();}}
        using(w.PushSequence()){
            w.WriteObjectIdentifier("1.2.840.113549.1.7.2");
            using(w.PushSequence(tag))using(w.PushSequence()){
                w.WriteInteger(1);using(w.PushSetOf())Alg(sha);using(w.PushSequence())w.WriteObjectIdentifier(data);
                using(w.PushSetOf(tag))w.WriteEncodedValue(cert.RawData);
                using(w.PushSetOf())using(w.PushSequence()){
                    w.WriteInteger(1);using(w.PushSequence()){w.WriteEncodedValue(cert.IssuerName.RawData);w.WriteIntegerUnsigned(Convert.FromHexString(cert.SerialNumber));}
                    Alg(sha);atributos[0]=0xA0;w.WriteEncodedValue(atributos);Alg("1.2.840.113549.1.1.1");w.WriteOctetString(assinatura);
                }
            }
        }
        return w.Encode();
    }
}
