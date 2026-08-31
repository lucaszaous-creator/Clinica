using System.ComponentModel;
using System.Windows;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Janelas;

/// <summary>
/// Diálogo do registro de um resultado de exame. Fecha sozinha quando o ViewModel grava;
/// recusa de validação NÃO fecha — a mensagem fica inline e a pessoa corrige com o que
/// digitou ainda na tela (o molde da colheita de medida).
/// </summary>
public partial class RegistrarResultadoExameWindow : Window
{
    private readonly ResultadoExameEdicaoViewModel _vm;

    public RegistrarResultadoExameWindow(ResultadoExameEdicaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.PropertyChanged += AoMudar;
        Closed += (_, _) => vm.PropertyChanged -= AoMudar;
    }

    private void AoMudar(object? remetente, PropertyChangedEventArgs e)
    {
        // `Registrado` não é observável — ele é preenchido ANTES de `Salvando` voltar a
        // false, que é o último aviso emitido na gravação bem-sucedida.
        if (e.PropertyName != nameof(ResultadoExameEdicaoViewModel.Salvando)) return;
        if (_vm.Salvando || !_vm.Registrado) return;

        DialogResult = true;
    }
}
