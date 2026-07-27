using System.Windows;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// Janela do horário da agenda. Quem fecha é o ViewModel, avisando pelo evento
/// <see cref="AgendamentoEdicaoViewModel.Concluido"/> — assim o comando de salvar
/// continua assíncrono e a janela não precisa saber nada dos serviços.
/// </summary>
public partial class AgendamentoWindow : Window
{
    public AgendamentoWindow(AgendamentoEdicaoViewModel vm)
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
