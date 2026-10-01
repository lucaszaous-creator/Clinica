using System.Formats.Asn1;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using Clinica.Application.Assinatura;
using Clinica.Assinaturas.Api;
using Clinica.Infrastructure;
using Clinica.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class A1Tests
{
    // Certificado fictício, nunca instalado em repositório confiável nem usado fora dos testes.
    private static byte[] Arquivo(string cpf = "52998224725", bool vencido = false, bool politica = true, string senha = "senha-ficticia")
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest("CN=Profissional ficticio", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var san = new AsnWriter(AsnEncodingRules.DER);
        using (san.PushSequence())
        using (san.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
        {
            san.WriteObjectIdentifier(CertificadoIcpBrasil.OidPessoaFisica);
            using (san.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0))) san.WriteOctetString(Encoding.ASCII.GetBytes("01011980" + cpf));
        }
        pedido.CertificateExtensions.Add(new X509Extension("2.5.29.17", san.Encode(), false));
        if (politica)
        {
            var oid = new AsnWriter(AsnEncodingRules.DER);
            using (oid.PushSequence()) using (oid.PushSequence()) oid.WriteObjectIdentifier("2.16.76.1.2.1.99");
            pedido.CertificateExtensions.Add(new X509Extension("2.5.29.32", oid.Encode(), false));
        }
        using var c = pedido.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(vencido ? -1 : 10));
        return c.Export(X509ContentType.Pfx, senha);
    }
    [Fact]
    public void Arquivo_exige_senha_titular_validade_politica_e_confianca()
    {
        var bytes = Arquivo();
        Assert.Throws<InvalidOperationException>(() => CertificadoA1.Abrir(bytes, "incorreta", "52998224725"));
        Assert.Throws<InvalidOperationException>(() => CertificadoA1.Abrir(bytes, "senha-ficticia", "12345678909"));
        Assert.Throws<InvalidOperationException>(() => CertificadoA1.Abrir(Arquivo(vencido: true), "senha-ficticia", "52998224725"));
        Assert.Throws<InvalidOperationException>(() => CertificadoA1.Abrir(Arquivo(politica: false), "senha-ficticia", "52998224725"));
        Assert.Throws<InvalidOperationException>(() => CertificadoA1.Abrir(new byte[CertificadoA1.LimiteBytes + 1], "x", "52998224725"));
        Assert.Throws<InvalidOperationException>(() => CertificadoA1.Abrir(Arquivo(senha: ""), "x", "52998224725"));
        using var c = CertificadoA1.Abrir(bytes, "senha-ficticia", "52998224725");
        Assert.True(c.Certificado.HasPrivateKey);
        Assert.Throws<InvalidOperationException>(() => new ConfiancaCertificadoA1().Exigir(c.Certificado));
    }
    [Fact]
    public void Autorizacao_e_individual_expira_e_so_pode_ser_consumida_uma_vez()
    {
        var tempo = new Relogio(); var svc = new AutorizacoesA1Tablet(tempo);
        var a = svc.Criar("sessao1", 1, "documento", 2, "hash", Guid.NewGuid());
        Assert.Throws<Clinica.Application.Tablet.RecursoClinicoIndisponivel>(() => svc.Consumir(a.Id, "sessao2"));
        Assert.Same(a, svc.Consumir(a.Id, "sessao1"));
        Assert.Throws<Clinica.Application.Tablet.ConflitoClinicoTablet>(() => svc.Consumir(a.Id, "sessao1"));
        a = svc.Criar("sessao1", 1, "documento", 2, "hash", Guid.NewGuid());
        tempo.Agora = tempo.Agora.AddMinutes(6);
        Assert.Throws<Clinica.Application.Tablet.RecursoClinicoIndisponivel>(() => svc.Consumir(a.Id, "sessao1"));
    }
    private sealed class Relogio : TimeProvider
    { public DateTimeOffset Agora = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Agora; }
    private sealed class ConfiancaFicticia : IConfiancaCertificadoA1
    { public void Exigir(X509Certificate2 c) { } }

    private static WebApplicationFactory<Program> CriarFactory(bool habilitado = true)
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-a1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, "index.html"), "<!doctype html><title>Fictício</title>");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.UseSetting("Portal:Demo", "true");
            b.UseSetting("ConnectionStrings:Clinica", ""); b.UseSetting("Portal:Interface", pasta);
            b.UseSetting("Portal:BancoDemo", Path.Combine(pasta, "demo.db")); b.UseSetting("Portal:A1:Habilitado", habilitado.ToString());
            b.UseSetting("Portal:DiretorioChaves", Path.Combine(pasta, "chaves"));
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IConfiancaCertificadoA1>(); s.AddSingleton<IConfiancaCertificadoA1, ConfiancaFicticia>();
                s.RemoveAll<AssinaturaDigitalService>(); s.AddScoped(_ => new AssinaturaDigitalService(exigirCadeiaConfiavel: false));
            });
        });
    }

    [Theory]
    [InlineData("relatorio-assinado-anterior.pdf", "documento", 1)]
    [InlineData("duas-assinaturas-anteriores.pdf", "infusao", 2)]
    [InlineData("duas-assinaturas-anteriores.pdf", "execucao", 2)]
    public async Task Arquivo_anterior_continua_identico_acessivel_e_integro_sem_certificado_cadastrado(
        string nome, string tipo, int quantidadeAssinaturas)
    {
        // PDFs fictícios produzidos ANTES da remoção da integração. Não são regenerados pelo teste.
        var original = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", nome));
        await using var factory = CriarFactory(habilitado: false);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var sessao = JsonNode.Parse(await client.GetStringAsync("/api/sessao"))!;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (string?)sessao["csrf"]);
        var login = tipo == "execucao" ? "enfermagem.demo" : "medica.demo";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/entrar", new { login, senha = "TabletDemo#2026" })).StatusCode);
        int id, arquivoId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var usuario = await db.Usuarios.SingleAsync(u => u.Login == "medica.demo");
            var paciente = await db.Pacientes.Select(p => p.Id).FirstAsync();
            var arquivo = new ArquivoAssinado { Conteudo = original, NomeArquivo = nome };
            db.Add(arquivo); await db.SaveChangesAsync(); arquivoId = arquivo.Id;
            if (tipo == "documento")
            {
                var d = new DocumentoClinico { Numero = "HISTORICO/1", Tipo = TipoDocumentoClinico.RelatorioEvolucao,
                    PacienteId = paciente, ProfissionalId = usuario.ProfissionalId, Data = new(2026, 10, 1),
                    Corpo = "Texto do cadastro não substitui a via arquivada.", ArquivoAssinadoId = arquivo.Id,
                    AssinaturaTipo = TipoAssinatura.IcpBrasil, AssinaturaHash = AssinaturaDigitalService.Hash(original),
                    AssinadoEm = DateTime.Today, AssinanteNome = "Profissional fictícia anterior" };
                db.Add(d); await db.SaveChangesAsync(); id = d.Id;
            }
            else
            {
                var p = new PrescricaoInterna { Numero = "HISTORICO/2", PacienteId = paciente,
                    ProfissionalId = usuario.ProfissionalId, Data = new(2026, 10, 1), Situacao = SituacaoPrescricao.Encerrada,
                    AssinadaEm = DateTime.Today, EncerradaEm = DateTime.Today,
                    Assinaturas = [new() { Papel = PapelAssinatura.Prescritor, Tipo = TipoAssinatura.IcpBrasil,
                        NomeAssinante = "Profissional fictícia anterior", ArquivoId = arquivo.Id },
                        new() { Papel = PapelAssinatura.Executante, Tipo = TipoAssinatura.IcpBrasil,
                        NomeAssinante = "Enfermagem fictícia anterior", ArquivoId = arquivo.Id, ArquivoRegistroId = arquivo.Id }] };
                db.Add(p); await db.SaveChangesAsync(); id = p.Id;
            }
            Assert.False(await db.CertificadosA1.AnyAsync());
        }
        var recebido = await client.GetByteArrayAsync($"/api/clinico/atendimentos/0/{tipo}/{id}/pdf");
        Assert.Equal(original, recebido);
        using (var scope = factory.Services.CreateScope())
        {
            // Os serviços compartilhados com o desktop também entregam os bytes arquivados.
            byte[] viaDesktop;
            if (tipo == "documento")
                viaDesktop = await scope.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.DocumentosClinicosPdfService>()
                    .GerarAsync(id, await scope.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.ParametrosService>().ObterPrestadorAsync());
            else
                viaDesktop = (await scope.ServiceProvider.GetRequiredService<Clinica.Application.Servicos.AssinaturaDePrescricaoService>()
                    .FolhaAsync(id, tipo == "execucao" ? FolhaPrescricao.RegistroExecucao : FolhaPrescricao.Prescricao)).Pdf;
            Assert.Equal(original, viaDesktop);
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            Assert.Equal(original, (await db.ArquivosAssinados.FindAsync(arquivoId))!.Conteudo);
        }
        var assinaturas = new AssinaturaDigitalService(false).ConferirTodas(recebido);
        Assert.Equal(quantidadeAssinaturas, assinaturas.Count);
        Assert.All(assinaturas, a => Assert.True(a.Conferida && a.Integra));
    }

    [Fact]
    public async Task Conferencia_cobre_diluicao_formatacao_e_registros_da_execucao()
    {
        await using var factory = CriarFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        async Task<string> Conferir(string tipo, int id)
        {
            using var leitura = factory.Services.CreateScope();
            return await leitura.ServiceProvider.GetRequiredService<ConferenciaAssinaturaTablet>().Conferir(tipo, id, true, default);
        }
        var p = new PrescricaoInterna { PacienteId = await db.Pacientes.Select(x => x.Id).FirstAsync(),
            Data = DateOnly.FromDateTime(DateTime.Today), Numero = "FICTICIO/1",
            Itens = [new() { Descricao = "Item fictício", Dose = "1", Ordem = 1 }] };
        db.Add(p); await db.SaveChangesAsync();
        var falhas = new List<string>();
        async Task Mudou(string tipo, string campo, Action alterar)
        {
            var antes = await Conferir(tipo, p.Id);
            alterar(); await db.SaveChangesAsync();
            if (antes == await Conferir(tipo, p.Id)) falhas.Add(tipo + ": " + campo);
        }
        await Mudou("infusao", "diluição única", () => p.DiluicaoUnica = true);
        await Mudou("infusao", "diluente global", () => p.DiluenteGlobal = "Solução fictícia");
        await Mudou("infusao", "volume total", () => p.VolumeTotal = "250 ml");
        await Mudou("infusao", "formatação da indicação", () => p.IndicacaoFormatada = "{}");
        await Mudou("infusao", "formatação das observações", () => p.ObservacoesFormatadas = "{}");
        await Mudou("infusao", "formatação do item", () => p.Itens[0].DescricaoFormatada = "{}");
        p.OrigemEnfermagem = true; p.Situacao = SituacaoPrescricao.Encerrada; p.EncerradaEm = DateTime.Now;
        p.Itens[0].Checagens.Add(new() { Situacao = SituacaoChecagem.Realizado, ExecutanteNome = "Fictícia", HoraRealizacao = new(10, 0) });
        await db.SaveChangesAsync();
        await Mudou("execucao", "identidade da checagem", () => p.Itens[0].Checagens[0].ExecutanteConselho = "COREN TESTE");
        await Mudou("execucao", "data de registro da checagem", () => p.Itens[0].Checagens[0].RegistradoEm = DateTime.Today.AddDays(-1));
        await Mudou("execucao", "observações", () => p.Observacoes = "Observação fictícia posterior");
        await Mudou("execucao", "evolução", () => p.EvolucoesEnfermagem.Add(new() { PacienteId = p.PacienteId, Data = p.Data, Hora = new(10, 0), Texto = "Registro fictício", AutorNome = "Fictícia" }));
        await Mudou("execucao", "intercorrência", () => p.EvolucoesEnfermagem[0].Intercorrencia = true);
        await Mudou("execucao", "sinais vitais", () => p.EvolucoesEnfermagem[0].Dor = 5);
        await Mudou("execucao", "consulta de enfermagem", () => p.EvolucoesEnfermagem[0].Diagnosticos.Add(new() { Titulo = "Diagnóstico fictício" }));
        Assert.True(falhas.Count == 0, "Campos que não invalidaram a conferência: " + string.Join(", ", falhas));
    }

    [Fact]
    public async Task Portal_guarda_cifrado_isola_usuarios_recusa_mudanca_e_assina_pdf_com_a1()
    {
        await using var factory = CriarFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        async Task<JsonNode> Get(string path) => JsonNode.Parse(await client.GetStringAsync(path))!;
        async Task Login(string nome)
        {
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (string?)(await Get("/api/sessao"))["csrf"]);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/entrar", new { login = nome, senha = "TabletDemo#2026" })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/clinico/a1")).StatusCode);
        await Login("medica.demo");
        var bytes = Arquivo(); var cadastro = new { arquivo = Convert.ToBase64String(bytes), senha = "senha-ficticia", consentiu = true };
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/clinico/a1/cadastrar", cadastro)).StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (string?)(await Get("/api/sessao"))["csrf"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/clinico/a1/cadastrar", new { cadastro.arquivo, cadastro.senha, consentiu = false })).StatusCode);
        var resposta = await client.PostAsJsonAsync("/api/clinico/a1/cadastrar", cadastro);
        Assert.True(resposta.IsSuccessStatusCode, await resposta.Content.ReadAsStringAsync());
        Assert.True((bool)(await Get("/api/clinico/a1"))["cadastrado"]!);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var salvo = await db.CertificadosA1.SingleAsync();
            Assert.False(bytes.SequenceEqual(salvo.ArquivoProtegido));
            Assert.Throws<CryptographicException>(() => new X509Certificate2(salvo.ArquivoProtegido, cadastro.senha));
            Assert.True(await db.Auditoria.AnyAsync(a => a.Acao == "CadastroCertificadoA1"));
        }
        await Login("enfermagem.demo"); Assert.False((bool)(await Get("/api/clinico/a1"))["cadastrado"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/clinico/a1/cadastrar", cadastro)).StatusCode);
        await Login("medica.demo");
        var agenda = (await Get("/api/clinico/dia"))["horarios"]!.AsArray()[0]!;
        var paciente = (int)agenda["pacienteId"]!;
        var emissao = await client.PostAsJsonAsync($"/api/posto/pacientes/{paciente}/documentos", new { idempotencia = Guid.NewGuid(), tipo = "relatorio", texto = "DEMONSTRAÇÃO - SEM VALIDADE CLÍNICA.\n\nRelatório fictício para conferir a apresentação da assinatura A1 no portal. Paciente, profissional e registros usados exclusivamente em testes.\n\nO PDF foi assinado pelo sistema com certificado autoassinado fictício, sem confiança ICP-Brasil. A identidade do profissional aparece no bloco de assinatura abaixo." });
        Assert.True(emissao.IsSuccessStatusCode, await emissao.Content.ReadAsStringAsync());
        var doc = (int)JsonNode.Parse(await emissao.Content.ReadAsStringAsync())!["id"]!;
        async Task<string> Preparar()
        {
            var r = await client.PostAsJsonAsync($"/api/clinico/atendimentos/0/documento/{doc}/a1", new { confirmouAlergia = true });
            Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
            return (string)JsonNode.Parse(await r.Content.ReadAsStringAsync())!["id"]!;
        }
        var autorizacao = await Preparar();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            (await db.DocumentosClinicos.FindAsync(doc))!.Corpo += " Alteração posterior à revisão.";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/clinico/a1/{autorizacao}/assinar", new { senha = cadastro.senha })).StatusCode);
        // Cada alteração ocorre DEPOIS da revisão e deve impedir qualquer PDF assinado.
        foreach (var alterar in new Action<DocumentoClinico>[] {
            d => d.HoraChegada = new(17, 45), d => d.HoraSaida = new(18, 0),
            d => d.PeriodoInicio = d.Data.AddDays(-1), d => d.PeriodoFim = d.Data.AddDays(1),
            d => d.CorpoFormatado = "{}", d => d.ObservacoesFormatadas = "{}",
            d => d.Itens.Add(new() { Descricao = "Item fictício" }),
            d => d.Itens[0].DescricaoFormatada = "{}", d => d.Itens[0].DetalheFormatado = "{}",
            d => d.Itens[0].Desenho = "{}", d => d.Itens[0].Ordem = 3 })
        {
            autorizacao = await Preparar();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            alterar(await db.DocumentosClinicos.Include(d => d.Itens).SingleAsync(d => d.Id == doc));
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/clinico/a1/{autorizacao}/assinar", new { senha = cadastro.senha })).StatusCode);
            Assert.Null((await db.DocumentosClinicos.FindAsync(doc))!.ArquivoAssinadoId);
        }
        autorizacao = await Preparar();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/clinico/a1/{autorizacao}/assinar", new { senha = "errada" })).StatusCode);
        autorizacao = await Preparar();
        Assert.True((await client.PostAsJsonAsync("/api/clinico/a1/cadastrar", cadastro)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/clinico/a1/{autorizacao}/assinar", new { senha = cadastro.senha })).StatusCode);
        autorizacao = await Preparar();
        resposta = await client.PostAsJsonAsync($"/api/clinico/a1/{autorizacao}/assinar", new { senha = cadastro.senha });
        Assert.True(resposta.IsSuccessStatusCode, await resposta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/clinico/a1/{autorizacao}/assinar", new { senha = cadastro.senha })).StatusCode);
        var pdf = await client.GetByteArrayAsync($"/api/clinico/atendimentos/0/documento/{doc}/pdf");
        Assert.True(new AssinaturaDigitalService(false).Conferir(pdf).Integra);
        var pastaPdf = Environment.GetEnvironmentVariable("CLINICA_DUMP_PDF");
        if (!string.IsNullOrWhiteSpace(pastaPdf) && Directory.Exists(pastaPdf))
            await File.WriteAllBytesAsync(Path.Combine(pastaPdf, "portal-a1-relatorio-teste.pdf"), pdf);
        Assert.True((await client.PostAsJsonAsync("/api/clinico/a1/remover", new { })).IsSuccessStatusCode);
        Assert.False((bool)(await Get("/api/clinico/a1"))["cadastrado"]!);
        Assert.Equal(pdf, await client.GetByteArrayAsync($"/api/clinico/atendimentos/0/documento/{doc}/pdf"));
    }
}
