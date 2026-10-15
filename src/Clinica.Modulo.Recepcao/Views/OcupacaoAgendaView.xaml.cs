using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>
/// A aba "Visão geral" da Agenda. A View liga e desliga o relógio (1 min) no
/// Loaded/Unloaded — timer no ViewModel manteria viva cada tela já trocada.
/// </summary>
public partial class OcupacaoAgendaView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(1) };

    public OcupacaoAgendaView(IServiceProvider servicos)
    {
        InitializeComponent();
        var vm = new OcupacaoAgendaViewModel(servicos);
        DataContext = vm;
        _timer.Tick += (_, _) => vm.Bater();
        Loaded += (_, _) => { vm.Iniciar(); _timer.Start(); };
        Unloaded += (_, _) => { _timer.Stop(); vm.Parar(); };
    }

    /// <summary>
    /// "Mais ações" — o menu é montado em CÓDIGO pelo motivo do "⋯" da lista do dia: um
    /// ContextMenu declarado vive num Popup fora da árvore, e binding errado ali falha calado.
    /// Os itens que mexem na agenda só aparecem para quem pode (a guarda continua no comando).
    /// </summary>
    private void AoAbrirMaisAcoes(object sender, RoutedEventArgs e)
    {
        if (DataContext is not OcupacaoAgendaViewModel vm) return;
        var menu = new ContextMenu { PlacementTarget = MaisAcoes, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        void Item(string rotulo, System.Windows.Input.ICommand comando) =>
            menu.Items.Add(new MenuItem { Header = rotulo, Command = comando });
        if (vm.PodeMexerNaAgenda)
        {
            Item("Fechar agenda (férias, feriado, folga)…", vm.FecharAgendaCommand);
            Item("Horários e travas dos profissionais…", vm.HorariosETravasCommand);
            Item("Lista de espera…", vm.ListaDeEsperaCommand);
            Item("Conciliar horários parados…", vm.ConciliarAgendaCommand);
            menu.Items.Add(new Separator());
        }
        Item("Imprimir a folha do dia", vm.ImprimirFolhaCommand);
        menu.IsOpen = true;
    }
}
