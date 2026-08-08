using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>Janela da autorização de sessões. Quem fecha é o ViewModel, ao gravar.</summary>
public partial class AutorizacaoWindow : Window
{
    public AutorizacaoWindow(AutorizacaoEdicaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        void AoSalvar()
        {
            vm.Salvou -= AoSalvar;
            DialogResult = true;
        }

        vm.Salvou += AoSalvar;
        Closed += (_, _) => vm.Salvou -= AoSalvar;
    }
}
