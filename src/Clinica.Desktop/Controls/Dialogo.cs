using System.Windows;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Diálogos bloqueantes (confirmação/aviso) abstraídos para os ViewModels não
/// dependerem de MessageBox diretamente (testabilidade e ponto único de estilo).
/// Snackbar (ISnackbarService) continua sendo o canal para mensagens informativas.
/// </summary>
public interface IDialogoService
{
    /// <summary>Pergunta Sim/Não neutra. Retorna true se o usuário confirmar.</summary>
    bool Confirmar(string titulo, string mensagem);

    /// <summary>Pergunta Sim/Não para ação destrutiva/perigosa (ícone de alerta).</summary>
    bool ConfirmarPerigo(string titulo, string mensagem);

    /// <summary>Aviso simples com OK.</summary>
    void Aviso(string titulo, string mensagem);
}

public sealed class DialogoService : IDialogoService
{
    public bool Confirmar(string titulo, string mensagem) =>
        MessageBox.Show(mensagem, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;

    public bool ConfirmarPerigo(string titulo, string mensagem) =>
        MessageBox.Show(mensagem, titulo, MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes;

    public void Aviso(string titulo, string mensagem) =>
        MessageBox.Show(mensagem, titulo, MessageBoxButton.OK, MessageBoxImage.Information);
}
