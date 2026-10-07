using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class ColetaNaEvolucaoHttpTests
{
    [Fact]
    public async Task Token_da_coleta_preserva_cookie_da_equipe_e_nao_funciona_sem_equipe()
    {
        var pasta=Path.Combine(Path.GetTempPath(),"clinica-coleta-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(pasta,"profissional"));
        await File.WriteAllTextAsync(Path.Combine(pasta,"profissional","coleta.html"),"<!doctype html><title>Teste</title>");
        await using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>
        {
            b.UseEnvironment("Development");b.UseSetting("Portal:Demo","true");b.UseSetting("ConnectionStrings:Clinica","");
            b.UseSetting("Portal:Interface",pasta);b.UseSetting("Portal:BancoDemo",Path.Combine(pasta,"demo.db"));
        });
        using var client=factory.CreateClient(new(){AllowAutoRedirect=false});
        async Task<JsonElement> Get(string path)=>JsonDocument.Parse(await client.GetStringAsync(path)).RootElement;
        var inicio=await Get("/api/sessao");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",inicio.GetProperty("csrf").GetString());
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/entrar",new {login="enfermagem.demo",senha="TabletDemo#2026"})).StatusCode);
        var contexto=(await Get("/api/sessao")).GetProperty("contexto").GetString();
        int paciente,agendamento;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var a=await db.Agendamentos.FirstAsync(a=>a.Paciente!.Nome=="Paciente fictício 1");paciente=a.PacienteId;agendamento=a.Id;
        }
        using var resposta=await client.PostAsJsonAsync($"/api/sessoes/{agendamento}/preparar-termos",new {pacienteId=paciente,modelos=new[]{1,2}});
        Assert.Equal(HttpStatusCode.OK,resposta.StatusCode);Assert.False(resposta.Headers.Contains("Set-Cookie"));
        var token=JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString();
        Assert.Equal(contexto,(await Get("/api/sessao")).GetProperty("contexto").GetString());
        Assert.Equal("equipe",(await Get("/api/sessao")).GetProperty("modo").GetString());
        client.DefaultRequestHeaders.Add("X-Coleta-Token",token);
        Assert.Equal("paciente",(await Get("/api/coleta-sessao")).GetProperty("modo").GetString());
        Assert.Equal(2,(await Get("/api/coletas")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/retornar-equipe",new {})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/encerrar",new {recusa=false})).StatusCode);
        Assert.Equal("equipe",(await Get("/api/sessao")).GetProperty("modo").GetString());
        using var anonimo=factory.CreateClient(new(){AllowAutoRedirect=false});anonimo.DefaultRequestHeaders.Add("X-Coleta-Token",token);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonimo.GetAsync("/api/coletas")).StatusCode);
        using var pagina=await client.GetAsync("/profissional/coleta.html");
        Assert.Equal("SAMEORIGIN",pagina.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'self'",pagina.Headers.GetValues("Content-Security-Policy").Single());
        await factory.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(pasta,true);
    }
}
