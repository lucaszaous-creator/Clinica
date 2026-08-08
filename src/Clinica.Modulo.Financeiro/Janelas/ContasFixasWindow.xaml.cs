using System.Windows;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Janelas;

/// <summary>
/// As contas fixas — os MOLDES que geram a conta prevista de cada mês.
///
/// Recebe o MESMO ViewModel da tela de Contas: gerar a série daqui tem de aparecer na
/// lista de trás sem ninguém clicar em atualizar.
/// </summary>
public partial class ContasFixasWindow : Window
{
    public ContasFixasWindow(ContasViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
