using System.Windows;
using System.Windows.Controls;
using Clinica.Clinico.ViewModels;
using Clinica.Domain.Entities;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Escrever e liberar a prescrição de infusão.
///
/// Só fecha sozinha quando a folha é LIBERADA: salvar rascunho deixa a janela aberta, com
/// a mensagem inline, porque quem salvou rascunho quase sempre vai continuar escrevendo.
/// Enquanto uma gravação está no ar a janela não fecha — fechar no meio deixaria a pessoa
/// sem saber se a folha foi gravada.
/// </summary>
public partial class PrescricaoInternaWindow : Window
{
    private readonly PrescricaoInternaEdicaoViewModel _vm;
    private bool _liberou;

    public PrescricaoInternaWindow(PrescricaoInternaEdicaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.Fechar = () => { _liberou = true; DialogResult = true; };
        Closing += (_, e) => { if (vm.Ocupado && !_liberou) e.Cancel = true; };
        Closed += (_, _) => vm.Fechar = null;
    }

    /// <summary>A folha foi liberada nesta janela — quem abriu recarrega a lista.</summary>
    public bool Assinou => _vm.Assinou;

    /// <summary>
    /// "Usar e revisar": o modelo vem da linha e a infusão vem do <c>Tag</c> (o grupo cujo
    /// <c>Expander</c> contém a lista). Quem aplica, confere permissão e pergunta antes de
    /// substituir o que já foi escrito é o ViewModel.
    /// </summary>
    private void UsarModelo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ModeloDocumento modelo, Tag: GrupoInfusaoEdicao grupo }) return;
        _vm.AplicarModelo(grupo, modelo);
    }
}
