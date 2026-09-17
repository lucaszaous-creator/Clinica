using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class FichaHttpTests
{
    [Fact]
    public async Task Ficha_entre_requisicoes_preserva_versoes_anexos_e_permissoes()
    {
        var pasta=Path.Combine(Path.GetTempPath(),"clinica-ficha-http-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);await File.WriteAllTextAsync(Path.Combine(pasta,"index.html"),"<!doctype html><title>Teste fictício</title>");
        await using var app=new WebApplicationFactory<Program>().WithWebHostBuilder(b=> {
            b.UseEnvironment("Development");b.UseSetting("Portal:Demo","true");b.UseSetting("ConnectionStrings:Clinica","");
            b.UseSetting("Portal:Interface",pasta);b.UseSetting("Portal:BancoDemo",Path.Combine(pasta,"demo.db"));
        });
        using var client=app.CreateClient(new(){AllowAutoRedirect=false});
        async Task<JsonNode> Get(string path)=>JsonNode.Parse(await client.GetStringAsync(path))!;
        async Task Login(string login) {
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(string?)(await Get("/api/sessao"))["csrf"]);
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/entrar",new {login,senha="TabletDemo#2026"})).StatusCode);
        }
        try {
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/posto/pendencias")).StatusCode);
            await Login("medica.demo");
            var agenda=(await Get("/api/clinico/dia"))["horarios"]!.AsArray();var paciente=(int)agenda[0]!["pacienteId"]!;
            var url=$"/api/posto/pacientes/{paciente}";
            var f=await Get(url);
            var a=new JsonObject {["idempotencia"]=Guid.NewGuid().ToString(),["versao"]=(string?)f["versaoAnamnese"],
                ["antecedentesPessoais"]="História fictícia para teste",["motivo"]="Revisão de teste"};
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(url+"/anamnese",a)).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(url+"/anamnese",a)).StatusCode);
            a["idempotencia"]=Guid.NewGuid().ToString();
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(url+"/anamnese",a)).StatusCode);
            f=await Get(url);a["versao"]=(string?)f["versaoAnamnese"];a["antecedentesPessoais"]="História fictícia revisada";
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(url+"/anamnese",a)).StatusCode);
            Assert.NotEmpty((await Get(url+"/anamnese/versoes")).AsArray());
            var bytes=new byte[1_100_000];"%PDF-1.7"u8.CopyTo(bytes);
            var anexo=new {idempotencia=Guid.NewGuid(),data=DateOnly.FromDateTime(DateTime.Today),titulo="Arquivo fictício maior que 1 MB",
                nomeArquivo="teste.pdf",tipoConteudo="application/pdf",conteudo=bytes};
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(url+"/anexos",anexo)).StatusCode);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(string?)(await Get("/api/sessao"))["csrf"]);
            var enviado=await client.PostAsJsonAsync(url+"/anexos",anexo);Assert.Equal(HttpStatusCode.OK,enviado.StatusCode);
            var id=(int)JsonNode.Parse(await enviado.Content.ReadAsStringAsync())!["id"]!;
            var conteudo=await client.GetAsync(url+$"/anexos/{id}/conteudo");Assert.Equal(bytes,await conteudo.Content.ReadAsByteArrayAsync());
            Assert.Contains("no-store",conteudo.Headers.CacheControl!.ToString());
            var emitido=await client.PostAsJsonAsync(url+"/documentos",new {idempotencia=Guid.NewGuid(),tipo="receita",texto="Receita fictícia"});
            Assert.Equal(HttpStatusCode.OK,emitido.StatusCode);var doc=(int)JsonNode.Parse(await emitido.Content.ReadAsStringAsync())!["id"]!;
            var r=await Get(url+$"/rascunhos/documento/{doc}");
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(url+$"/rascunhos/documento/{doc}",new {idempotencia=Guid.NewGuid(),versao=(string?)r["versao"],motivo="Correção de teste",corpo="Texto revisto"})).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync(url+$"/rascunhos/documento/{doc}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/posto/modelos-documento")).StatusCode);
            await Login("enfermagem.demo");
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync(url+"/anamnese",a)).StatusCode);
            var pendencias=await Get("/api/posto/pendencias");Assert.Empty(pendencias["documentos"]!.AsArray());
            Assert.True((bool)(await Get(url))["podeAnexar"]!);
        } finally { await app.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(pasta,true); }
    }
}
