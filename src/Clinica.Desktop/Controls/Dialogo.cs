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

    /// <summary>
    /// Pede um texto ao usuário (motivo, justificativa, senha provisória). Retorna
    /// <c>null</c> quando ele DESISTE (Cancelar/Esc) — e nesse caso o chamador não deve
    /// seguir com a ação.
    ///
    /// ⚠️ Com <paramref name="obrigatorio"/> em <c>false</c> a janela aceita resposta em
    /// branco e devolve <see cref="string.Empty"/>: é isso que separa "desisti" de "siga,
    /// sem texto". Pergunta que anuncia o campo como opcional PRECISA passar <c>false</c> —
    /// senão a janela recusa o vazio e sobra ao usuário o Cancelar, que quer dizer o
    /// contrário do que o chamador vai entender.
    /// </summary>
    string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null,
                           bool obrigatorio = true);
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

    public string? PerguntarTexto(string titulo, string pergunta, string? textoInicial = null,
                                 bool obrigatorio = true)
    {
        var janela = new PromptWindow(titulo, pergunta, textoInicial, obrigatorio)
        {
            // Qualificado: dentro de Clinica.*, "Application" é o namespace Clinica.Application.
            Owner = System.Windows.Application.Current?.MainWindow
        };
        return janela.ShowDialog() == true ? janela.Resposta : null;
    }
}
