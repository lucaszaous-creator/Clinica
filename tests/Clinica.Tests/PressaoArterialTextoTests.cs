using Clinica.Application.Modelos;

namespace Clinica.Tests;

/// <summary>A PA num campo só (set/2026): o que a técnica digita e o que o serviço recebe.</summary>
public class PressaoArterialTextoTests
{
    [Theory]
    [InlineData("120/80", "120", "80")]
    [InlineData("120x80", "120", "80")]
    [InlineData("120X80", "120", "80")]
    [InlineData("120-80", "120", "80")]
    [InlineData("120 80", "120", "80")]
    [InlineData(" 130 / 85 ", "130", "85")]
    [InlineData("90/60", "90", "60")]
    public void Os_separadores_da_pratica_viram_dois_numeros(string texto, string s, string d)
        => Assert.Equal((s, d), PressaoArterialTexto.Separar(texto));

    [Theory]
    [InlineData("120", "120", "")]
    [InlineData("120/", "120", "")]
    public void Um_numero_so_e_a_sistolica(string texto, string s, string d)
        => Assert.Equal((s, d), PressaoArterialTexto.Separar(texto));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("12/8/5")]
    public void Lixo_e_vazio_devolvem_em_branco_nunca_chute(string? texto)
        => Assert.Equal((string.Empty, string.Empty), PressaoArterialTexto.Separar(texto));

    [Theory]
    [InlineData("120", "80", "120/80")]
    [InlineData("120", "", "120")]
    [InlineData("", "", "")]
    [InlineData("", "80", "/80")]
    public void O_caminho_de_volta(string s, string d, string esperado)
        => Assert.Equal(esperado, PressaoArterialTexto.Juntar(s, d));

    [Fact]
    public void Ida_e_volta_preserva_o_par()
    {
        var (s, d) = PressaoArterialTexto.Separar("120x80");
        Assert.Equal("120/80", PressaoArterialTexto.Juntar(s, d));
    }
}
