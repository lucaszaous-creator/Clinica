using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Escolher qual termo o paciente vai assinar. Ver <see cref="EscolherTermoViewModel"/>
/// para as decisões.
/// </summary>
public partial class EscolherTermoWindow : Window
{
    private readonly EscolherTermoViewModel _vm;

    public EscolherTermoWindow(EscolherTermoViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        DataContext = vm;

        vm.Escolheu += AoEscolher;
        Closed += (_, _) => vm.Escolheu -= AoEscolher;
    }

    private void AoEscolher()
    {
        DialogResult = true;
        Close();
    }
}
