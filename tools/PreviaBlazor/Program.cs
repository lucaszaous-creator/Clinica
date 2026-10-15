// Prévia das telas Blazor com dados FICTÍCIOS (ver PreviaBlazor.csproj).
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PreviaBlazor.Sementes;

// A clínica roda num Windows em pt-BR, e os serviços formatam algumas mensagens na cultura
// do processo ("difere do esperado em R$ 15,00"). Sem isto a prévia mostraria "¤15.00" —
// um defeito que não existe no app, e esconderia um que existisse.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo("pt-BR");
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new System.Globalization.CultureInfo("pt-BR");

var builder = WebApplication.CreateBuilder(args);

// OS MESMOS serviços do app (AddClinica), com o banco trocado por um SQLite em memória que
// vive enquanto a prévia estiver no ar. Registrar à mão só os que a tela usa faria a prévia
// passar com uma tela que, no app, pede um serviço a mais.
builder.Services.AddClinica("Host=previa;Database=previa");
foreach (var d in builder.Services.Where(d =>
             d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>) ||
             d.ServiceType == typeof(DbContextOptions) ||
             d.ServiceType == typeof(ClinicaDbContext)).ToList())
    builder.Services.Remove(d);
var conexao = new SqliteConnection("Data Source=:memory:");
conexao.Open();
builder.Services.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(conexao));
builder.Services.AddTelasBlazor();
builder.Services.AddSingleton<IHostDaTela, HostDaPrevia>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

using (var escopo = app.Services.CreateScope())
{
    var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
    db.Database.EnsureCreated();
    foreach (var semente in typeof(ISemente).Assembly.GetTypes()
                 .Where(t => typeof(ISemente).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                 .Select(t => (ISemente)Activator.CreateInstance(t)!)
                 .OrderBy(s => s.Ordem))
    {
        semente.Semear(db);
        db.ChangeTracker.Clear();
    }
}

// Os MESMOS arquivos embutidos que o BlazorWebView do WPF serve.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new ManifestEmbeddedFileProvider(typeof(IHostDaTela).Assembly, "Ativos")
});
app.UseAntiforgery();
app.MapRazorComponents<PreviaBlazor.Componentes.App>().AddInteractiveServerRenderMode();
app.Run();

/// <summary>O hospedeiro da prévia: não há disco nem shell — cada pedido vira uma frase.</summary>
sealed class HostDaPrevia : IHostDaTela
{
    public Task<string?> ExportarCsvAsync(IReadOnlyList<string> cabecalho,
        IReadOnlyList<IReadOnlyList<string>> linhas, string nomeArquivo)
        => Task.FromResult<string?>($"Na prévia o CSV não é gravado ({linhas.Count} linhas montadas).");

    public Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao)
        => Task.FromResult<string?>($"Na prévia o arquivo não é gravado ({nomeSugerido}, {conteudo.Length} bytes).");

    // O extrato do banco é a única tela que lê arquivo: a prévia entrega o OFX FICTÍCIO que a
    // semente montou, coerente com os lançamentos semeados. Sem semente, desistiu.
    public Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo)
        => Task.FromResult(SementeTaxasExtrato.ExemploOfx is { } ofx
            ? new ArquivoEscolhido("extrato-exemplo.ofx", System.Text.Encoding.Latin1.GetBytes(ofx))
            : null);

    public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);

    public Task<bool> NavegarAsync(string chave) => Task.FromResult(false);

    public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);
}
