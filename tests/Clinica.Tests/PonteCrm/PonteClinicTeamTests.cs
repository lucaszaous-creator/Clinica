using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

// O acesso ao CRM é o acesso do sistema: a lista de atendentes e a senha vêm da ponte, com a
// MESMA regra de login dos apps (incluindo o travamento depois de 5 erros).
public sealed class PonteClinicTeamTests : IDisposable
{
    const string Senha = "senha-boa-do-balcao";
    readonly SqliteConnection connection = new("Data Source=:memory:");
    readonly ClinicaDbContext db;
    readonly ClinicTeamBridge bridge;

    public PonteClinicTeamTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options); db.Database.EnsureCreated();
        var repo = new ClinicaRepositorio(db);
        bridge = new(repo, new AcessoService(repo));
    }

    UsuarioSistema Usuario(string login, PerfilAcesso perfil, bool ativo = true, Permissao negadas = Permissao.Nenhuma)
    {
        var (hash, sal) = HashSenha.Gerar(Senha);
        var u = new UsuarioSistema { Login = login, Nome = "Nome de " + login, Perfil = perfil, Ativo = ativo,
            SenhaHash = hash, SenhaSalt = sal, PermissoesNegadas = negadas };
        db.Usuarios.Add(u); db.SaveChanges(); return u;
    }

    static int Status(IResult r) => r switch { IStatusCodeHttpResult s => s.StatusCode ?? 200, _ => 200 };

    [Fact] public async Task A_equipe_e_quem_esta_ativo_e_mexe_na_agenda()
    {
        Usuario("ana", PerfilAcesso.Recepcao);
        Usuario("direcao", PerfilAcesso.Gerente);
        Usuario("fatura", PerfilAcesso.Faturista);
        Usuario("saiu", PerfilAcesso.Recepcao, ativo: false);
        Usuario("sem-agenda", PerfilAcesso.Recepcao, negadas: Permissao.EditarAgenda);

        var team = await bridge.Team(default);

        Assert.Equal(["ana", "direcao"], team.Select(t => t.Login).Order().ToArray());
        Assert.True(team.Single(t => t.Login == "direcao").Admin);
        Assert.False(team.Single(t => t.Login == "ana").Admin);
        Assert.Equal("Nome de ana", team.Single(t => t.Login == "ana").Name);
    }

    [Fact] public async Task Login_e_senha_do_sistema_entram()
    {
        Usuario("ana", PerfilAcesso.Recepcao);
        var r = await bridge.Enter(new("ANA", Senha), default); // o login é normalizado como nos apps
        var ok = Assert.IsType<Ok<ClinicTeamMember>>(r);
        Assert.Equal("ana", ok.Value!.Login);
        Assert.NotNull((await db.Usuarios.AsNoTracking().SingleAsync()).UltimoAcessoEm);
    }

    [Fact] public async Task Senha_errada_e_401_e_conta_a_tentativa()
    {
        Usuario("ana", PerfilAcesso.Recepcao);
        var r = await bridge.Enter(new("ana", "errada"), default);
        Assert.Equal(401, Status(r));
        Assert.Equal(1, (await db.Usuarios.AsNoTracking().SingleAsync()).TentativasFalhas);
    }

    [Fact] public async Task Login_inexistente_responde_igual_a_senha_errada()
    {
        var r = await bridge.Enter(new("ninguem", "x"), default);
        Assert.Equal(401, Status(r));
    }

    [Fact] public async Task Cinco_erros_travam_o_login_tambem_no_CRM()
    {
        Usuario("ana", PerfilAcesso.Recepcao);
        for (var i = 0; i < AcessoService.TentativasAteTravar; i++) await bridge.Enter(new("ana", "errada"), default);
        // Travado: nem a senha certa entra — é a regra dos apps, e as tentativas contam juntas.
        Assert.Equal(401, Status(await bridge.Enter(new("ana", Senha), default)));
        Assert.NotNull((await db.Usuarios.AsNoTracking().SingleAsync()).BloqueadoAte);
    }

    [Fact] public async Task Quem_nao_mexe_na_agenda_e_recusado_com_a_frase_de_acesso()
    {
        Usuario("fatura", PerfilAcesso.Faturista);
        var r = await bridge.Enter(new("fatura", Senha), default);
        Assert.Equal(403, Status(r));
    }

    [Fact] public async Task A_ponte_nunca_reescreve_a_senha()
    {
        var u = Usuario("ana", PerfilAcesso.Recepcao);
        var antes = (u.SenhaHash, u.SenhaSalt);
        await bridge.Enter(new("ana", Senha), default);
        var depois = await db.Usuarios.AsNoTracking().SingleAsync();
        Assert.Equal(antes, (depois.SenhaHash, depois.SenhaSalt));
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
