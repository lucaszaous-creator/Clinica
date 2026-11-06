namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Abra já o CASO desta pessoa" (out/2026, WhatsApp nativo): o "Ver caso" da conversa leva à
/// conversa do acompanhamento daquele paciente, não à fila inteira para a recepção procurar de novo.
/// Um pedido só; ler limpa — como o <see cref="PedidoDeDocumento"/>. (A fila de retornos pedidos,
/// que também era aberta por aqui, saiu com o "Retorno sugerido" em 26/10/2026, a pedido da clínica.)
/// </summary>
public sealed class PedidoDoAcompanhamento
{
    private int _caso;

    /// <summary>Pede que o Acompanhamento abra o caso <paramref name="acompanhamentoId"/> (o Id da <c>LinhaAcompanhamento</c>).</summary>
    public void PedirCaso(int acompanhamentoId) => Interlocked.Exchange(ref _caso, acompanhamentoId);

    /// <summary>O caso pedido, ou nulo; ler limpa.</summary>
    public int? ConsumirCaso() => Interlocked.Exchange(ref _caso, 0) is var id and > 0 ? id : null;
}
