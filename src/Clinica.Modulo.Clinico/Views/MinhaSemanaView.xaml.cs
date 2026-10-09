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
            _painelBusca = AbasAgendaWeb.Montar(ConteudoAgendaBusca, "Grade da semana", () => AgendaMedicoWeb.Semana(vm), m => AgendaMedicoWeb.ExecutarSemana(vm, m));
            if (Window.GetWindow(this) is { } janela) janela.Closed += (_, _) => _painelBusca.Dispose();
        };
    }
}
