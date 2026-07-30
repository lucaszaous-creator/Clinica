using System.Windows.Controls;

namespace Clinica.Gerente.Views;

/// <summary>
/// Tabela de preço por convênio, cadastrada na direção e usada pela Conciliação do
/// Financeiro. Quem negocia tabela é a direção; quem concilia guia é o balcão.
/// </summary>
public partial class PrecosConvenioView : UserControl
{
    public PrecosConvenioView()
    {
        InitializeComponent();
    }
}
