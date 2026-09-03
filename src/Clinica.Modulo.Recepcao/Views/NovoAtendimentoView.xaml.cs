using System.Windows;
using System.Windows.Controls;
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
    /// Revalida o paciente escolhido toda vez que esta aba VOLTA a ficar visível.
    ///
    /// Por que no code-behind, e por que <c>IsVisibleChanged</c>
    /// ---------------------------------------------------------
    /// A conferência do dia virou a aba "Lançamentos" (set/2026), e o ESTORNO foi com ela.
    /// O <c>TelaComAbas</c> monta cada aba UMA vez e a guarda — voltar para cá não
    /// reconstrói nada e não chama <c>CarregarAsync</c> de novo. Sem este gancho a tela
    /// continuaria dizendo "já lançado hoje" sobre um atendimento que a aba do lado acabou
    /// de estornar, e esconderia o horário que o estorno devolveu para a agenda.
    ///
    /// Visibilidade é assunto da VIEW: é ela que sabe que existe uma aba por cima. A
    /// ViewModel só expõe o que revalidar, e não tem como saber que sumiu da vista.
    /// </summary>
    private void AoMudarVisibilidade(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Só na VOLTA. Sair da aba não precisa reler nada, e reler nas duas pontas
        // dobraria as consultas a cada troca de aba.
        if (e.NewValue is not true) return;
        if (DataContext is NovoAtendimentoViewModel vm) vm.RevalidarPacienteEscolhido();
    }
}
