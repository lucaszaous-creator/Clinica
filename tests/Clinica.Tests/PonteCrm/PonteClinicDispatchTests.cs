using System.Net.Http.Json;
using Clinica.Application.Abstracoes;
using Clinica.Application.Email;
using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using Xunit;

namespace Clinica.Tests;

// A fila de envios ao paciente pela PONTE (out/2026): as quatro rotas que o
// ClinicalDispatchWorker do CRM já chama, num servidor de verdade — o consumidor vai na
// consulta, a reserva e a autorização em JSON com o protocolo, e o PDF volta em base64.
// O DbContext ganha a estratégia que repete (a do AddClinica), porque foi ela que derrubou
// as outras gravações da ponte em produção (PonteComRepeticaoTests).
public sealed class PonteClinicDispatchTests : IAsyncLifetime
{
    sealed class RepeteComoONpgsql(ExecutionStrategyDependencies d) : ExecutionStrategy(d, 3, TimeSpan.FromMilliseconds(10))
    {
        protected override bool ShouldRetryOn(Exception exception) => false;
    }

    readonly SqliteConnection conexao = new("Data Source=:memory:");
    WebApplication? app;
    HttpClient http = new();

    DbContextOptions<ClinicaDbContext> Opcoes() => new DbContextOptionsBuilder<ClinicaDbContext>()
        .UseSqlite(conexao, o => o.ExecutionStrategy(d => new RepeteComoONpgsql(d))).Options;

    public async Task InitializeAsync()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        conexao.Open();
        await using (var db = new ClinicaDbContext(Opcoes())) await db.Database.EnsureCreatedAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped(_ => new ClinicaDbContext(Opcoes()));
        builder.Services.AddScoped<IClinicaRepositorio>(sp => new ClinicaRepositorio(sp.GetRequiredService<ClinicaDbContext>()));
        builder.Services.AddScoped<FilaEnviosCrmService>();
        builder.Services.AddScoped<ClinicDispatchBridge>();
        app = builder.Build();
        RotasDaFilaDeEnvios.Mapear(app);
        await app.StartAsync();
        http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
    }

    public async Task DisposeAsync()
    {
        http.Dispose();
        if (app is not null) await app.DisposeAsync();
        conexao.Dispose();
    }

    async Task<(string Id, byte[] Pdf)> PedidoAsync()
    {
        await using var db = new ClinicaDbContext(Opcoes());
        var repo = new ClinicaRepositorio(db);
        var p = new Paciente { Nome = "Pessoa Fictícia", Telefone = "(11) 99999-0001", Convenio = Convenio.UnimedIntercambio };
        var m = new Profissional { Nome = "Dra. Fictícia", RegistroConselho = "CRM-SP 1" };
        db.Pacientes.Add(p); db.Profissionais.Add(m); await db.SaveChangesAsync();
        var doc = await new DocumentoClinicoService(repo, new ProntuarioService(repo), new ConsentimentoService(repo)).EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Comparecimento, PacienteId = p.Id, ProfissionalId = m.Id, Data = DateOnly.FromDateTime(DateTime.Today)
        }, "ana");
        var envio = new EnvioAoPacienteService(repo, new DocumentosClinicosPdfService(repo), new ParametrosService(repo), new SemEmail());
        Assert.True((await envio.EnfileirarWhatsAppAsync(doc.Id, "ana")).Ok);
        var linha = await db.EnviosPacienteCrm.AsNoTracking().SingleAsync();
        return (linha.Id, linha.Arquivo!);
    }

    [Fact]
    public async Task O_fluxo_do_CRM_pelas_rotas_da_ponte()
    {
        var (id, pdf) = await PedidoAsync();

        // Listar: sem texto e sem PDF, com a validade no fuso da clínica.
        var lista = await http.GetFromJsonAsync<ClinicDispatch[]>("/crm/v1/dispatches?consumer=crm-1");
        var item = Assert.Single(lista!);
        Assert.Equal(id, item.Id);
        Assert.Equal("queued", item.State);
        Assert.Null(item.Text);
        Assert.Null(item.File);
        Assert.Equal("whatsapp", item.Channel);
        Assert.Equal("DocumentoClinico", item.Source);
        Assert.Equal("5511999990001", item.Phone);
        Assert.Equal(TimeSpan.FromHours(-3), item.ExpiresAt.Offset);
        Assert.Equal(TimeSpan.FromHours(24), item.ExpiresAt - item.CreatedAt);

        // Reservar (protocolo 2): a linha inteira, com os MESMOS bytes.
        var reserva = await (await http.PostAsJsonAsync($"/crm/v1/dispatches/{id}/claim", new ClinicDispatchClaim("crm-1", 2))).Content.ReadFromJsonAsync<ClinicDispatch>();
        Assert.Equal("processing", reserva!.State);
        Assert.Equal(pdf, reserva.File);
        Assert.Contains("Olá, Pessoa!", reserva.Text);

        // Autorizar: uma vez só.
        var permit = await (await http.PostAsJsonAsync($"/crm/v1/dispatches/{id}/submit", new ClinicDispatchClaim("crm-1", 2))).Content.ReadFromJsonAsync<ClinicDispatchPermit>();
        Assert.True(permit!.Granted);
        var segunda = await (await http.PostAsJsonAsync($"/crm/v1/dispatches/{id}/submit", new ClinicDispatchClaim("crm-1", 2))).Content.ReadFromJsonAsync<ClinicDispatchPermit>();
        Assert.False(segunda!.Granted);
        Assert.Equal("uncertain", segunda.State);

        // Relatar: aceite, entrega; o recibo atrasado não regride.
        async Task<ClinicDecisionResult> Relatar(string status) => (await (await http.PostAsJsonAsync($"/crm/v1/dispatches/{id}/report",
            new ClinicDispatchReport("crm-1", status, "do provedor"))).Content.ReadFromJsonAsync<ClinicDecisionResult>())!;
        Assert.Equal("accepted", (await Relatar("accepted")).Status);
        Assert.Equal("delivered", (await Relatar("delivered")).Status);
        Assert.Equal("delivered", (await Relatar("accepted")).Status);

        Assert.Empty((await http.GetFromJsonAsync<ClinicDispatch[]>("/crm/v1/dispatches?consumer=crm-1"))!);
        await using var db = new ClinicaDbContext(Opcoes());
        var final = await db.EnviosPacienteCrm.AsNoTracking().SingleAsync();
        Assert.Equal("delivered", final.Estado);
        Assert.Null(final.Arquivo);
    }

    [Fact]
    public async Task Cliente_antigo_nao_recebe_o_conteudo()
    {
        var (id, _) = await PedidoAsync();
        var r = await (await http.PostAsJsonAsync($"/crm/v1/dispatches/{id}/claim", new { consumer = "crm-1" })).Content.ReadFromJsonAsync<ClinicDispatch>();
        Assert.Equal("blocked", r!.State);
        Assert.Null(r.File);
        Assert.Null(r.Text);
    }

    sealed class SemEmail : IEnviadorDeEmail
    {
        public Task EnviarAsync(OpcoesEmail opcoes, string destinatario, string assunto, string corpo, CancellationToken ct = default)
            => throw new InvalidOperationException();
    }
}
