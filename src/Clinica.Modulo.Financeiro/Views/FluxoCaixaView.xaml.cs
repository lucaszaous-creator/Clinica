using System.Windows.Controls;

namespace Clinica.Financeiro.Views;

/// <summary>
/// Fluxo de caixa: a série dos meses e a quebra por categoria. O Caixa mostra o extrato
/// do período e a Produção mostra o volume; nenhum dos dois mostra a tendência.
/// </summary>
public partial class FluxoCaixaView : UserControl
{
    public FluxoCaixaView()
    {
        InitializeComponent();
    }
}
