using System.Windows.Controls;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A lista das passagens de enfermagem do paciente, com Corrigir e Cancelar — um XAML para
/// as três portas (janela da sala, seção do Consultório, tela da Enfermagem). O DataContext
/// é o <see cref="EvolucaoEnfermagemViewModel"/> de quem hospeda.
/// </summary>
public partial class PassagensDeEnfermagemView : UserControl
{
    public PassagensDeEnfermagemView() => InitializeComponent();
}
