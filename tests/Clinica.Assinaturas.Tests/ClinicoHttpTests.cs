using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Clinica.Domain;
using Clinica.Application.Servicos;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Assinaturas.Tests;

public sealed class ClinicoHttpTests
{
    [Fact]
    public async Task Percurso_real_isola_profissionais_preserva_rascunho_e_conclui_uma_vez()
    {
        var pasta=Path.Combine(Path.GetTempPath(),"clinica-clinico-http-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        await File.WriteAllTextAsync(Path.Combine(pasta,"index.html"),"<!doctype html><title>Dados fictícios</title>");
        await using var factory=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>
        {
            b.UseEnvironment("Development");b.UseSetting("Portal:Demo","true");
            b.UseSetting("ConnectionStrings:Clinica","");b.UseSetting("Portal:Interface",pasta);
            b.UseSetting("Portal:BancoDemo",Path.Combine(pasta,"demo.db"));
        });
        using var client=factory.CreateClient(new(){AllowAutoRedirect=false});
        async Task<JsonNode> Get(string path)=>JsonNode.Parse(await client.GetStringAsync(path))!;
        async Task Login(string usuario)
        {
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(string?)(await Get("/api/sessao"))["csrf"]);
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/entrar",new {login=usuario,senha="TabletDemo#2026"})).StatusCode);
        }
        try
        {
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/clinico/dia")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/posto/infusoes")).StatusCode);
            await Login("demo");Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/clinico/dia")).StatusCode);
            await Login("medica.demo");
            var horarios=(await Get("/api/clinico/dia"))["horarios"]!.AsArray();Assert.Equal(2,horarios.Count);
            var id=(int)horarios[0]!["id"]!;var paciente=(int)horarios[0]!["pacienteId"]!;
            var ficha=await Get($"/api/posto/pacientes/{paciente}");
            Assert.Equal(paciente,(int)ficha["paciente"]!["id"]!);
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/posto/pacientes/buscar",new {busca="Marina"})).StatusCode);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(string?)(await Get("/api/sessao"))["csrf"]);
            var avulso=new {idempotencia=Guid.NewGuid(),tipo="relatorio",texto="Relatório fictício fora do atendimento"};
            var emissao=await client.PostAsJsonAsync($"/api/posto/pacientes/{paciente}/documentos",avulso);
            Assert.Equal(HttpStatusCode.OK,emissao.StatusCode);var avulsoId=(int)JsonNode.Parse(await emissao.Content.ReadAsStringAsync())!["id"]!;
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/api/clinico/atendimentos/0/documento/{avulsoId}/pdf")).StatusCode);
            await Login("enfermagem.demo");
            var capacidades=await Get("/api/clinico/acesso");Assert.True((bool)capacidades["enfermagem"]!);Assert.False((bool)capacidades["atender"]!);
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/api/posto/pacientes/{paciente}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/posto/infusoes")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync($"/api/posto/pacientes/{paciente}/documentos",avulso)).StatusCode);
            await Login("medica.demo");
            int restrito,pacienteRestrito;
            using(var scope=factory.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
                var outro=await db.Agendamentos.SingleAsync(a=>a.Profissional!=null&&a.Profissional.Nome.StartsWith("Dr. Bruno"));
                restrito=outro.Id;pacienteRestrito=outro.PacienteId;
                var bsv=await db.Agendamentos.SingleAsync(a=>a.Id==id);
                bsv.ModalidadePrevista=ModalidadeAtendimento.BsvApenas;
                bsv.ModalidadeCodigo=nameof(ModalidadeAtendimento.BsvApenas);
                await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/clinico/atendimentos/{restrito}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/clinico/atendimentos/{restrito}/materiais")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/clinico/atendimentos/{id}/materiais")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/pacientes/{pacienteRestrito}")).StatusCode);
            var p=await Get($"/api/clinico/atendimentos/{id}");Assert.Equal(12,p["silhueta"]!["formas"]!.AsArray().Count);
            Assert.True((bool)p["exigeConferenciaEnfermagem"]!);
            var evolucao=p["evolucao"]!.DeepClone();evolucao["textoEvolucao"]="Texto fictício <script>alert(1)</script>\nSegunda linha";
            var salvar=new JsonObject {["idempotencia"]=Guid.NewGuid().ToString(),["evolucao"]=evolucao,["finalizar"]=false};
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/salvar",salvar)).StatusCode);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(string?)(await Get("/api/sessao"))["csrf"]);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync($"/api/clinico/atendimentos/{restrito}/salvar",salvar)).StatusCode);
            var a=await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/salvar",salvar);Assert.Equal(HttpStatusCode.OK,a.StatusCode);
            var primeiro=await a.Content.ReadAsStringAsync();
            Assert.Equal(primeiro,await (await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/salvar",salvar)).Content.ReadAsStringAsync());
            var atualizado=await Get($"/api/clinico/atendimentos/{id}");
            Assert.Equal((string?)evolucao["textoEvolucao"],(string?)atualizado["evolucao"]!["textoEvolucao"]);
            salvar["idempotencia"]=Guid.NewGuid().ToString();
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/salvar",salvar)).StatusCode);
            var receita=await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/documentos",new {idempotencia=Guid.NewGuid(),tipo="receita",texto="Prescrição fictícia para teste"});
            Assert.Equal(HttpStatusCode.OK,receita.StatusCode);var doc=JsonNode.Parse(await receita.Content.ReadAsStringAsync())!;
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/documento/{doc["id"]}/safeid",new{confirmouAlergia=true})).StatusCode);
            var pdf=await client.GetAsync($"/api/clinico/atendimentos/{id}/documento/{doc["id"]}/pdf");
            Assert.Equal(HttpStatusCode.OK,pdf.StatusCode);Assert.Equal("application/pdf",pdf.Content.Headers.ContentType!.MediaType);
            Assert.Contains("no-store",pdf.Headers.CacheControl!.ToString());
            var semConferencia=new {idempotencia=Guid.NewGuid(),evolucao=atualizado["evolucao"],finalizar=true};
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/salvar",semConferencia)).StatusCode);
            var encerrar=new {idempotencia=Guid.NewGuid(),evolucao=atualizado["evolucao"],finalizar=true,houveEnfermagem=false};
            var encerrado=await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/salvar",encerrar);Assert.Equal(HttpStatusCode.OK,encerrado.StatusCode);
            var fim=JsonNode.Parse(await encerrado.Content.ReadAsStringAsync())!;Assert.True((bool)fim["finalizado"]!);Assert.True((int)fim["guias"]!>0);
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/salvar",encerrar)).StatusCode);
            using(var scope=factory.Services.CreateScope())
            {
                var repo=scope.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
                var ag=(await repo.ObterAgendamentoAsync(id))!;
                Assert.Null(await repo.ConferenciaConsumoAsync(ag.AtendimentoId!.Value));
                await repo.SalvarConfiguracaoAsync(PoliticaMateriaisService.Chave,
                    System.Text.Json.JsonSerializer.Serialize(new PoliticaMateriais(ModoMateriais.Equipe,ag.DataHora.AddMinutes(-1))));
                await repo.SalvarAsync();
            }
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync($"/api/clinico/atendimentos/{id}/materiais")).StatusCode);
            var materiais=new {idempotencia=Guid.NewGuid(),consumo=new {materiais=Array.Empty<object>(),semConsumo=true}};
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/materiais",materiais)).StatusCode);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",(string?)(await Get("/api/sessao"))["csrf"]);
            var materialSalvo=await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/materiais",materiais);
            Assert.Equal(HttpStatusCode.OK,materialSalvo.StatusCode);
            Assert.Equal(await materialSalvo.Content.ReadAsStringAsync(),await (await client.PostAsJsonAsync($"/api/clinico/atendimentos/{id}/materiais",materiais)).Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync($"/api/clinico/atendimentos/{restrito}/materiais",materiais)).StatusCode);
            using(var scope=factory.Services.CreateScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
                Assert.Single(await db.Set<ConferenciaConsumoProcedimento>().ToListAsync());
                var ag=await db.Agendamentos.SingleAsync(a=>a.Id==id);Assert.NotNull(ag.FimAtendimentoEm);
                Assert.Equal(ag.AtendimentoId,(await db.Evolucoes.SingleAsync(e=>e.AgendamentoId==id)).AtendimentoId);
                Assert.Single(await db.Atendimentos.Where(a=>a.PacienteId==paciente).ToListAsync());
                var u=await db.Usuarios.SingleAsync(u=>u.Login=="medica.demo");u.PermissoesNegadas=Permissao.VerProntuario;await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync($"/api/clinico/atendimentos/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/safeid/retorno?state="+new string('0',64)+"&code=ficticio")).StatusCode);
        }
        finally
        {
            await factory.DisposeAsync();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(pasta,true);
        }
    }
}
