using System.Windows;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Escrever e assinar a prescrição de infusão.
///
/// Só fecha sozinha quando a folha é ASSINADA: salvar rascunho deixa a janela aberta, com
/// a mensagem inline, porque quem salvou rascunho quase sempre vai continuar escrevendo.
/// </summary>
public partial class PrescricaoInternaWindow : Window
{
    private readonly PrescricaoInternaEdicaoViewModel _vm;

    public PrescricaoInternaWindow(PrescricaoInternaEdicaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.Fechar = () => DialogResult = true;
        Closed += (_, _) => vm.Fechar = null;
    }

    /// <summary>A folha foi assinada nesta janela — quem abriu recarrega a lista.</summary>
    public bool Assinou => _vm.Assinou;
}
