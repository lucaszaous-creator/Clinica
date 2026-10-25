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
// A prévia não tem CRM: o WhatsApp da Recepção conversa com o CRM de mentira (CrmDeMentira.cs),
// e o ClienteDoCrmHttp do AddClinica nunca é chamado. CLINICA_PREVIA_CRM=fora-do-ar|nao-configurado
// fotografa os estados de falha.
PreviaBlazor.CrmDeMentira.Registrar(builder.Services);
builder.Services.AddTelasBlazor();
// Por navegador (Scoped): na página da aula o hospedeiro troca a tela pela moldura daquele
// navegador — é o que faz "Abrir a ficha" ou "Ir para o Caixa" funcionarem no vídeo.
builder.Services.AddScoped<PreviaBlazor.AulaAtual>();
builder.Services.AddScoped<IHostDaTela>(sp => new HostDaPrevia(sp.GetRequiredService<PreviaBlazor.AulaAtual>()));
// As pontes que, no app, abrem outra tela (PontesDaAula.cs): na aula elas navegam.
builder.Services.AddScoped<IPostoDoConsultorio, PreviaBlazor.PostoDaAula>();
builder.Services.AddScoped<IJanelasDaAgenda, PreviaBlazor.JanelasDaAgendaDaAula>();
builder.Services.AddScoped<IMarcacaoDeHorario, PreviaBlazor.MarcacaoDaAula>();
// A navegação real dos apps (navegacao.json), para a página da aula — as videoaulas.
builder.Services.AddSingleton<PreviaBlazor.NavegacaoDaPrevia>();
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
sealed class HostDaPrevia(PreviaBlazor.AulaAtual aula) : IHostDaTela
{
    public Task<string?> ExportarCsvAsync(IReadOnlyList<string> cabecalho,
        IReadOnlyList<IReadOnlyList<string>> linhas, string nomeArquivo)
        => Task.FromResult<string?>($"Na prévia o CSV não é gravado ({linhas.Count} linhas montadas).");

    public Task<string?> SalvarEAbrirAsync(byte[] conteudo, string nomeSugerido, string filtro, string extensao)
        => Task.FromResult<string?>($"Na prévia o arquivo não é gravado ({nomeSugerido}, {conteudo.Length} bytes).");

    // Não há disco: a prévia entrega o arquivo FICTÍCIO que a semente da tela montou, pelo filtro
    // do pedido — o CSV/ZIP da importação de pacientes, ou o OFX do extrato do banco (coerente
    // com os lançamentos semeados). Sem semente, desistiu.
    public Task<ArquivoEscolhido?> EscolherArquivoAsync(string filtro, string titulo)
        => Task.FromResult(SementeImportacaoDePacientes.ExemploPara(filtro)
            ?? (SementeTaxasExtrato.ExemploOfx is { } ofx
                ? new ArquivoEscolhido("extrato-exemplo.ofx", System.Text.Encoding.Latin1.GetBytes(ofx))
                : null));

    public Task<bool> CopiarAsync(string texto) => Task.FromResult(true);

    // Fora da aula não há para onde ir; dentro dela, a moldura troca a tela como no app.
    public Task<bool> NavegarAsync(string chave) => Task.FromResult(aula.Navegar(chave));

    // Na aula, o que a moldura do app conhece; fora dela (/tela/…), a prévia mostra tudo o que a tela tem.
    public bool TemTela(string chave) => aula.Moldura?.Conhece(chave) ?? true;

    public Task<bool> AbrirEnderecoAsync(string url) => Task.FromResult(false);

    public Task<bool> AbrirFichaDoPacienteAsync(int pacienteId) => Task.FromResult(false);

    // A prévia não tem disco: devolve um destino fictício, e "gravar" só confere que veio algo.
    public Task<string?> PerguntarOndeSalvarAsync(string nomeSugerido, string filtro, string extensao)
        => Task.FromResult<string?>("/previa/" + nomeSugerido);

    public Task<string?> PerguntarPastaAsync(string titulo) => Task.FromResult<string?>("/previa");

    public Task<string?> GravarArquivoAsync(string caminho, byte[] conteudo) => Task.FromResult<string?>(null);
}
