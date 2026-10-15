namespace Clinica.Desktop.Shell.Modulos;

/// <summary>A leitura local atualiza o cabeçalho; o relógio busca alterações feitas em outros dispositivos.</summary>
public static class NotificacoesEnfermagem
{
    public static event Action? Mudaram;
    public static void SinalizarMudanca() => Mudaram?.Invoke();

    private static int? _pedidoParaAbrir;

    /// <summary>
    /// O sino pede à caixa de pedidos que abra UM aviso (out/2026: o sino virou a porta única
    /// dos pedidos). Mesmo desenho das pontes da agenda: um pedido, e ler LIMPA — pedido órfão
    /// abriria amanhã o aviso de um clique de ontem.
    /// </summary>
    public static void PedirAbertura(int pedidoId) => _pedidoParaAbrir = pedidoId;
    public static int? ConsumirAbertura() { var id = _pedidoParaAbrir; _pedidoParaAbrir = null; return id; }
}
