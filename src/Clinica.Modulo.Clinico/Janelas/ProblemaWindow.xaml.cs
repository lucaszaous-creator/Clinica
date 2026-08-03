using System.ComponentModel;
using System.Windows;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Janela de uma linha da lista de problemas.
///
/// Fecha sozinha quando o ViewModel consegue gravar — a recusa de validação (descrição em
/// branco, fim antes do início) não fecha nada: a mensagem fica inline e o profissional
/// corrige com o que digitou ainda na tela.
/// </summary>
public partial class ProblemaWindow : Window
{
    private readonly ProblemaEdicaoViewModel _vm;

    public ProblemaWindow(ProblemaEdicaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.PropertyChanged += AoMudar;
        Closed += (_, _) => vm.PropertyChanged -= AoMudar;
    }

    private void AoMudar(object? remetente, PropertyChangedEventArgs e)
    {
        // `Salvo` não é observável — ela é preenchida ANTES de `Salvando` voltar a false,
        // que é o último aviso que o ViewModel emite na gravação bem-sucedida.
        if (e.PropertyName != nameof(ProblemaEdicaoViewModel.Salvando)) return;
        if (_vm.Salvando || _vm.Salvo is null) return;

        DialogResult = true;
    }
}
