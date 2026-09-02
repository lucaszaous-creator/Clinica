using System.Windows;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>O quick-view do prontuário: fecha sozinho quando a navegação para a tela do
/// paciente foi disparada — modal aberto por cima da tela nova seria um órfão.</summary>
public partial class ResumoProntuarioWindow : Window
{
    public ResumoProntuarioWindow(ResumoProntuarioViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.NavegouParaOPaciente += Close;
    }
}
