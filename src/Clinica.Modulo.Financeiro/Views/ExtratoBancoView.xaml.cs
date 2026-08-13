using System.Windows.Controls;

namespace Clinica.Financeiro.Views;

/// <summary>
/// Conciliação bancária por OFX (parcela 63). Ver <c>ExtratoBancoViewModel</c> para as
/// decisões — em especial: o sistema PROPÕE, a pessoa confirma.
/// </summary>
public partial class ExtratoBancoView : UserControl
{
    public ExtratoBancoView() => InitializeComponent();
}
