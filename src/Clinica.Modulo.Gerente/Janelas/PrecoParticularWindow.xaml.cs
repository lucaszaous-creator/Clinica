using System.Windows;
using Clinica.Gerente.ViewModels;

namespace Clinica.Gerente.Janelas;

/// <summary>Quem fecha é o ViewModel, pelo evento Concluido — a janela não conhece serviço.</summary>
public partial class PrecoParticularWindow : Window
{
    public PrecoParticularWindow(PrecoParticularEdicaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        void AoConcluir()
        {
            vm.Concluido -= AoConcluir;
            DialogResult = true;
        }

        vm.Concluido += AoConcluir;
        Closed += (_, _) => vm.Concluido -= AoConcluir;
    }
}
