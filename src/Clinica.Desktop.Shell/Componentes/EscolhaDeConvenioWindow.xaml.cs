using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Escolher o convênio de um paciente que ainda não tem um, sem sair do lançamento.
/// Ver <see cref="EscolhaDeConvenioViewModel"/> para as decisões.
/// </summary>
public partial class EscolhaDeConvenioWindow : Window
{
    private readonly EscolhaDeConvenioViewModel _vm;

    public EscolhaDeConvenioWindow(EscolhaDeConvenioViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        DataContext = vm;

        vm.Vinculou += AoVincular;
        Closed += (_, _) => vm.Vinculou -= AoVincular;
    }

    private void AoVincular()
    {
        DialogResult = true;
        Close();
    }
}
