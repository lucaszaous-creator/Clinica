using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// Orçamento livre: itens digitados, sem depender de um pacote do catálogo. Fecha sozinha
/// quando o documento é emitido — o <c>Concluido</c> é disparado depois da impressão.
/// </summary>
public partial class OrcamentoWindow : Window
{
    public OrcamentoWindow(OrcamentoViewModel vm)
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
