using System.Windows.Controls;
using System.Windows;
using System.ComponentModel;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>
/// Agenda multiprofissional: uma coluna por profissional, a linha do tempo descendo pela
/// régua da esquerda e o vão livre clicável.
///
/// A View liga e desliga a releitura de fundo (parcela 62). Ela mora aqui, e não no
/// ViewModel, pela mesma razão da Fila: o shell constrói uma tela nova a cada navegação,
/// e um <c>DispatcherTimer</c> rodando manteria vivo cada ViewModel já trocado.
/// </summary>
public partial class AgendaView : UserControl
{
    private Clinica.Desktop.Shell.WebClinica.PainelClinicoWeb? _painelBusca;

    public AgendaView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => AjustarLaterais();

        Loaded += (_, _) =>
        {
            if (DataContext is not AgendaViewModel vm) return;
            vm.PropertyChanged -= AoMudarModo;
            vm.PropertyChanged += AoMudarModo;
            if (_painelBusca is null)
            {
                var adaptador = new AgendaBuscaWeb(vm);
                _painelBusca = Clinica.Desktop.Shell.WebClinica.FiltrosAgendaWeb.Montar(ConteudoAgendaBusca, adaptador.Estado, adaptador.Executar);
                if (Window.GetWindow(this) is { } janela) janela.Closed += (_, _) => _painelBusca.Dispose();
            }
            AjustarLaterais(); vm.AoEntrarEmCena();
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is not AgendaViewModel vm) return;
            vm.PropertyChanged -= AoMudarModo; vm.AoSairDeCena();
        };
    }

    private void AoMudarModo(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgendaViewModel.ModoSemana)) AjustarLaterais();
    }
    private void AjustarLaterais()
    {
        var exibirCalendario = ActualWidth >= 1250;
        LateralCalendario.Visibility = exibirCalendario ? Visibility.Visible : Visibility.Collapsed;
        ColunaCalendario.Width = new GridLength(exibirCalendario ? 224 : 0);
        var semana = DataContext is AgendaViewModel { ModoSemana: true };
        LateralVagas.Visibility = semana ? Visibility.Collapsed : Visibility.Visible;
        ColunaVagas.Width = new GridLength(semana ? 0 : ActualWidth < 1000 ? 224 : 264);
    }
}
