using System.Windows.Controls;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// O paciente das páginas de documentos — busca ou "Paciente selecionado". Ver o
/// comentário do XAML: o DataContext é a ViewModel da página.
/// </summary>
public partial class PacienteDaPaginaView : UserControl
{
    public PacienteDaPaginaView() => InitializeComponent();
}
