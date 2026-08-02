using System.ComponentModel;
using System.Windows;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Janela de aplicação de um instrumento.
///
/// Fecha sozinha quando o ViewModel consegue gravar — ele expõe
/// <see cref="AplicarAvaliacaoViewModel.Salva"/>, e é a mudança dessa propriedade que a
/// janela observa. Recusa de validação (escala incompleta, peso inválido) não fecha nada:
/// a mensagem fica inline e o profissional corrige com as respostas ainda na tela.
/// </summary>
public partial class AplicarAvaliacaoWindow : Window
{
    private readonly AplicarAvaliacaoViewModel _vm;

    public AplicarAvaliacaoWindow(AplicarAvaliacaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.PropertyChanged += AoMudar;
        Closed += (_, _) => vm.PropertyChanged -= AoMudar;
    }

    private void AoMudar(object? remetente, PropertyChangedEventArgs e)
    {
        // `Salva` não é observável — ela é preenchida ANTES de `Salvando` voltar a false,
        // que é o último aviso que o ViewModel emite na gravação bem-sucedida.
        if (e.PropertyName != nameof(AplicarAvaliacaoViewModel.Salvando)) return;
        if (_vm.Salvando || _vm.Salva is null) return;

        DialogResult = true;
    }
}
