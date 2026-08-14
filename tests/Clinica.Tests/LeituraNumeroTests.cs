using Clinica.Domain;

namespace Clinica.Tests;

/// <summary>
/// O leitor do número digitado (parcela 68).
///
/// ⚠️ <b>Estes testes existem porque a regra estava num lugar onde eles não chegavam.</b> Ela
/// morava em <c>Clinica.Desktop.Controls.Valores</c>, projeto <c>net8.0-windows</c>, e por
/// isso passou parcelas lendo <c>"40.5"</c> como <b>405</b> com o CI verde — a tentativa
/// pt-BR NUNCA falha para texto com ponto (lá o ponto é separador de MILHAR), então o
/// caminho invariante que a documentação prometia era inalcançável.
///
/// O que estava em jogo não é digitação: é o percentual do repasse, a contagem da gaveta,
/// o valor da guia conciliada e o preço do pacote vendido no balcão. Todos liam o mesmo
/// método.
/// </summary>
public class LeituraNumeroTests
{
    // ---------------------------------------------------------------- o defeito
    // O caso exato que a auditoria da parcela 68 provou rodando `decimal.TryParse` numa
    // máquina pt-BR: os três davam certo, com o número errado.
    [Theory]
    [InlineData("50.00", 50)]     // era 5.000
    [InlineData("40.5", 40.5)]    // era 405 — o percentual do repasse
    [InlineData("3.9", 3.9)]      // era 39  — a taxa da maquininha
    [InlineData("1250.00", 1250)] // era 125.000
    [InlineData("12.50", 12.5)]   // era 1.250 — o custo do insumo
    public void Ponto_como_separador_decimal_e_lido_como_decimal(string texto, decimal esperado)
    {
        Assert.True(LeituraNumero.TentarLer(texto, out var valor));
        Assert.Equal(esperado, valor);
    }

    // ------------------------------------------------- o jeito que o Brasil escreve
    [Theory]
    [InlineData("1.250,00", 1250)]
    [InlineData("40,5", 40.5)]
    [InlineData("0,5", 0.5)]
    [InlineData("1.234.567,89", 1234567.89)]
    [InlineData("R$ 1.250,00", 1250)]
    [InlineData("R$1250", 1250)]
    public void Formato_pt_BR_continua_valendo(string texto, decimal esperado)
    {
        Assert.True(LeituraNumero.TentarLer(texto, out var valor));
        Assert.Equal(esperado, valor);
    }

    // --------------------------------------------------------- o formato invariante
    [Theory]
    [InlineData("1,250.00", 1250)]
    [InlineData("1,234,567.89", 1234567.89)]
    public void Formato_invariante_tambem(string texto, decimal esperado)
    {
        Assert.True(LeituraNumero.TentarLer(texto, out var valor));
        Assert.Equal(esperado, valor);
    }

    // ------------------------------------------------------ o único julgamento da regra
    // Ponto único com TRÊS dígitos atrás é milhar — é como o Brasil escreve mil duzentos
    // e cinquenta, e ninguém digita três casas decimais num campo de dinheiro. A exceção
    // é a parte inteira ser só "0", que é como se escreve fração no formato invariante.
    [Theory]
    [InlineData("1.250", 1250)]
    [InlineData("50.000", 50000)]
    [InlineData("0.500", 0.5)]
    [InlineData("0.5", 0.5)]
    [InlineData("1.2345", 1.2345)]
    public void Ponto_unico_com_tres_digitos_e_milhar_salvo_quando_o_inteiro_e_zero(
        string texto, decimal esperado)
    {
        Assert.True(LeituraNumero.TentarLer(texto, out var valor));
        Assert.Equal(esperado, valor);
    }

    // ------------------------------------------------------------------ não é número
    // Leitura PARCIAL é o pior desfecho possível: "6x" virando 6 salvaria a taxa valendo
    // para um parcelamento que ninguém escolheu. Ou é número inteiro, ou é recusa.
    [Theory]
    [InlineData("6x")]
    [InlineData("12/03")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("1.2,3.4")]
    [InlineData("-")]
    public void Texto_que_nao_e_numero_e_recusado(string? texto)
    {
        Assert.False(LeituraNumero.TentarLer(texto, out var valor));
        Assert.Equal(0m, valor);
    }

    // ------------------------------------------------------------- sinal e piso
    // O leitor não opina sobre sinal nem sobre piso: quem decide se zero vale é o campo.
    // Zero é engano no percentual do repasse e é a cortesia na venda do pacote.
    [Fact]
    public void O_leitor_nao_decide_piso_nem_sinal()
    {
        Assert.True(LeituraNumero.TentarLer("0", out var zero));
        Assert.Equal(0m, zero);

        Assert.True(LeituraNumero.TentarLer("-25,50", out var negativo));
        Assert.Equal(-25.5m, negativo);
    }

    // ------------------------------------------------- independente da cultura da máquina
    // A prova de que o defeito não volta: a mesma entrada tem de dar o mesmo número com o
    // Windows em português, em inglês ou em alemão (onde o ponto é milhar como no Brasil).
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("")]
    public void A_leitura_nao_depende_da_cultura_da_maquina(string cultura)
    {
        var anterior = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo(cultura);

            Assert.True(LeituraNumero.TentarLer("40.5", out var ponto));
            Assert.Equal(40.5m, ponto);

            Assert.True(LeituraNumero.TentarLer("1.250,00", out var brasileiro));
            Assert.Equal(1250m, brasileiro);

            Assert.True(LeituraNumero.TentarLer("1,250.00", out var invariante));
            Assert.Equal(1250m, invariante);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = anterior;
        }
    }
}
