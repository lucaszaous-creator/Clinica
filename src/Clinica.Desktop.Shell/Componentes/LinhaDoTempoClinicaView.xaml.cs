using System.Windows.Controls;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A linha do tempo clínica do paciente — o mesmo componente na ficha da Recepção, no
/// Consultório e na tela da Enfermagem.
///
/// Ele mora no SHELL pela razão de sempre (parcela 36): copiar a tela entre módulos daria
/// três leituras do mesmo prontuário divergindo na primeira correção — e o que elas
/// mostram é dado de saúde, com regra de acesso por natureza.
/// </summary>
public partial class LinhaDoTempoClinicaView : UserControl
{
    public LinhaDoTempoClinicaView() => InitializeComponent();
}
