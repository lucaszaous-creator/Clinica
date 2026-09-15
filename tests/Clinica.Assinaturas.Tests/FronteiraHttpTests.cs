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

public sealed class FronteiraHttpTests
{
    [Fact]
    public async Task Csrf_sessao_restrita_reentrada_e_revogacao_valem_no_http()
    {
        var pasta=Path.Combine(Path.GetTempPath(),"clinica-tablet-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);await File.WriteAllTextAsync(Path.Combine(pasta,"index.html"),"<!doctype html><title>Teste fictício</title>");
        await using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>
        {
            b.UseEnvironment("Development");b.UseSetting("Portal:Demo","true");
            b.UseSetting("ConnectionStrings:Clinica","");b.UseSetting("Portal:Interface",pasta);
            b.UseSetting("Portal:BancoDemo",Path.Combine(pasta,"demo.db"));
        });
        using var client=factory.CreateClient(new(){AllowAutoRedirect=false});
        async Task<JsonElement> Get(string path)=>JsonDocument.Parse(await client.GetStringAsync(path)).RootElement;
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/dia")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/entrar",new{login="demo",senha="TabletDemo#2026"})).StatusCode);
        var inicio=await Get("/api/sessao");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",inicio.GetProperty("csrf").GetString());
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/entrar",new{login="demo",senha="TabletDemo#2026"})).StatusCode);
        var dia=await Get("/api/dia");var id=dia.GetProperty("pacientes")[0].GetProperty("pacienteId").GetInt32();
        var preparar=new{pacienteId=id,modelos=new[]{1,2},nascimento="1980-01-15",identidadeConferida="Documento fictício conferido"};
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/preparar",preparar)).StatusCode);
        foreach(var path in new[]{"/api/dia","/api/pacientes?q=Paciente","/api/pacientes/2","/api/documentos/1/via"})
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(path)).StatusCode);
        var coletas=await Get("/api/coletas");Assert.Equal(2,coletas.GetArrayLength());
        Assert.Equal(JsonValueKind.Null,coletas[0].GetProperty("documento").GetProperty("identificacao").ValueKind);
        var c=coletas[0];
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/coletas/"+c.GetProperty("id").GetString()+"/assinar",
            new{idempotencia=Guid.NewGuid(),conteudoHash=c.GetProperty("conteudoHash").GetString(),respostas=new Dictionary<string,string>(),tracoPng="",confirmo=true})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/entrar",new{login="demo",senha="TabletDemo#2026"})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/dia")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/coletas")).StatusCode);
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            Assert.All(await db.ColetasTablet.ToListAsync(),c=>Assert.Equal("encerrado",c.Estado));
            var u=await db.Usuarios.SingleAsync();u.Ativo=false;await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/dia")).StatusCode);
        using var resposta=await client.GetAsync("/");Assert.Contains("no-store",resposta.Headers.CacheControl!.ToString());
        Assert.Contains("noindex",string.Join("",resposta.Headers.GetValues("X-Robots-Tag")));
        Assert.Contains("frame-ancestors 'none'",string.Join("",resposta.Headers.GetValues("Content-Security-Policy")));
        // Pasta única com somente dados fictícios. A fábrica fecha o SQLite antes da limpeza.
        await factory.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(pasta,true);
    }
}
