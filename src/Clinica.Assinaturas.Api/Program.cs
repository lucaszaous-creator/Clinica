using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Clinica.Assinaturas.Api;

var builder=WebApplication.CreateBuilder(args);
var demo=builder.Configuration.GetValue<bool>("Portal:Demo");
var homologacao=builder.Configuration.GetValue<bool>("Portal:Homologacao");
var socketPortal=builder.Configuration["Portal:Socket"];
if(!demo && !string.IsNullOrWhiteSpace(socketPortal))
{
    if(!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(socketPortal))
        throw new InvalidOperationException("O socket privado do portal exige Linux e caminho absoluto.");
    builder.WebHost.ConfigureKestrel(o=>o.ListenUnixSocket(socketPortal));
}
if(demo && !builder.Environment.IsDevelopment()) throw new InvalidOperationException("Demonstração só é permitida em Development.");
var codigoTablet=builder.Configuration["Portal:CodigoTablet"] ?? "";
var modelos=builder.Configuration.GetSection("Portal:Modelos").Get<int[]>() ?? [];
if(!demo && (codigoTablet.Length<32 || modelos.Length!=2 || modelos.Distinct().Count()!=2 || modelos.Any(id=>id<=0)))
    throw new InvalidOperationException("Configure o código de cadastro dos tablets e os dois modelos clínicos aprovados.");
if(!demo && !builder.Configuration.GetValue<bool>("Portal:Habilitado"))
    throw new InvalidOperationException("Portal desabilitado. Habilite somente após a homologação e configuração.");

var conexao=builder.Configuration.GetConnectionString("Clinica");
if(demo)
{
    if(!string.IsNullOrEmpty(conexao)) throw new InvalidOperationException("Demonstração não aceita conexão clínica.");
    builder.WebHost.UseUrls("http://127.0.0.1:18120");
    var arquivo=builder.Configuration["Portal:BancoDemo"] ?? Path.Combine(builder.Environment.ContentRootPath,"tablet-demo.db");
    builder.Services.AddDbContext<ClinicaDbContext>(o=>o.UseSqlite($"Data Source={arquivo}"));
}
else
{
    if(string.IsNullOrEmpty(conexao)) throw new InvalidOperationException("Configure a conexão restrita do portal.");
    // Transações explícitas curtas, sem replay implícito de emissão de documentos.
    builder.Services.AddDbContext<ClinicaDbContext>(o=>o.UseNpgsql(conexao,n=>n.CommandTimeout(20)));
}
builder.Services.AddScoped<IClinicaRepositorio,ClinicaRepositorio>();
builder.Services.AddScoped<AcessoService>();
builder.Services.AddScoped<DocumentoClinicoService>();
builder.Services.AddScoped<ProntuarioService>();
builder.Services.AddScoped<ConsentimentoService>();
builder.Services.AddScoped<AssinaturaDoPacienteService>();
builder.Services.AddScoped<DocumentosClinicosPdfService>();
builder.Services.AddScoped<ParametrosService>();
builder.Services.AddScoped<PortalTabletService>();
var atendimentoHabilitado=demo || builder.Configuration.GetValue<bool>("Portal:AtendimentoHabilitado");
var opcoes=new OpcoesTablet(demo ? [1,2] : modelos,atendimentoHabilitado);
builder.Services.AddScoped<AtendimentoTabletService>();
builder.Services.AddScoped<PostoTabletService>();
builder.Services.AddScoped<ChecagemPrescricaoService>();
builder.Services.AddScoped<AtendimentoService>();
builder.Services.AddScoped<AgendaService>();
builder.Services.AddScoped<PrescricaoService>();
builder.Services.AddScoped<PrescricaoInternaService>();
builder.Services.AddScoped<PrescricaoInternaPdfService>();
builder.Services.AddScoped<Clinica.Application.Assinatura.AssinaturaDigitalService>();
builder.Services.AddScoped<AssinaturaDeDocumentoClinicoService>();
builder.Services.AddScoped<AssinaturaDePrescricaoService>();
builder.Services.AddSingleton<AutorizacoesSafeIdTablet>();
builder.Services.AddScoped<SafeIdTabletService>();
builder.Services.AddSingleton(opcoes);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<FinalizadorTablet>();
builder.Services.AddHostedService<ConclusaoAutomaticaWorker>();
var protecao=builder.Services.AddDataProtection().SetApplicationName("Clinica.Assinaturas.Tablet.v1");
var chaves=builder.Configuration["Portal:DiretorioChaves"];
if(!demo && string.IsNullOrWhiteSpace(chaves)) throw new InvalidOperationException("Configure o diretório protegido e persistente das chaves do portal.");
if(!string.IsNullOrWhiteSpace(chaves)) protecao.PersistKeysToFileSystem(new DirectoryInfo(chaves));
builder.Services.AddAntiforgery(o=>
{
    o.HeaderName="X-CSRF-TOKEN";
    o.Cookie.Name=demo ? "clinica.tablet.csrf" : "__Host-clinica.tablet.csrf";
    o.Cookie.SameSite=SameSiteMode.Strict;
    o.Cookie.SecurePolicy=demo ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.Configure<ForwardedHeadersOptions>(o=>o.ForwardedHeaders=ForwardedHeaders.XForwardedProto);
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=1_000_000);
builder.Services.AddRateLimiter(o=>
{
    o.RejectionStatusCode=429;
    o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(ctx=>RateLimitPartition.GetFixedWindowLimiter(
        ctx.Request.Path.StartsWithSegments("/api/entrar") ? "login" : ctx.Connection.RemoteIpAddress?.ToString() ?? "local",
        _=>new FixedWindowRateLimiterOptions {PermitLimit=ctx.Request.Path.StartsWithSegments("/api/entrar") ? 30 : 240,
            Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
// Nunca registrar bodies, cookies, buscas ou exceções com dados de saúde.
builder.Logging.AddFilter("Microsoft.AspNetCore",LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore",LogLevel.Critical);
var app=builder.Build();
var cookieSessao=demo ? "clinica.tablet" : "__Host-clinica.tablet";
var cookieDispositivo=demo ? "clinica.tablet.device" : "__Host-clinica.tablet.device";
var protetor=app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("DispositivoTablet.v1");
CookieOptions Cookie(int horas)=>new() {HttpOnly=true,Secure=!demo,SameSite=SameSiteMode.Strict,Path="/",MaxAge=TimeSpan.FromHours(horas)};
string Dispositivo(HttpContext ctx)
{
    if(demo) return "tablet-ficticio";
    try
    {
        var valor=protetor.Unprotect(ctx.Request.Cookies[cookieDispositivo] ?? "").Split('|');
        if(valor.Length==3 && valor[0]==ContratoTablet.Hash(codigoTablet)
            && long.TryParse(valor[2],out var expira) && expira>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) return valor[1];
    }
    catch(System.Security.Cryptography.CryptographicException) { }
    throw new UnauthorizedAccessException();
}
async Task<Clinica.Domain.Entities.SessaoTablet> Sessao(HttpContext ctx,PortalTabletService svc,bool equipe)
    => await svc.AutorizarAsync(ctx.Request.Cookies[cookieSessao],Dispositivo(ctx),equipe,ctx.RequestAborted);

int? EscopoColeta(SessaoTablet s) => s.Usuario!.Perfil==PerfilAcesso.Profissional
    ? s.Usuario.ProfissionalId ?? throw new UnauthorizedAccessException() : null;
async Task ConferirPacienteColeta(HttpContext ctx,SessaoTablet s,int paciente)
{
    if(EscopoColeta(s) is {} profissional && !await ctx.RequestServices.GetRequiredService<ClinicaDbContext>()
        .Agendamentos.AnyAsync(a=>a.PacienteId==paciente && a.ProfissionalId==profissional
            && (a.Status==StatusAgendamento.Agendado || a.Status==StatusAgendamento.Realizado),ctx.RequestAborted))
        throw new RecursoClinicoIndisponivel();
}

app.UseForwardedHeaders();
app.Use(async(ctx,next)=>
{
    ctx.Response.Headers.CacheControl="no-store, no-cache, must-revalidate";
    ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
    ctx.Response.Headers["X-Robots-Tag"]="noindex, nofollow, noarchive";
    ctx.Response.Headers["Referrer-Policy"]="no-referrer";
    ctx.Response.Headers["X-Frame-Options"]="DENY";
    ctx.Response.Headers["Content-Security-Policy"]="default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; font-src 'self'; worker-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    ctx.Response.Headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=()";
    if(!demo) ctx.Response.Headers["Strict-Transport-Security"]="max-age=31536000";
    if((demo && ctx.Connection.RemoteIpAddress is { } ip && !IPAddress.IsLoopback(ip))
        || (!demo && !ctx.Request.IsHttps)) {ctx.Response.StatusCode=403; return;}
    try
    {
        if(ctx.Request.Path.StartsWithSegments("/api") && HttpMethods.IsPost(ctx.Request.Method))
            await ctx.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(ctx);
        if(HttpMethods.IsPost(ctx.Request.Method) && System.Text.RegularExpressions.Regex.IsMatch(
            ctx.Request.Path.Value??"",@"^/api/posto/pacientes/[0-9]+/anexos$"))
        {
            // Apenas a rota autenticada de anexos recebe o limite maior (5 MiB em base64).
            var limite=ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if(limite is {IsReadOnly:false})limite.MaxRequestBodySize=7_100_000;
        }
        await next();
    }
    catch(Exception e) when(e is UnauthorizedAccessException or RecursoClinicoIndisponivel or ConflitoClinicoTablet or AcessoTabletBloqueado or InvalidOperationException
        or DbUpdateException or AntiforgeryValidationException or FormatException)
    {
        ctx.Response.StatusCode=e switch {UnauthorizedAccessException=>401,AcessoTabletBloqueado=>403,
            RecursoClinicoIndisponivel=>404,ConflitoClinicoTablet=>409,
            AntiforgeryValidationException=>400,DbUpdateException=>409,_=>400};
        await ctx.Response.WriteAsJsonAsync(new {erro=e switch {
            UnauthorizedAccessException=>"Entre com uma conta autorizada neste tablet.",
            RecursoClinicoIndisponivel=>"Registro indisponível para este acesso.",
            ConflitoClinicoTablet=>e.Message,
            AcessoTabletBloqueado=>"O tablet está em modo paciente. A equipe precisa entrar novamente.",
            AntiforgeryValidationException=>"A proteção da página expirou. Atualize antes de continuar.",
            DbUpdateException=>"A operação mudou em outro acesso. Atualize para conferir antes de repetir.",
            FormatException=>"Confira os dados informados.",_=>e.Message}});
    }
    catch(Exception) when(!ctx.RequestAborted.IsCancellationRequested)
    {
        ctx.Response.StatusCode=503;
        await ctx.Response.WriteAsJsonAsync(new {erro="Não foi possível concluir agora. Confira o estado da coleta antes de repetir."});
    }
});
app.UseRateLimiter();
app.MapGet("/api/sessao",async(HttpContext ctx,IAntiforgery csrf,PortalTabletService svc)=>
{
    var token=csrf.GetAndStoreTokens(ctx).RequestToken;
    try
    {
        var s=await Sessao(ctx,svc,false);
        // Referência de contexto, sem expor token/cookie: permite preservar a página
        // ao voltar de outra aba, mas descartar conteúdo após troca de acesso.
        return Results.Ok(new {csrf=token,modo=s.Modo,demo,homologacao,operadora=s.Modo=="equipe" ? s.Usuario!.Nome : null,
            expiraEm=s.ExpiraEm,contexto=ContratoTablet.Hash("contexto-portal:"+s.Id),
            atendimento=atendimentoHabilitado && s.Modo=="equipe" && PoliticaAtendimentoTablet.PodeUsarPosto(s.Usuario!),
            coleta=s.Modo=="equipe" && PortalTabletService.PodeColher(s.Usuario!)});
    }
    catch(UnauthorizedAccessException) {return Results.Ok(new {csrf=token,modo="entrada",demo,homologacao});}
});
app.MapPost("/api/entrar",async(HttpContext ctx,Entrada pedido,AcessoService acesso,PortalTabletService svc)=>
{
    if(pedido.Login?.Length is not (>=3 and <=80) || pedido.Senha?.Length is not (>0 and <=200)) throw new UnauthorizedAccessException();
    var resultado=await acesso.AutenticarAsync(pedido.Login,pedido.Senha,ct:ctx.RequestAborted);
    if(!resultado.Sucesso || resultado.Usuario is not { } u || !svc.PodeEntrar(u)) throw new UnauthorizedAccessException();
    string dispositivo;
    try {dispositivo=Dispositivo(ctx);}
    catch(UnauthorizedAccessException)
    {
        if(!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(pedido.CodigoTablet ?? "")),
            SHA256.HashData(Encoding.UTF8.GetBytes(codigoTablet)))) throw new UnauthorizedAccessException();
        dispositivo=PortalTabletService.Token();
        ctx.Response.Cookies.Append(cookieDispositivo,protetor.Protect(ContratoTablet.Hash(codigoTablet)+"|"+dispositivo+"|"
            +DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds()),Cookie(720));
    }
    var entrada=await svc.EntrarAsync(u,dispositivo,ctx.Request.Cookies[cookieSessao],ctx.RequestAborted);
    ctx.Response.Cookies.Append(cookieSessao,entrada.Token,Cookie(2));
    return Results.Ok(new {modo="equipe"});
});
app.MapPost("/api/sair",async(HttpContext ctx,PortalTabletService svc,ClinicaDbContext db)=>
{
    var s=await Sessao(ctx,svc,false);
    if(s.Modo=="paciente") await svc.EncerrarAsync(s,false,null,ctx.RequestAborted);
    s.Modo="revogada"; s.ExpiraEm=svc.Agora;
    await db.SaveChangesAsync(ctx.RequestAborted); ctx.Response.Cookies.Delete(cookieSessao,Cookie(0));
    return Results.NoContent();
});
app.MapGet("/api/dia",async(HttpContext ctx,PortalTabletService svc)=>
{var s=await Sessao(ctx,svc,true); return Results.Ok(await svc.DiaAsync(ctx.RequestAborted,EscopoColeta(s)));});
app.MapGet("/api/pacientes",async(HttpContext ctx,PortalTabletService svc,string? q)=>
{var s=await Sessao(ctx,svc,true); return Results.Ok(await svc.BuscarAsync(q,ctx.RequestAborted,EscopoColeta(s)));});
app.MapGet("/api/pacientes/{id:int}",async(HttpContext ctx,PortalTabletService svc,int id)=>
{var s=await Sessao(ctx,svc,true); await ConferirPacienteColeta(ctx,s,id); return Results.Ok(await svc.PacienteAsync(id,s.Usuario!.Login,ctx.RequestAborted));});
app.MapPost("/api/preparar",async(HttpContext ctx,PortalTabletService svc,PrepararTablet pedido)=>
{var s=await Sessao(ctx,svc,true); await ConferirPacienteColeta(ctx,s,pedido.PacienteId); await svc.PrepararAsync(s,pedido,ctx.RequestAborted); return Results.Ok(new {modo="paciente"});});
app.MapGet("/api/coletas",async(HttpContext ctx,PortalTabletService svc)=>
{var s=await Sessao(ctx,svc,false); return Results.Ok(await svc.ColetasAsync(s,ctx.RequestAborted));});
app.MapPost("/api/coletas/{id:guid}/assinar",async(HttpContext ctx,PortalTabletService svc,Guid id,EnviarRubrica pedido)=>
{var s=await Sessao(ctx,svc,false); await svc.ReceberAsync(s,id,pedido,ctx.RequestAborted); return Results.Accepted(value:new {estado="recebido"});});
app.MapPost("/api/encerrar",async(HttpContext ctx,PortalTabletService svc,Encerrar pedido)=>
{var s=await Sessao(ctx,svc,false); await svc.EncerrarAsync(s,pedido.Recusa,pedido.Motivo,ctx.RequestAborted); return Results.NoContent();});
app.MapGet("/api/documentos/{id:int}/via",async(HttpContext ctx,PortalTabletService svc,int id)=>
{var s=await Sessao(ctx,svc,true); var p=await ctx.RequestServices.GetRequiredService<ClinicaDbContext>().DocumentosClinicos
    .Where(d=>d.Id==id).Select(d=>(int?)d.PacienteId).SingleOrDefaultAsync(ctx.RequestAborted) ?? throw new RecursoClinicoIndisponivel();
 await ConferirPacienteColeta(ctx,s,p); return Results.File(await svc.AbrirViaAsync(id,s.Usuario!.Login,ctx.RequestAborted),"application/pdf",$"termo-{id}-assinado.pdf");});
app.MapPost("/api/coletas/{id:guid}/retomar",async(HttpContext ctx,PortalTabletService svc,Guid id)=>
{var s=await Sessao(ctx,svc,true); var p=await ctx.RequestServices.GetRequiredService<ClinicaDbContext>().ColetasTablet
    .Where(c=>c.Id==id).Select(c=>(int?)c.PacienteId).SingleOrDefaultAsync(ctx.RequestAborted) ?? throw new RecursoClinicoIndisponivel();
 await ConferirPacienteColeta(ctx,s,p); await svc.RetomarAsync(id,s.Usuario!.Login,ctx.RequestAborted); return Results.Accepted();});
if(atendimentoHabilitado)
{
    RotasAtendimentoTablet.Mapear(app,(ctx,svc)=>Sessao(ctx,svc,false));
    RotasPostoTablet.Mapear(app,(ctx,svc)=>Sessao(ctx,svc,false));
}
app.MapGet("/health",()=>Results.Ok(new {status="ok",contrato=1}));
var interfaceDir=Path.GetFullPath(builder.Configuration["Portal:Interface"] ?? Path.Combine(app.Environment.ContentRootPath,"wwwroot"));
if(!Directory.Exists(interfaceDir)) throw new InvalidOperationException("Configure Portal:Interface com o artefato portal do clinica-site.");
app.UseDefaultFiles(new DefaultFilesOptions {FileProvider=new PhysicalFileProvider(interfaceDir)});
app.UseStaticFiles(new StaticFileOptions {FileProvider=new PhysicalFileProvider(interfaceDir)});
if(demo) await DemoTablet.PrepararAsync(app.Services);
app.Run();

public record Entrada(string Login,string Senha,string? CodigoTablet);
public record Encerrar(bool Recusa,string? Motivo);
public partial class Program;
