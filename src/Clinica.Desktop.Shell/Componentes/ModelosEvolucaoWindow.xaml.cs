using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Escolher, guardar e apagar modelos de evolução (parcela 63).
///
/// Mora no shell porque a evolução é escrita em DOIS módulos — a Recepção, pela ficha do
/// paciente, e o Consultório, pela tela de atendimento. Duas janelas divergiriam na
/// primeira correção; é o mesmo argumento que trouxe para cá o mapa corporal e a emissão
/// de documento na parcela 36.
///
/// A janela não grava nada na sessão: ela DEVOLVE o texto escolhido
/// (<see cref="ModelosEvolucaoViewModel.Resultado"/>), e quem o aplica na evolução é a
/// tela de trás — que continua exigindo o Salvar dela para efetivar. Mesma regra de
/// "repetir a sessão anterior" e "aplicar protocolo" do mapa corporal.
/// </summary>
public partial class ModelosEvolucaoWindow : Window
{
    private readonly ModelosEvolucaoViewModel _vm;

    public ModelosEvolucaoWindow(ModelosEvolucaoViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        DataContext = vm;

        vm.Aplicou += AoAplicar;
        Closed += (_, _) => vm.Aplicou -= AoAplicar;
    }

    /// <summary>O modelo escolhido, ou nulo quando a pessoa fechou sem aplicar.</summary>
    public ModeloAplicado? Escolhido => _vm.Resultado;

    private void AoAplicar()
    {
        DialogResult = true;
        Close();
    }
}
