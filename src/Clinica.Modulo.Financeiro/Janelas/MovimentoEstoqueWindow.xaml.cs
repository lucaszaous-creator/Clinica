using System.Windows;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Janelas;

/// <summary>Quem fecha é o ViewModel, pelo evento Concluido — a janela não conhece serviço.</summary>
public partial class MovimentoEstoqueWindow : Window
{
    public MovimentoEstoqueWindow(MovimentoEstoqueViewModel vm)
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
