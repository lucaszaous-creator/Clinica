using System.ComponentModel;
using System.Windows;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Diálogo da colheita de uma medida.
///
/// Fecha sozinha quando o ViewModel consegue gravar. Recusa de validação (peso
/// implausível, meia pressão arterial, IMC digitado) NÃO fecha nada: a mensagem fica
/// inline e o profissional corrige com o que digitou ainda na tela.
/// </summary>
public partial class RegistrarMedidaWindow : Window
{
    private readonly MedidaEdicaoViewModel _vm;

    public RegistrarMedidaWindow(MedidaEdicaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.PropertyChanged += AoMudar;
        Closed += (_, _) => vm.PropertyChanged -= AoMudar;
    }

    private void AoMudar(object? remetente, PropertyChangedEventArgs e)
    {
        // `Registrada` não é observável — ela é preenchida ANTES de `Salvando` voltar a
        // false, que é o último aviso emitido na gravação bem-sucedida.
        if (e.PropertyName != nameof(MedidaEdicaoViewModel.Salvando)) return;
        if (_vm.Salvando || !_vm.Registrada) return;

        DialogResult = true;
    }
}
