// A GESTÃO NO NAVEGADOR (nov/2026) — docs/gestao-web.md.
// O Gerente Geral no portal: as mesmas telas e a mesma moldura dos apps, por Blazor Server, em /gestao.
using System.Globalization;
using System.Threading.RateLimiting;
using Clinica.Domain.Entities;
using Clinica.Gestao.Web;
using Clinica.Gestao.Web.Hospedagem;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;

// ⛔ Antes de qualquer serviço: no servidor, `SessaoUsuario.Atual` é a sessão da CONEXÃO e, sem
// login, NEGA tudo (no desktop, sem login, libera — aqui seria uma porta aberta na internet).
SessaoUsuario.AtivarModoServidor();

// A clínica roda em pt-BR: dinheiro, datas e as mensagens dos serviços saem na cultura do processo.
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("pt-BR");

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var conexao = cfg.GetConnectionString("Clinica");
if (string.IsNullOrWhiteSpace(conexao)) throw new InvalidOperationException("Configure ConnectionStrings:Clinica (papel próprio da gestão web).");
var chaves = cfg["Gestao:DiretorioChaves"];
if (string.IsNullOrWhiteSpace(chaves)) throw new InvalidOperationException("Configure Gestao:DiretorioChaves (o MESMO anel de chaves da API do portal, em leitura).");
// A origem pública do portal (https://…): o WebSocket do Blazor só é aceito vindo dela.
var origem = cfg["Gestao:Origem"]?.TrimEnd('/');
if (string.IsNullOrWhiteSpace(origem) || !Uri.TryCreate(origem, UriKind.Absolute, out var origemUri) || origemUri.Scheme != Uri.UriSchemeHttps)
    throw new InvalidOperationException("Configure Gestao:Origem com o endereço https público do portal.");
var desenvolvimento = builder.Environment.IsDevelopment();

var socket = cfg["Gestao:Socket"];
if (!string.IsNullOrWhiteSpace(socket))
{
    if (!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(socket))
        throw new InvalidOperationException("O socket privado da gestão exige Linux e caminho absoluto.");
    // Como a API do portal: só o cloudflared fala com este processo, por um socket Unix restrito.
    builder.WebHost.ConfigureKestrel(o => o.ListenUnixSocket(socket));
}
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);

builder.Services.AddClinica(conexao);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SessaoDoPortalNaGestao>();
// Desligada por padrão: o gerente entra só com a senha até ativar o segundo fator no portal.
// Ligue (Gestao__ExigirSegundoFator=true) para a gestão abrir só para quem já ativou.
builder.Services.AddSingleton(new OpcoesDaGestaoWeb(cfg.GetValue<bool>("Gestao:ExigirSegundoFator")));

// Os pedidos entre telas ("abra a ficha deste paciente") são SINGLETON no desktop, onde há uma
// pessoa por processo. Aqui são POR CONEXÃO — registrados antes do AddTelasBlazor, que só os
// acrescenta se faltarem: o pedido de uma pessoa nunca aparece na tela de outra.
builder.Services.AddScoped<PedidoDaAgendaDoDia>();
builder.Services.AddScoped<PedidoDasConsultas>();
builder.Services.AddScoped<PedidoDaTelaDoPaciente>();
builder.Services.AddScoped<PedidoDaFichaDoBalcao>();
builder.Services.AddScoped<PedidoDeDocumento>();
builder.Services.AddScoped<PedidoDeConversaNoCrm>();
builder.Services.AddScoped<PedidoDoAcompanhamento>();
builder.Services.AddTelasBlazor();
// Assinatura digital: área congelada — a gestão web não assina (AssinaturaForaDaGestao).
builder.Services.AddScoped<IAssinaturaNaTela, AssinaturaForaDaGestao>();

builder.Services.AddSingleton(CatalogoDaGestao.Embutido());
// Convênios, modalidades e especialidades em memória: nos apps quem carrega é a abertura; aqui,
// este serviço — na subida e de novo a cada poucos minutos (o processo vive semanas).
builder.Services.AddHostedService<CatalogosDaGestao>();
builder.Services.AddScoped<SessaoDaConexao>();
builder.Services.AddScoped<NavegacaoDaGestao>();
builder.Services.AddScoped<IHostDaTela, HostDaTelaWeb>();
builder.Services.AddScoped<IPostoDoConsultorio, PostoDaGestao>();
builder.Services.AddScoped<IJanelasDaAgenda, JanelasDaAgendaDaGestao>();
builder.Services.AddScoped<IMarcacaoDeHorario, MarcacaoDaGestao>();
builder.Services.AddScoped<CircuitHandler, AtividadeComSessao>();

// O MESMO anel de chaves da API do portal (abre o cookie do aparelho). A gestão só LÊ: nunca
// gera chave — quem gira o anel é a API.
builder.Services.AddDataProtection()
    .SetApplicationName(CookiesDoPortal.AplicacaoDaProtecao)
    .PersistKeysToFileSystem(new DirectoryInfo(chaves))
    .DisableAutomaticKeyGeneration();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o =>
    {
        o.DetailedErrors = desenvolvimento;
        // Conexão caída não fica guardada no servidor por muito tempo: a tela é de dado de saúde.
        o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);
        o.DisconnectedCircuitMaxRetained = 20;
        o.MaxBufferedUnacknowledgedRenderBatches = 10;
    })
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 64 * 1024);

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(
        "gestao:" + OrigemDaRequisicao.Obter(ctx, socketPrivado: !string.IsNullOrWhiteSpace(socket)),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Nunca registrar corpo, cookie ou exceção com dado de saúde.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Critical);

var app = builder.Build();

app.UsePathBase("/gestao");
// Só /gestao é deste processo: o túnel encaminha só esse caminho, e qualquer outro recebe 404 —
// nada de servir a moldura ou a porta em endereço que o portal não anuncia.
app.Use(async (ctx, next) =>
{
    if (!ctx.Request.PathBase.HasValue) { ctx.Response.StatusCode = StatusCodes.Status404NotFound; return; }
    await next();
});
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h.CacheControl = "no-store, no-cache, must-revalidate";
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Robots-Tag"] = "noindex, nofollow, noarchive";
    h["Referrer-Policy"] = "no-referrer";
    h["X-Frame-Options"] = "DENY";
    h["Cross-Origin-Opener-Policy"] = "same-origin";
    h["Cross-Origin-Resource-Policy"] = "same-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    // style 'unsafe-inline': as telas desenham barras e gráficos com `style="width:…"` no atributo.
    // Nenhum script em linha, nenhuma origem de fora; o WebSocket do Blazor é da mesma origem.
    h["Content-Security-Policy"] = "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
        "font-src 'self'; connect-src 'self'; frame-src blob:; worker-src 'none'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    if (!desenvolvimento) h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});
app.UseRateLimiter();

// ⛔ A porta: toda requisição precisa da sessão do portal válida para a gestão. Sem ela, a página
// volta ao login do portal; o resto (WebSocket, arquivos) recebe 401. E o WebSocket só é aceito
// vindo da origem do portal (contra o sequestro de WebSocket entre sites — o "CORS" do Blazor).
app.Use(async (ctx, next) =>
{
    {
        // A origem é EXIGIDA no WebSocket e no POST (o navegador sempre a manda nos dois) e, quando vem,
        // tem de ser a do portal. Um GET da mesma origem NÃO traz Origin (o /_blazor/initializers e os
        // arquivos do arranque): exigir ali deixava a gestão em tela branca (403 no próprio arranque).
        var o = ctx.Request.Headers.Origin.ToString().TrimEnd('/');
        var daPropria = string.Equals(o, origem, StringComparison.OrdinalIgnoreCase);
        var exigida = ctx.WebSockets.IsWebSocketRequest || HttpMethods.IsPost(ctx.Request.Method);
        if ((o.Length > 0 && !daPropria) || (exigida && o.Length == 0 && !desenvolvimento))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }
    var protetor = ctx.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector(CookiesDoPortal.PropositoDispositivo);
    string? dispositivo = null;
    try
    {
        dispositivo = CookiesDoPortal.LerDispositivo(protetor.Unprotect(ctx.Request.Cookies[CookiesDoPortal.Dispositivo] ?? ""),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
    catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or FormatException) { }
    var valida = await ctx.RequestServices.GetRequiredService<SessaoDoPortalNaGestao>()
        .ConferirAsync(ctx.Request.Cookies[CookiesDoPortal.Sessao], dispositivo, ctx.RequestAborted);
    if (valida is null)
    {
        if (HttpMethods.IsGet(ctx.Request.Method) && ctx.Request.Headers.Accept.ToString().Contains("text/html"))
            ctx.Response.Redirect("/profissional/");
        else
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    ctx.Items[GestaoNaRequisicao.Chave] = valida;
    await next();
});

// As telas: os MESMOS arquivos embutidos que o BlazorWebView dos apps serve; e o script da gestão.
app.UseStaticFiles(new StaticFileOptions { FileProvider = new ManifestEmbeddedFileProvider(typeof(IHostDaTela).Assembly, "Ativos") });
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<Clinica.Gestao.Web.Componentes.App>().AddInteractiveServerRenderMode();
app.Run();

/// <summary>Para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
