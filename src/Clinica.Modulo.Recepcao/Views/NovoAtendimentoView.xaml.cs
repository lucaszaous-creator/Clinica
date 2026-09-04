using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

public partial class NovoAtendimentoView : UserControl
{
    public NovoAtendimentoView()
    {
        InitializeComponent();
        IsVisibleChanged += AoMudarVisibilidade;
    }

    /// <summary>
    /// Revalida o paciente escolhido toda vez que esta aba VOLTA a ficar visível, e põe o
    /// foco na busca quando ainda não há ninguém escolhido.
    ///
    /// Por que no code-behind, e por que <c>IsVisibleChanged</c>
    /// ---------------------------------------------------------
    /// A conferência do dia virou a aba "Lançamentos" (set/2026), e o ESTORNO foi com ela.
    /// O <c>TelaComAbas</c> monta cada aba UMA vez e a guarda — voltar para cá não
    /// reconstrói nada e não chama <c>CarregarAsync</c> de novo. Sem este gancho a tela
    /// continuaria dizendo "já lançado hoje" sobre um atendimento que a aba do lado acabou
    /// de estornar, e esconderia o horário que o estorno devolveu para a agenda.
    ///
    /// Visibilidade e FOCO são assunto da VIEW: é ela que sabe que existe uma aba por cima,
    /// e foco não se dá pelo ViewModel.
    /// </summary>
    private void AoMudarVisibilidade(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Só na VOLTA. Sair da aba não precisa reler nada, e reler nas duas pontas
        // dobraria as consultas a cada troca de aba.
        if (e.NewValue is not true) return;
        if (DataContext is not NovoAtendimentoViewModel vm) return;

        vm.RevalidarPacienteEscolhido();

        // O foco vai para a busca só quando ela EXISTE na tela — com paciente escolhido o
        // campo está colapsado, e mandar foco para um elemento invisível é um no-op que
        // deixaria o teclado no lugar errado.
        if (vm.SemPaciente) FocarBusca();
    }

    /// <summary>
    /// Põe o cursor na busca. O <c>Dispatcher</c> mora DENTRO do componente desde que o
    /// bloco subiu para o shell — a razão dele (a árvore pode não estar pronta, e o
    /// <c>Focus()</c> devolve false em silêncio) é do componente, não desta tela.
    /// </summary>
    private void FocarBusca() => Busca.Focar();
}
