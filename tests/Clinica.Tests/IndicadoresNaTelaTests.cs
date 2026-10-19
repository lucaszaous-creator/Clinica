using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela de Indicadores do Gerente AFIRMA (Blazor desde out/2026, etapa 6) — antes no
/// <c>IndicadoresViewModel</c> do WPF. Sem base de cálculo é "—", nunca zero.
/// </summary>
public class IndicadoresNaTelaTests
{
    private static MesGerencial Mes(int mes, int atendidos, int faltas, int minutosOcupados = 0, int minutosDisponiveis = 0)
        => new(2026, mes, $"{mes:00}/2026", atendidos, faltas, 0, minutosOcupados, minutosDisponiveis);

    private static ProdutividadeProfissional Prof(string nome, int atendidos, int faltas, int ocupados, int disponiveis,
        int evolucoes = 0, double? eva = null)
        => new(1, nome, atendidos + faltas, atendidos, faltas, 0, 0, atendidos, ocupados, 1, disponiveis, evolucoes, eva);

    private static PainelGerencial Painel(IReadOnlyList<MesGerencial> serie, IReadOnlyList<ProdutividadeProfissional>? prods = null,
        IndicadoresAgenda? agenda = null)
        => new(new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 18), 480,
            agenda ?? new IndicadoresAgenda(0, 0, 0, 0, 0, 0, 0, 0), prods ?? [], serie);

    private static readonly ResumoNps SemNps = new(0, 0, 0, 0, 0, 0);

    [Fact]
    public void Sem_horario_fechado_as_taxas_sao_travessao_e_a_lista_diz_que_esta_vazia()
    {
        var p = IndicadoresNaTela.Montar(Painel([]), SemNps);
        p.NoShow.Should().Be("—");
        p.Ocupacao.Should().Be("—");
        p.Nps.Should().Be("—");
        p.NpsDetalhe.Should().Be("nenhuma pesquisa no período");
        p.MensagemVazio.Should().Be("Nenhum horário na agenda neste período.");
        p.TemSerie.Should().BeFalse("um mês só não é evolução");
    }

    [Fact]
    public void Taxas_em_ptBR_e_jornada_escrita()
    {
        var agenda = new IndicadoresAgenda(20, 18, 2, 0, 1, 15, 600, 2400);
        var p = IndicadoresNaTela.Montar(Painel([], agenda: agenda), SemNps);
        p.NoShow.Should().Be("10%");
        p.Ocupacao.Should().Be("25%");
        p.HorasOcupadas.Should().Be("10 h");
        p.Jornada.Should().Be("Ocupação medida sobre 8 h por dia com agenda aberta.");
    }

    [Fact]
    public void Nps_enviado_sem_resposta_diz_isso_e_nao_mostra_numero()
    {
        var p = IndicadoresNaTela.Montar(Painel([]), new ResumoNps(5, 4, 0, 0, 0, 0));
        p.Nps.Should().Be("—");
        p.NpsDetalhe.Should().Be("4 enviada(s), nenhuma resposta ainda");

        var medido = IndicadoresNaTela.Montar(Painel([]), new ResumoNps(10, 10, 4, 3, 0, 1));
        medido.Nps.Should().Be("50");
        medido.NpsDetalhe.Should().Be("4 resposta(s): 3 promotor(es), 0 neutro(s), 1 detrator(es)");
    }

    [Fact]
    public void Variacao_de_atendidos_e_relativa_e_a_do_noshow_e_em_pontos_com_o_sinal_invertido()
    {
        var serie = new[] { Mes(8, 40, 10), Mes(9, 50, 5) };
        var p = IndicadoresNaTela.Montar(Painel(serie), SemNps);
        p.VariacaoAtendidos!.Texto.Should().Be("▲ 25% vs 08/2026");
        p.VariacaoAtendidos.Boa.Should().BeTrue();
        // 20% → 9,1%: caiu 10,9 pontos — e cair falta é BOM.
        p.VariacaoNoShow!.Texto.Should().Be("▼ 10,9 ponto(s) vs 08/2026");
        p.VariacaoNoShow.Boa.Should().BeTrue();
    }

    [Fact]
    public void Sem_base_no_mes_anterior_nao_ha_variacao()
    {
        var serie = new[] { Mes(8, 0, 0), Mes(9, 50, 5) };
        var p = IndicadoresNaTela.Montar(Painel(serie), SemNps);
        p.VariacaoAtendidos.Should().BeNull("0% vs mês anterior sem mês anterior é invenção");
        p.VariacaoNoShow.Should().BeNull();
    }

    [Fact]
    public void Mes_sem_horario_fechado_e_buraco_na_serie_e_travessao_na_lista()
    {
        var serie = new[] { Mes(8, 10, 2), Mes(9, 0, 0), Mes(10, 12, 0) };
        var p = IndicadoresNaTela.Montar(Painel(serie), SemNps);
        p.SerieNoShow.Select(x => x.Valor).Should().Equal(16.7, null, 0);
        p.Meses[1].NoShow.Should().Be("—");
        p.Meses[1].Ocupacao.Should().Be("—");
        p.TemSerie.Should().BeTrue();
    }

    [Fact]
    public void Barras_normalizam_pela_maior_ocupacao_e_deixam_fora_quem_nao_tem_base()
    {
        var prods = new[] { Prof("Ana", 10, 0, 300, 1000), Prof("Bia", 5, 0, 600, 1000), Prof("Caio", 1, 0, 30, 0) };
        var p = IndicadoresNaTela.Montar(Painel([], prods), SemNps);
        p.Barras.Select(b => b.Rotulo).Should().Equal("Bia", "Ana");
        p.Barras[0].Fracao.Should().Be(1);
        p.Barras[1].Fracao.Should().Be(0.5);
        p.Barras[1].ValorRotulo.Should().Be("30% · 10 atendido(s)");
    }

    [Fact]
    public void Linha_de_produtividade_nao_afirma_taxa_sem_base()
    {
        var prods = new[] { Prof("Sem fechado", 0, 0, 0, 0), Prof("Com EVA", 10, 0, 60, 480, evolucoes: 8, eva: 1) };
        var p = IndicadoresNaTela.Montar(Painel([], prods), SemNps);
        p.Produtividade[0].NoShow.Should().Be("—", "0% diria que ninguém faltou");
        p.Produtividade[0].Prontuario.Should().Be("—", "0% acusaria de negligência quem não atendeu");
        p.Produtividade[1].Prontuario.Should().Be("80%");
        p.Produtividade[1].QuedaDaDor.Should().Be("1 ponto");
    }

    [Fact]
    public void Csv_mantem_os_cabecalhos_do_wpf_e_escreve_travessao_sem_base()
    {
        var (cab, linhas) = IndicadoresNaTela.Csv(Painel([Mes(9, 0, 0)], [Prof("Ana", 3, 1, 60, 480)]));
        cab.Should().Equal("Tipo", "Nome", "Atendidos", "Faltas", "Cancelados", "No-show %", "Ocupacao %", "Prontuario %");
        linhas[0].Should().Equal("Mes", "09/2026", "0", "0", "0", "—", "—", "—");
        linhas[1].Should().Equal("Profissional", "Ana", "3", "1", "0", "25", "12,5", "0");
    }
}
