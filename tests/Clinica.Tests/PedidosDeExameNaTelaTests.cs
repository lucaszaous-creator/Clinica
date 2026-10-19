using Clinica.Application.Modelos;
using Xunit;

namespace Clinica.Tests;

/// <summary>O que a tela de Exames AFIRMA (migrada do ExamesViewModel WPF em out/2026).</summary>
public class PedidosDeExameNaTelaTests
{
    private static PedidoDeExameLinha Pedido(int resultados = 0, bool cancelado = false) =>
        new(1, "2026/0001", 7, "Maria", new DateOnly(2026, 10, 1), "Hemograma", 1, resultados, cancelado, null);

    [Fact]
    public void Sem_pedido_o_resumo_diz_a_janela()
        => Assert.Equal($"Nenhum pedido nos últimos {PedidosDeExameNaTela.JanelaDias} dias", PedidosDeExameNaTela.Resumo([]));

    [Fact]
    public void O_resumo_conta_so_quem_aguarda_resultado()
        => Assert.Equal($"3 pedido(s) nos últimos {PedidosDeExameNaTela.JanelaDias} dias · 1 aguardando resultado",
            PedidosDeExameNaTela.Resumo([Pedido(), Pedido(resultados: 1), Pedido(cancelado: true)]));

    [Fact]
    public void O_tom_segue_a_situacao_derivada_de_fato()
    {
        Assert.Equal("aviso", PedidosDeExameNaTela.Tom(Pedido()));
        Assert.Equal("sucesso", PedidosDeExameNaTela.Tom(Pedido(resultados: 2)));
        Assert.Equal(string.Empty, PedidosDeExameNaTela.Tom(Pedido(resultados: 2, cancelado: true)));
    }
}
