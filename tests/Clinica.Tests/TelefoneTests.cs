using Clinica.Domain;
using FluentAssertions;

namespace Clinica.Tests;

public class TelefoneTests
{
    [Theory]
    [InlineData("22996105104", "(22) 99610-5104")]
    [InlineData("2226651234", "(22) 2665-1234")]
    [InlineData("996105104", "99610-5104")]
    [InlineData("26651234", "2665-1234")]
    [InlineData("(22) 99610-5104", "(22) 99610-5104")]
    public void Formatar_PadraoBrasileiro(string entrada, string esperado)
        => Telefone.Formatar(entrada).Should().Be(esperado);

    [Fact]
    public void Formatar_ForaDoPadrao_DevolveComoDigitado()
        => Telefone.Formatar("123").Should().Be("123");

    [Fact]
    public void Normalizar_SoDigitos()
        => Telefone.Normalizar("(22) 99610-5104").Should().Be("22996105104");
}
