using System.Windows;

namespace Clinica.Desktop.Shell.Componentes;

/// <summary>
/// A janela de escrever a sessão pelo Prontuário e pela ficha do paciente.
///
/// ⚠️ PONTO ÚNICO de abertura (o padrão da <see cref="ConsultaDeEnfermagemWindow"/> e do
/// <see cref="ColetaDeTermo"/>): são DUAS portas na Recepção hoje, e cada uma montando a
/// janela por conta própria daria duas montagens que divergem na primeira correção.
///
/// Ela devolve <c>true</c> quando ALGUMA gravação houve — é isso que faz a tela de trás
/// reler. Fechar sem gravar devolve <c>false</c> e não custa consulta nenhuma.
/// </summary>
public partial class EscreverSessaoWindow : Window
{
    private readonly EscreverSessaoViewModel _vm;

    private EscreverSessaoWindow(EscreverSessaoViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    /// <summary>
    /// Abre a sessão para escrever ou corrigir, sobre a janela ATIVA.
    ///
    /// ⚠️ O dono é <see cref="JanelaDona.Atual"/>, nunca a <c>MainWindow</c>: a porta pode
    /// ser uma janela modal, e ali esta nasceria ATRÁS dela — quem clicou concluiria que o
    /// botão não fez nada (a lição da parcela 58).
    /// </summary>
    public static bool Abrir(EscreverSessaoViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);

        var janela = new EscreverSessaoWindow(vm) { Owner = JanelaDona.Atual() };
        janela.ShowDialog();

        // O desfecho é "gravou alguma vez", e não o DialogResult: o Salvar NÃO fecha (é o
        // que destrava o anexo numa sessão nova), então quem responde é o ViewModel.
        return vm.Gravou;
    }

    private void Fechar(object remetente, RoutedEventArgs e) => Close();
}
