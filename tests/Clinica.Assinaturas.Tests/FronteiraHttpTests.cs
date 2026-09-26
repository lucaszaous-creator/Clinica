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
    // UTC já está no dia seguinte; a clínica ainda está em 16/09 às 21:30.
    private sealed class RelogioNoturno : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026,9,17,0,30,0,TimeSpan.Zero);
    }
    [Fact]
    public async Task Csrf_sessao_restrita_reentrada_e_revogacao_valem_no_http()
    {
        var pasta=Path.Combine(Path.GetTempPath(),"clinica-tablet-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);await File.WriteAllTextAsync(Path.Combine(pasta,"index.html"),"<!doctype html><title>Teste fictício</title>");
        var aulas=Path.Combine(pasta,"profissional","treinamento");Directory.CreateDirectory(aulas);
        await File.WriteAllTextAsync(Path.Combine(aulas,"catalogo.json"),"[]");
        var videos=Path.Combine(aulas,"videos");Directory.CreateDirectory(videos);
        await File.WriteAllBytesAsync(Path.Combine(videos,"exemplo.mp4"),[0,0,0,0]);
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
        var contexto=(await Get("/api/sessao")).GetProperty("contexto").GetString();
        Assert.Equal(contexto,(await Get("/api/sessao")).GetProperty("contexto").GetString());
        var dia=await Get("/api/dia");
        var id=dia.GetProperty("pacientes").EnumerateArray().Single(p=>p.GetProperty("nome").GetString()=="Paciente fictício 1").GetProperty("pacienteId").GetInt32();
        var preparar=new{pacienteId=id,modelos=new[]{1,2},nascimento="1980-01-15",identidadeConferida="Documento fictício conferido"};
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/preparar",preparar)).StatusCode);
        Assert.Equal(contexto,(await Get("/api/sessao")).GetProperty("contexto").GetString());
        Assert.Equal("paciente",(await Get("/api/sessao")).GetProperty("modo").GetString());
        foreach(var path in new[]{"/api/dia","/api/pacientes?q=Paciente","/api/pacientes/2","/api/documentos/1/via"})
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(path)).StatusCode);
        var coletas=await Get("/api/coletas");Assert.Equal(2,coletas.GetArrayLength());
        Assert.Equal(JsonValueKind.Null,coletas[0].GetProperty("documento").GetProperty("identificacao").ValueKind);
        var c=coletas[0];
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/coletas/"+c.GetProperty("id").GetString()+"/assinar",
            new{idempotencia=Guid.NewGuid(),conteudoHash=c.GetProperty("conteudoHash").GetString(),respostas=new Dictionary<string,string>(),tracoPng="",confirmo=true})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/entrar",new{login="demo",senha="TabletDemo#2026"})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/dia")).StatusCode);
        Assert.NotEqual(contexto,(await Get("/api/sessao")).GetProperty("contexto").GetString());
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/coletas")).StatusCode);
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            Assert.All(await db.ColetasTablet.ToListAsync(),c=>Assert.Equal("encerrado",c.Estado));
            var u=await db.Usuarios.SingleAsync(u=>u.Login=="demo");u.Ativo=false;await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/dia")).StatusCode);
        using var resposta=await client.GetAsync("/");Assert.Contains("no-store",resposta.Headers.CacheControl!.ToString());
        Assert.Contains("noindex",string.Join("",resposta.Headers.GetValues("X-Robots-Tag")));
        Assert.Contains("frame-ancestors 'none'",string.Join("",resposta.Headers.GetValues("Content-Security-Policy")));
        Assert.Contains("media-src 'self'",string.Join("",resposta.Headers.GetValues("Content-Security-Policy")));
        using(var catalogo=await client.GetAsync("/profissional/treinamento/catalogo.json"))
        {
            Assert.Equal(HttpStatusCode.OK,catalogo.StatusCode);
            Assert.Equal("application/json",catalogo.Content.Headers.ContentType?.MediaType);
        }
        using(var video=await client.GetAsync("/profissional/treinamento/videos/exemplo.mp4"))
        {
            Assert.Equal(HttpStatusCode.OK,video.StatusCode);
            Assert.Equal("video/mp4",video.Content.Headers.ContentType?.MediaType);
        }
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/profissional/treinamento/videos/exemplo.env")).StatusCode);
        // Pasta única com somente dados fictícios. A fábrica fecha o SQLite antes da limpeza.
        await factory.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(pasta,true);
    }

    [Fact]
    public async Task Demonstracao_usa_o_mesmo_dia_brasileiro_para_coleta_e_consultorio()
    {
        var pasta=Path.Combine(Path.GetTempPath(),"clinica-demo-noturna-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(b=> {
            b.UseEnvironment("Development");b.UseSetting("Portal:Demo","true");
            b.UseSetting("ConnectionStrings:Clinica","");b.UseSetting("Portal:Interface",pasta);
            b.UseSetting("Portal:BancoDemo",Path.Combine(pasta,"demo.db"));
            b.ConfigureServices(s=>s.AddSingleton<TimeProvider>(new RelogioNoturno()));
        });
        try {
            // Inicializa o seed sem autenticar: cookies usam o relógio do cliente real.
            using var client=factory.CreateClient();
            using var scope=factory.Services.CreateScope();
            var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var horarios=await db.Agendamentos.Select(a=>a.DataHora).ToListAsync();
            Assert.Equal(5,horarios.Count);
            Assert.All(horarios,h=>Assert.Equal(new DateOnly(2026,9,16),DateOnly.FromDateTime(h)));
        } finally {
            await factory.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(pasta,true);
        }
    }
}
