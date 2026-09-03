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
    /// Põe o cursor na busca, depois que o WPF terminou de montar/religar a tela.
    ///
    /// ⚠️ O <c>Dispatcher</c> não é cerimônia: no instante do <c>IsVisibleChanged</c> o
    /// <c>TextBox</c> pode ainda não estar carregado (a aba acabou de ser materializada) e
    /// o <c>Focus()</c> devolve false em silêncio. Adiar para depois do Render dá o foco
    /// com a árvore pronta.
    /// </summary>
    private void FocarBusca() => Dispatcher.BeginInvoke(
        System.Windows.Threading.DispatcherPriority.Input,
        new Action(() => CampoBuscaPaciente.Focus()));

    /// <summary>
    /// O teclado da busca: ↓ desce para a lista, Enter escolhe o primeiro.
    ///
    /// É o gesto que a recepcionista repete quarenta vezes por dia — digitar três letras e
    /// escolher —, e sem isto ele exige tirar a mão do teclado para clicar. Com a lista
    /// vazia as duas teclas não fazem nada, em vez de mover o foco para o vazio.
    ///
    /// ⚠️ `PreviewKeyDown`, e não `KeyDown`: o `TextBox` trata as setas como movimento do
    /// cursor e marca o evento, então no `KeyDown` a seta para baixo nunca chegaria aqui.
    /// </summary>
    private void AoTeclarNaBusca(object sender, KeyEventArgs e)
    {
        if (ListaDePacientes.Items.Count == 0) return;

        if (e.Key == Key.Down)
        {
            if (ListaDePacientes.SelectedIndex < 0) ListaDePacientes.SelectedIndex = 0;
            (ListaDePacientes.ItemContainerGenerator
                .ContainerFromIndex(ListaDePacientes.SelectedIndex) as ListBoxItem)?.Focus();
            e.Handled = true;
            return;
        }

        // Enter com UM resultado é a escolha óbvia — quem digitou o CPF inteiro não deve
        // precisar de mais um gesto. Com vários, escolher o primeiro seria chutar; a tecla
        // então só desce para a lista, e a escolha continua sendo de quem está lá.
        if (e.Key == Key.Enter)
        {
            if (ListaDePacientes.Items.Count == 1) ListaDePacientes.SelectedIndex = 0;
            else (ListaDePacientes.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
    }
}
