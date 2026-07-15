using System.Windows;
using System.Windows.Media;
using Clinica.Desktop.Configuracao;

namespace Clinica.Desktop;

/// <summary>Tela de primeiro acesso: a secretária informa e testa a conexão do banco, que é salva com segurança.</summary>
public partial class SetupWindow : Window
{
    private string? _conexaoValidada;

    public SetupWindow() => InitializeComponent();

    private async void BtnTestar_Click(object sender, RoutedEventArgs e)
    {
        var entrada = TxtConexao.Text;
        if (string.IsNullOrWhiteSpace(entrada))
        {
            Mostrar(false, "Informe a connection string ou a URL da Neon.");
            return;
        }

        BtnTestar.IsEnabled = false;
        Mostrar(null, "Testando conexão...");

        string conexao;
        try
        {
            conexao = ConexaoStore.Normalizar(entrada);
        }
        catch (Exception ex)
        {
            BtnTestar.IsEnabled = true;
            Mostrar(false, $"Não foi possível interpretar a string informada: {ex.Message}");
            return;
        }

        var (ok, mensagem) = await ConexaoStore.TestarAsync(conexao);
        BtnTestar.IsEnabled = true;

        if (ok)
        {
            _conexaoValidada = conexao;
            BtnSalvar.IsEnabled = true;
            Mostrar(true, "Conexão bem-sucedida! Clique em \"Salvar e continuar\".");
        }
        else
        {
            _conexaoValidada = null;
            BtnSalvar.IsEnabled = false;
            Mostrar(false, $"Falha na conexão: {mensagem}");
        }
    }

    private void BtnSalvar_Click(object sender, RoutedEventArgs e)
    {
        if (_conexaoValidada is null)
        {
            Mostrar(false, "Teste a conexão antes de salvar.");
            return;
        }

        ConexaoStore.Salvar(_conexaoValidada);
        DialogResult = true;
        Close();
    }

    /// <summary>ok=true verde, ok=false vermelho, ok=null neutro.</summary>
    private void Mostrar(bool? ok, string mensagem)
    {
        BoxMensagem.Visibility = Visibility.Visible;
        TxtMensagem.Text = mensagem;
        (BoxMensagem.Background, TxtMensagem.Foreground) = ok switch
        {
            true => (new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)), new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34))),
            false => (new SolidColorBrush(Color.FromRgb(0xFE, 0xE2, 0xE2)), new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B))),
            null => (new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)), new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)))
        };
    }
}
