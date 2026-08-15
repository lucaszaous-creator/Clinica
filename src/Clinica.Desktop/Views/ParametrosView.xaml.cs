using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Clinica.Desktop.ViewModels;

namespace Clinica.Desktop.Views;

public partial class ParametrosView : UserControl
{
    public ParametrosView() => InitializeComponent();

    // Confirma edições pendentes das grades antes do SalvarCommand executar
    // (o Click dispara antes do Command); sem isso, a célula em edição no
    // momento do clique podia ser descartada e a alteração se perdia.
    //
    // A grade de convênios saiu daqui na parcela 69: ela virou somente leitura (o
    // "Ativo" é um CheckBox de template, que não entra em modo de edição) e a de
    // famílias foi para a janela "Números das regras", que confirma no fechamento.
    private void Salvar_Click(object sender, RoutedEventArgs e)
    {
        foreach (var grid in new[] { GridModalidades, GridEspecialidades })
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        }
    }

    /// <summary>
    /// Duplo clique na linha abre o convênio — é o gesto que se espera de uma lista, e
    /// o botão "Abrir" continua ali para quem não o conhece.
    /// </summary>
    private void Catalogo_DuploClique(object sender, MouseButtonEventArgs e)
    {
        // O clique em cima do "Ativo" ou dos botões de ação já fez o que tinha de
        // fazer; abrir a janela por cima dele seria uma segunda ação não pedida.
        if (EhControleDeAcao(e.OriginalSource as DependencyObject)) return;

        if (DataContext is ParametrosViewModel vm && GridCatalogo.SelectedItem is ConvenioEdicao alvo)
            vm.EditarConvenioCommand.Execute(alvo);
    }

    private static bool EhControleDeAcao(DependencyObject? origem)
    {
        for (var no = origem; no is not null; no = VisualTreeHelper.GetParent(no))
        {
            if (no is ButtonBase) return true;
            if (no is DataGridRow) return false; // subiu até a linha sem achar botão
        }
        return false;
    }
}
