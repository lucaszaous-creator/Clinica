using Clinica.Application.Modelos;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// "MEUS NÚMEROS" — a regra do <c>MeusNumerosViewModel</c> WPF em <see cref="MeusNumerosNaTela"/>:
/// os intervalos dos períodos, a soma da clínica sem vínculo (taxas recalculadas, nunca média de
/// percentuais), "—" e nunca zero sem base (e a barra some junto), a leitura da completude, os
/// deltas com a direção certa e a dívida em palavras.
/// </summary>
public class MeusNumerosNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 15);

    private static ProdutividadeProfissional P(int? id, int agendados, int atendidos, int faltas, int evolucoes,
        int minOcupados = 0, int minDisponiveis = 0, double? eva = null)
        => new(id, $"Prof {id}", agendados, atendidos, faltas, 0, 0, atendidos, minOcupados, 1, minDisponiveis, evolucoes, eva);

    [Fact]
    public void Os_periodos_e_seus_intervalos()
    {
        Assert.Equal((new DateOnly(2026, 10, 1), Hoje), MeusNumerosNaTela.Intervalo("Este mês", Hoje));
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)), MeusNumerosNaTela.Intervalo("Mês passado", Hoje));
        Assert.Equal((Hoje.AddDays(-89), Hoje), MeusNumerosNaTela.Intervalo("Últimos 90 dias", Hoje));
        Assert.Equal((new DateOnly(2026, 1, 1), Hoje), MeusNumerosNaTela.Intervalo("Este ano", Hoje));
    }

    [Fact]
    public void Sem_vinculo_soma_a_clinica_e_recalcula_as_taxas_sobre_os_totais()
    {
        var soma = MeusNumerosNaTela.Escolher([P(1, 10, 9, 1, 9), P(2, 10, 5, 5, 0)], profissionalId: null)!;
        Assert.Equal(MeusNumerosNaTela.TodosOsProfissionais, soma.Nome);
        Assert.Equal(14, soma.Atendidos);
        Assert.Equal(30.0, soma.TaxaNoShow); // 6 / 20, e não a média de 10% e 50%
        Assert.Null(MeusNumerosNaTela.Somar([]));
        Assert.Equal("Prof 2", MeusNumerosNaTela.Escolher([P(1, 1, 1, 0, 0), P(2, 1, 1, 0, 0)], 2)!.Nome);
    }

    [Fact]
    public void Sem_linha_no_periodo_tudo_e_travessao_e_nenhuma_barra()
    {
        var n = MeusNumerosNaTela.Montar(null, null, "");
        Assert.True(n.SemMovimento);
        Assert.All(new[] { n.Atendidos, n.NoShow, n.Ocupacao, n.Completude, n.MelhoraDor }, v => Assert.Equal("—", v));
        Assert.Null(n.NoShowFracao);
        Assert.Null(n.CompletudeFracao);
        Assert.Null(n.VariacaoAtendidos);
    }

    [Fact]
    public void Taxa_sem_base_nao_e_zero_e_a_completude_sem_atendimento_e_nula()
    {
        var n = MeusNumerosNaTela.Montar(P(1, 3, 0, 0, 0), null, "");
        Assert.False(n.SemMovimento);
        Assert.Equal("—", n.NoShow);
        Assert.Null(n.NoShowFracao);
        Assert.Equal("—", n.Completude);
        Assert.Equal("Nenhuma sessão atendida no período — não há o que medir.", n.LeituraCompletude);
    }

    [Fact]
    public void Os_numeros_em_pt_br_com_a_barra_e_a_leitura_da_completude()
    {
        var n = MeusNumerosNaTela.Montar(P(1, 20, 16, 4, 13, minOcupados: 300, minDisponiveis: 480, eva: 3.25), null, "");
        Assert.Equal("16", n.Atendidos);
        Assert.Equal("20%", n.NoShow);
        Assert.Equal(0.2, n.NoShowFracao!.Value, 3);
        Assert.Equal("62,5%", n.Ocupacao);
        Assert.Equal("81,2%", n.Completude);
        Assert.Equal("A maior parte tem registro; o que falta ainda dá para escrever.", n.LeituraCompletude);
        Assert.Equal("3,3 pontos", n.MelhoraDor); // meio arredonda para cima, como em todo número da tela
    }

    [Fact]
    public void Os_deltas_tem_a_direcao_da_metrica()
    {
        var n = MeusNumerosNaTela.Montar(P(1, 20, 16, 4, 16), P(1, 20, 10, 2, 5), "Trecho anterior");
        Assert.Equal(LeituraKpi.Neutra, n.VariacaoAtendidos!.Leitura); // volume não é bom nem ruim
        Assert.Equal(LeituraKpi.Ruim, n.VariacaoFaltas!.Leitura);       // mais faltas é ruim
        Assert.Equal(MeusNumerosNaTela.RotuloDelta, n.VariacaoAtendidos.Rotulo);
    }

    [Theory]
    [InlineData(0, "Nenhuma sessão em aberto — prontuário em dia.")]
    [InlineData(1, "1 sessão desde 27/09/2026 continua sem evolução escrita.")]
    [InlineData(3, "3 sessões desde 27/09/2026 continuam sem evolução escrita.")]
    public void A_divida_e_de_hoje_em_palavras(int n, string frase) => Assert.Equal(frase, MeusNumerosNaTela.Divida(n));

    [Fact]
    public void O_nome_diz_que_e_a_clinica_sem_vinculo()
    {
        Assert.Equal(MeusNumerosNaTela.TodosOsProfissionais, MeusNumerosNaTela.NomeDoProfissional(null, null));
        Assert.Equal("Profissional", MeusNumerosNaTela.NomeDoProfissional(7, null));
        Assert.Equal(string.Empty, MeusNumerosNaTela.DetalheDoTrecho(null));
    }
}
