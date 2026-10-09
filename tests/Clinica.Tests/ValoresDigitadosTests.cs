using System.Globalization;
using Clinica.Desktop.Controls;
using Xunit;

namespace Clinica.Tests;

public class ValoresDigitadosTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    public void Dinheiro_e_quantidade_preservam_a_fracao_digitada_em_qualquer_cultura(string cultura)
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultura);
            foreach (var (texto, esperado) in new (string, decimal)[]
            {
                ("12.5", 12.5m), ("12,5", 12.5m), ("0.125", 0.125m),
                ("1250.00", 1250m), ("1.250,00", 1250m), ("1,250.00", 1250m),
                ("1.234.567,89", 1234567.89m), ("1,234,567.89", 1234567.89m),
                ("1.234.567", 1234567m), ("1,234,567", 1234567m)
            })
            {
                Assert.True(Valores.TentarLerDecimal(texto, out var dinheiro), texto);
                Assert.Equal(esperado, dinheiro);
                Assert.True(Valores.TentarLerQuantidade(texto, out var quantidade), texto);
                Assert.Equal(esperado, quantidade);
            }
            Assert.True(Valores.TentarLerDecimal("R$ 1.250,00", out var reais));
            Assert.Equal(1250m, reais);
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("-12.5")]
    [InlineData("-12,5")]
    [InlineData("abc")]
    public void Dinheiro_continua_exigindo_valor_positivo(string? texto)
        => Assert.False(Valores.TentarLerDecimal(texto, out _));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    public void Quantidade_vazia_ou_zero_continua_sem_consumo(string? texto)
    {
        Assert.True(Valores.TentarLerQuantidade(texto, out var quantidade));
        Assert.Equal(0m, quantidade);
    }

    [Theory]
    [InlineData("-12.5")]
    [InlineData("-12,5")]
    [InlineData("abc")]
    public void Quantidade_negativa_ou_invalida_e_recusada(string texto)
        => Assert.False(Valores.TentarLerQuantidade(texto, out _));
}
