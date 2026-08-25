using System.Windows.Controls;
using Clinica.Clinico.ViewModels;

namespace Clinica.Clinico.Views;

/// <summary>
/// A tela do paciente: identidade no topo, as sete seções clínicas num rail à esquerda, e a
/// largura inteira para o conteúdo — sem a coluna de lista que antes se repetia em toda tela.
///
/// ⚠️ A View liga e desliga o relógio da barra de atendimento, e isso NÃO é conforto: o shell
/// constrói uma tela nova a cada navegação (<c>ShellViewModel.Navegar</c>), e um
/// <c>DispatcherTimer</c> ligado mantém viva a ViewModel que o criou — aqui, junto com as
/// SETE sub-ViewModels dela. Num turno de vinte pacientes seriam vinte workspaces abandonados
/// batendo a cada quinze segundos. É a mesma razão pela qual <c>MeuDiaView</c> faz assim
/// desde a parcela 38, e a primeira versão desta parcela ligava o relógio no ViewModel.
/// </summary>
public partial class PacienteWorkspaceView : UserControl
{
    public PacienteWorkspaceView()
    {
        InitializeComponent();

        Loaded += (_, _) => (DataContext as PacienteWorkspaceViewModel)?.IniciarRelogio();
        Unloaded += (_, _) => (DataContext as PacienteWorkspaceViewModel)?.PararRelogio();
    }
}
