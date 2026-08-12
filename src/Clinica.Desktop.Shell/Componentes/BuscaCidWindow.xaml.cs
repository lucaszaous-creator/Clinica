using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// Procurar um código da CID-10. Ver <see cref="BuscaCidViewModel"/> para as decisões.
///
/// Devolve o código escolhido em <see cref="Escolhido"/>; nulo quando a pessoa fechou sem
/// escolher — e aí o campo de trás fica como estava, que é o certo: ela pode ter aberto a
/// janela só para conferir o que o código que já digitou quer dizer.
/// </summary>
public partial class BuscaCidWindow : Window
{
    private readonly BuscaCidViewModel _vm;

    public BuscaCidWindow(BuscaCidViewModel vm)
    {
        InitializeComponent();

        _vm = vm;
        DataContext = vm;

        vm.Escolheu += AoEscolher;
        Closed += (_, _) => vm.Escolheu -= AoEscolher;
    }

    public string? Escolhido => _vm.Escolhido;

    private void AoEscolher()
    {
        DialogResult = true;
        Close();
    }
}
