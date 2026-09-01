using System.Windows;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>O quick-view do resultado: fecha sozinho quando a navegação para o paciente
/// foi disparada — modal aberto por cima da tela nova seria um órfão.</summary>
public partial class ResultadosDoPedidoWindow : Window
{
    public ResultadosDoPedidoWindow(ResultadosDoPedidoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.NavegouParaOPaciente += Close;
    }
}
