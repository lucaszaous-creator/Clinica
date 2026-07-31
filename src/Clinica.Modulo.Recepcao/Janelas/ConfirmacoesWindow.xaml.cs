using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// A rodada de confirmação de amanhã. Não fecha ao enviar: a recepcionista percorre a
/// lista inteira de uma vez, e fechar a cada clique a obrigaria a reabrir trinta vezes.
/// </summary>
public partial class ConfirmacoesWindow : Window
{
    public ConfirmacoesWindow(ConfirmacoesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
