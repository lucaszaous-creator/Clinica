using Clinica.Domain;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    public void Valido_AceitaCpfsCorretos(string cpf)
        => Cpf.Valido(cpf).Should().BeTrue();

    [Theory]
    [InlineData("529.982.247-24")] // dígito errado
    [InlineData("111.111.111-11")] // todos iguais
    [InlineData("123")]            // curto
    [InlineData("")]               // vazio
    [InlineData(null)]
    public void Valido_RejeitaCpfsIncorretos(string? cpf)
        => Cpf.Valido(cpf).Should().BeFalse();

    [Fact]
    public void Normalizar_MantemSoDigitos()
        => Cpf.Normalizar("529.982.247-25").Should().Be("52998224725");

    [Fact]
    public void Formatar_AplicaMascara()
        => Cpf.Formatar("52998224725").Should().Be("529.982.247-25");
}
