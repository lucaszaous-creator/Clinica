using System.Windows.Controls;

namespace Clinica.Clinico.Views;

/// <summary>
/// A tela do paciente: identidade no topo, as cinco seções clínicas em abas, e a largura
/// inteira para o conteúdo — sem a coluna de lista que antes se repetia em toda tela.
/// </summary>
public partial class PacienteWorkspaceView : UserControl
{
    public PacienteWorkspaceView() => InitializeComponent();
}
