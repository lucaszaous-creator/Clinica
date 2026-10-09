using System.Windows;
using System.Windows.Controls;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell.WebClinica;
namespace Clinica.Clinico.Views;
public partial class MinhaSemanaView : UserControl
{
    private PainelClinicoWeb? _painelBusca;
    public MinhaSemanaView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_painelBusca is not null || DataContext is not MinhaSemanaViewModel vm) return;
            var adaptador = new AgendaMedicoWeb();
            _painelBusca = FiltrosAgendaWeb.Montar(ConteudoAgendaBusca, () => adaptador.Semana(vm), m => adaptador.ExecutarSemana(vm, m));
            if (Window.GetWindow(this) is { } janela) janela.Closed += (_, _) => _painelBusca.Dispose();
        };
    }
}
