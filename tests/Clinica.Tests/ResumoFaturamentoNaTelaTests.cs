using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using FluentAssertions;

namespace Clinica.Tests;

/// <summary>O que o Resumo do faturamento (Blazor, out/2026) AFIRMA — antes no FaturamentoGerencialViewModel.</summary>
public class ResumoFaturamentoNaTelaTests
{
    private static RelatorioFaturamento Relatorio(ResumoFaturamento resumo,
        IReadOnlyList<FaturamentoPorConvenio>? porConvenio = null,
        IReadOnlyList<FaixaEnvelhecimento>? faixas = null)
        => new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 16), resumo,
            porConvenio ?? [], faixas ?? [], [], []);

    [Fact]
    public void Sem_guia_as_taxas_sao_travessao_e_a_barra_some()
    {
        var p = ResumoFaturamentoNaTela.Montar(Relatorio(new ResumoFaturamento(0, 0, 0, 0)), null, null, [], 0);
        p.TaxaBaixa.Should().Be("—");
        p.TaxaBaixaFracao.Should().BeNull("barra vazia ao lado do travessão afirmaria zero");
        p.TaxaGlosa.Should().Be("—");
        p.TempoMedio.Should().Be("—");
        p.MensagemVazio.Should().Be("Nenhuma guia com atendimento neste período.");
        p.VariacaoGuias.Should().BeNull();
    }

    [Fact]
    public void Taxas_em_ptBR_com_a_fracao_presa_entre_0_e_1()
    {
        var p = ResumoFaturamentoNaTela.Montar(
            Relatorio(new ResumoFaturamento(10, 8, 2, 80.5, Glosadas: 1, TaxaGlosa: 12.5, TempoMedioBaixaDias: 3.24)),
            null, null, [], 4);
        p.TaxaBaixa.Should().Be("80,5%");
        p.TaxaBaixaFracao.Should().BeApproximately(0.805, 1e-9);
        p.TaxaGlosa.Should().Be("12,5%");
        p.TempoMedio.Should().Be("3,2 dias");
        p.PendenciasEmAberto.Should().Be(4);
        p.MensagemVazio.Should().BeNull();
    }

    [Fact]
    public void Envelhecimento_traz_todas_as_faixas_com_a_fracao_do_total()
    {
        var p = ResumoFaturamentoNaTela.Montar(Relatorio(new ResumoFaturamento(4, 0, 4, 0), faixas:
            [new("Até 7 dias", 3), new("8 a 30 dias", 1), new("Mais de 30 dias", 0)]), null, null, [], 4);
        p.Envelhecimento.Should().HaveCount(3, "a faixa vazia também aparece");
        p.Envelhecimento[0].Fracao.Should().Be(0.75);
        p.Envelhecimento[0].ValorRotulo.Should().Be("3 · 75%");
        p.Envelhecimento[2].Fracao.Should().Be(0);
    }

    [Fact]
    public void Envelhecimento_sem_nada_em_aberto_nao_divide_por_zero()
    {
        var p = ResumoFaturamentoNaTela.Montar(Relatorio(new ResumoFaturamento(0, 0, 0, 0), faixas:
            [new("Até 7 dias", 0)]), null, null, [], 0);
        p.Envelhecimento[0].ValorRotulo.Should().Be("0");
        p.Envelhecimento[0].Fracao.Should().Be(0);
    }

    [Fact]
    public void Mes_sem_guia_e_buraco_na_curva_e_travessao_na_lista()
    {
        var meses = new List<ResumoMensal>
        {
            new(2026, 8, "ago/2026", 10, 9, 1, 90),
            new(2026, 9, "set/2026", 0, 0, 0, 0),
            new(2026, 10, "out/2026", 5, 4, 1, 80)
        };
        var p = ResumoFaturamentoNaTela.Montar(Relatorio(new ResumoFaturamento(5, 4, 1, 80)), null, null, meses, 1);
        p.Serie[1].Valor.Should().BeNull("um mês sem faturamento não é uma queda a 0%");
        p.Meses[1].TaxaBaixa.Should().Be("—");
        p.Meses[0].TaxaBaixa.Should().Be("90%");
        p.TemSerie.Should().BeTrue();
    }

    [Fact]
    public void Um_ponto_so_nao_e_curva()
    {
        var p = ResumoFaturamentoNaTela.Montar(Relatorio(new ResumoFaturamento(5, 4, 1, 80)), null, null,
            [new(2026, 10, "out/2026", 5, 4, 1, 80), new(2026, 9, "set/2026", 0, 0, 0, 0)], 1);
        p.TemSerie.Should().BeFalse();
    }

    [Fact]
    public void Linha_por_convenio_usa_o_nome_da_operadora_e_travessao_sem_base()
    {
        var p = ResumoFaturamentoNaTela.Montar(Relatorio(new ResumoFaturamento(3, 0, 3, 0), porConvenio:
            [new FaturamentoPorConvenio(Convenio.Amil, 3, 0, 3, 0) { ConvenioCodigo = "Amil" }]), null, null, [], 3);
        var linha = p.PorConvenio.Single();
        linha.Convenio.Should().NotBe("Personalizado");
        linha.TaxaBaixa.Should().Be("0%");
        linha.TaxaGlosa.Should().Be("—", "sem baixa não há base para glosa");
        linha.TempoMedio.Should().Be("—");
    }

    [Fact]
    public void Delta_compara_com_o_trecho_anterior_e_glosa_subindo_e_ruim()
    {
        var atual = Relatorio(new ResumoFaturamento(20, 10, 10, 50, TaxaGlosa: 20));
        var anterior = Relatorio(new ResumoFaturamento(10, 10, 0, 100, TaxaGlosa: 10));
        var trecho = (new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 16));
        var p = ResumoFaturamentoNaTela.Montar(atual, anterior, trecho, [], 0);
        p.VariacaoGuias!.Texto.Should().StartWith("↑");
        p.VariacaoTaxaGlosa!.Leitura.Should().Be(LeituraKpi.Ruim);
        p.VariacaoTaxaBaixa!.Leitura.Should().Be(LeituraKpi.Ruim);
        p.VariacaoGuias.Detalhe.Should().Contain("01/09/2026 a 16/09/2026");
    }

    [Fact]
    public void Periodos_terminam_hoje()
    {
        var hoje = new DateOnly(2026, 10, 16);
        ResumoFaturamentoNaTela.Intervalo(ResumoFaturamentoNaTela.EsteMes, hoje).Should().Be((new DateOnly(2026, 10, 1), hoje));
        ResumoFaturamentoNaTela.Intervalo(ResumoFaturamentoNaTela.UltimosTresMeses, hoje).Should().Be((new DateOnly(2026, 8, 1), hoje));
        ResumoFaturamentoNaTela.Intervalo(ResumoFaturamentoNaTela.EsteAno, hoje).Should().Be((new DateOnly(2026, 1, 1), hoje));
    }
}
