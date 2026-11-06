using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Clinica.Application.Tablet;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Clinica.Assinaturas.Tests;

/// <summary>
/// A entrada do portal SEM o código do tablet (nov/2026): senha → autenticador pessoal → sessão,
/// com aparelho confiável e código de recuperação, pela API de verdade (demonstração com o
/// segundo fator ligado).
/// </summary>
public sealed class SegundoFatorHttpTests
{
    private const string Senha = "TabletDemo#2026";

    private static WebApplicationFactory<Program> Fabrica(string pasta) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.UseSetting("Portal:Demo", "true");
            b.UseSetting("Portal:SegundoFatorNaDemo", "true");
            b.UseSetting("ConnectionStrings:Clinica", ""); b.UseSetting("Portal:Interface", pasta);
            b.UseSetting("Portal:BancoDemo", Path.Combine(pasta, "demo.db"));
            b.UseSetting("Portal:DiretorioChaves", Path.Combine(pasta, "chaves"));
        });

    private static async Task<HttpClient> Cliente(WebApplicationFactory<Program> f)
    {
        var c = f.CreateClient(new() { AllowAutoRedirect = false });
        var s = JsonNode.Parse(await c.GetStringAsync("/api/sessao"))!;
        c.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (string?)s["csrf"]);
        return c;
    }

    private static async Task<(HttpStatusCode Status, JsonNode? Json)> Post(HttpClient c, string rota, object corpo)
    {
        var r = await c.PostAsJsonAsync(rota, corpo);
        var texto = await r.Content.ReadAsStringAsync();
        return (r.StatusCode, string.IsNullOrWhiteSpace(texto) ? null : JsonNode.Parse(texto));
    }

    private static byte[] DeBase32(string chave)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0; var buffer = 0; var saida = new List<byte>();
        foreach (var ch in chave.Replace(" ", ""))
        {
            buffer = (buffer << 5) | alfabeto.IndexOf(ch); bits += 5;
            if (bits >= 8) { saida.Add((byte)(buffer >> (bits - 8))); bits -= 8; }
        }
        return saida.ToArray();
    }

    // O passo é FIXADO no começo do teste: recalcular a cada chamada fazia o "mesmo código"
    // virar o código novo quando a virada dos 30 s caía no meio da suíte (teste instável).
    private readonly long _passo = Totp.Passo(DateTimeOffset.UtcNow);

    private string CodigoAgora(byte[] segredo, int deslocamento = 0)
        => Totp.Codigo(segredo, _passo + deslocamento);

    [Fact]
    public async Task Primeiro_acesso_cadastra_o_autenticador_e_os_seguintes_pedem_o_codigo()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-2fa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await File.WriteAllTextAsync(Path.Combine(pasta, "index.html"), "<!doctype html><title>Dados fictícios</title>");
        await using var f = Fabrica(pasta);
        try
        {
            var c = await Cliente(f);

            // 1) Senha certa, sem autenticador: o cadastro, e NENHUMA sessão ainda.
            var (st, j) = await Post(c, "/api/entrar", new { login = "medica.demo", senha = Senha });
            Assert.Equal(HttpStatusCode.OK, st);
            Assert.Equal("cadastro", (string?)j!["etapa"]);
            Assert.StartsWith("<svg", (string?)j["qrSvg"]);
            var segredo = DeBase32((string)j["chave"]!);
            Assert.Equal(20, segredo.Length);
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/clinico/dia")).StatusCode);

            // Código errado não cadastra.
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(c, "/api/entrar/cadastro", new { codigo = "000000", confiar = false })).Status);
            // Código certo cadastra, abre a sessão e devolve os dez códigos de recuperação, uma vez.
            (st, j) = await Post(c, "/api/entrar/cadastro", new { codigo = CodigoAgora(segredo), confiar = false });
            Assert.Equal(HttpStatusCode.OK, st);
            var recuperacao = j!["recuperacao"]!.AsArray().Select(x => (string)x!).ToList();
            Assert.Equal(10, recuperacao.Count);
            Assert.Equal(10, recuperacao.Distinct().Count());
            Assert.Contains("posto", j["destinos"]!.AsArray().Select(x => (string)x!));
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/clinico/dia")).StatusCode);
            // A entrada pendente acabou: repetir o cadastro é "entrada vencida".
            Assert.Equal(HttpStatusCode.Gone, (await Post(c, "/api/entrar/cadastro", new { codigo = CodigoAgora(segredo), confiar = false })).Status);

            // 2) Sair e entrar de novo NESTE aparelho (não confiável): pede o código.
            Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/sair", null)).StatusCode);
            (st, j) = await Post(c, "/api/entrar", new { login = "medica.demo", senha = Senha });
            Assert.Equal("codigo", (string?)j!["etapa"]);
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/clinico/dia")).StatusCode);
            // O código do cadastro (mesmo passo) não vale de novo: um código entra uma vez.
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(c, "/api/entrar/codigo", new { codigo = CodigoAgora(segredo), confiar = true })).Status);
            // O do passo seguinte (relógio do celular 30 s adiantado) entra; marca o aparelho como confiável.
            (st, j) = await Post(c, "/api/entrar/codigo", new { codigo = CodigoAgora(segredo, +1), confiar = true });
            Assert.Equal(HttpStatusCode.OK, st);
            Assert.Equal("equipe", (string?)j!["modo"]);

            // 3) Aparelho confiável: a senha basta.
            Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/sair", null)).StatusCode);
            (st, j) = await Post(c, "/api/entrar", new { login = "medica.demo", senha = Senha });
            Assert.Equal("equipe", (string?)j!["modo"]);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/clinico/dia")).StatusCode);

            // 4) Sem entrada pendente, as rotas do código respondem "vencida" — e nunca abrem sessão.
            var outro = await Cliente(f);
            Assert.Equal(HttpStatusCode.Gone, (await Post(outro, "/api/entrar/codigo", new { codigo = CodigoAgora(segredo, +1), confiar = false })).Status);
            Assert.Equal(HttpStatusCode.Gone, (await Post(outro, "/api/entrar/recuperacao", new { codigo = recuperacao[0], confiar = false })).Status);

            // 5) Perdeu o celular: o código de recuperação entra uma vez (a confiança é por aparelho;
            //    na demonstração todos os clientes são o mesmo "aparelho", então a pessoa é outra).
            var g = await Cliente(f);
            (st, j) = await Post(g, "/api/entrar", new { login = "medica.demo", senha = Senha });
            Assert.Equal("equipe", (string?)j!["modo"]); // mesmo aparelho de demonstração, já confiável
        }
        finally { Directory.Delete(pasta, true); }
    }

    [Fact]
    public async Task Codigo_de_recuperacao_vale_uma_vez_e_gerente_vai_para_a_gestao()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-2fa-rec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await File.WriteAllTextAsync(Path.Combine(pasta, "index.html"), "<!doctype html><title>Dados fictícios</title>");
        await using var f = Fabrica(pasta);
        try
        {
            var c = await Cliente(f);
            var (_, j) = await Post(c, "/api/entrar", new { login = "demo", senha = Senha }); // perfil Gerente
            var segredo = DeBase32((string)j!["chave"]!);
            (_, j) = await Post(c, "/api/entrar/cadastro", new { codigo = CodigoAgora(segredo), confiar = false });
            var destinos = j!["destinos"]!.AsArray().Select(x => (string)x!).ToList();
            Assert.Equal("gestao", destinos[0]); // o gerente vai primeiro à gestão
            var recuperacao = (string)j["recuperacao"]![0]!;

            Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/sair", null)).StatusCode);
            (_, j) = await Post(c, "/api/entrar", new { login = "demo", senha = Senha });
            Assert.Equal("codigo", (string?)j!["etapa"]);
            Assert.Equal(HttpStatusCode.OK, (await Post(c, "/api/entrar/recuperacao", new { codigo = recuperacao.ToUpperInvariant(), confiar = false })).Status);

            Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/sair", null)).StatusCode);
            (_, j) = await Post(c, "/api/entrar", new { login = "demo", senha = Senha });
            Assert.Equal("codigo", (string?)j!["etapa"]);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(c, "/api/entrar/recuperacao", new { codigo = recuperacao, confiar = false })).Status);

            // A sessão informa os destinos ao recarregar a página.
            Assert.Equal(HttpStatusCode.OK, (await Post(c, "/api/entrar/codigo", new { codigo = CodigoAgora(segredo, +1), confiar = false })).Status);
            var s = JsonNode.Parse(await c.GetStringAsync("/api/sessao"))!;
            Assert.Equal("gestao", (string?)s["destinos"]![0]);
        }
        finally { Directory.Delete(pasta, true); }
    }

    [Fact]
    public async Task Senha_errada_nao_revela_etapa_nem_cria_entrada_pendente()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-2fa-err-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await File.WriteAllTextAsync(Path.Combine(pasta, "index.html"), "<!doctype html><title>Dados fictícios</title>");
        await using var f = Fabrica(pasta);
        try
        {
            var c = await Cliente(f);
            var (st, j) = await Post(c, "/api/entrar", new { login = "medica.demo", senha = "errada" });
            Assert.Equal(HttpStatusCode.Unauthorized, st);
            Assert.Null(j?["etapa"]);
            Assert.Equal(HttpStatusCode.Gone, (await Post(c, "/api/entrar/codigo", new { codigo = "123456", confiar = false })).Status);
        }
        finally { Directory.Delete(pasta, true); }
    }
}
