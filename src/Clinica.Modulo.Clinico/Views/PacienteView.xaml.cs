using System.Windows.Controls;

namespace Clinica.Clinico.Views;

/// <summary>A seção "Paciente" da tela do paciente (set/2026) — ver o XAML.</summary>
public partial class PacienteView : UserControl
{
    public PacienteView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is ViewModels.PacienteWorkspaceViewModel vm && !vm.Capa.Carregando)
                await vm.Capa.CarregarAsync();
        };
    }
}
