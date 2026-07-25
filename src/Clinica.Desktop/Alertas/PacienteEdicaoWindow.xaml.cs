using System.Windows;
using Clinica.Desktop.ViewModels;

namespace Clinica.Desktop.Alertas;

/// <summary>
/// Cadastro do paciente em janela própria (novo ou edição). A tela de Pacientes fica
/// só com a listagem; o cadastro é uma tarefa com começo, meio e fim — abre, preenche,
/// salva e fecha. Retorna <c>true</c> quando gravou, para a lista se recarregar.
/// </summary>
public partial class PacienteEdicaoWindow : Window
{
    public PacienteEdicaoWindow(PacienteEdicaoViewModel vm, int? pacienteId)
    {
        InitializeComponent();
        DataContext = vm;

        vm.Salvou += AoSalvar;
        Closed += (_, _) => vm.Salvou -= AoSalvar;

        Loaded += async (_, _) => await vm.CarregarAsync(pacienteId);
    }

    private void AoSalvar()
    {
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();
}
