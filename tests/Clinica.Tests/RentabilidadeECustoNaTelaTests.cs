using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as telas de Rentabilidade por convênio e de Custo de taxas e impostos AFIRMAM (Blazor
/// desde out/2026, etapa 6) — antes no <c>RentabilidadeConvenioViewModel</c> e no
/// <c>CustoTransacaoViewModel</c> do WPF.
/// </summary>
public class RentabilidadeECustoNaTelaTests
{
    private static readonly (DateOnly, DateOnly) Periodo = (new DateOnly(2026, 5, 1), new DateOnly(2026, 10, 31));

    private static RentabilidadeConvenio Conv(string nome, int guias, int comReceita, decimal bruto, decimal retido,
        int glosadas = 0, double? prazo = null, double fracao = 0)
        => new(nome, nome, guias, comReceita, bruto, retido, glosadas, prazo) { Fracao = fracao };

    [Fact]
    public void Janela_inclui_o_mes_corrente_inteiro()
    {
        var p = RentabilidadeNaTela.Intervalo(new DateOnly(2026, 10, 18), 6);
        p.Should().Be((new DateOnly(2026, 5, 1), new DateOnly(2026, 10, 31)));
        RentabilidadeNaTela.RotuloDoPeriodo(p).Should().Be("05/2026 a 10/2026");
        RentabilidadeNaTela.Intervalo(new DateOnly(2026, 10, 18), 1).Should().Be((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Linha_nao_inventa_zero_nem_media()
    {
        var l = RentabilidadeNaTela.LinhaDe(Conv("Amil", 3, 0, 0m, 0m));
        l.Retido.Should().Be("—");
        l.PorGuia.Should().Be("—", "média sobre nenhuma guia recebida é invenção");
        l.Prazo.Should().Be("—");
        l.Pendencias.Should().Be("3 sem receita");
        l.TemPendencia.Should().BeTrue();
    }

    [Fact]
    public void Pendencias_juntam_sem_receita_e_glosa()
    {
        RentabilidadeNaTela.Pendencias(Conv("X", 4, 4, 400m, 0m)).Should().Be("—");
        RentabilidadeNaTela.Pendencias(Conv("X", 4, 4, 400m, 0m, glosadas: 1)).Should().Be("1 glosada(s)");
        RentabilidadeNaTela.Pendencias(Conv("X", 4, 2, 200m, 0m, glosadas: 1)).Should().Be("2 sem receita · 1 glosada(s)");
    }

    [Fact]
    public void Comparacao_so_com_duas_operadoras_de_liquido_diferente()
    {
        var uma = RentabilidadeNaTela.Montar([Conv("Unimed", 2, 2, 300m, 0m)], Periodo);
        uma.MelhorEPior.Should().BeNull();

        // Empate no líquido por guia: o serviço devolve a MESMA operadora nas duas pontas.
        var empate = RentabilidadeNaTela.Montar([Conv("Unimed", 2, 2, 290m, 0m), Conv("Amil", 1, 1, 145m, 0m)], Periodo);
        empate.MelhorEPior.Should().BeNull("«rende mais: X · rende menos: X» não diz nada");

        var duas = RentabilidadeNaTela.Montar([Conv("Unimed", 2, 2, 400m, 0m), Conv("Amil", 1, 1, 120m, 0m)], Periodo);
        duas.MelhorEPior.Should().Be("Rende mais por atendimento: Unimed (R$ 200,00/guia) · rende menos: Amil (R$ 120,00/guia)");
    }

    [Fact]
    public void Resumo_diz_quantos_tem_guia_sem_receita()
    {
        RentabilidadeNaTela.Montar([], Periodo).Resumo.Should().Be("Nenhuma guia no período.");
        RentabilidadeNaTela.Montar([Conv("A", 2, 2, 200m, 0m)], Periodo).Resumo.Should().Be("1 convênio(s) no período.");
        RentabilidadeNaTela.Montar([Conv("A", 2, 1, 200m, 0m), Conv("B", 1, 1, 90m, 0m)], Periodo).Resumo
            .Should().Be("2 convênio(s) · 1 com guia efetivada e SEM receita lançada.");
    }

    [Fact]
    public void Csv_da_rentabilidade_mantem_os_cabecalhos_do_wpf()
    {
        var painel = RentabilidadeNaTela.Montar([Conv("Unimed", 2, 2, 300m, 13.95m, prazo: 30)], Periodo);
        var (cab, linhas) = RentabilidadeNaTela.Csv(painel);
        cab.Should().Equal("Convenio", "Guias", "Bruto", "Retido", "Liquido", "Liquido/guia", "Prazo medio", "Pendencias");
        linhas.Single().Should().Equal("Unimed", "2", "R$ 300,00", "R$ 13,95", "R$ 286,05", "R$ 143,02", "30 dias", "—");
        painel.PercentualRetido.Should().Be("4,65%");
    }

    // ==================== Custo de taxas e impostos ====================

    [Fact]
    public void Comparacao_com_a_tabela_tem_os_quatro_casos()
    {
        CustoTransacaoNaTela.Comparacao(null, 3.9m).Should().Be("efetiva 3,9% · sem taxa cadastrada para comparar");
        CustoTransacaoNaTela.Comparacao((3.1m, 3.1m), 3.9m).Should().Be("efetiva 3,9% · tabela 3,1%");
        CustoTransacaoNaTela.Comparacao((1.39m, 5.1m), 3.13m).Should().Be("efetiva 3,13% · tabela 1,39% a 5,1%");
        CustoTransacaoNaTela.Comparacao((3m, 3m), null).Should().Be("sem base para medir");
    }

    [Fact]
    public void Acima_da_tabela_so_quando_passa_do_TETO()
    {
        CustoTransacaoNaTela.AcimaDaTabela((2.5m, 2.5m), 3.19m).Should().BeTrue();
        CustoTransacaoNaTela.AcimaDaTabela((1.39m, 5.1m), 3.13m).Should().BeFalse("dentro da faixa não há o que cobrar");
        CustoTransacaoNaTela.AcimaDaTabela(null, 9m).Should().BeFalse("sem tabela não se inventa um teto");
    }

    [Fact]
    public void Mes_sem_faturamento_e_buraco_no_percentual_e_travessao_na_tabela()
    {
        var serie = new[]
        {
            new MesCusto(2026, 9, "set/26", 0m, 0m, 0m),
            new MesCusto(2026, 10, "out/26", 1000m, 30m, 20m)
        };
        var resumo = new ResumoCustoTransacao(1000m, 30m, 20m, null);
        var p = CustoTransacaoNaTela.Montar(serie, resumo, [], new Dictionary<string, (decimal, decimal)>(),
            (new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 31)));

        p.SeriePercentual.Select(x => x.Valor).Should().Equal(null, 5.0);
        p.SerieDeducoes.Select(x => x.Valor).Should().Equal(0.0, 50.0); // em reais, zero É zero
        p.Meses[0].Percentual.Should().Be("—");
        p.PercentualDeducoes.Should().Be("5%");
        p.Periodo.Should().Be("09/2026 a 10/2026");
        p.Resumo.Should().Be("Nenhum recebimento no período.");
        p.MaisCara.Should().BeNull();
    }

    [Fact]
    public void Resumo_e_csv_do_custo_nomeiam_quem_passou_do_teto()
    {
        var adquirentes = new[] { new CustoAdquirente("Rede", 1000m, 32m, 4) { Fracao = 1 } };
        var faixas = new Dictionary<string, (decimal, decimal)>(StringComparer.OrdinalIgnoreCase) { ["rede"] = (2.5m, 2.5m) };
        var p = CustoTransacaoNaTela.Montar([], new ResumoCustoTransacao(1000m, 32m, 0m, adquirentes[0]), adquirentes, faixas,
            (new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        p.Resumo.Should().Be("1 origem(ns) · 1 com taxa efetiva ACIMA do teto de tabela.");
        p.Adquirentes.Single().ValorRotulo.Should().Be("R$ 32,00 de R$ 1.000,00 (4)");
        p.MaisCara.Should().Be("Adquirente que mais descontou: Rede (R$ 32,00)");

        var (cab, linhas) = CustoTransacaoNaTela.Csv(p);
        cab.Should().Equal("Tipo", "Nome", "Bruto", "Taxa", "Imposto", "Liquido", "Deducoes %");
        linhas.Single().Should().Equal("Adquirente", "Rede", "R$ 32,00 de R$ 1.000,00 (4)", "", "", "", "efetiva 3,2% · tabela 2,5%");
    }
}
