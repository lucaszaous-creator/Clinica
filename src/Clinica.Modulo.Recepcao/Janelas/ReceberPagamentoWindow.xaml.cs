using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

public partial class ReceberPagamentoWindow : Window
{
    public ReceberPagamentoWindow(ReceberPagamentoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        void Concluir() => DialogResult = true;
        vm.Concluido += Concluir;
        Closed += (_, _) => vm.Concluido -= Concluir;
    }
}
