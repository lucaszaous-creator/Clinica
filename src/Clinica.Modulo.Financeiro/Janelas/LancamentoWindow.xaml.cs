using System.Windows;
using Clinica.Financeiro.ViewModels;

namespace Clinica.Financeiro.Janelas;

/// <summary>
/// Janela do lançamento manual do caixa. Quem fecha é o ViewModel, avisando pelo evento
/// <see cref="LancamentoEdicaoViewModel.Concluido"/> — assim o comando de salvar continua
/// assíncrono e a janela não precisa saber nada do serviço.
/// </summary>
public partial class LancamentoWindow : Window
{
    public LancamentoWindow(LancamentoEdicaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        void AoConcluir()
        {
            vm.Concluido -= AoConcluir;
            DialogResult = true;
        }

        vm.Concluido += AoConcluir;
        Closed += (_, _) => vm.Concluido -= AoConcluir;
    }
}
