using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O número da guia no prestador para o envio pelo web service TISS: único por guia do sistema,
/// dentro do campo de 20 caracteres do padrão, sem repetir entre lotes — e com volta, para a
/// resposta da operadora achar a guia.
/// </summary>
public class NumeroGuiaPrestadorTissTests
{
    [Theory]
    [InlineData(1, "C1")]
    [InlineData(1234, "C1234")]
    [InlineData(int.MaxValue, "C2147483647")]
    public void Primeiro_envio_e_o_prefixo_mais_o_Id_do_codigo(int id, string esperado)
        => NumeroGuiaPrestadorTiss.Gerar(id).Should().Be(esperado);

    [Fact]
    public void Reenvio_ganha_sufixo_e_o_primeiro_envio_nao()
    {
        NumeroGuiaPrestadorTiss.Gerar(1234, reenvio: 1).Should().Be("C1234");
        NumeroGuiaPrestadorTiss.Gerar(1234, reenvio: 2).Should().Be("C1234R2");
        NumeroGuiaPrestadorTiss.Gerar(1234, reenvio: 15).Should().Be("C1234R15");
    }

    [Fact]
    public void Numero_cabe_no_campo_do_padrao_mesmo_no_maior_Id_com_muitos_reenvios()
    {
        var maior = NumeroGuiaPrestadorTiss.Gerar(int.MaxValue, reenvio: 99_999_999);

        maior.Should().Be("C2147483647R99999999");
        maior.Length.Should().Be(NumeroGuiaPrestadorTiss.TamanhoMaximo);
        var acao = () => NumeroGuiaPrestadorTiss.Gerar(int.MaxValue, reenvio: 100_000_000);
        acao.Should().Throw<ArgumentException>().WithMessage("*20 caracteres*");
    }

    [Fact]
    public void Numeros_nao_repetem_entre_guias_nem_entre_envios_da_mesma_guia()
    {
        // Dois lotes com guias diferentes, e o reenvio de uma delas: nenhum número coincide.
        var numeros = Enumerable.Range(1, 500).Select(id => NumeroGuiaPrestadorTiss.Gerar(id))
            .Concat(Enumerable.Range(1, 500).Select(id => NumeroGuiaPrestadorTiss.Gerar(id, reenvio: 2)))
            .Concat([NumeroGuiaPrestadorTiss.Gerar(12, reenvio: 3), NumeroGuiaPrestadorTiss.Gerar(123)])
            .ToList();

        // 123 aparece duas vezes de propósito (Range e avulso): a MESMA guia no MESMO envio é o mesmo número.
        numeros.Distinct().Should().HaveCount(numeros.Count - 1);
        // E "C12R3" não se confunde com "C123": a marca de reenvio separa o Id do envio.
        NumeroGuiaPrestadorTiss.Gerar(12, reenvio: 3).Should().NotBe(NumeroGuiaPrestadorTiss.Gerar(123));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Guia_ainda_nao_gravada_nao_ganha_numero(int id)
    {
        var acao = () => NumeroGuiaPrestadorTiss.Gerar(id);

        acao.Should().Throw<ArgumentException>().WithMessage("*Id do código*");
    }

    [Fact]
    public void Envio_zero_ou_negativo_e_recusado()
    {
        var acao = () => NumeroGuiaPrestadorTiss.Gerar(10, reenvio: 0);

        acao.Should().Throw<ArgumentException>().WithMessage("*começa em 1*");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1234, 1)]
    [InlineData(1234, 2)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MaxValue, 99_999_999)]
    public void A_volta_devolve_o_Id_e_o_envio_de_todo_numero_gerado(int id, int reenvio)
    {
        var achou = NumeroGuiaPrestadorTiss.TentarLer(NumeroGuiaPrestadorTiss.Gerar(id, reenvio), out var idLido, out var reenvioLido);

        achou.Should().BeTrue();
        idLido.Should().Be(id);
        reenvioLido.Should().Be(reenvio);
    }

    [Fact]
    public void A_volta_tolera_espaco_em_volta_como_a_operadora_pode_devolver()
    {
        NumeroGuiaPrestadorTiss.TentarLer("  C1234R2 ", out var id, out var reenvio).Should().BeTrue();

        id.Should().Be(1234);
        reenvio.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]            // guia numerada fora do sistema (portal)
    [InlineData("OP-777001")]       // número da operadora
    [InlineData("C")]               // prefixo sem Id
    [InlineData("C0")]              // Id nunca é zero
    [InlineData("C01234")]          // zero à esquerda não sai de Gerar
    [InlineData("C-5")]
    [InlineData("C12 34")]
    [InlineData("C1234R")]          // marca sem envio
    [InlineData("C1234R1")]         // o primeiro envio não leva sufixo
    [InlineData("C1234R0")]
    [InlineData("C1234R2R3")]
    [InlineData("c1234")]           // prefixo em minúscula não é o do sistema
    [InlineData("C99999999999")]    // não cabe num inteiro
    [InlineData("C2147483647R999999999")] // 21 caracteres
    public void Numero_que_nao_saiu_do_sistema_nao_e_lido_como_guia_do_sistema(string? numero)
    {
        var achou = NumeroGuiaPrestadorTiss.TentarLer(numero, out var id, out var reenvio);

        achou.Should().BeFalse();
        id.Should().Be(0);
        reenvio.Should().Be(0);
    }
}
