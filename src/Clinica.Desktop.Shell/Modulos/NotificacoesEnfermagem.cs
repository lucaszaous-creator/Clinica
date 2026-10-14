namespace Clinica.Desktop.Shell.Modulos;

/// <summary>A leitura local atualiza o cabeçalho; o relógio busca alterações feitas em outros dispositivos.</summary>
public static class NotificacoesEnfermagem
{
    public static event Action? Mudaram;
    public static void SinalizarMudanca() => Mudaram?.Invoke();
}
