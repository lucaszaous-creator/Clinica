using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// As frases e decisões das telas de Repasses, Resultado do mês e Produção, que subiram
/// do ViewModel WPF para a Application na migração para Blazor (out/2026). Os testes rodam
/// sob a cultura INVARIANTE de propósito: é a cultura da prévia em Linux, e o pt-BR tem de
/// sair fixo mesmo assim.
/// </summary>
public class LeiturasRepasseResultadoProducaoTests
{
    private static T SobInvariante<T>(Func<T> f)
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            return f();
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }

    private static RepasseCalculado Calc(decimal valor, bool temRegra = true, bool jaApurado = false) =>
        new(1, "Dra. Helena", 3, 540m, valor, "40% da receita", temRegra, jaApurado);

    // ==================== Repasses ====================

    [Fact]
    public void Apurar_so_acende_com_regra_sem_apuracao_e_valor()
    {
        LeituraDosRepasses.PodeApurar(Calc(100m)).Should().BeTrue();
        LeituraDosRepasses.PodeApurar(Calc(0m)).Should().BeFalse("repasse zerado não tem o que pagar");
        LeituraDosRepasses.PodeApurar(Calc(100m, temRegra: false)).Should().BeFalse();
        LeituraDosRepasses.PodeApurar(Calc(100m, jaApurado: true)).Should().BeFalse("apurar duas vezes é pagar duas vezes");
    }

    [Fact]
    public void Resumo_conta_quem_esta_sem_regra_e_sai_em_reais()
    {
        var resumo = SobInvariante(() => LeituraDosRepasses.Resumo([Calc(1234.5m), Calc(0m, temRegra: false)]));
        resumo.Should().Be("2 profissional(is) com atendimento no período · R$ 1.234,50 a repassar · 1 sem regra cadastrada");
    }

    [Fact]
    public void Resumo_sem_profissional_sem_regra_nao_menciona_o_cadastro_que_falta()
        => SobInvariante(() => LeituraDosRepasses.Resumo([Calc(10m)])).Should().NotContain("sem regra");

    [Fact]
    public void Totais_somam_o_periodo()
    {
        var t = LeituraDosRepasses.Totais([Calc(100m), Calc(50m, jaApurado: true), Calc(0m, temRegra: false)]);
        t.Should().Be(new TotaisDosRepasses(3, 150m, 1, 1, 1));
    }

    [Theory]
    [InlineData(null, null, "sempre")]
    [InlineData("2026-01-01", null, "a partir de 01/01/2026")]
    [InlineData(null, "2026-06-30", "até 30/06/2026")]
    [InlineData("2026-01-01", "2026-06-30", "01/01/2026 a 30/06/2026")]
    public void Vigencia_por_extenso(string? de, string? ate, string esperado)
        => LeituraDosRepasses.Vigencia(de is null ? null : DateOnly.Parse(de, CultureInfo.InvariantCulture),
            ate is null ? null : DateOnly.Parse(ate, CultureInfo.InvariantCulture)).Should().Be(esperado);

    [Fact]
    public void Apuracao_cancelada_diz_quando()
    {
        LeituraDosRepasses.SituacaoDaApuracao(new RepasseApurado()).Should().Be("Apurado");
        LeituraDosRepasses.SituacaoDaApuracao(new RepasseApurado { CanceladoEm = new DateTime(2026, 9, 12, 10, 0, 0) })
            .Should().Be("Cancelado em 12/09/2026");
    }

    [Fact]
    public void Descricao_da_regra_e_gravada_em_pt_BR_fixo()
    {
        // O texto é GRAVADO na apuração e na saída do caixa: a cultura da máquina não pode mudá-lo.
        SobInvariante(() => new RegraRepasse { Base = BaseRepasse.ValorPorAtendimento, ValorPorAtendimento = 60m }.Descricao)
            .Should().Be("R$ 60,00 por atendimento");
        SobInvariante(() => new RegraRepasse { Base = BaseRepasse.PercentualDaReceita, Percentual = 37.5m }.Descricao)
            .Should().Be("37,5% da receita");
    }

    // ==================== Resultado do mês ====================

    private static ResultadoMensal Mes(decimal receita, decimal deducoes, decimal despesas) =>
        new(2026, 9, receita, deducoes, despesas, [], []);

    [Fact]
    public void Resumo_do_mes_nas_tres_formas()
    {
        SobInvariante(() => LeituraDoResultado.Resumo(Mes(0m, 0m, 0m))).Should().Be("Nenhum movimento realizado neste mês.");
        SobInvariante(() => LeituraDoResultado.Resumo(Mes(1000m, 0m, 1500m))).Should().Be("O mês fechou R$ 500,00 no vermelho.");
        SobInvariante(() => LeituraDoResultado.Resumo(Mes(1000m, 50m, 700m))).Should().Be("Sobraram R$ 250,00 — 25% do faturado.");
    }

    [Fact]
    public void Mes_so_com_despesa_fecha_no_vermelho_sem_inventar_margem()
        => SobInvariante(() => LeituraDoResultado.Resumo(Mes(0m, 0m, 100m)))
            .Should().Be("O mês fechou R$ 100,00 no vermelho.");

    [Fact]
    public void Margem_sobre_zero_e_traco_nunca_zero_por_cento()
    {
        LeituraDoResultado.Margem(null).Should().Be("—");
        SobInvariante(() => LeituraDoResultado.Margem(12.5)).Should().Be("12,5%");
        Mes(0m, 0m, 100m).Margem.Should().BeNull();
    }

    [Fact]
    public void Teto_estourado_projetado_e_usado()
    {
        var estourou = new OrcamentoApurado(1, "Insumos", 1000m, 1200m, null);
        var vaiEstourar = new OrcamentoApurado(2, "Aluguel", 1000m, 500m, 800m);
        var normal = new OrcamentoApurado(3, "Marketing", 1000m, 250m, null);
        var tetoZero = new OrcamentoApurado(4, "Brindes", 0m, 0m, null);

        SobInvariante(() => LeituraDoResultado.Situacao(estourou)).Should().Be("estourou em R$ 200,00");
        SobInvariante(() => LeituraDoResultado.Situacao(vaiEstourar)).Should().Be("o já contratado leva a R$ 1.300,00 — passa do teto");
        SobInvariante(() => LeituraDoResultado.Situacao(normal)).Should().Be("25% do teto usado");
        LeituraDoResultado.Situacao(tetoZero).Should().Be("sem teto para comparar");

        LeituraDoResultado.Gravidade(estourou).Should().Be(GravidadeDoTeto.Estourou);
        LeituraDoResultado.Gravidade(normal).Should().Be(GravidadeDoTeto.Normal);
        LeituraDoResultado.Fracao(estourou).Should().Be(1.0, "a barra enche e para — nunca passa do trilho");
        LeituraDoResultado.Fracao(normal).Should().BeApproximately(0.25, 0.0001);
    }

    [Fact]
    public void Resumo_dos_tetos_diz_o_que_se_perde_sem_teto()
    {
        LeituraDoResultado.ResumoDosTetos([]).Should().StartWith("Nenhum teto definido");
        LeituraDoResultado.ResumoDosTetos([new OrcamentoApurado(1, "A", 10m, 5m, null)])
            .Should().Be("1 categoria(s) com teto — nenhuma estourou.");
        LeituraDoResultado.ResumoDosTetos([new OrcamentoApurado(1, "A", 10m, 50m, null), new OrcamentoApurado(2, "B", 10m, 5m, null)])
            .Should().Be("1 de 2 categoria(s) passaram do teto.");
    }

    // ==================== Produção ====================

    private static ResumoMensal Prod(int total, int baixados, int pendentes, double taxa) =>
        new(2026, 9, "set/2026", total, baixados, pendentes, taxa);

    [Fact]
    public void Producao_soma_e_taxa_sem_base_e_nula()
    {
        var t = LeituraDaProducao.Somar([Prod(10, 8, 1, 80), Prod(0, 0, 0, 0)]);
        t.Should().Be(new TotaisDaProducao(10, 8, 1));
        SobInvariante(() => LeituraDaProducao.Taxa(t)).Should().Be("80,0%");
        LeituraDaProducao.Taxa(new TotaisDaProducao(0, 0, 0)).Should().Be("—");
    }

    [Fact]
    public void Taxa_do_mes_ja_vem_em_cem_e_mes_vazio_e_traco()
    {
        SobInvariante(() => LeituraDaProducao.TaxaDoMes(Prod(40, 37, 3, 92.5))).Should().Be("92,5%", "não multiplica por 100 de novo");
        LeituraDaProducao.TaxaDoMes(Prod(0, 0, 0, 0)).Should().Be("—");
    }

    [Fact]
    public void Nao_conformidade_e_o_que_sobra_do_total()
        => LeituraDaProducao.NaoConformidades(Prod(10, 6, 3, 60)).Should().Be(1);
}
