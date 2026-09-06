using System.Windows;
using System.Windows.Controls;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

public partial class RetornosAMarcarView : UserControl
{
    /// <summary>
    /// Já ficou visível uma vez. A PRIMEIRA exibição é carregada pelo shell
    /// (<c>ICarregarAoAbrir</c>); reler também aqui seria a mesma consulta duas vezes na
    /// abertura.
    /// </summary>
    private bool _jaFicouVisivel;

    public RetornosAMarcarView()
    {
        InitializeComponent();
        IsVisibleChanged += AoMudarVisibilidade;
    }

    /// <summary>
    /// Relê a fila quando esta aba VOLTA a ficar visível — o espelho do gancho de
    /// Lançamentos, pela mesma razão: o <c>TelaComAbas</c> monta cada aba UMA vez e a
    /// guarda. A linha desta fila SOME quando o horário é marcado, e quem marca é a aba
    /// Marcar ao lado; sem a releitura, a recepcionista voltava e via o retorno que acabou
    /// de marcar ainda listado como pendente.
    /// </summary>
    private void AoMudarVisibilidade(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;
        if (!_jaFicouVisivel) { _jaFicouVisivel = true; return; }
        if (DataContext is RetornosAMarcarViewModel vm) _ = vm.CarregarAsync();
    }
}
