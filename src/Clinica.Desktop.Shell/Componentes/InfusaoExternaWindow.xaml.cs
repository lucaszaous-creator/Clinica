using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

public partial class InfusaoExternaWindow : Window
{
    public InfusaoExternaWindow(InfusaoExternaViewModel vm)
    {
        InitializeComponent(); DataContext = vm;
        void AoSalvar() => DialogResult = true;
        vm.Salvou += AoSalvar;
        Closed += (_, _) => vm.Salvou -= AoSalvar;
        Loaded += async (_, _) => await vm.CarregarAsync();
    }
}
