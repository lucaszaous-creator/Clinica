using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// Usuários, senhas e permissões (parcela 5 — feature 13).
///
/// As três regras que sustentam a tela: a senha nunca é gravada em claro, a permissão
/// efetiva é resolvida na LEITURA (perfil + extras − negadas) e a clínica não pode
/// ficar sem ninguém capaz de gerenciar acessos.
/// </summary>
public class AcessoServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AcessoService _acesso;

    public AcessoServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _acesso = new AcessoService(_repo);
    }

    private Task<UsuarioSistema> CriarGerenteAsync(string login = "direcao")
        => _acesso.CriarAsync("Direção", login, "segredo123", PerfilAcesso.Gerente);

    // ===== Entrada pelo certificado (SafeID, parcela 44) =====
    //
    // O método não autentica ninguém: quem autenticou foi o PSC, quando a médica confirmou
    // no celular. O que se testa aqui é a SEGUNDA pergunta — esta pessoa tem acesso a ESTE
    // sistema? —, que é a que decide se o certificado vira entrada ou não.

    private async Task<UsuarioSistema> CriarMedicaAsync(string cpf, bool ativo = true)
    {
        var profissional = new Profissional { Nome = "Dra. Ana Souza", Cpf = cpf };
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();

        var usuario = await _acesso.CriarAsync(
            "Dra. Ana Souza", "ana", "segredo123", PerfilAcesso.Gerente);

        var rastreado = await _db.Usuarios.FirstAsync(u => u.Id == usuario.Id);
        rastreado.ProfissionalId = profissional.Id;
        rastreado.Ativo = ativo;
        await _db.SaveChangesAsync();

        return rastreado;
    }

    [Fact]
    public async Task Certificado_DeQuemTemAcesso_Entra()
    {
        await CriarMedicaAsync("12345678909");

        var resultado = await _acesso.AutenticarPorCertificadoAsync("123.456.789-09");

        resultado.Sucesso.Should().BeTrue();
        resultado.Usuario!.Login.Should().Be("ana");
    }

    [Fact]
    public async Task Certificado_DeQuemNaoTemUsuario_NAO_CriaUsuario()
    {
        // A regra que impede o certificado de virar a porta mais larga do sistema: ele prova
        // quem a pessoa é, e quem concede acesso é a direção.
        await CriarGerenteAsync();
        var antes = (await _repo.UsuariosAsync()).Count;

        var resultado = await _acesso.AutenticarPorCertificadoAsync("12345678909");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Contain("Acessos");
        (await _repo.UsuariosAsync()).Should().HaveCount(antes);
    }

    [Fact]
    public async Task Certificado_DeUsuarioDesativado_NaoEntra()
    {
        await CriarMedicaAsync("12345678909", ativo: false);

        var resultado = await _acesso.AutenticarPorCertificadoAsync("12345678909");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Contain("desativado");
    }

    [Fact]
    public async Task Certificado_SemCpfValido_NaoEntra()
    {
        var resultado = await _acesso.AutenticarPorCertificadoAsync("   ");

        resultado.Sucesso.Should().BeFalse();
        resultado.Erro.Should().Contain("CPF");
    }

    [Fact]
    public async Task Certificado_NaoDestravaContaSobForcaBruta()
    {
        // O travamento é do caminho da SENHA. Se o certificado o limpasse, ele viraria a
        // saída de emergência de uma conta justamente enquanto ela está sob ataque.
        var usuario = await CriarMedicaAsync("12345678909");
        usuario.BloqueadoAte = DateTime.Now.AddMinutes(5);
        usuario.TentativasFalhas = 3;
        await _db.SaveChangesAsync();

        await _acesso.AutenticarPorCertificadoAsync("12345678909");

        var depois = await _db.Usuarios.AsNoTracking().FirstAsync(u => u.Id == usuario.Id);
        depois.BloqueadoAte.Should().NotBeNull();
        depois.UltimoAcessoEm.Should().NotBeNull();   // e a entrada foi mesmo gravada
    }

    [Fact]
    public async Task Certificado_GravaAuditoria()
    {
        await CriarMedicaAsync("12345678909");

        await _acesso.AutenticarPorCertificadoAsync("12345678909");

        var trilha = await _db.Auditoria.AsNoTracking().ToListAsync();
        trilha.Should().Contain(e => e.Acao == "LoginPorCertificado");
    }

    // ===== Hash de senha =====

    [Fact]
    public void Hash_NuncaGuardaASenhaEmClaro()
    {
        var (hash, sal) = HashSenha.Gerar("segredo123");

        hash.Should().NotContain("segredo123");
        sal.Should().NotBeNullOrWhiteSpace();
        HashSenha.Confere("segredo123", hash, sal).Should().BeTrue();
        HashSenha.Confere("Segredo123", hash, sal).Should().BeFalse();
    }

    [Fact]
    public void Hash_DuasSenhasIguais_GeramHashesDiferentes()
    {
        var (hash1, _) = HashSenha.Gerar("segredo123");
        var (hash2, _) = HashSenha.Gerar("segredo123");

        // Sal por usuário: sem ele, quem visse o banco saberia quem repetiu senha.
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Hash_ParGravadoCorrompido_NaoLanca()
    {
        HashSenha.Confere("segredo123", "isto-não-é-base64!", "nem-isto").Should().BeFalse();
    }

    [Fact]
    public void Criticar_RecusaSenhaCurta()
    {
        HashSenha.Criticar("123").Should().NotBeNull();
        HashSenha.Criticar("segredo123").Should().BeNull();
    }

    // ===== Cadastro =====

    [Fact]
    public async Task Criar_NormalizaOLoginEGuardaOPerfil()
    {
        var usuario = await _acesso.CriarAsync(
            "Ana Souza", "  ANA  ", "segredo123", PerfilAcesso.Recepcao);

        usuario.Login.Should().Be("ana");
        usuario.Perfil.Should().Be(PerfilAcesso.Recepcao);
        usuario.SenhaHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Criar_LoginRepetido_EhRecusado()
    {
        await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);

        var acao = () => _acesso.CriarAsync("Outra Ana", "ANA", "segredo123", PerfilAcesso.Recepcao);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Criar_SenhaCurta_EhRecusada()
    {
        var acao = () => _acesso.CriarAsync("Ana", "ana", "123", PerfilAcesso.Recepcao);

        await acao.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Criar_RegistraNaTrilhaDeAuditoria()
    {
        await CriarGerenteAsync();

        var eventos = await _repo.EventosAuditoriaAsync();
        eventos.Should().Contain(e => e.Acao == "UsuarioCriado");
    }

    [Fact]
    public async Task Criar_PodeApontarParaOProfissionalDaParcela1()
    {
        var profissional = new Profissional { Nome = "Dra. Ana" };
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();

        var usuario = await _acesso.CriarAsync(
            "Ana", "ana", "segredo123", PerfilAcesso.Profissional, profissional.Id);

        usuario.ProfissionalId.Should().Be(profissional.Id);
    }

    // ===== Permissões =====

    [Fact]
    public void Perfil_DefineOConjuntoBase()
    {
        var recepcao = new UsuarioSistema { Perfil = PerfilAcesso.Recepcao };

        recepcao.Pode(Permissao.EditarAgenda).Should().BeTrue();
        recepcao.Pode(Permissao.VerFinanceiro).Should().BeFalse();
        recepcao.Pode(Permissao.GerenciarUsuarios).Should().BeFalse();
    }

    [Fact]
    public void Gerente_PodeTudo()
    {
        var gerente = new UsuarioSistema { Perfil = PerfilAcesso.Gerente };

        foreach (var permissao in PerfisAcesso.Individuais)
            gerente.Pode(permissao).Should().BeTrue($"o Gerente precisa de {permissao}");
    }

    [Fact]
    public void PermissaoExtra_SomaAoPerfil()
    {
        var usuario = new UsuarioSistema
        {
            Perfil = PerfilAcesso.Recepcao,
            PermissoesExtras = Permissao.VerFinanceiro
        };

        usuario.Pode(Permissao.VerFinanceiro).Should().BeTrue();
        usuario.Pode(Permissao.EditarAgenda).Should().BeTrue("o perfil continua valendo");
    }

    [Fact]
    public void PermissaoNegada_VenceAExtra()
    {
        // Tirar acesso é a decisão que não pode ser anulada por engano de configuração.
        var usuario = new UsuarioSistema
        {
            Perfil = PerfilAcesso.Recepcao,
            PermissoesExtras = Permissao.VerFinanceiro,
            PermissoesNegadas = Permissao.VerFinanceiro
        };

        usuario.Pode(Permissao.VerFinanceiro).Should().BeFalse();
    }

    [Fact]
    public void PermissaoNegada_TiraOQueOPerfilDava()
    {
        var usuario = new UsuarioSistema
        {
            Perfil = PerfilAcesso.Recepcao,
            PermissoesNegadas = Permissao.EditarProntuario
        };

        usuario.Pode(Permissao.EditarProntuario).Should().BeFalse();
        usuario.Pode(Permissao.VerProntuario).Should().BeTrue();
    }

    [Fact]
    public async Task Atualizar_GravaExtrasENegadas()
    {
        var usuario = await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);
        await CriarGerenteAsync();

        await _acesso.AtualizarAsync(
            usuario.Id, "Ana Souza", PerfilAcesso.Recepcao, null,
            extras: Permissao.VerFinanceiro, negadas: Permissao.EditarProntuario, ativo: true);

        var salvo = await _acesso.ObterAsync(usuario.Id);
        salvo!.Pode(Permissao.VerFinanceiro).Should().BeTrue();
        salvo.Pode(Permissao.EditarProntuario).Should().BeFalse();
    }

    // ===== O último gestor =====

    [Fact]
    public async Task Atualizar_NaoDeixaAClinicaSemNinguemQueGerencieAcessos()
    {
        var gerente = await CriarGerenteAsync();

        var acao = () => _acesso.AtualizarAsync(
            gerente.Id, "Direção", PerfilAcesso.Recepcao, null,
            Permissao.Nenhuma, Permissao.Nenhuma, ativo: true);

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*último usuário*");
    }

    [Fact]
    public async Task Atualizar_ComOutroGestorAtivo_Libera()
    {
        var primeiro = await CriarGerenteAsync("direcao");
        await CriarGerenteAsync("socio");

        await _acesso.AtualizarAsync(
            primeiro.Id, "Direção", PerfilAcesso.Recepcao, null,
            Permissao.Nenhuma, Permissao.Nenhuma, ativo: true);

        var salvo = await _acesso.ObterAsync(primeiro.Id);
        salvo!.Perfil.Should().Be(PerfilAcesso.Recepcao);
    }

    // ===== Autenticação =====

    [Fact]
    public async Task Autenticar_ComSenhaCerta_Entra()
    {
        await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);

        var r = await _acesso.AutenticarAsync("ANA", "segredo123");

        r.Sucesso.Should().BeTrue();
        r.Usuario!.Login.Should().Be("ana");
        r.Usuario.UltimoAcessoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Autenticar_LoginInexistenteESenhaErrada_DaoAMesmaResposta()
    {
        await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);

        var inexistente = await _acesso.AutenticarAsync("ninguem", "segredo123");
        var senhaErrada = await _acesso.AutenticarAsync("ana", "outra-coisa");

        // Distinguir os dois entregaria a lista de logins válidos a quem tentasse adivinhar.
        inexistente.Sucesso.Should().BeFalse();
        senhaErrada.Sucesso.Should().BeFalse();
        senhaErrada.Erro.Should().Be(inexistente.Erro);
    }

    [Fact]
    public async Task Autenticar_UsuarioDesativado_NaoEntra()
    {
        var usuario = await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);
        await CriarGerenteAsync();
        await _acesso.AtualizarAsync(
            usuario.Id, "Ana", PerfilAcesso.Recepcao, null,
            Permissao.Nenhuma, Permissao.Nenhuma, ativo: false);

        var r = await _acesso.AutenticarAsync("ana", "segredo123");

        r.Sucesso.Should().BeFalse();
    }

    [Fact]
    public async Task Autenticar_CincoErrosSeguidos_TravamOLogin()
    {
        await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);
        var agora = new DateTime(2026, 8, 3, 9, 0, 0);

        for (var i = 0; i < AcessoService.TentativasAteTravar; i++)
            await _acesso.AutenticarAsync("ana", "errada", agora);

        // Agora nem a senha CERTA entra: é o travamento, não a senha.
        var comSenhaCerta = await _acesso.AutenticarAsync("ana", "segredo123", agora);
        comSenhaCerta.Sucesso.Should().BeFalse();
        comSenhaCerta.Erro.Should().Contain("tentativas");
    }

    [Fact]
    public async Task Autenticar_DepoisDoTravamentoExpirar_VoltaAEntrar()
    {
        await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);
        var agora = new DateTime(2026, 8, 3, 9, 0, 0);

        for (var i = 0; i < AcessoService.TentativasAteTravar; i++)
            await _acesso.AutenticarAsync("ana", "errada", agora);

        var depois = agora.Add(AcessoService.DuracaoDoTravamento).AddMinutes(1);
        var r = await _acesso.AutenticarAsync("ana", "segredo123", depois);

        r.Sucesso.Should().BeTrue();
    }

    [Fact]
    public async Task Autenticar_AcertoZeraAsTentativas()
    {
        await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);

        await _acesso.AutenticarAsync("ana", "errada");
        await _acesso.AutenticarAsync("ana", "segredo123");

        var usuario = await _repo.ObterUsuarioPorLoginAsync("ana");
        usuario!.TentativasFalhas.Should().Be(0);
    }

    // ===== Troca de senha =====

    [Fact]
    public async Task TrocarSenha_ExigeASenhaAtual()
    {
        var usuario = await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);

        var acao = () => _acesso.TrocarSenhaAsync(usuario.Id, "chute", "novasenha1");

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DefinirSenha_ProvisoriaObrigaATrocarNoProximoAcesso()
    {
        var usuario = await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);

        await _acesso.DefinirSenhaAsync(usuario.Id, "provisoria1", deveTrocar: true);

        var r = await _acesso.AutenticarAsync("ana", "provisoria1");
        r.Sucesso.Should().BeTrue();
        r.Usuario!.DeveTrocarSenha.Should().BeTrue();
    }

    // ===== Exclusão =====

    [Fact]
    public async Task Excluir_UsuarioQueJaEntrou_EhRecusado()
    {
        await CriarGerenteAsync();
        var usuario = await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);
        await _acesso.AutenticarAsync("ana", "segredo123");

        var acao = () => _acesso.ExcluirAsync(usuario.Id);

        // Excluir apagaria o nome que a auditoria gravou nas ações antigas.
        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*desative*");
    }

    [Fact]
    public async Task Excluir_UsuarioQueNuncaEntrou_Funciona()
    {
        await CriarGerenteAsync();
        var usuario = await _acesso.CriarAsync("Ana", "ana", "segredo123", PerfilAcesso.Recepcao);

        await _acesso.ExcluirAsync(usuario.Id);

        (await _acesso.ObterAsync(usuario.Id)).Should().BeNull();
    }

    [Fact]
    public async Task ExisteUsuarioAtivo_BaseVazia_EhFalso()
    {
        // É o que decide se a abertura pede login ou oferece o primeiro acesso.
        (await _acesso.ExisteUsuarioAtivoAsync()).Should().BeFalse();

        await CriarGerenteAsync();

        (await _acesso.ExisteUsuarioAtivoAsync()).Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
