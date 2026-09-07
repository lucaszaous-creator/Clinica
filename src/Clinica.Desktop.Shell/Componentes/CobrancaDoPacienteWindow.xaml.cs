using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A cobrança no balcão: as contas vencidas do paciente, com "Receber" em cada uma. Não
/// fecha ao receber — quem recebe uma parcela costuma receber a próxima em seguida. Quem
/// abriu pergunta <see cref="CobrancaDoPacienteViewModel.Mudou"/> depois do ShowDialog
/// para reler o alerta.
/// </summary>
public partial class CobrancaDoPacienteWindow : Window
{
    public CobrancaDoPacienteViewModel ViewModel { get; }

    public CobrancaDoPacienteWindow(CobrancaDoPacienteViewModel vm)
    {
        InitializeComponent();
        ViewModel = vm;
        DataContext = vm;
        Loaded += async (_, _) => await vm.CarregarAsync();
    }
}
