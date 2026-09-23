using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class EnderecoPrescricaoHttpTests
{
    [Fact]
    public async Task Prescritor_completa_endereco_sem_reemitir_sem_sobrescrever_e_com_auditoria()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "clinica-endereco-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await File.WriteAllTextAsync(Path.Combine(pasta,"index.html"),"<!doctype html><title>Teste fictício</title>");
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
            b.UseEnvironment("Development"); b.UseSetting("Portal:Demo","true");
            b.UseSetting("ConnectionStrings:Clinica",""); b.UseSetting("Portal:Interface",pasta);
            b.UseSetting("Portal:BancoDemo",Path.Combine(pasta,"demo.db"));
        });
        using var client = app.CreateClient(new() {AllowAutoRedirect=false});
        async Task<JsonNode> Get(string path) => JsonNode.Parse(await client.GetStringAsync(path))!;
        async Task Login(string login) {
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(string?)(await Get("/api/sessao"))["csrf"]);
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/entrar",new {login,senha="TabletDemo#2026"})).StatusCode);
        }
        try {
            await Login("medica.demo");
            var paciente = (int)(await Get("/api/clinico/dia"))["horarios"]![0]!["pacienteId"]!;
            var emissao = await client.PostAsJsonAsync($"/api/posto/pacientes/{paciente}/documentos",
                new {idempotencia=Guid.NewGuid(),tipo="receita",texto="Prescrição fictícia para teste"});
            Assert.Equal(HttpStatusCode.OK,emissao.StatusCode);
            var doc = (int)JsonNode.Parse(await emissao.Content.ReadAsStringAsync())!["id"]!;
            using (var scope=app.Services.CreateScope()) {
                var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
                (await db.Pacientes.SingleAsync(p=>p.Id==paciente)).Endereco=null;
                await db.SaveChangesAsync();
            }
            var url=$"/api/clinico/atendimentos/0/documento/{doc}/safeid/endereco";
            Assert.True((bool)(await Get(url))["precisaEndereco"]!);

            // Só habilita a leitura da configuração do serviço. A conferência falha
            // antes de qualquer chamada ao PSC; não há credencial/certificado real.
            var config=app.Services.GetRequiredService<IConfiguration>();
            config["Portal:Demo"]="false"; config["Portal:SafeId:Habilitado"]="true";
            config["Portal:SafeId:ClientId"]="ficticio-sem-credencial";
            config["Portal:SafeId:ClientSecret"]="segredo-ficticio-nao-exibir";
            config["Portal:SafeId:Retorno"]="https://portal.clinicasemdormacae.com.br/safeid/retorno";
            var inicio=await client.PostAsJsonAsync(url.Replace("/endereco",""),new {confirmouAlergia=true});
            Assert.Equal(HttpStatusCode.BadRequest,inicio.StatusCode);
            var mensagem=(string?)JsonNode.Parse(await inicio.Content.ReadAsStringAsync())!["erro"];
            Assert.Contains("endereço residencial",mensagem);
            Assert.DoesNotContain("segredo-ficticio",mensagem);
            config["Portal:Demo"]="true";

            foreach(var invalido in new[]{"", "abc", new string('x',301), "Rua\nInválida"})
                Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(url,new {endereco=invalido})).StatusCode);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(url,new {endereco="Rua fictícia, 10"})).StatusCode);
            await Login("enfermagem.demo");
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync(url,new {endereco="Rua fictícia, 10"})).StatusCode);
            await Login("medica.demo");
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(url.Replace($"/documento/{doc}/","/documento/999999/"),new {endereco="Rua fictícia, 10"})).StatusCode);
            const string endereco="Rua de Teste, 10, Centro, Macaé/RJ";
            for(var n=0;n<2;n++)
                Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync(url,new {endereco})).StatusCode);
            Assert.False((bool)(await Get(url))["precisaEndereco"]!);
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(url,new {endereco="Outro endereço, 20"})).StatusCode);
            using (var scope=app.Services.CreateScope()) {
                var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
                Assert.Equal(endereco,(await db.Pacientes.SingleAsync(p=>p.Id==paciente)).Endereco);
                var auditoria=await db.Auditoria.Where(a=>a.Acao=="TabletEnderecoPrescricao").ToListAsync();
                Assert.Single(auditoria); Assert.Equal("medica.demo",auditoria[0].Operador);
                Assert.DoesNotContain(endereco,auditoria[0].Detalhe);
                var documento=await db.DocumentosClinicos.SingleAsync(d=>d.Id==doc);
                Assert.False(documento.AssinadoEletronicamente); Assert.False(documento.Cancelado);
                Assert.Equal("Prescrição fictícia para teste",documento.Corpo);
                documento.CanceladoEm=DateTime.UtcNow; await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync(url,new {endereco})).StatusCode);
        }
        finally {
            await app.DisposeAsync(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(pasta,true);
        }
    }
}
