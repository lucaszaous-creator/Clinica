using System.Windows.Controls;

namespace Clinica.Financeiro.Views;

/// <summary>
/// Quem me deve: conta de paciente vencida, agrupada por pessoa, envelhecida por faixa,
/// com o WhatsApp a um clique. A situação é calculada — não existe campo "inadimplente"
/// no cadastro, e não deve existir.
/// </summary>
public partial class InadimplenciaView : UserControl
{
    public InadimplenciaView()
    {
        InitializeComponent();
    }
}
