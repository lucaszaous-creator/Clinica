using System.Windows;

namespace Clinica.Desktop.Controls;

/// <summary>
/// Pergunta de texto livre — o motivo de um cancelamento, uma justificativa, um nome, uma
/// quantidade. Existe porque o MessageBox não tem campo de entrada, e a maior parte dessas
/// ações não pode ser registrada sem explicação: é o texto que fica no histórico para quem
/// for auditar. Por isso o padrão é OBRIGATÓRIO.
///
/// ⚠️ Desistir e responder EM BRANCO são coisas diferentes, e o chamador precisa distinguir
/// as duas. Enquanto esta janela só aceitava texto obrigatório, quem queria a segunda não
/// tinha por onde: a pergunta anunciava o campo como opcional ("se quiser", "deixe em
/// branco"), a janela recusava o vazio com um erro em vermelho, e a ÚNICA saída era o
/// Cancelar — que o chamador lia como "siga". A porta rotulada "desisti" era a que gravava.
/// </summary>
public partial class PromptWindow : Window
{
    private readonly bool _obrigatorio;

    public string? Resposta { get; private set; }

    public PromptWindow(string titulo, string pergunta, string? textoInicial = null,
                        bool obrigatorio = true)
    {
        _obrigatorio = obrigatorio;
        InitializeComponent();
        Title = titulo;
        TxtTitulo.Text = titulo;
        TxtPergunta.Text = pergunta;
        TxtResposta.Text = textoInicial ?? string.Empty;
        Loaded += (_, _) => TxtResposta.Focus();
    }

    private void Confirmar(object remetente, RoutedEventArgs e)
    {
        var texto = TxtResposta.Text?.Trim() ?? string.Empty;
        if (_obrigatorio && string.IsNullOrWhiteSpace(texto))
        {
            // Sem resposta não há o que registrar: erro inline, a janela continua aberta.
            // A frase não diz "motivo" porque esta janela também pergunta nome do recibo,
            // agente da alergia e quantidade contada — dizer "motivo" ali descreve um campo
            // que não é o que está na frente da pessoa.
            TxtErro.Text = "Escreva uma resposta para continuar.";
            TxtErro.Visibility = Visibility.Visible;
            TxtResposta.Focus();
            return;
        }

        Resposta = texto;
        DialogResult = true;
    }
}
