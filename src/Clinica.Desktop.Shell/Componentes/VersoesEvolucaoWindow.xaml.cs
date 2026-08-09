using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// O histórico de correções de uma sessão do prontuário.
///
/// Mora no shell porque a sessão é lida em DOIS módulos (Recepção e Consultório) — duas
/// janelas divergiriam na primeira correção. Somente leitura: ver
/// <see cref="VersoesEvolucaoViewModel"/>.
/// </summary>
public partial class VersoesEvolucaoWindow : Window
{
    public VersoesEvolucaoWindow()
    {
        InitializeComponent();
    }
}
