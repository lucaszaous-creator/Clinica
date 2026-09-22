using System.Windows;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Janelas;

public partial class BaixarLancamentoWindow : Window
{
    public BaixarLancamentoWindow(BaixarLancamentoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.Concluido += Concluir;
        Closed += (_, _) => vm.Concluido -= Concluir;
    }
    private void Concluir() => DialogResult = true;
}
