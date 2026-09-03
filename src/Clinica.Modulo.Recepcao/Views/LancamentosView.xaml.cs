using System.Windows;
using System.Windows.Controls;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

public partial class LancamentosView : UserControl
{
    public LancamentosView()
    {
        InitializeComponent();
        IsVisibleChanged += AoMudarVisibilidade;
    }

    /// <summary>
    /// Relê o período quando esta aba VOLTA a ficar visível.
    ///
    /// É o espelho do gancho do Novo atendimento, e pela mesma razão: o
    /// <c>TelaComAbas</c> monta cada aba UMA vez e a guarda. Sem isto, quem abrisse a
    /// conferência, voltasse para lançar e voltasse de novo veria a lista de ANTES do
    /// lançamento — "cadê o que eu acabei de lançar?" com a resposta escondida atrás do
    /// botão Atualizar.
    ///
    /// ⚠️ Só quando o período inclui HOJE. Uma sessão lançada na aba do lado nasce com a
    /// data que aquela tela mostra — hoje, no caso normal —, então um período antigo não
    /// pode ter mudado e reconsultá-lo a cada troca de aba pagaria até 366 dias de
    /// consulta para não mudar nada. O lançamento retroativo para um dia que está no
    /// período aberto é o caso que sobra, e para ele existe o botão Atualizar.
    /// </summary>
    private void AoMudarVisibilidade(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;
        if (DataContext is not LancamentosViewModel vm) return;

        var hoje = System.DateTime.Today;
        if (vm.De.Date <= hoje && hoje <= vm.Ate.Date) _ = vm.BuscarAsync();
    }
}
