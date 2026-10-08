using System.Windows;
using System.Windows.Controls;

namespace Clinica.Financeiro.Views;

/// <summary>
/// Conferência da gaveta. O módulo sabia tudo sobre o dinheiro registrado e nada sobre o
/// dinheiro físico — e é entre os dois que ele some.
/// </summary>
public partial class FechamentoCaixaView : UserControl
{
    public FechamentoCaixaView()
    {
        InitializeComponent();
    }

    private void AoMudarTamanho(object sender, SizeChangedEventArgs e)
    {
        if (PainelContagem is null) return;
        var empilhar = ActualWidth < 1050;
        DockPanel.SetDock(PainelContagem, empilhar ? Dock.Top : Dock.Left);
        PainelContagem.Width = empilhar ? double.NaN : 340;
        PainelContagem.MaxHeight = empilhar ? Math.Max(100, ActualHeight * 0.23) : double.PositiveInfinity;
        PainelContagem.Margin = empilhar ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 16, 0);
    }
}
