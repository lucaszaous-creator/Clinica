using System.Windows;

namespace Clinica.Desktop.Shell.Componentes.Cadastro;

public partial class CadastroPacienteWindow : Window
{
    public CadastroPacienteWindow(CadastroPacienteViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
        void Concluir() => DialogResult = true;
        vm.Concluido += Concluir;
        Closed += (_, _) => vm.Concluido -= Concluir;
    }
}
