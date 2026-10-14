using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class PlanejamentoEnfermagemHttpTests
{
    [Fact]
    public async Task Aviso_chega_a_recepcao_sem_conceder_edicao_e_exige_sessao_csrf_e_papel()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-planejamento-http-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await File.WriteAllTextAsync(Path.Combine(pasta, "index.html"), "<!doctype html><title>Teste fictício</title>");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.UseSetting("Portal:Demo", "true");
            b.UseSetting("ConnectionStrings:Clinica", ""); b.UseSetting("Portal:Interface", pasta);
            b.UseSetting("Portal:BancoDemo", Path.Combine(pasta, "demo.db"));
        });
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        const string url = "/api/posto/enfermagem/planejamento";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url + "/pedidos")).StatusCode);
        async Task Entrar(string login)
        {
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            var sessao = JsonNode.Parse(await client.GetStringAsync("/api/sessao"))!;
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (string?)sessao["csrf"]);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/entrar", new { login, senha = "TabletDemo#2026" })).StatusCode);
        }
        await Entrar("enfermagem.demo");
        var inicio = DateTime.SpecifyKind(DateTime.Today.AddDays(2).AddHours(8), DateTimeKind.Unspecified);
        var periodo = new PeriodoEnfermagem(inicio, inicio.AddHours(10));
        var semana = await client.PostAsJsonAsync(url + "/semana", new SemanaEnfermagem(DateOnly.FromDateTime(inicio)));
        Assert.Equal(HttpStatusCode.OK, semana.StatusCode);
        Assert.Equal(7, JsonNode.Parse(await semana.Content.ReadAsStringAsync())!.AsArray().Count);
        var resumo = await client.PostAsJsonAsync(url + "/periodo", periodo);
        Assert.Equal(HttpStatusCode.OK, resumo.StatusCode);
        var referencia = (string)JsonNode.Parse(await resumo.Content.ReadAsStringAsync())!["referencia"]!;
        var pedido = new CriarSolicitacaoEnfermagem(Guid.NewGuid(), inicio, inicio.AddHours(10), inicio,
            ModalidadeAtendimento.BsvApenas, 4, "Aviso fictício", referencia);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url + "/pedidos", pedido)).StatusCode);
        await Entrar("enfermagem.demo");
        var envio = await client.PostAsJsonAsync(url + "/pedidos", pedido);
        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
        var id = (int)JsonNode.Parse(await envio.Content.ReadAsStringAsync())!["id"]!;
        var repeticao = await client.PostAsJsonAsync(url + "/pedidos", pedido);
        Assert.Equal(HttpStatusCode.OK, repeticao.StatusCode);
        Assert.Equal(id, (int)JsonNode.Parse(await repeticao.Content.ReadAsStringAsync())!["id"]!);
        int horarios;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var enfermeira = await db.Usuarios.SingleAsync(u => u.Login == "enfermagem.demo");
            Assert.False(enfermeira.Pode(Permissao.EditarAgenda));
            var recepcao = new UsuarioSistema { Login = "recepcao.aviso", Nome = "Recepção fictícia", Perfil = PerfilAcesso.Recepcao };
            db.Usuarios.Add(recepcao); await db.SaveChangesAsync();
            var svc = scope.ServiceProvider.GetRequiredService<IPlanejamentoEnfermagemService>();
            var aviso = Assert.Single(await svc.ListarAsync(recepcao.Id));
            Assert.Equal(id, aviso.Id); Assert.Equal(4, aviso.Quantidade);
            horarios = await db.Agendamentos.CountAsync();
            await svc.AtualizarAsync(recepcao.Id, id, new(Guid.NewGuid(), aviso.Versao, "visto"));
        }
        var detalhe = JsonNode.Parse(await client.GetStringAsync(url + "/pedidos/" + id))!;
        Assert.Equal("Visto", (string?)detalhe["pedido"]!["situacao"]);
        var versao = Guid.Parse((string)detalhe["pedido"]!["versao"]!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(url + $"/pedidos/{id}/retirar",
            new AcaoSolicitacaoEnfermagem(Guid.NewGuid(), versao, "encerrar"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(url + $"/pedidos/{id}/retirar",
            new AcaoSolicitacaoEnfermagem(Guid.NewGuid(), versao, "retirar"))).StatusCode);
        await client.PostAsJsonAsync("/api/sair", new { });
        await Entrar("medica.demo");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url + "/pedidos")).StatusCode);
        using var final = factory.Services.CreateScope();
        var banco = final.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        Assert.Equal(horarios, await banco.Agendamentos.CountAsync());
        Assert.Equal(1, await banco.Set<SolicitacaoEnfermagem>().CountAsync());
        Assert.Equal("Retirado", (await banco.Set<SolicitacaoEnfermagem>().SingleAsync()).Situacao);
    }
}
