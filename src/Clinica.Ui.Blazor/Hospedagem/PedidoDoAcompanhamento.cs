namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Abra o Acompanhamento já na fila de RETORNOS PEDIDOS" (out/2026, fase 2 — docs/crm-na-recepcao.md):
/// o contador "Retornos" do Início levava à tela "Retornos a marcar", que passou a ser a terceira fila
/// do Acompanhamento. Um pedido só; ler limpa — como o <see cref="PedidoDeDocumento"/>.
/// E "abra já o CASO desta pessoa" (out/2026, WhatsApp nativo): o "Ver caso" da conversa leva à
/// conversa do acompanhamento daquele paciente, não à fila inteira para a recepção procurar de novo.
/// </summary>
public sealed class PedidoDoAcompanhamento
{
    private int _retornos;
    private int _caso;

    public void PedirRetornos() => Interlocked.Exchange(ref _retornos, 1);

    public bool ConsumirRetornos() => Interlocked.Exchange(ref _retornos, 0) == 1;

    /// <summary>Pede que o Acompanhamento abra o caso <paramref name="acompanhamentoId"/> (o Id da <c>LinhaAcompanhamento</c>).</summary>
    public void PedirCaso(int acompanhamentoId) => Interlocked.Exchange(ref _caso, acompanhamentoId);

    /// <summary>O caso pedido, ou nulo; ler limpa.</summary>
    public int? ConsumirCaso() => Interlocked.Exchange(ref _caso, 0) is var id and > 0 ? id : null;
}
