using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class AcompanhamentoBsvHttpTests
{
    [Fact]
    public async Task Indicacao_autorizada_exige_csrf_e_nao_duplica_nem_altera_evolucao()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-bsv-http-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await File.WriteAllTextAsync(Path.Combine(pasta, "index.html"), "<!doctype html><title>Teste fictício</title>");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.UseSetting("Portal:Demo", "true");
            b.UseSetting("ConnectionStrings:Clinica", ""); b.UseSetting("Portal:Interface", pasta);
            b.UseSetting("Portal:BancoDemo", Path.Combine(pasta, "demo.db"));
        });
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var sessao = JsonNode.Parse(await client.GetStringAsync("/api/sessao"))!;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (string?)sessao["csrf"]);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/entrar", new { login = "medica.demo", senha = "TabletDemo#2026" })).StatusCode);
        int horario, paciente;
        string evolucoesAntes;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var medico = await db.Usuarios.SingleAsync(u => u.Login == "medica.demo");
            var responsavel = new UsuarioSistema { Login = "responsavel.bsv", Nome = "Responsável fictícia", Perfil = PerfilAcesso.Recepcao };
            db.Usuarios.Add(responsavel); await db.SaveChangesAsync();
            var ag = await db.Agendamentos.FirstAsync(a => a.ProfissionalId == medico.ProfissionalId);
            ag.ModalidadePrevista = ModalidadeAtendimento.Consulta; horario = ag.Id; paciente = ag.PacienteId;
            db.Configuracoes.AddRange(new ConfiguracaoGlobal { Chave = AcompanhamentoPacienteService.ChaveProfissional, Valor = medico.ProfissionalId.ToString()! },
                new ConfiguracaoGlobal { Chave = AcompanhamentoPacienteService.ChaveResponsavel, Valor = responsavel.Id.ToString() });
            await db.SaveChangesAsync();
            evolucoesAntes = System.Text.Json.JsonSerializer.Serialize(await db.Evolucoes.AsNoTracking().OrderBy(e => e.Id).ToListAsync());
        }
        var url = $"/api/clinico/atendimentos/{horario}/novo-bsv";
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(url, new { })).StatusCode);
        sessao = JsonNode.Parse(await client.GetStringAsync("/api/sessao"))!;
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", (string?)sessao["csrf"]);
        var retorno = await client.PostAsJsonAsync(url, new { }); Assert.Equal(HttpStatusCode.OK, retorno.StatusCode);
        Assert.True((bool)JsonNode.Parse(await retorno.Content.ReadAsStringAsync())!["indicado"]!);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(url, new { })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            Assert.Equal(1, await db.Acompanhamentos.CountAsync(c => c.PacienteId == paciente));
            Assert.Equal(1, await db.ContatosAcompanhamento.CountAsync());
            Assert.Equal(evolucoesAntes, System.Text.Json.JsonSerializer.Serialize(await db.Evolucoes.AsNoTracking().OrderBy(e => e.Id).ToListAsync()));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/clinico/atendimentos/999999/novo-bsv", new { })).StatusCode);
    }
}
