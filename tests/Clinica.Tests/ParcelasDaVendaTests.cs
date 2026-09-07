using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O desenho das parcelas da venda de pacote (set/2026) — puro, sem banco.
///
/// É o MESMO desenho que a tela mostra na prévia e que o serviço grava; o que estes testes
/// fixam é a aritmética que ninguém confere de cabeça no balcão: a soma fecha ao centavo, o
/// dia 31 não vira dia 28 para sempre, e a decisão que não fecha é recusada ANTES de existir
/// uma venda.
/// </summary>
public class ParcelasDaVendaTests
{
    private static readonly DateOnly Compra = new(2026, 9, 6);

    [Fact]
    public void A_vista_e_um_lancamento_so_realizado_na_data_da_compra()
    {
        var desenho = ParcelasDaVenda.Desenhar(1200m, Compra, PagamentoDaVenda.AVista(FormaPagamento.Pix));

        desenho.Should().ContainSingle();
        desenho[0].PagaAgora.Should().BeTrue();
        desenho[0].Valor.Should().Be(1200m);
        desenho[0].Vencimento.Should().Be(Compra);
    }

    [Fact]
    public void Os_centavos_que_sobram_vao_para_a_PRIMEIRA_parcela_e_a_soma_fecha()
    {
        var desenho = ParcelasDaVenda.Desenhar(
            100.01m, Compra, PagamentoDaVenda.APrazo(3, Compra.AddDays(30)));

        desenho.Should().HaveCount(3);
        desenho.Select(p => p.Valor).Should().Equal(33.35m, 33.33m, 33.33m);
        desenho.Sum(p => p.Valor).Should().Be(100.01m);
        desenho.Should().OnlyContain(p => !p.PagaAgora);
    }

    [Fact]
    public void Parcelas_sao_mensais_contadas_do_PRIMEIRO_vencimento_e_o_dia_31_nao_encolhe_para_sempre()
    {
        // A regra da conta recorrente (parcela 12): encadear "a anterior mais um mês" faria
        // 31/jan → 28/fev → 28/mar. Contadas da primeira, março volta ao dia 31.
        var primeira = new DateOnly(2027, 1, 31);
        var desenho = ParcelasDaVenda.Desenhar(
            300m, new DateOnly(2027, 1, 10), PagamentoDaVenda.APrazo(3, primeira));

        desenho.Select(p => p.Vencimento).Should().Equal(
            new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31));
    }

    [Fact]
    public void Entrada_hoje_e_realizada_e_o_restante_vira_parcelas_previstas()
    {
        var desenho = ParcelasDaVenda.Desenhar(
            1000m, Compra, PagamentoDaVenda.APrazo(2, Compra.AddMonths(1), entradaAgora: 400m,
                formaDaEntrada: FormaPagamento.Dinheiro));

        desenho.Should().HaveCount(3);
        desenho[0].PagaAgora.Should().BeTrue();
        desenho[0].Valor.Should().Be(400m);
        desenho.Skip(1).Select(p => p.Valor).Should().Equal(300m, 300m);
        desenho.Skip(1).Should().OnlyContain(p => !p.PagaAgora);
    }

    [Fact]
    public void Entrada_igual_ao_total_e_a_vista_sem_parcela_nenhuma()
    {
        var desenho = ParcelasDaVenda.Desenhar(
            500m, Compra, PagamentoDaVenda.APrazo(3, Compra.AddMonths(1), entradaAgora: 500m,
                formaDaEntrada: FormaPagamento.Pix));

        desenho.Should().ContainSingle().Which.PagaAgora.Should().BeTrue();
    }

    [Fact]
    public void Pacote_de_valor_zero_nao_gera_lancamento()
    {
        ParcelasDaVenda.Desenhar(0m, Compra, PagamentoDaVenda.AVista(FormaPagamento.Pix))
            .Should().BeEmpty();
        ParcelasDaVenda.Desenhar(0m, Compra, PagamentoDaVenda.APrazo(3, Compra.AddMonths(1)))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("sem forma à vista")]
    [InlineData("entrada maior que o valor")]
    [InlineData("sem vencimento")]
    [InlineData("zero parcelas")]
    [InlineData("vencimento antes da compra")]
    [InlineData("entrada sem forma")]
    public void A_decisao_que_nao_fecha_e_recusada_dizendo_o_que_falta(string caso)
    {
        PagamentoDaVenda pagamento = caso switch
        {
            "sem forma à vista" => new PagamentoDaVenda { TudoAgora = true },
            "entrada maior que o valor" => PagamentoDaVenda.APrazo(2, Compra.AddMonths(1), 900m, FormaPagamento.Pix),
            "sem vencimento" => new PagamentoDaVenda { Parcelas = 2 },
            "zero parcelas" => PagamentoDaVenda.APrazo(0, Compra.AddMonths(1)),
            "vencimento antes da compra" => PagamentoDaVenda.APrazo(2, Compra.AddDays(-1)),
            _ => PagamentoDaVenda.APrazo(2, Compra.AddMonths(1), entradaAgora: 100m)
        };

        var desenhar = () => ParcelasDaVenda.Desenhar(600m, Compra, pagamento);
        desenhar.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Montar_grava_status_forma_vencimento_e_o_vinculo_com_o_pacote()
    {
        var pacote = new PacotePaciente
        {
            PacienteId = 7, Nome = "Pacote 10 sessões", Valor = 1000m, DataCompra = Compra
        };

        var lancamentos = ParcelasDaVenda.Montar(
            pacote, "Maria", PagamentoDaVenda.APrazo(2, Compra.AddMonths(1), 200m, FormaPagamento.Pix),
            operador: "balcao");

        lancamentos.Should().HaveCount(3);
        lancamentos.Should().OnlyContain(l => l.PacotePaciente == pacote && l.PacienteId == 7
                                              && l.Tipo == TipoLancamento.Entrada);

        var entrada = lancamentos[0];
        entrada.Status.Should().Be(StatusLancamento.Realizado);
        entrada.FormaPagamento.Should().Be(FormaPagamento.Pix);
        entrada.DataPagamento.Should().Be(Compra);
        entrada.DataVencimento.Should().BeNull();
        entrada.Descricao.Should().Contain("entrada").And.Contain("Maria");

        var parcela = lancamentos[1];
        parcela.Status.Should().Be(StatusLancamento.Previsto);
        parcela.FormaPagamento.Should().BeNull("a forma da parcela é decidida no dia em que ela é paga");
        parcela.DataVencimento.Should().Be(Compra.AddMonths(1));
        parcela.Descricao.Should().Contain("parcela 1/2");
    }

    [Fact]
    public void A_descricao_cabe_nos_200_da_coluna_mesmo_com_nomes_compridos()
    {
        var pacote = new PacotePaciente
        {
            PacienteId = 1, Nome = new string('P', 120), Valor = 100m, DataCompra = Compra
        };

        var lancamentos = ParcelasDaVenda.Montar(
            pacote, new string('N', 120), PagamentoDaVenda.AVista(FormaPagamento.Pix), null);

        lancamentos.Should().OnlyContain(l => l.Descricao.Length <= 200);
    }

    [Fact]
    public void O_resumo_diz_a_decisao_por_extenso()
    {
        var aVista = ParcelasDaVenda.Desenhar(300m, Compra, PagamentoDaVenda.AVista(FormaPagamento.Pix));
        ParcelasDaVenda.Resumir(aVista).Should().StartWith("à vista");

        var aPrazo = ParcelasDaVenda.Desenhar(
            1000m, Compra, PagamentoDaVenda.APrazo(3, Compra.AddMonths(1), 100m, FormaPagamento.Pix));
        ParcelasDaVenda.Resumir(aPrazo).Should().Contain("entrada").And.Contain("3 parcelas");
    }
}
