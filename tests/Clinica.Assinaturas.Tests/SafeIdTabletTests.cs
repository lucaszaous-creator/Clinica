using System.Formats.Asn1;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Clinica.Application.Assinatura;
using Clinica.Application.Assinatura.SafeID;
using Clinica.Application.Tablet;
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

/// <summary>
/// SafeID no portal, de volta em out/2026 ao lado do A1: o contrato HTTP de antes
/// (iniciar → retorno → consulta → concluir), só para documentos clínicos.
///
/// O PSC aqui é DE MENTIRA (um <see cref="HttpMessageHandler"/>): ele assina de verdade o
/// hash recebido com um e-CPF autoassinado, e o PDF produzido é conferido. Isso prova a
/// costura do portal, não a Safeweb — verde aqui não quer dizer que a chamada real funciona
/// (ver docs/safeid-congelado.md).
/// </summary>
public sealed class SafeIdTabletTests
{
    private const string Cpf = "52998224725"; // o da profissional fictícia do DemoTablet

    [Fact]
    public void Autorizacao_e_da_sessao_de_uso_unico_e_expira()
    {
        var tempo = new Relogio(); var svc = new AutorizacoesSafeIdTablet(tempo);
        var a = svc.Criar("sessao1", 0, 7, "documento", "hash", false);
        Assert.Throws<RecursoClinicoIndisponivel>(() => svc.Obter(a.Id, "sessao2"));
        Assert.Throws<ConflitoClinicoTablet>(() => svc.Obter(a.Id, "sessao1", consumir: true));
        Assert.Throws<ConflitoClinicoTablet>(() => svc.Criar("sessao1", 0, 8, "documento", "hash", false));
        Assert.Throws<RecursoClinicoIndisponivel>(() => svc.Receber(new string('0', 64), "codigo", null));
        Assert.Equal(a.Id, svc.Receber(a.Estado, "codigo", null));
        Assert.Throws<RecursoClinicoIndisponivel>(() => svc.Receber(a.Estado, "outro", null));
        Assert.Same(a, svc.Obter(a.Id, "sessao1", consumir: true));
        Assert.Throws<ConflitoClinicoTablet>(() => svc.Obter(a.Id, "sessao1", consumir: true));
        svc.Concluir(a, false);
        var b = svc.Criar("sessao1", 0, 7, "documento", "hash", false);
        tempo.Agora = tempo.Agora.AddMinutes(6);
        Assert.Throws<RecursoClinicoIndisponivel>(() => svc.Obter(b.Id, "sessao1"));
    }

    [Fact]
    public async Task Desligado_responde_habilitado_falso_e_recusa_iniciar()
    {
        await using var factory = CriarFactory(new PscDeMentira());
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/clinico/safeid")).StatusCode);
        await Entrar(client, "medica.demo");
        var estado = JsonNode.Parse(await client.GetStringAsync("/api/clinico/safeid"))!;
        Assert.False((bool)estado["habilitado"]!);
        Assert.Equal("documento", (string?)estado["tipos"]![0]);
        var doc = await EmitirRelatorio(client);
        var r = await client.PostAsJsonAsync($"/api/clinico/atendimentos/0/documento/{doc}/safeid", new { confirmouAlergia = true });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("ainda não foi habilitada", (string?)JsonNode.Parse(await r.Content.ReadAsStringAsync())!["erro"]);
        // A conferência de endereço da receita responde também pelo caminho antigo.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/clinico/atendimentos/0/documento/{doc}/safeid/endereco")).StatusCode);
    }

    [Theory]
    [InlineData("infusao")]
    [InlineData("execucao")]
    public async Task Infusao_e_execucao_continuam_so_com_A1(string tipo)
    {
        var psc = new PscDeMentira();
        await using var factory = CriarFactory(psc);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Entrar(client, "medica.demo");
        Habilitar(factory);
        var r = await client.PostAsJsonAsync($"/api/clinico/atendimentos/0/{tipo}/1/safeid", new { confirmouAlergia = true });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(SafeIdTabletService.SomenteA1, (string?)JsonNode.Parse(await r.Content.ReadAsStringAsync())!["erro"]);
        Assert.Empty(psc.Chamadas);
    }

    [Fact]
    public async Task Documento_vai_do_retorno_ao_pdf_assinado_e_recusa_mudanca_sem_gastar_assinatura()
    {
        var psc = new PscDeMentira();
        await using var factory = CriarFactory(psc);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await Entrar(client, "medica.demo");
        Habilitar(factory);
        Assert.True((bool)JsonNode.Parse(await client.GetStringAsync("/api/clinico/safeid"))!["habilitado"]!);
        var doc = await EmitirRelatorio(client);

        async Task<string> Autorizar()
        {
            var r = await client.PostAsJsonAsync($"/api/clinico/atendimentos/0/documento/{doc}/safeid", new { confirmouAlergia = true });
            Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
            var corpo = JsonNode.Parse(await r.Content.ReadAsStringAsync())!;
            var url = new Uri((string)corpo["url"]!);
            Assert.StartsWith(OpcoesSafeID.BaseHomologacao.AbsoluteUri + "authorize?", url.AbsoluteUri);
            var consulta = System.Web.HttpUtility.ParseQueryString(url.Query);
            Assert.Equal("cliente-ficticio", consulta["client_id"]);
            Assert.Equal("https://portal.clinicasemdormacae.com.br/safeid/retorno", consulta["redirect_uri"]);
            Assert.Equal(Cpf, consulta["login_hint"]);
            Assert.Equal("single_signature", consulta["scope"]);
            Assert.Equal("S256", consulta["code_challenge_method"]);
            Assert.DoesNotContain("segredo-ficticio", url.AbsoluteUri);
            // O navegador do tablet volta da Safeweb para o retorno, que só anota o código.
            var retorno = await client.GetAsync($"/safeid/retorno?state={consulta["state"]}&code=codigo-ficticio");
            Assert.Equal(HttpStatusCode.Redirect, retorno.StatusCode);
            var id = (string)corpo["id"]!;
            Assert.Equal("/profissional/?assinatura=" + id, retorno.Headers.Location!.OriginalString);
            var situacao = JsonNode.Parse(await client.GetStringAsync($"/api/clinico/safeid/{id}"))!;
            Assert.Equal("autorizado", (string?)situacao["situacao"]);
            Assert.Equal(doc, (int)situacao["documento"]!);
            Assert.Equal("documento", (string?)situacao["tipo"]);
            return id;
        }

        // Mudança depois da autorização: recusa ANTES de trocar o código ou pedir assinatura.
        var autorizacao = await Autorizar();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            (await db.DocumentosClinicos.FindAsync(doc))!.Corpo += " Alteração posterior à autorização.";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/clinico/safeid/{autorizacao}/concluir", null)).StatusCode);
        Assert.DoesNotContain(psc.Chamadas, c => c is "token" or "signature");

        autorizacao = await Autorizar();
        var concluir = await client.PostAsync($"/api/clinico/safeid/{autorizacao}/concluir", null);
        Assert.True(concluir.IsSuccessStatusCode, await concluir.Content.ReadAsStringAsync());
        Assert.Equal("concluido", (string?)JsonNode.Parse(await concluir.Content.ReadAsStringAsync())!["estado"]);
        Assert.Equal(1, psc.Chamadas.Count(c => c == "signature"));
        Assert.Contains("Documento clínico nº", psc.Rotulo);
        // Repetir a conclusão (tablet que perdeu a resposta) não assina de novo.
        Assert.True((await client.PostAsync($"/api/clinico/safeid/{autorizacao}/concluir", null)).IsSuccessStatusCode);
        Assert.Equal(1, psc.Chamadas.Count(c => c == "signature"));

        var pdf = await client.GetByteArrayAsync($"/api/clinico/atendimentos/0/documento/{doc}/pdf");
        var conferencia = new AssinaturaDigitalService(false).Conferir(pdf);
        Assert.True(conferencia.Integra, conferencia.Frase);
        using (var scope = factory.Services.CreateScope())
        {
            var d = await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().DocumentosClinicos.FindAsync(doc);
            Assert.True(d!.AssinadoEletronicamente);
        }
        // Documento assinado não aceita nova autorização.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/clinico/atendimentos/0/documento/{doc}/safeid", new { confirmouAlergia = true })).StatusCode);
    }

    // ---- Apoio ----

    private static WebApplicationFactory<Program> CriarFactory(PscDeMentira psc)
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-safeid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, "index.html"), "<!doctype html><title>Fictício</title>");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.UseSetting("Portal:Demo", "true");
            b.UseSetting("ConnectionStrings:Clinica", ""); b.UseSetting("Portal:Interface", pasta);
            b.UseSetting("Portal:BancoDemo", Path.Combine(pasta, "demo.db"));
            b.UseSetting("Portal:DiretorioChaves", Path.Combine(pasta, "chaves"));
            b.ConfigureServices(s =>
            {
                s.RemoveAll<AssinaturaDigitalService>(); s.AddScoped(_ => new AssinaturaDigitalService(exigirCadeiaConfiavel: false));
                s.AddHttpClient(SafeIdTabletService.NomeHttp).ConfigurePrimaryHttpMessageHandler(() => psc);
            });
        });
    }

    /// <summary>
    /// O SafeID só liga fora da demonstração; a leitura é por requisição, então basta
    /// virar a configuração depois de o banco fictício existir. Credencial fictícia.
    /// </summary>
    private static void Habilitar(WebApplicationFactory<Program> factory)
    {
        var config = factory.Services.GetRequiredService<IConfiguration>();
        config["Portal:Demo"] = "false"; config["Portal:SafeId:Habilitado"] = "true";
        config["Portal:SafeId:Homologacao"] = "true";
        config["Portal:SafeId:ClientId"] = "cliente-ficticio";
        config["Portal:SafeId:ClientSecret"] = "segredo-ficticio-nao-exibir";
        config["Portal:SafeId:Retorno"] = "https://portal.clinicasemdormacae.com.br/safeid/retorno";
    }

    private static async Task Entrar(HttpClient client, string login)
    {
        var sessao = JsonNode.Parse(await client.GetStringAsync("/api/sessao"))!;
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (string?)sessao["csrf"]);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/entrar", new { login, senha = "TabletDemo#2026" })).StatusCode);
    }

    private static async Task<int> EmitirRelatorio(HttpClient client)
    {
        var agenda = JsonNode.Parse(await client.GetStringAsync("/api/clinico/dia"))!["horarios"]!.AsArray()[0]!;
        var paciente = (int)agenda["pacienteId"]!;
        var emissao = await client.PostAsJsonAsync($"/api/posto/pacientes/{paciente}/documentos", new { idempotencia = Guid.NewGuid(), tipo = "relatorio",
            texto = "DEMONSTRAÇÃO - SEM VALIDADE CLÍNICA.\n\nRelatório fictício para conferir a assinatura SafeID no portal, com PSC simulado e certificado autoassinado." });
        Assert.True(emissao.IsSuccessStatusCode, await emissao.Content.ReadAsStringAsync());
        return (int)JsonNode.Parse(await emissao.Content.ReadAsStringAsync())!["id"]!;
    }

    private sealed class Relogio : TimeProvider
    { public DateTimeOffset Agora = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Agora; }

    /// <summary>Faz o papel da Safeweb: tokens fictícios, um e-CPF e um CMS destacado sobre o hash recebido.</summary>
    private sealed class PscDeMentira : HttpMessageHandler
    {
        private readonly X509Certificate2 certificado = CertificadoComChave();
        public List<string> Chamadas { get; } = [];
        public string Rotulo { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage requisicao, CancellationToken ct)
        {
            var caminho = requisicao.RequestUri!.AbsolutePath.Split('/')[^1];
            lock (Chamadas) Chamadas.Add(caminho);
            string json;
            switch (caminho)
            {
                case "client_token":
                case "token":
                    json = """{"access_token":"token-ficticio-de-teste","expires_in":300,"token_type":"Bearer"}""";
                    break;
                case "certificate-discovery":
                    json = JsonSerializer.Serialize(new { status = "S", certificates = new[] { new { alias = "A3:1", certificate = certificado.ExportCertificatePem() } } });
                    break;
                case "signature":
                    var pedido = JsonNode.Parse(await requisicao.Content!.ReadAsStringAsync(ct))!["hashes"]![0]!;
                    Rotulo = (string?)pedido["alias"] ?? "";
                    var cms = CmsDoHash(certificado, Convert.FromBase64String((string)pedido["hash"]!));
                    json = JsonSerializer.Serialize(new { certificate_alias = "A3:1", signatures = new[] { new { id = (string?)pedido["id"], raw_signature = Convert.ToBase64String(cms) } } });
                    break;
                default:
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    /// <summary>e-CPF fictício: CPF no SAN (OID 2.16.76.1.3.1), nunca confiável fora dos testes.</summary>
    private static X509Certificate2 CertificadoComChave()
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest("CN=Dra. Ana Martins ficticia", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        var san = new AsnWriter(AsnEncodingRules.DER);
        using (san.PushSequence())
        using (san.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
        {
            san.WriteObjectIdentifier(CertificadoIcpBrasil.OidPessoaFisica);
            using (san.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0))) san.WriteOctetString(Encoding.ASCII.GetBytes("01011980" + Cpf));
        }
        pedido.CertificateExtensions.Add(new X509Extension("2.5.29.17", san.Encode(), false));
        using var c = pedido.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(10));
        return new X509Certificate2(c.Export(X509ContentType.Pfx));
    }

    /// <summary>
    /// CMS destacado montado a partir só do HASH, como o PSC faz (o <c>SignedCms</c> do .NET
    /// exige o conteúdo): atributos assinados contentType, signingTime e messageDigest.
    /// </summary>
    private static byte[] CmsDoHash(X509Certificate2 certificado, byte[] hash)
    {
        const string sha256 = "2.16.840.1.101.3.4.2.1", dados = "1.2.840.113549.1.7.1";
        var atributos = new AsnWriter(AsnEncodingRules.DER);
        using (atributos.PushSetOf())
        {
            using (atributos.PushSequence()) { atributos.WriteObjectIdentifier("1.2.840.113549.1.9.3"); using (atributos.PushSetOf()) atributos.WriteObjectIdentifier(dados); }
            using (atributos.PushSequence()) { atributos.WriteObjectIdentifier("1.2.840.113549.1.9.5"); using (atributos.PushSetOf()) atributos.WriteUtcTime(DateTimeOffset.UtcNow); }
            using (atributos.PushSequence()) { atributos.WriteObjectIdentifier("1.2.840.113549.1.9.4"); using (atributos.PushSetOf()) atributos.WriteOctetString(hash); }
        }
        var assinados = atributos.Encode();
        using var chave = certificado.GetRSAPrivateKey()!;
        var assinatura = chave.SignData(assinados, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        assinados[0] = 0xA0; // [0] IMPLICIT no SignerInfo; a assinatura cobre a forma SET.
        var w = new AsnWriter(AsnEncodingRules.DER);
        using (w.PushSequence())
        {
            w.WriteObjectIdentifier("1.2.840.113549.1.7.2");
            using (w.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)))
            using (w.PushSequence())
            {
                w.WriteInteger(1);
                using (w.PushSetOf()) using (w.PushSequence()) w.WriteObjectIdentifier(sha256);
                using (w.PushSequence()) w.WriteObjectIdentifier(dados);
                using (w.PushSetOf(new Asn1Tag(TagClass.ContextSpecific, 0, true))) w.WriteEncodedValue(certificado.RawData);
                using (w.PushSetOf())
                using (w.PushSequence())
                {
                    w.WriteInteger(1);
                    using (w.PushSequence()) { w.WriteEncodedValue(certificado.IssuerName.RawData); w.WriteInteger(certificado.SerialNumberBytes.Span); }
                    using (w.PushSequence()) w.WriteObjectIdentifier(sha256);
                    w.WriteEncodedValue(assinados);
                    using (w.PushSequence()) { w.WriteObjectIdentifier("1.2.840.113549.1.1.1"); w.WriteNull(); }
                    w.WriteOctetString(assinatura);
                }
            }
        }
        return w.Encode();
    }
}
