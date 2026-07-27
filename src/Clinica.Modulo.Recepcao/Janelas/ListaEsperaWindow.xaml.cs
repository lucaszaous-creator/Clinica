using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>Janela de entrada na lista de espera. Quem fecha é o ViewModel.</summary>
public partial class ListaEsperaWindow : Window
{
    public ListaEsperaWindow(ListaEsperaEdicaoViewModel vm)
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
