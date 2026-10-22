using System.Formats.Asn1;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using Clinica.Application.Assinatura;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Clinica.Assinaturas.Tests;

/// <summary>
/// O documento pedido pelo WhatsApp (@SemDor fase 4, out/2026): o link abre uma vez, a sessão dele
/// só alcança o rascunho dela, e a emissão e o A1 são os do atendimento no tablet.
/// </summary>
public sealed class LinkWhatsAppHttpTests
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static DateTime AgoraNaClinica => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Brasilia).DateTime;

    private sealed class ConfiancaFicticia : IConfiancaCertificadoA1 { public void Exigir(X509Certificate2 c) { } }

    // Certificado fictício, nunca instalado em repositório confiável nem usado fora dos testes (o mesmo de A1Tests).
    private static byte[] Arquivo()
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest("CN=Profissional ficticio", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var san = new AsnWriter(AsnEncodingRules.DER);
        using (san.PushSequence())
        using (san.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
        {
            san.WriteObjectIdentifier(CertificadoIcpBrasil.OidPessoaFisica);
            using (san.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0))) san.WriteOctetString(Encoding.ASCII.GetBytes("0101198052998224725"));
        }
        pedido.CertificateExtensions.Add(new X509Extension("2.5.29.17", san.Encode(), false));
        var oid = new AsnWriter(AsnEncodingRules.DER);
        using (oid.PushSequence()) using (oid.PushSequence()) oid.WriteObjectIdentifier("2.16.76.1.2.1.99");
        pedido.CertificateExtensions.Add(new X509Extension("2.5.29.32", oid.Encode(), false));
        using var c = pedido.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(10));
        return c.Export(X509ContentType.Pfx, "senha-ficticia");
    }

    private static WebApplicationFactory<Program> CriarFactory(bool link = true)
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, "index.html"), "<!doctype html><title>Fictício</title>");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.UseSetting("Portal:Demo", "true");
            b.UseSetting("ConnectionStrings:Clinica", ""); b.UseSetting("Portal:Interface", pasta);
            b.UseSetting("Portal:BancoDemo", Path.Combine(pasta, "demo.db")); b.UseSetting("Portal:A1:Habilitado", "true");
            b.UseSetting("Portal:LinkWhatsApp:Habilitado", link.ToString());
            b.UseSetting("Portal:DiretorioChaves", Path.Combine(pasta, "chaves"));
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IConfiancaCertificadoA1>(); s.AddSingleton<IConfiancaCertificadoA1, ConfiancaFicticia>();
                s.RemoveAll<AssinaturaDigitalService>(); s.AddScoped(_ => new AssinaturaDigitalService(exigirCadeiaConfiavel: false));
            });
        });
    }

    private static async Task Csrf(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (string?)JsonNode.Parse(await client.GetStringAsync("/api/sessao"))!["csrf"]);
    }

    /// <summary>O rascunho como a ponte o grava; devolve o token que iria no link.</summary>
    private static async Task<(string Token, int Pedido)> Rascunho(WebApplicationFactory<Program> factory, string login = "medica.demo",
        DateTime? expira = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var u = await db.Usuarios.SingleAsync(x => x.Login == login);
        var paciente = await db.Agendamentos.Where(a => a.ProfissionalId == u.ProfissionalId).Select(a => a.PacienteId).FirstAsync();
        var token = DocumentoPeloWhatsAppRegras.NovoToken();
        var p = new PedidoDocumentoWhatsApp { TokenHash = ContratoTablet.Hash(token), IdMensagem = "wamid." + Guid.NewGuid(),
            UsuarioId = u.Id, PacienteId = paciente, Tipo = "atestado", DiasAfastamento = 2,
            Texto = "Atesto, para os devidos fins, que o(a) paciente acima necessita de 2 dias de afastamento.",
            CriadoEm = AgoraNaClinica, ExpiraEm = expira ?? AgoraNaClinica.AddMinutes(15) };
        db.Add(p);
        await db.SaveChangesAsync();
        return (token, p.Id);
    }

    [Fact]
    public async Task Desligado_as_rotas_do_link_nao_existem()
    {
        await using var factory = CriarFactory(link: false);
        using var client = factory.CreateClient();
        await Csrf(client);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/link/abrir", new { token = new string('A', 64) })).StatusCode);
    }

    [Fact]
    public async Task O_link_abre_uma_vez_so_e_o_vencido_nao_abre()
    {
        await using var factory = CriarFactory();
        var (token, _) = await Rascunho(factory);
        using var primeiro = factory.CreateClient();
        await Csrf(primeiro);
        Assert.Equal(HttpStatusCode.NoContent, (await primeiro.PostAsJsonAsync("/api/link/abrir", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await primeiro.GetAsync("/api/link/estado")).StatusCode);

        using var segundo = factory.CreateClient();
        await Csrf(segundo);
        var de_novo = await segundo.PostAsJsonAsync("/api/link/abrir", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, de_novo.StatusCode);
        Assert.Contains("já foi usado ou venceu", await de_novo.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await segundo.GetAsync("/api/link/estado")).StatusCode);

        var (vencido, _) = await Rascunho(factory, expira: AgoraNaClinica.AddMinutes(-1));
        Assert.Equal(HttpStatusCode.BadRequest, (await segundo.PostAsJsonAsync("/api/link/abrir", new { token = vencido })).StatusCode);
    }

    [Fact]
    public async Task A_sessao_do_link_nao_abre_as_rotas_da_equipe()
    {
        await using var factory = CriarFactory();
        var (token, _) = await Rascunho(factory);
        using var client = factory.CreateClient();
        await Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/link/abrir", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/clinico/a1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/clinico/dia")).StatusCode);
    }

    [Fact]
    public async Task Revisa_emite_uma_vez_assina_com_o_a1_e_o_pdf_sai_integro()
    {
        await using var factory = CriarFactory();
        // O A1 é cadastrado antes, pelo portal da equipe ("Meu certificado") — nunca pelo link.
        using (var equipe = factory.CreateClient())
        {
            await Csrf(equipe);
            Assert.Equal(HttpStatusCode.OK, (await equipe.PostAsJsonAsync("/api/entrar", new { login = "medica.demo", senha = "TabletDemo#2026" })).StatusCode);
            await Csrf(equipe);
            var cadastro = await equipe.PostAsJsonAsync("/api/clinico/a1/cadastrar", new { arquivo = Convert.ToBase64String(Arquivo()), senha = "senha-ficticia", consentiu = true });
            Assert.True(cadastro.IsSuccessStatusCode, await cadastro.Content.ReadAsStringAsync());
        }

        var (token, pedido) = await Rascunho(factory);
        using var client = factory.CreateClient();
        await Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/link/abrir", new { token })).StatusCode);
        var estado = JsonNode.Parse(await client.GetStringAsync("/api/link/estado"))!;
        Assert.Equal("atestado", (string?)estado["rascunho"]!["tipo"]);
        Assert.True((bool)estado["a1"]!["cadastrado"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/link/a1/preparar", new { confirmouAlergia = true })).StatusCode);

        var texto = "Atesto, para os devidos fins, que o(a) paciente acima necessita de 3 dias de afastamento. Texto fictício de teste.";
        var emitido = await client.PostAsJsonAsync("/api/link/emitir", new { texto, dias = 3 });
        Assert.True(emitido.IsSuccessStatusCode, await emitido.Content.ReadAsStringAsync());
        var doc = (int)JsonNode.Parse(await emitido.Content.ReadAsStringAsync())!["id"]!;
        var repetido = await client.PostAsJsonAsync("/api/link/emitir", new { texto, dias = 3 });
        Assert.Equal(doc, (int)JsonNode.Parse(await repetido.Content.ReadAsStringAsync())!["id"]!);

        var preparo = await client.PostAsJsonAsync("/api/link/a1/preparar", new { confirmouAlergia = true });
        Assert.True(preparo.IsSuccessStatusCode, await preparo.Content.ReadAsStringAsync());
        var autorizacao = (string)JsonNode.Parse(await preparo.Content.ReadAsStringAsync())!["id"]!;
        var assinado = await client.PostAsJsonAsync($"/api/link/a1/{autorizacao}/assinar", new { senha = "senha-ficticia" });
        Assert.True(assinado.IsSuccessStatusCode, await assinado.Content.ReadAsStringAsync());

        Assert.True(new AssinaturaDigitalService(false).Conferir(await client.GetByteArrayAsync("/api/link/pdf")).Integra);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var p = await db.PedidosDocumentoWhatsApp.AsNoTracking().SingleAsync(x => x.Id == pedido);
        Assert.Equal(doc, p.DocumentoClinicoId);
        Assert.True(p.AssinadoComA1);
        Assert.NotNull(p.ConcluidoEm);
        var d = await db.DocumentosClinicos.AsNoTracking().SingleAsync(x => x.Id == doc);
        Assert.Equal((TipoDocumentoClinico.Atestado, 3, texto), (d.Tipo, d.DiasAfastamento!.Value, d.Corpo));
        Assert.NotNull(d.AssinadoEm);
        Assert.True(await db.Auditoria.AnyAsync(a => a.Acao == "TabletLinkWhatsApp" && a.PacienteId == p.PacienteId));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/link/emitir", new { texto, dias = 3 })).StatusCode);
    }

    [Fact]
    public async Task Sem_a1_o_documento_fica_emitido_sem_assinatura_digital()
    {
        await using var factory = CriarFactory();
        var (token, pedido) = await Rascunho(factory);
        using var client = factory.CreateClient();
        await Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/link/abrir", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/link/emitir", new { texto = "Texto fictício.", dias = 0 })).StatusCode);
        Assert.True((await client.PostAsJsonAsync("/api/link/emitir", new { texto = "Texto fictício de teste.", dias = 1 })).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/link/concluir", new { })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var p = await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().PedidosDocumentoWhatsApp.AsNoTracking().SingleAsync(x => x.Id == pedido);
        Assert.False(p.AssinadoComA1);
        Assert.NotNull(p.ConcluidoEm);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/link/sair", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/link/estado")).StatusCode);
    }
}
