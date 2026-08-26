using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A consulta de enfermagem (COFEN 358/2009) numa janela que MAXIMIZA — pedido da clínica
/// (parcela 88, 5ª rodada).
///
/// ⚠️ PONTO ÚNICO de abertura, e ela recebe o MESMO ViewModel da tela de trás: são duas
/// portas (a janela da sala de infusão e a seção do Consultório), e duas montagens
/// divergiriam na primeira correção.
/// </summary>
public partial class ConsultaDeEnfermagemWindow : Window
{
    private ConsultaDeEnfermagemWindow(EvolucaoEnfermagemViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    /// <summary>
    /// Abre a consulta sobre a janela ATIVA.
    ///
    /// ⚠️ O dono é <see cref="JanelaDona.Atual"/>, nunca a <c>MainWindow</c>: a passagem
    /// pode estar sendo escrita dentro de uma janela modal (a da sala de infusão), e ali a
    /// consulta nasceria ATRÁS dela — quem clicou concluiria que a caixinha não fez nada
    /// (a lição da parcela 58).
    /// </summary>
    public static void Abrir(EvolucaoEnfermagemViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);

        new ConsultaDeEnfermagemWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();
    }

    private void Fechar(object sender, RoutedEventArgs e) => Close();
}
