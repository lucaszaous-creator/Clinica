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

    /// <summary>
    /// PONTO ÚNICO de abertura (parcela 73). Devolve o código escolhido, ou <c>null</c>
    /// quando a pessoa fechou sem escolher — e aí quem chamou deixa o campo como estava,
    /// que é o certo: ela pode ter aberto a janela só para conferir o que o código já
    /// digitado quer dizer.
    ///
    /// ⚠️ Ele nasce porque eram TRÊS montagens à mão da mesma janela (o documento clínico,
    /// a lista de problemas e agora a sessão), e três montagens divergem na primeira
    /// correção — escopo, dono, o que fazer com o cancelamento. É a lição do
    /// <c>ColetaDeTermo.Abrir</c> da parcela 66.
    /// </summary>
    public static string? Perguntar(string? cidAtual)
    {
        var janela = new BuscaCidWindow(new BuscaCidViewModel(cidAtual))
        {
            Owner = JanelaDona.Atual()
        };

        return janela.ShowDialog() == true ? janela.Escolhido : null;
    }

    private void AoEscolher()
    {
        DialogResult = true;
        Close();
    }
}
