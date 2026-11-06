using System.Text;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;

namespace Clinica.Tests;

/// <summary>O TOTP do portal contra os vetores da RFC 6238 e a regra de destino do login (nov/2026).</summary>
public class TotpTests
{
    private static readonly byte[] SegredoRfc = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    // RFC 6238, apêndice B (SHA1): os 6 últimos dígitos dos códigos de 8.
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Codigos_batem_com_a_rfc(long segundos, string esperado)
        => Assert.Equal(esperado, Totp.Codigo(SegredoRfc, segundos / 30));

    [Fact]
    public void Aceita_um_passo_de_folga_e_recusa_reutilizar()
    {
        var agora = DateTimeOffset.FromUnixTimeSeconds(1234567890);
        var passo = Totp.Passo(agora);
        Assert.Equal(passo, Totp.Verificar(SegredoRfc, Totp.Codigo(SegredoRfc, passo), agora, 0));
        Assert.Equal(passo - 1, Totp.Verificar(SegredoRfc, Totp.Codigo(SegredoRfc, passo - 1), agora, 0));
        Assert.Equal(passo + 1, Totp.Verificar(SegredoRfc, Totp.Codigo(SegredoRfc, passo + 1), agora, 0));
        Assert.Null(Totp.Verificar(SegredoRfc, Totp.Codigo(SegredoRfc, passo + 2), agora, 0));
        // O mesmo passo (ou um anterior) já aceito não entra de novo.
        Assert.Null(Totp.Verificar(SegredoRfc, Totp.Codigo(SegredoRfc, passo), agora, passo));
        Assert.Null(Totp.Verificar(SegredoRfc, Totp.Codigo(SegredoRfc, passo - 1), agora, passo - 1));
    }

    [Theory]
    [InlineData("123 456", "123456")]
    [InlineData("123-456", "123456")]
    [InlineData("12345", null)]
    [InlineData("12345a", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normaliza_o_que_a_pessoa_digita(string? entrada, string? esperado)
        => Assert.Equal(esperado, Totp.Normalizar(entrada));

    [Fact]
    public void Base32_e_endereco_otpauth()
    {
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Totp.Base32(SegredoRfc));
        var uri = Totp.Endereco("Clínica SemDor", "ana", SegredoRfc);
        Assert.StartsWith("otpauth://totp/Cl%C3%ADnica%20SemDor:ana?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", uri);
        Assert.Contains("digits=6", uri);
    }

    [Fact]
    public void Codigos_de_recuperacao_sao_dez_distintos_e_o_resumo_e_por_usuario()
    {
        var c = Totp.NovosCodigosDeRecuperacao();
        Assert.Equal(10, c.Distinct().Count());
        Assert.All(c, x => Assert.Matches("^[a-z2-7]{5}-[a-z2-7]{5}$", x));
        Assert.Equal(Totp.ResumoRecuperacao(1, c[0]), Totp.ResumoRecuperacao(1, c[0].ToUpperInvariant().Replace("-", " ")));
        Assert.NotEqual(Totp.ResumoRecuperacao(1, c[0]), Totp.ResumoRecuperacao(2, c[0]));
    }

    [Fact]
    public void So_o_perfil_gerente_vai_para_a_gestao()
    {
        UsuarioSistema U(PerfilAcesso p, bool ativo = true, bool trocar = false)
            => new() { Id = 1, Login = "x", Perfil = p, Ativo = ativo, DeveTrocarSenha = trocar };

        Assert.Equal(["gestao"], AcessoAoPortal.Destinos(U(PerfilAcesso.Gerente), podePosto: false));
        Assert.Equal(["posto", "gestao"], AcessoAoPortal.Destinos(U(PerfilAcesso.Gerente), podePosto: true));
        Assert.Equal(["posto"], AcessoAoPortal.Destinos(U(PerfilAcesso.Enfermagem), podePosto: true));
        Assert.Empty(AcessoAoPortal.Destinos(U(PerfilAcesso.Financeiro), podePosto: false));
        Assert.False(AcessoAoPortal.PodeGestao(U(PerfilAcesso.Gerente, ativo: false)));
        Assert.False(AcessoAoPortal.PodeGestao(U(PerfilAcesso.Gerente, trocar: true)));
    }
}
