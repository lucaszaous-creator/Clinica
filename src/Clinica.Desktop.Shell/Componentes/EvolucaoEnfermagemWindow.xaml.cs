using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A janela onde a enfermagem escreve o que observou no paciente (parcela 71).
///
/// ⚠️ PONTO ÚNICO de abertura. São quatro portas — a linha da fila da sala, a barra da
/// folha de execução, o prontuário da Recepção e o do Consultório —, e quatro montagens da
/// mesma janela divergiriam na primeira correção (escopo, dono, recarga). É a lição do
/// <c>ColetaDeTermo.Abrir</c> da parcela 66.
/// </summary>
public partial class EvolucaoEnfermagemWindow : Window
{
    public EvolucaoEnfermagemWindow(EvolucaoEnfermagemViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    /// <summary>
    /// Abre a evolução de enfermagem do paciente. <paramref name="prescricaoId"/> nulo é a
    /// passagem AVULSA — curativo, observação, triagem —, e a janela diz isso em vez de
    /// deixar a pessoa supor.
    /// </summary>
    public static void Abrir(
        IServiceScopeFactory escopos, Clinica.Desktop.Controls.IDialogoService dialogo,
        int pacienteId, string paciente,
        int? prescricaoId = null, string? folha = null, int? agendamentoId = null)
    {
        var janela = new EvolucaoEnfermagemWindow(
            new EvolucaoEnfermagemViewModel(
                escopos, dialogo, pacienteId, paciente, prescricaoId, folha, agendamentoId))
        {
            // O dono é a janela ATIVA, nunca a MainWindow: com um modal aberto, esta
            // nasceria ATRÁS dele e quem clicou concluiria que o botão não fez nada.
            Owner = JanelaDona.Atual()
        };

        janela.ShowDialog();
    }
}
