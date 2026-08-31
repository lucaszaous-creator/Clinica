using System.Globalization;
using Clinica.Application.Modelos;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O delta dos cartões de KPI (ago/2026). O que estes testes fixam é o que a TELA vai
/// AFIRMAR — a seta, a leitura boa/ruim/neutra e, principalmente, quando o delta NÃO
/// aparece: sem base de comparação a resposta é nulo, nunca "+∞%" nem "0%".
/// </summary>
public class VariacaoKpiTests
{
    [Fact]
    public void Sem_base_nao_ha_seta()
    {
        Assert.Null(VariacaoKpi.Relativa(12, null, "b", "d"));
        Assert.Null(VariacaoKpi.Relativa(12, 0, "b", "d"));
        Assert.Null(VariacaoKpi.EmPontos(10.0, null, "b", "d"));
        Assert.Null(VariacaoKpi.EmPontos(null, 10.0, "b", "d"));
        Assert.Null(VariacaoKpi.EmValor(null, 4.0, "pt", "b", "d"));
    }

    [Fact]
    public void Contagem_que_subiu_um_quarto_diz_25_por_cento()
    {
        var v = VariacaoKpi.Relativa(15, 12, "vs. mês anterior", "01/07 a 15/07");
        Assert.NotNull(v);
        Assert.Equal("↑ 25%", v!.Texto);
        Assert.Equal(LeituraKpi.Neutra, v.Leitura);
        Assert.Equal("vs. mês anterior", v.Rotulo);
        Assert.Equal("01/07 a 15/07", v.Detalhe);
    }

    [Fact]
    public void A_cor_e_da_metrica_e_nao_da_seta()
    {
        // Taxa de baixa subindo é BOA; taxa de glosa subindo é RUIM — a mesma seta.
        var baixa = VariacaoKpi.EmPontos(90.0, 80.0, "b", "d", melhorQuandoMenor: false);
        var glosa = VariacaoKpi.EmPontos(6.0, 4.0, "b", "d", melhorQuandoMenor: true);
        Assert.Equal(LeituraKpi.Boa, baixa!.Leitura);
        Assert.Equal(LeituraKpi.Ruim, glosa!.Leitura);

        // E caindo, os papéis se invertem.
        var baixaCaiu = VariacaoKpi.EmPontos(80.0, 90.0, "b", "d", melhorQuandoMenor: false);
        var glosaCaiu = VariacaoKpi.EmPontos(4.0, 6.0, "b", "d", melhorQuandoMenor: true);
        Assert.Equal(LeituraKpi.Ruim, baixaCaiu!.Leitura);
        Assert.Equal(LeituraKpi.Boa, glosaCaiu!.Leitura);
    }

    [Fact]
    public void Taxa_compara_em_pontos_percentuais_e_nunca_em_percentual_do_percentual()
    {
        // 10% -> 12% de falta: subiu 2 p.p. — dizer "+20%" leria como um quinto a mais.
        var v = VariacaoKpi.EmPontos(12.0, 10.0, "b", "d", melhorQuandoMenor: true);
        Assert.StartsWith("↑ 2", v!.Texto);
        Assert.EndsWith("p.p.", v.Texto);
    }

    [Fact]
    public void Variacao_zero_e_neutra_mesmo_em_metrica_com_direcao()
    {
        var v = VariacaoKpi.EmPontos(10.0, 10.0, "b", "d", melhorQuandoMenor: true);
        Assert.NotNull(v);
        Assert.StartsWith("=", v!.Texto);
        Assert.Equal(LeituraKpi.Neutra, v.Leitura);
    }

    [Fact]
    public void Valor_absoluto_sai_com_a_unidade()
    {
        var v = VariacaoKpi.EmValor(4.7, 4.1, "pt", "b", "d", melhorQuandoMenor: false);
        var esperado = string.Format(CultureInfo.CurrentCulture, "↑ {0:0.#} pt", 0.6);
        Assert.Equal(esperado, v!.Texto);
        Assert.Equal(LeituraKpi.Boa, v.Leitura);
    }
}

/// <summary>
/// O trecho anterior equivalente — a régua do delta. Cada caso aqui é um período que
/// alguma tela oferece de verdade.
/// </summary>
public class TrechoAnteriorTests
{
    [Fact]
    public void Mes_corrente_compara_com_o_mesmo_trecho_do_mes_anterior()
    {
        var t = TrechoAnterior.De(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15));
        Assert.Equal((new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15)), t);
    }

    [Fact]
    public void O_dia_do_fim_e_preso_ao_tamanho_do_mes_de_destino()
    {
        // 31/03 contra fevereiro cai no último dia dele — o clamp do painel da direção.
        var t = TrechoAnterior.De(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), t);
    }

    [Fact]
    public void Ultimos_tres_meses_comparam_com_os_tres_anteriores()
    {
        // A tela monta: 1º do mês -2 até hoje (01/06 → 20/08 cobre jun-jul-ago).
        var t = TrechoAnterior.De(new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 20));
        Assert.Equal((new DateOnly(2026, 3, 1), new DateOnly(2026, 5, 20)), t);
    }

    [Fact]
    public void Ano_corrente_compara_com_o_mesmo_trecho_do_ano_anterior()
    {
        // Sazonalidade: janeiro–agosto contra janeiro–agosto, nunca contra mai–dez.
        var t = TrechoAnterior.De(new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 20));
        Assert.Equal((new DateOnly(2025, 1, 1), new DateOnly(2025, 8, 20)), t);
    }

    [Fact]
    public void Ano_bissexto_29_02_cai_em_28_02_no_ano_anterior()
    {
        var t = TrechoAnterior.De(new DateOnly(2028, 1, 1), new DateOnly(2028, 2, 29));
        Assert.Equal((new DateOnly(2027, 1, 1), new DateOnly(2027, 2, 28)), t);
    }

    [Fact]
    public void Intervalo_corrido_desloca_pelo_numero_de_dias()
    {
        // "Últimos 90 dias": 23/05 → 20/08 (90 dias) compara com os 90 anteriores.
        var t = TrechoAnterior.De(new DateOnly(2026, 5, 23), new DateOnly(2026, 8, 20));
        Assert.Equal((new DateOnly(2026, 2, 22), new DateOnly(2026, 5, 22)), t);
    }

    [Fact]
    public void Intervalo_invertido_nao_tem_anterior()
    {
        Assert.Null(TrechoAnterior.De(new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 1)));
    }
}
