using System.Windows;

namespace Clinica.Desktop.Shell.Componentes.Cadastro;

public partial class CadastroPacienteWindow : Window
{
    public CadastroPacienteWindow(CadastroPacienteViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        // O Windows maximiza na área útil do monitor; limites fixos cortariam a janela.
        var concluido = false;
        void Concluir() { concluido = true; DialogResult = true; }
        vm.Concluido += Concluir;
        Closing += (_, e) => { if (vm.Salvando && !concluido) e.Cancel = true; };
        Closed += (_, _) => vm.Concluido -= Concluir;
    }
}
