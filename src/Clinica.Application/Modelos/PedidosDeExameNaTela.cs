namespace Clinica.Application.Modelos;

/// <summary>
/// O que a tela de Exames do Consultório AFIRMA (out/2026, migrado do <c>ExamesViewModel</c>
/// WPF para morar onde o teste alcança). A situação de cada pedido continua derivada de FATO
/// em <see cref="PedidoDeExameLinha"/>; aqui ficam a janela da lista e a frase do resumo.
/// </summary>
public static class PedidosDeExameNaTela
{
    /// <summary>A janela da lista. 90 dias cobrem o ciclo real de um laudo; o resumo DIZ o recorte.</summary>
    public const int JanelaDias = 90;

    /// <summary>"12 pedido(s) nos últimos 90 dias · 3 aguardando resultado".</summary>
    public static string Resumo(IReadOnlyCollection<PedidoDeExameLinha> pedidos)
    {
        if (pedidos.Count == 0) return $"Nenhum pedido nos últimos {JanelaDias} dias";
        var aguardando = pedidos.Count(p => p.Situacao == SituacaoPedidoExame.AguardandoResultado);
        return $"{pedidos.Count} pedido(s) nos últimos {JanelaDias} dias · {aguardando} aguardando resultado";
    }

    /// <summary>O tom do selo da situação: aguardar é aviso, resultado é sucesso, cancelado é neutro.</summary>
    public static string Tom(PedidoDeExameLinha pedido) => pedido.Situacao switch
    {
        SituacaoPedidoExame.AguardandoResultado => "aviso",
        SituacaoPedidoExame.ResultadoDisponivel => "sucesso",
        _ => string.Empty
    };
}
