using System.Windows.Controls;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A tela da enfermagem: a carteira inteira e a evolução de cada paciente.
///
/// A View liga e desliga o relógio do CRONÔMETRO (set/2026) — e quem o liga é ela, não a
/// ViewModel, como em toda tela da suíte com timer desde a parcela 38: o shell constrói uma
/// tela nova a cada navegação, e um timer ligado manteria viva cada ViewModel já trocada.
/// </summary>
public partial class EnfermagemView : UserControl
{
    public EnfermagemView()
    {
        InitializeComponent();

        Loaded += (_, _) => (DataContext as EnfermagemViewModel)?.AoEntrarEmCena();
        Unloaded += (_, _) => (DataContext as EnfermagemViewModel)?.AoSairDeCena();
    }
}
