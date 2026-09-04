using System.Windows.Controls;

namespace Clinica.Clinico.Views;

/// <summary>
/// A capa do paciente: a ficha em leitura e a lista de problemas.
/// Ver <see cref="ViewModels.PacienteCapaViewModel"/> para o que ela responde e por que
/// não edita cadastro.
/// </summary>
public partial class PacienteCapaView : UserControl
{
    public PacienteCapaView() => InitializeComponent();
}
