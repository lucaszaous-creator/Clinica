using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Janelas;

/// <summary>
/// Confirmação do fechamento da sessão. Quem fecha é o ViewModel, pelo evento
/// <see cref="FechamentoSessaoViewModel.Concluido"/> — a janela não conhece serviço.
///
/// Fechamento com aviso NÃO dispara o evento: a janela fica aberta mostrando o que
/// falhou, e sai pelo botão "Fechar". Sumir da tela com um "pronto" mandaria a
/// secretária embora achando que o pacote baixou.
/// </summary>
public partial class FechamentoSessaoWindow : Window
{
    /// <summary>O que de fato aconteceu — a fila usa para montar a confirmação.</summary>
    public ResultadoFechamento? Resultado { get; private set; }

    public FechamentoSessaoWindow(FechamentoSessaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        void AoConcluir(ResultadoFechamento resultado)
        {
            vm.Concluido -= AoConcluir;
            Resultado = resultado;
            DialogResult = true;
        }

        vm.Concluido += AoConcluir;
        Closed += (_, _) => vm.Concluido -= AoConcluir;
    }
}
