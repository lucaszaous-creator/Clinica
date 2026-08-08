using System.Windows;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Janelas;

/// <summary>
/// As regras de repasse e as apurações já fechadas.
///
/// Recebe o MESMO ViewModel da tela de Repasses: mudar uma regra ou cancelar uma apuração
/// muda o cálculo do período que está na tela de trás, e dois VMs dariam dois números
/// para a mesma pergunta.
/// </summary>
public partial class RegrasRepasseWindow : Window
{
    public RegrasRepasseWindow(RepassesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
