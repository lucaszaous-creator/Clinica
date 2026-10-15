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
        Tabela.SizeChanged += (_, _) => AjustarColunas();
    }

    /// <summary>Soma dos mínimos das colunas COM a nota (180 + 140 + 150 + 160 + 250 = 880), mais a
    /// barra de rolagem vertical e a borda. Abaixo disso a tabela rolaria de lado.</summary>
    private const double LarguraComNota = 910;

    /// <summary>
    /// Na janela estreita (880–1024 px) a NOTA sai da tabela, e o resto cabe sem rolar de lado:
    /// tabela que rola para a direita esconde justamente a coluna de AÇÃO. A nota continua no
    /// prontuário e volta sozinha quando a janela alarga.
    /// </summary>
    private void AjustarColunas()
    {
        var v = Tabela.ActualWidth >= LarguraComNota ? Visibility.Visible : Visibility.Collapsed;
        if (ColunaNota.Visibility != v) ColunaNota.Visibility = v;
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
