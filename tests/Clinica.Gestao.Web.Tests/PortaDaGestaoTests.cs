using System.Net;
using Clinica.Application.Tablet;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Gestao.Web.Tests;

/// <summary>
/// A PORTA da gestão no navegador (nov/2026): quem entra, por onde, com que cabeçalhos. A sessão é
/// a do portal (senha + segundo fator na API); aqui ela é semeada direto no banco de teste, com o
/// cookie do aparelho cifrado pelo MESMO anel de chaves que a gestão lê.
/// </summary>
public sealed class PortaDaGestaoTests : IAsyncLifetime
{
    private const string Origem = "https://portal.exemplo.invalid";
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "gestao-web-" + Guid.NewGuid().ToString("N"));
    private SqliteConnection _conn = null!;
    private WebApplicationFactory<Program> _fabrica = null!;
    private IDataProtector _protetor = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_pasta);
        var chaves = Path.Combine(_pasta, "chaves");
        // O anel de chaves nasce aqui (como a API faz); a gestão só o lê (DisableAutomaticKeyGeneration).
        var sc = new ServiceCollection();
        sc.AddDataProtection().SetApplicationName(CookiesDoPortal.AplicacaoDaProtecao).PersistKeysToFileSystem(new DirectoryInfo(chaves));
        _protetor = sc.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>().CreateProtector(CookiesDoPortal.PropositoDispositivo);
        _protetor.Protect("aquecer");

        _conn = new SqliteConnection("DataSource=:memory:");
        await _conn.OpenAsync();
        _fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("ConnectionStrings:Clinica", "Host=nao-usado;Database=nao-usado");
            b.UseSetting("Gestao:DiretorioChaves", chaves);
            b.UseSetting("Gestao:Origem", Origem);
            b.ConfigureServices(s =>
            {
                foreach (var d in s.Where(d => d.ServiceType == typeof(DbContextOptions<ClinicaDbContext>) || d.ServiceType == typeof(DbContextOptions)
                                               || d.ServiceType == typeof(ClinicaDbContext)).ToList())
                    s.Remove(d);
                s.AddDbContext<ClinicaDbContext>(o => o.UseSqlite(_conn));
            });
        });
        using var escopo = _fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _fabrica.DisposeAsync();
        await _conn.DisposeAsync();
        try { Directory.Delete(_pasta, true); } catch (IOException) { }
    }

    private async Task<(string Token, string Aparelho)> Sessao(PerfilAcesso perfil, string modo = "equipe", long? expira = null, bool senhaTrocada = false)
    {
        using var escopo = _fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var (hash, sal) = HashSenha.Gerar("Teste#Gestao2026");
        var u = new UsuarioSistema { Nome = "Pessoa " + perfil, Login = "p" + Guid.NewGuid().ToString("N")[..8], SenhaHash = hash, SenhaSalt = sal, Perfil = perfil, Ativo = true };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var aparelho = "aparelho-" + u.Login;
        db.SessoesTablet.Add(new SessaoTablet
        {
            Id = ContratoTablet.Hash(token), UsuarioId = u.Id, Dispositivo = aparelho, Modo = modo,
            CredencialVersao = ContratoTablet.Hash(senhaTrocada ? "senha-antiga" : u.SenhaHash),
            ExpiraEm = expira ?? DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds()
        });
        await db.SaveChangesAsync();
        return (token, aparelho);
    }

    private HttpRequestMessage Pedido(string caminho, (string Token, string Aparelho)? sessao, string? origem = null, string accept = "text/html")
    {
        var r = new HttpRequestMessage(HttpMethod.Get, caminho);
        r.Headers.Add("Accept", accept);
        if (sessao is { } s)
        {
            var disp = _protetor.Protect(CookiesDoPortal.EscreverDispositivo(s.Aparelho, DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds()));
            r.Headers.Add("Cookie", $"{CookiesDoPortal.Sessao}={s.Token}; {CookiesDoPortal.Dispositivo}={disp}");
        }
        if (origem is not null) r.Headers.Add("Origin", origem);
        return r;
    }

    private HttpClient Cliente() => _fabrica.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    [Fact]
    public async Task Sem_sessao_volta_ao_login_do_portal()
    {
        var r = await Cliente().SendAsync(Pedido("/gestao/", null));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/profissional/", r.Headers.Location?.OriginalString);
        // Arquivo e WebSocket sem sessão: 401, sem página.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente().SendAsync(Pedido("/gestao/css/clinica.css", null, accept: "text/css"))).StatusCode);
    }

    [Fact]
    public async Task Gerente_entra_e_a_pagina_sai_com_os_cabecalhos_de_seguranca()
    {
        var s = await Sessao(PerfilAcesso.Gerente);
        var r = await Cliente().SendAsync(Pedido("/gestao/", s));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var csp = r.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'none'", csp);
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("no-store", r.Headers.CacheControl!.ToString());
        Assert.StartsWith("max-age=", r.Headers.GetValues("Strict-Transport-Security").Single());
        Assert.False(r.Headers.Contains("Access-Control-Allow-Origin")); // nenhum CORS: só a própria origem
        var html = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain(s.Token, html); // o token nunca vai à página (só o resumo da sessão)
    }

    [Theory]
    [InlineData(PerfilAcesso.Recepcao, "equipe", false, false)]   // não é gerente
    [InlineData(PerfilAcesso.Gerente, "revogada", false, false)]  // saiu do portal
    [InlineData(PerfilAcesso.Gerente, "paciente", false, false)]  // tablet com o paciente
    [InlineData(PerfilAcesso.Gerente, "equipe", true, false)]     // sessão vencida
    [InlineData(PerfilAcesso.Gerente, "equipe", false, true)]     // senha trocada depois da entrada
    public async Task Sessao_que_nao_vale_para_a_gestao_volta_ao_login(PerfilAcesso perfil, string modo, bool vencida, bool senhaTrocada)
    {
        var s = await Sessao(perfil, modo, vencida ? DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds() : null, senhaTrocada);
        var r = await Cliente().SendAsync(Pedido("/gestao/", s));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
    }

    [Fact]
    public async Task Cookie_do_aparelho_de_outra_pessoa_nao_abre_a_sessao()
    {
        var s = await Sessao(PerfilAcesso.Gerente);
        var r = await Cliente().SendAsync(Pedido("/gestao/", (s.Token, "aparelho-alheio")));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
    }

    [Fact]
    public async Task WebSocket_de_outra_origem_e_recusado()
    {
        var s = await Sessao(PerfilAcesso.Gerente);
        var alheio = await Cliente().SendAsync(Pedido("/gestao/_blazor/negotiate?negotiateVersion=1", s, origem: "https://atacante.invalid", accept: "*/*"));
        Assert.Equal(HttpStatusCode.Forbidden, alheio.StatusCode);
        // O negotiate de verdade é POST: sem Origin, recusado.
        var post = Pedido("/gestao/_blazor/negotiate?negotiateVersion=1", s, origem: null, accept: "*/*");
        post.Method = HttpMethod.Post;
        post.Content = new StringContent("");
        Assert.Equal(HttpStatusCode.Forbidden, (await Cliente().SendAsync(post)).StatusCode);
    }

    [Fact]
    public async Task GET_da_mesma_origem_sem_Origin_passa_no_arranque()
    {
        // O navegador NÃO manda Origin num GET da mesma origem: o /_blazor/initializers do arranque vinha 403 e a
        // gestão ficava em tela branca em produção (nov/2026).
        var s = await Sessao(PerfilAcesso.Gerente);
        var r = await Cliente().SendAsync(Pedido("/gestao/_blazor/initializers", s, origem: null, accept: "*/*"));
        Assert.NotEqual(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Caminho_fora_de_gestao_nao_e_deste_processo()
    {
        var s = await Sessao(PerfilAcesso.Gerente);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente().SendAsync(Pedido("/profissional/", s))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente().SendAsync(Pedido("/", s))).StatusCode);
    }

    [Fact]
    public async Task Com_a_chave_ligada_a_gestao_so_abre_para_quem_ativou_o_segundo_fator()
    {
        var s = await Sessao(PerfilAcesso.Gerente);
        using var escopo = _fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var id = ContratoTablet.Hash(s.Token);
        var exige = new SessaoDoPortalNaGestao(db, TimeProvider.System, new OpcoesDaGestaoWeb(ExigirSegundoFator: true));
        var padrao = new SessaoDoPortalNaGestao(db, TimeProvider.System, new OpcoesDaGestaoWeb(ExigirSegundoFator: false));

        Assert.NotNull(await padrao.ReconferirAsync(id, default));   // desligada: entra só com a senha
        Assert.Null(await exige.ReconferirAsync(id, default));       // ligada: sem autenticador, não entra
        var usuarioId = db.SessoesTablet.Single(x => x.Id == id).UsuarioId;
        db.SegundosFatoresPortal.Add(new SegundoFatorPortal { UsuarioId = usuarioId, SegredoProtegido = "x", AtivadoEm = 1 });
        await db.SaveChangesAsync();
        Assert.NotNull(await exige.ReconferirAsync(id, default));    // ativou: entra
    }
}

