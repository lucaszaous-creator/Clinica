using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class EnfermagemEdicaoHttpTests
{
    private sealed class Relogio : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026,9,23,16,0,0,TimeSpan.Zero); }

    [Fact]
    public async Task Edicao_exclusiva_preserva_texto_validacoes_e_reenvio_sem_duplicar()
    {
        var dir=Path.Combine(Path.GetTempPath(),"enfermagem-http-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir,"index.html"),"<!doctype html><title>Teste fictício</title>");
        await using var app=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>{
            b.UseEnvironment("Development");b.UseSetting("Portal:Demo","true");b.UseSetting("ConnectionStrings:Clinica","");
            b.UseSetting("Portal:Interface",dir);b.UseSetting("Portal:BancoDemo",Path.Combine(dir,"demo.db"));
            b.ConfigureServices(s=>{s.RemoveAll<TimeProvider>();s.AddSingleton<TimeProvider>(new Relogio());});
        });
        using var a=app.CreateClient();using var b=app.CreateClient();
        async Task Login(HttpClient c,string login){
            var s=JsonNode.Parse(await c.GetStringAsync("/api/sessao"))!;
            c.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(string?)s["csrf"]);
            Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync("/api/entrar",new {login,senha="TabletDemo#2026"})).StatusCode);
        }
        try{
            await Login(a,"enfermagem.demo");
            int paciente,agendamento;
            using(var scope=app.Services.CreateScope()){
                var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
                var agenda=await db.Agendamentos.FirstAsync(x=>x.ModalidadePrevista==ModalidadeAtendimento.BsvApenas);
                paciente=agenda.PacienteId;agendamento=agenda.Id;
                var p=new Profissional{Nome="Enfermeira B fictícia",RegistroConselho="COREN FICTICIO",Ativo=true};db.Profissionais.Add(p);await db.SaveChangesAsync();
                var u=await scope.ServiceProvider.GetRequiredService<AcessoService>().CriarAsync("Enfermeira B fictícia","enfermagem.b","TabletDemo#2026",PerfilAcesso.Enfermagem);
                u.ProfissionalId=p.Id;u.DeveTrocarSenha=false;await db.SaveChangesAsync();
            }
            await Login(b,"enfermagem.b");
            var root=$"/api/posto/pacientes/{paciente}/enfermagem";
            var ea=Guid.NewGuid();var eb=Guid.NewGuid();
            Assert.Equal(HttpStatusCode.OK,(await a.PostAsJsonAsync(root+"/edicao",new{agendamentoId=agendamento,editorId=ea})).StatusCode);
            var blocked=await b.PostAsJsonAsync(root+"/edicao",new{agendamentoId=agendamento,editorId=eb});
            Assert.Equal(HttpStatusCode.Conflict,blocked.StatusCode);Assert.Contains("Enfermeira — demonstração",(string?)JsonNode.Parse(await blocked.Content.ReadAsStringAsync())!["erro"]);
            Assert.Equal(HttpStatusCode.Conflict,(await a.PostAsJsonAsync(root+"/edicao",new{agendamentoId=agendamento,editorId=Guid.NewGuid()})).StatusCode);
            ObservacoesEnfermagemTablet Pedido(string fase="Chegada",SinaisVitais? sinais=null)=>new(Guid.NewGuid(),new(2026,9,23),agendamento,
                [new(new(8,0),"Evolução inteiramente fictícia",false,sinais??new(120,80,72,18,SaturacaoOxigenio:98),NegaAlergia:true,FaseAtendimento:fase)],ea);
            var invalido=await a.PostAsJsonAsync(root+"/observacoes",Pedido(sinais:new(120,80,0,18,SaturacaoOxigenio:98)));
            Assert.Equal(HttpStatusCode.BadRequest,invalido.StatusCode);
            Assert.Contains("Frequência cardíaca",(string?)JsonNode.Parse(await invalido.Content.ReadAsStringAsync())!["erro"]);
            var futuro=await a.PostAsJsonAsync(root+"/observacoes",Pedido() with{Data=new(2099,1,1)});
            Assert.Equal(HttpStatusCode.BadRequest,futuro.StatusCode);Assert.Contains("futuro",(string?)JsonNode.Parse(await futuro.Content.ReadAsStringAsync())!["erro"]);
            var pedido=Pedido();var first=await a.PostAsJsonAsync(root+"/observacoes",pedido);
            Assert.Equal(HttpStatusCode.OK,first.StatusCode);
            Assert.Equal(await first.Content.ReadAsStringAsync(),await (await a.PostAsJsonAsync(root+"/observacoes",pedido)).Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK,(await a.PostAsJsonAsync(root+"/observacoes",Pedido("AposAplicacao"))).StatusCode);
            using(var scope=app.Services.CreateScope()){
                var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
                Assert.Equal(2,await db.EvolucoesEnfermagem.CountAsync());Assert.Equal(0,await db.Codigos.CountAsync());
                (await db.Set<EdicaoEnfermagemTablet>().SingleAsync()).ExpiraEm=0;await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.OK,(await b.PostAsJsonAsync(root+"/edicao",new{agendamentoId=agendamento,editorId=eb})).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await a.PostAsJsonAsync(root+"/observacoes",Pedido())).StatusCode);
            await a.PostAsJsonAsync(root+"/edicao",new{agendamentoId=agendamento,editorId=ea,liberar=true});
            Assert.Equal(HttpStatusCode.Conflict,(await a.PostAsJsonAsync(root+"/edicao",new{agendamentoId=agendamento,editorId=ea})).StatusCode);
            await b.PostAsJsonAsync(root+"/edicao",new{agendamentoId=agendamento,editorId=eb,liberar=true});
            Assert.Equal(HttpStatusCode.OK,(await a.PostAsJsonAsync(root+"/edicao",new{agendamentoId=agendamento,editorId=ea})).StatusCode);
        }finally{await app.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
}
