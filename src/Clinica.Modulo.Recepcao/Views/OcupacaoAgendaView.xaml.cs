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
}
