using System.Windows;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// "Qual papel?" — a escolha do documento antes da janela de emissão, com os MESMOS
/// cartões da página de documentos (<see cref="DocumentosDoConsultorio.Cartoes"/>). Devolve
/// a FOLHA escolhida, e é o tipo dela que decide o formulário que abre.
/// </summary>
public partial class EscolhaDeDocumentoWindow : Window
{
    private FolhaCatalogo? _escolhida;

    private EscolhaDeDocumentoWindow(IReadOnlyList<CartaoDeFolha> cartoes, string paciente)
    {
        InitializeComponent();
        Cartoes.ItemsSource = cartoes;
        Sobretitulo.Text = string.IsNullOrWhiteSpace(paciente)
            ? "DOCUMENTOS"
            : $"DOCUMENTOS · {paciente.Trim().ToUpperInvariant()}";
    }

    private void AoEscolher(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartaoDeFolha cartao })
        {
            _escolhida = cartao.Folha;
            DialogResult = true;
        }
    }

    /// <summary>A folha escolhida, ou <c>null</c> se a pessoa fechou a janela.</summary>
    public static FolhaCatalogo? Perguntar(
        IReadOnlyList<CartaoDeFolha> cartoes, string paciente, Window? dono)
    {
        var janela = new EscolhaDeDocumentoWindow(cartoes, paciente) { Owner = dono };
        return janela.ShowDialog() == true ? janela._escolhida : null;
    }
}
