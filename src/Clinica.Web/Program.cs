using System.Security.Claims;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

var construtor = WebApplication.CreateBuilder(args);

// A conexão é a MESMA do desktop, e vem da mesma fonte de sempre: a variável de ambiente
// que o app já usa. Sem ela o site não sobe — subir apontando para lugar nenhum daria uma
// tela de login que recusa todo mundo, e ninguém saberia por quê.
var conexao = construtor.Configuration.GetConnectionString("Clinica")
              ?? Environment.GetEnvironmentVariable("ConnectionStrings__Clinica");
if (string.IsNullOrWhiteSpace(conexao))
    throw new InvalidOperationException(
        "A conexão com o banco não está configurada. Defina ConnectionStrings__Clinica — "
        + "é a MESMA cadeia dos apps da clínica, com o certificado do mTLS (docs/banco-na-vps.md).");

construtor.Services.AddClinica(conexao);

construtor.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opcoes =>
    {
        opcoes.LoginPath = "/entrar";
        opcoes.LogoutPath = "/sair";
        opcoes.AccessDeniedPath = "/entrar";
        opcoes.Cookie.Name = "clinica.leitura";
        opcoes.Cookie.HttpOnly = true;
        opcoes.Cookie.SameSite = SameSiteMode.Strict;
        // A web mostra dado de saúde: cookie que sobrevive ao dia inteiro é a sessão que
        // fica aberta no celular esquecido em cima da mesa.
        opcoes.ExpireTimeSpan = TimeSpan.FromHours(2);
        opcoes.SlidingExpiration = true;
    });

construtor.Services.AddAuthorization();

// ⚠️ Antiforgery de VERDADE, e não registrado-e-desligado: os dois POST desta web (entrar
// e sair) recebem o token no formulário e são VALIDADOS abaixo. `SameSite=Strict` no
// cookie já barra a maior parte do CSRF, mas ele protege a sessão que EXISTE — o
// login-CSRF acontece antes de haver cookie, e é ele que planta uma sessão de outra pessoa
// no navegador de quem lê prontuário.
construtor.Services.AddAntiforgery();

var app = construtor.Build();

// ⚠️ HSTS e redirecionamento não são enfeite: sem HTTPS, o cookie de sessão e o dado de
// saúde da ficha viajam em claro pela rede da clínica.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (contexto, seguir) =>
{
    // Cabeçalhos que valem para TODA resposta. `no-store` é o que impede a ficha de um
    // paciente de ficar no cache do navegador de um computador compartilhado — e o botão
    // "voltar" depois do logout é exatamente onde isso aparece.
    var cabecalhos = contexto.Response.Headers;
    cabecalhos["Cache-Control"] = "no-store, no-cache, must-revalidate";
    cabecalhos["X-Content-Type-Options"] = "nosniff";
    cabecalhos["Referrer-Policy"] = "no-referrer";
    cabecalhos["X-Frame-Options"] = "DENY";
    // Sem script, sem estilo de fora, sem imagem de fora: a página é HTML e CSS embutidos.
    cabecalhos["Content-Security-Policy"] =
        "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
    await seguir();
});

app.UseAuthentication();
app.UseAuthorization();

// ==================== Entrar e sair ====================

app.MapGet("/", (HttpContext ctx) =>
{
    var abertura = AcessoWeb.AberturaDe(Sessao.Permissoes(ctx.User));
    return Results.Redirect(ctx.User.Identity?.IsAuthenticated == true && abertura is not null
        ? abertura
        : "/entrar");
});

app.MapGet("/entrar", (HttpContext ctx, IAntiforgery antiforgery, string? erro) =>
    Paginas.Html(Paginas.Login(erro, antiforgery.GetAndStoreTokens(ctx).RequestToken)));

app.MapPost("/entrar", async (
    HttpContext ctx, IAntiforgery antiforgery, AcessoService acesso,
    [FromForm] string login, [FromForm] string senha) =>
{
    if (await Sessao.PedidoForjadoAsync(ctx, antiforgery) is { } forjado) return forjado;

    var resultado = await acesso.AutenticarAsync(login, senha);
    if (!resultado.Sucesso || resultado.Usuario is not { } usuario)
        // A frase vem do serviço: ele já distingue credencial errada de usuário travado, e
        // reescrevê-la aqui daria duas verdades sobre a mesma recusa.
        return Results.Redirect($"/entrar?erro={Uri.EscapeDataString(resultado.Erro ?? "Não foi possível entrar.")}");

    var efetivas = usuario.Efetivas;
    if (AcessoWeb.AberturaDe(efetivas) is not { } abertura)
        // Deixar entrar e mostrar um menu vazio faz a pessoa ligar para o suporte em vez de
        // falar com a direção (a regra da parcela 45).
        return Results.Redirect("/entrar?erro=" + Uri.EscapeDataString(
            "Seu acesso não alcança nenhuma das telas de leitura. Fale com a direção da clínica."));

    var identidade = new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new Claim(ClaimTypes.Name, usuario.Nome),
        new Claim(Sessao.ClaimLogin, usuario.Login),
        // A fotografia da permissão, como o desktop faz no `SessaoUsuario.Entrar`: quem
        // relê o banco a cada requisição pagaria uma consulta por clique, e quem não relê
        // NADA teria a permissão de ontem — o cookie de 2 h é o meio-termo escrito.
        new Claim(Sessao.ClaimPermissoes, ((long)efetivas).ToString())
    ], CookieAuthenticationDefaults.AuthenticationScheme);

    await ctx.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));

    return Results.Redirect(abertura);
}).DisableAntiforgery();

app.MapPost("/sair", async (HttpContext ctx, IAntiforgery antiforgery) =>
{
    if (await Sessao.PedidoForjadoAsync(ctx, antiforgery) is { } forjado) return forjado;

    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/entrar");
}).DisableAntiforgery();

// ==================== O DIA ====================

app.MapGet("/dia", async (HttpContext ctx, IAntiforgery antiforgery, AgendaService agenda, string? data) =>
{
    if (Sessao.Recusar(ctx, Permissao.VerAgenda) is { } recusa) return recusa;

    var dia = DateOnly.TryParse(data, out var escolhido)
        ? escolhido
        : DateOnly.FromDateTime(DateTime.Today);

    var horarios = await agenda.DoDiaAsync(dia);
    return Paginas.Html(Paginas.Dia(ctx.User, Sessao.Token(ctx, antiforgery), dia, horarios));
}).RequireAuthorization();

// ==================== O MÊS ====================

app.MapGet("/painel", async (HttpContext ctx, IAntiforgery antiforgery, PainelDirecaoService painel) =>
{
    if (Sessao.Recusar(ctx, Permissao.VerIndicadores) is { } recusa) return recusa;

    var retrato = await painel.MontarAsync(DateOnly.FromDateTime(DateTime.Today));
    return Paginas.Html(Paginas.Painel(ctx.User, Sessao.Token(ctx, antiforgery), retrato));
}).RequireAuthorization();

// ==================== PACIENTES ====================

app.MapGet("/pacientes", async (HttpContext ctx, IAntiforgery antiforgery, PacienteService pacientes, string? q) =>
{
    if (Sessao.Recusar(ctx, Permissao.VerFichaPaciente) is { } recusa) return recusa;

    // Sem termo NÃO consulta: com o campo vazio a busca não filtra nada e traria o começo
    // do alfabeto de 2.238 fichas (a regra do `SemBuscaInicial`, set/2026).
    var achados = string.IsNullOrWhiteSpace(q)
        ? []
        : await pacientes.BuscarAsync(q, 30);

    return Paginas.Html(Paginas.Pacientes(ctx.User, Sessao.Token(ctx, antiforgery), q, achados));
}).RequireAuthorization();

app.MapGet("/paciente/{id:int}", async (
    HttpContext ctx, IAntiforgery antiforgery, int id,
    PacienteService pacientes, ProntuarioService prontuario,
    IClinicaRepositorio repo, AcessoProntuarioService trilha) =>
{
    if (Sessao.Recusar(ctx, Permissao.VerFichaPaciente) is { } recusa) return recusa;

    var paciente = await pacientes.ObterAsync(id);
    if (paciente is null) return Results.NotFound();

    var permissoes = Sessao.Permissoes(ctx.User);
    var operador = Sessao.Operador(ctx.User);

    // SEQUENCIAL, nunca WhenAll: é o mesmo DbContext do escopo da requisição (parcela 74).
    var futuros = await repo.AgendamentosFuturosDoPacienteAsync(id, DateTime.Now, 5);

    IReadOnlyList<Evolucao> sessoes = [];
    if (AcessoWeb.MostraProntuario(permissoes))
    {
        sessoes = await prontuario.DoPacienteAsync(id);

        // ⚠️ A ÚNICA escrita da web, e ela é obrigatória: o ponto 4 do compromisso vale
        // para toda tela que ABRE prontuário. Porta de leitura sem trilha é o buraco que
        // só aparece no dia em que alguém precisa investigar — e falhar aqui NÃO impede
        // ler o prontuário do paciente que está na frente de quem consulta.
        try
        {
            await trilha.RegistrarAsync(id, operador, OrigemAcessoProntuario.WebDeLeitura);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Web — acesso ao prontuário não registrado", ex);
        }
    }

    return Paginas.Html(Paginas.Ficha(
        ctx.User, Sessao.Token(ctx, antiforgery), paciente, futuros, sessoes));
}).RequireAuthorization();

app.Run();

/// <summary>Exposto para o teste de montagem — a web sobe com o mesmo grafo do desktop.</summary>
public partial class Program;
