using System.Windows.Controls;

namespace Clinica.Financeiro.Views;

/// <summary>
/// Taxas da maquininha e imposto retido. Sem esta tela, o caixa mostrava o bruto e o
/// extrato da adquirente mostrava outro número — e ninguém sabia de onde vinha a
/// diferença.
/// </summary>
public partial class TaxasView : UserControl
{
    public TaxasView()
    {
        InitializeComponent();
    }
}
