using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O serviço do segundo fator do portal (nov/2026): o que a API promete na entrada e que a tela
/// não enxerga — confiança presa à senha, travamento que soma código e recuperação, e trilha.
/// </summary>
public sealed class SegundoFatorPortalServiceTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly SegundoFatorPortalService _svc;
    private readonly UsuarioSistema _u = new() { Nome = "Rita", Login = "rita", SenhaHash = "hash-1", SenhaSalt = "s", Perfil = PerfilAcesso.Enfermagem };

    private sealed class SemCifra : IProtecaoDoSegredo
    {
        public string Proteger(byte[] segredo) => Convert.ToBase64String(segredo);
        public byte[] Abrir(string protegido) => Convert.FromBase64String(protegido);
    }

    public SegundoFatorPortalServiceTests()
    {
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _db.Usuarios.Add(_u);
        _db.SaveChanges();
        _svc = new SegundoFatorPortalService(_db, new SemCifra(), TimeProvider.System).Para(_u);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    private AdministracaoDoSegundoFator Admin() => new(_db, TimeProvider.System);

    private async Task<(byte[] Segredo, IReadOnlyList<string> Recuperacao)> Cadastrar()
    {
        await Admin().LiberarPrimeiroAcessoAsync(_u.Id, "direcao");
        var segredo = Totp.NovoSegredo();
        var codigos = await _svc.CadastrarAsync(_u.Id, segredo, Totp.Codigo(segredo, Totp.Passo(DateTimeOffset.UtcNow)), default);
        Assert.NotNull(codigos);
        return (segredo, codigos!);
    }

    [Fact]
    public async Task Trocar_a_senha_desfaz_a_confianca_no_aparelho()
    {
        await Cadastrar();
        await _svc.ConfiarAsync(_u, "aparelho-1", "203.0.113.9", default);
        Assert.True(await _svc.ConfiavelAsync(_u, "aparelho-1", default));
        Assert.False(await _svc.ConfiavelAsync(_u, "aparelho-2", default)); // outro aparelho
        _u.SenhaHash = "hash-2"; // a senha mudou
        Assert.False(await _svc.ConfiavelAsync(_u, "aparelho-1", default));
    }

    [Fact]
    public async Task Erro_de_codigo_e_de_recuperacao_somam_no_mesmo_travamento()
    {
        var (segredo, recuperacao) = await Cadastrar();
        for (var i = 0; i < SegundoFatorPortalService.FalhasAteTravar / 2; i++)
            Assert.False(await _svc.VerificarAsync(_u.Id, "000000", default));
        for (var i = 0; i < SegundoFatorPortalService.FalhasAteTravar / 2; i++)
            Assert.False(await _svc.UsarRecuperacaoAsync(_u.Id, "aaaaa-bbbbb", default));
        // Travado: nem o código de recuperação certo nem o código certo entram.
        Assert.False(await _svc.UsarRecuperacaoAsync(_u.Id, recuperacao[0], default));
        Assert.False(await _svc.VerificarAsync(_u.Id, Totp.Codigo(segredo, Totp.Passo(DateTimeOffset.UtcNow) + 1), default));
        Assert.Contains(_db.Auditoria, a => a.Acao == "PortalSegundoFatorTravado" && a.Operador == "rita");

        // A direção libera de novo (zera): o próximo login cadastra de novo.
        await Admin().LiberarPrimeiroAcessoAsync(_u.Id, "direcao");
        Assert.False(await _svc.TemAutenticadorAsync(_u.Id, default));
        Assert.Empty(_db.CodigosRecuperacaoPortal.Where(c => c.UsuarioId == _u.Id));
    }

    [Fact]
    public async Task Recuperacao_vale_uma_vez_e_o_banco_guarda_so_o_resumo()
    {
        var (_, recuperacao) = await Cadastrar();
        Assert.DoesNotContain(_db.CodigosRecuperacaoPortal, c => recuperacao.Contains(c.Resumo));
        Assert.True(await _svc.UsarRecuperacaoAsync(_u.Id, " " + recuperacao[3].ToUpperInvariant() + " ", default));
        Assert.False(await _svc.UsarRecuperacaoAsync(_u.Id, recuperacao[3], default));
        Assert.True(await _svc.UsarRecuperacaoAsync(_u.Id, recuperacao[4], default));
    }

    [Fact]
    public async Task Sem_a_liberacao_da_direcao_a_senha_nao_cadastra_o_autenticador()
    {
        var segredo = Totp.NovoSegredo();
        Assert.Null(await _svc.CadastrarAsync(_u.Id, segredo, Totp.Codigo(segredo, Totp.Passo(DateTimeOffset.UtcNow)), default));
        Assert.False(await _svc.TemAutenticadorAsync(_u.Id, default));
    }

    [Fact]
    public async Task Liberacao_vale_uma_vez_e_cinco_erros_a_invalidam()
    {
        var codigo = await Admin().LiberarPrimeiroAcessoAsync(_u.Id, "direcao");
        Assert.Equal(SituacaoDoSegundoFator.Liberado, (await Admin().SituacoesAsync())[_u.Id]);
        Assert.True(await _svc.ConferirLiberacaoAsync(_u.Id, codigo, default));
        var segredo = Totp.NovoSegredo();
        Assert.NotNull(await _svc.CadastrarAsync(_u.Id, segredo, Totp.Codigo(segredo, Totp.Passo(DateTimeOffset.UtcNow)), default));
        Assert.False(await _svc.ConferirLiberacaoAsync(_u.Id, codigo, default)); // consumida no cadastro
        Assert.Equal(SituacaoDoSegundoFator.Ativo, (await Admin().SituacoesAsync())[_u.Id]);

        var outro = await Admin().LiberarPrimeiroAcessoAsync(_u.Id, "direcao"); // celular perdido: zera e libera
        Assert.False(await _svc.TemAutenticadorAsync(_u.Id, default));
        for (var i = 0; i < SegundoFatorPortalService.FalhasDaLiberacao; i++)
            Assert.False(await _svc.ConferirLiberacaoAsync(_u.Id, "aaaaa-bbbbb", default));
        Assert.False(await _svc.ConferirLiberacaoAsync(_u.Id, outro, default)); // invalidada
        Assert.Contains(_db.Auditoria, a => a.Acao == "PortalLiberacaoInvalidada");
    }

    [Fact]
    public async Task Cadastro_com_codigo_errado_nao_grava_nada()
    {
        await Admin().LiberarPrimeiroAcessoAsync(_u.Id, "direcao");
        var segredo = Totp.NovoSegredo();
        Assert.Null(await _svc.CadastrarAsync(_u.Id, segredo, "000000", default));
        Assert.False(await _svc.TemAutenticadorAsync(_u.Id, default));
        Assert.Empty(_db.CodigosRecuperacaoPortal);
    }
}
