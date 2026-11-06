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
    public async Task Sem_autenticador_entra_com_a_senha_e_e_convidado_a_ativar_por_dentro()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-2fa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await File.WriteAllTextAsync(Path.Combine(pasta, "index.html"), "<!doctype html><title>Dados fictícios</title>");
        await using var f = Fabrica(pasta);
        try
        {
            var c = await Cliente(f);

            // 1) Sem autenticador: entra só com a senha, e a resposta convida a ativar.
            var (st, j) = await Post(c, "/api/entrar", new { login = "medica.demo", senha = Senha });
            Assert.Equal(HttpStatusCode.OK, st);
            Assert.Equal("equipe", (string?)j!["modo"]);
            Assert.True((bool)j["convidarSegundoFator"]!);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/clinico/dia")).StatusCode);
            Assert.False((bool)JsonNode.Parse(await c.GetStringAsync("/api/sessao"))!["segundoFatorAtivo"]!);

            // 2) Ativar por dentro: sem iniciar, a ativação é "vencida"; iniciar entrega o QR.
            Assert.Equal(HttpStatusCode.Gone, (await Post(c, "/api/conta/segundo-fator/ativar", new { codigo = "123456", confiar = false })).Status);
            (st, j) = await Post(c, "/api/conta/segundo-fator/iniciar", new { });
            Assert.Equal(HttpStatusCode.OK, st);
            Assert.StartsWith("<svg", (string?)j!["qrSvg"]);
            var segredo = DeBase32((string)j["chave"]!);
            // Dois /iniciar QUASE JUNTOS (o convite e a tela disputando) devolvem a MESMA chave: a que a tela
            // mostra é a que o servidor confere. Era a causa do "não confere" com o código certo em produção.
            var corrida = await Task.WhenAll(Post(c, "/api/conta/segundo-fator/iniciar", new { }), Post(c, "/api/conta/segundo-fator/iniciar", new { }));
            Assert.All(corrida, r => Assert.Equal((string?)j["chave"], (string?)r.Json!["chave"]));
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(c, "/api/conta/segundo-fator/ativar", new { codigo = "000000", confiar = false })).Status);
            (st, j) = await Post(c, "/api/conta/segundo-fator/ativar", new { codigo = CodigoAgora(segredo), confiar = false });
            Assert.Equal(HttpStatusCode.OK, st);
            var recuperacao = j!["recuperacao"]!.AsArray().Select(x => (string)x!).ToList();
            Assert.Equal(10, recuperacao.Distinct().Count());
            Assert.True((bool)JsonNode.Parse(await c.GetStringAsync("/api/sessao"))!["segundoFatorAtivo"]!);
            Assert.Equal(HttpStatusCode.Conflict, (await Post(c, "/api/conta/segundo-fator/iniciar", new { })).Status);

            // 3) Ativado: o próximo login pede o código; o mesmo código não entra duas vezes.
            Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/sair", null)).StatusCode);
            (st, j) = await Post(c, "/api/entrar", new { login = "medica.demo", senha = Senha });
            Assert.Equal("codigo", (string?)j!["etapa"]);
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/clinico/dia")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(c, "/api/entrar/codigo", new { codigo = CodigoAgora(segredo), confiar = true })).Status);
            (st, j) = await Post(c, "/api/entrar/codigo", new { codigo = CodigoAgora(segredo, +1), confiar = true });
            Assert.Equal(HttpStatusCode.OK, st);
            Assert.False((bool)j!["convidarSegundoFator"]!);

            // 4) Aparelho confiável: a senha basta.
            Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/sair", null)).StatusCode);
            (st, j) = await Post(c, "/api/entrar", new { login = "medica.demo", senha = Senha });
            Assert.Equal("equipe", (string?)j!["modo"]);

            // 5) Sem entrada pendente, as rotas do código respondem "vencida".
            var outro = await Cliente(f);
            Assert.Equal(HttpStatusCode.Gone, (await Post(outro, "/api/entrar/codigo", new { codigo = CodigoAgora(segredo, +1), confiar = false })).Status);
            Assert.Equal(HttpStatusCode.Gone, (await Post(outro, "/api/entrar/recuperacao", new { codigo = recuperacao[0], confiar = false })).Status);
            // E ativar exige sessão.
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(outro, "/api/conta/segundo-fator/iniciar", new { })).Status);
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
            Assert.Equal("gestao", (string?)j!["destinos"]![0]); // o gerente vai primeiro à gestão
            (_, j) = await Post(c, "/api/conta/segundo-fator/iniciar", new { });
            var segredo = DeBase32((string)j!["chave"]!);
            (_, j) = await Post(c, "/api/conta/segundo-fator/ativar", new { codigo = CodigoAgora(segredo), confiar = false });
            var recuperacao = (string)j!["recuperacao"]![0]!;

            Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/sair", null)).StatusCode);
            (_, j) = await Post(c, "/api/entrar", new { login = "demo", senha = Senha });
            Assert.Equal("codigo", (string?)j!["etapa"]);
            Assert.Equal(HttpStatusCode.OK, (await Post(c, "/api/entrar/recuperacao", new { codigo = recuperacao.ToUpperInvariant(), confiar = false })).Status);

            Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/sair", null)).StatusCode);
            (_, j) = await Post(c, "/api/entrar", new { login = "demo", senha = Senha });
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(c, "/api/entrar/recuperacao", new { codigo = recuperacao, confiar = false })).Status);
            Assert.Equal(HttpStatusCode.OK, (await Post(c, "/api/entrar/codigo", new { codigo = CodigoAgora(segredo, +1), confiar = false })).Status);
            Assert.Equal("gestao", (string?)JsonNode.Parse(await c.GetStringAsync("/api/sessao"))!["destinos"]![0]);
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
