using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Escolher a sessão a que o termo se refere. Ver
/// <see cref="EscolherSessaoDoTermoViewModel"/> para as decisões.
/// </summary>
public partial class EscolherSessaoDoTermoWindow : Window
{
    private readonly EscolherSessaoDoTermoViewModel _vm;

    public EscolherSessaoDoTermoWindow(EscolherSessaoDoTermoViewModel vm)
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
