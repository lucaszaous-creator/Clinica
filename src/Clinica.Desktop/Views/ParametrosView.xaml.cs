using System.Windows;
using System.Windows.Controls;

namespace Clinica.Desktop.Views;

public partial class ParametrosView : UserControl
{
    public ParametrosView() => InitializeComponent();

    // Confirma edições pendentes das grades antes do SalvarCommand executar
    // (o Click dispara antes do Command); sem isso, a célula em edição no
    // momento do clique podia ser descartada e a alteração se perdia.
    private void Salvar_Click(object sender, RoutedEventArgs e)
    {
        foreach (var grid in new[] { GridCatalogo, GridFamilias, GridModalidades, GridEspecialidades })
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        }
    }
}
