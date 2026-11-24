using System.Collections.Concurrent;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// A folha do documento (a receita) maximizada ou não, por quem está logado NESTA máquina (nov/2026, pedido da Dra.): quem
/// maximizou uma vez abre a próxima já maximizada. Vale enquanto o app está aberto; fechar o app volta ao tamanho de sempre.
/// </summary>
public static class PreferenciaDaFolha
{
    private static readonly ConcurrentDictionary<int, bool> Maximizadas = new();

    public static bool Maximizada(int usuarioId) => Maximizadas.TryGetValue(usuarioId, out var m) && m;

    public static void Guardar(int usuarioId, bool maximizada) => Maximizadas[usuarioId] = maximizada;
}
