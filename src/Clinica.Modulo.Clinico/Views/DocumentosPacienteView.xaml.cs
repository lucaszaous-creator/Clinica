using System.Windows.Controls;
namespace Clinica.Clinico.Views;
public partial class DocumentosPacienteView : UserControl
{
    public DocumentosPacienteView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ViewModels.PacienteWorkspaceViewModel vm && !vm.AtualizarDocumentosCommand.IsRunning)
                await vm.AtualizarDocumentosCommand.ExecuteAsync(null);
        };
    }
}
