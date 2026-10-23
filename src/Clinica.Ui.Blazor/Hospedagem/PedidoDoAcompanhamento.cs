namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// "Abra o Acompanhamento já na fila de RETORNOS PEDIDOS" (out/2026, fase 2 — docs/crm-na-recepcao.md):
/// o contador "Retornos" do Início levava à tela "Retornos a marcar", que passou a ser a terceira fila
/// do Acompanhamento. Um pedido só; ler limpa — como o <see cref="PedidoDeDocumento"/>.
/// </summary>
public sealed class PedidoDoAcompanhamento
{
    private int _retornos;

    public void PedirRetornos() => Interlocked.Exchange(ref _retornos, 1);

    public bool ConsumirRetornos() => Interlocked.Exchange(ref _retornos, 0) == 1;
}
