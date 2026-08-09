using System.Windows;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Pergunta de texto livre obrigatória — o motivo de um cancelamento, uma justificativa.
/// Existe porque o MessageBox não tem campo de entrada, e essas ações não podem ser
/// registradas sem explicação: é o texto que fica no histórico para quem for auditar.
/// </summary>
public partial class PromptWindow : Window
{
    public string? Resposta { get; private set; }

    public PromptWindow(string titulo, string pergunta, string? textoInicial = null)
    {
        InitializeComponent();
        Title = titulo;
        TxtTitulo.Text = titulo;
        TxtPergunta.Text = pergunta;
        TxtResposta.Text = textoInicial ?? string.Empty;
        Loaded += (_, _) => TxtResposta.Focus();
    }

    private void Confirmar(object remetente, RoutedEventArgs e)
    {
        var texto = TxtResposta.Text?.Trim();
        if (string.IsNullOrWhiteSpace(texto))
        {
            // Sem resposta não há o que registrar: erro inline, a janela continua aberta.
            TxtErro.Text = "Escreva o motivo para continuar.";
            TxtErro.Visibility = Visibility.Visible;
            TxtResposta.Focus();
            return;
        }

        Resposta = texto;
        DialogResult = true;
    }
}
