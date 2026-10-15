using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Clinica.Application.Modelos;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Recepcao.ViewModels;

namespace Clinica.Recepcao.Views;

/// <summary>
/// Agenda multiprofissional: uma coluna por profissional, a linha do tempo descendo pela
/// régua da esquerda e o vão livre clicável.
///
/// A View liga e desliga a releitura de fundo (parcela 62). Ela mora aqui, e não no
/// ViewModel, pela mesma razão da Fila: o shell constrói uma tela nova a cada navegação,
/// e um <c>DispatcherTimer</c> rodando manteria vivo cada ViewModel já trocado.
///
/// Também moram aqui, por serem apresentação, a busca da grade (<see cref="FiltroListaAgenda"/>
/// sobre a vista dos blocos de cada coluna) e o resumo dos pedidos da enfermagem, com a
/// janela que abre a lista inteira.
/// </summary>
public partial class AgendaView : UserControl
{
    private readonly FiltroListaAgenda _filtro = new(item => item is BlocoAgendaVisual { Cartao: { } l }
        ? new HorarioFiltravel(l.Paciente, l.Modalidade, SituacaoVisualAgenda.Grupo(l.Situacao, l.Etapa))
        : null, "no período selecionado");
    private AgendaViewModel? _ligado;
    private PedidosEnfermagemViewModel? _pedidos;
    private Window? _janelaPedidos;

    public AgendaView()
    {
        InitializeComponent();
        BarraFiltros.DataContext = _filtro;
        _filtro.Mudou += AoMudarFiltro;
        SizeChanged += (_, _) => AjustarLaterais();

        Loaded += (_, _) =>
        {
            if (DataContext is not AgendaViewModel vm) return;
            vm.PropertyChanged -= AoMudarModo;
            vm.PropertyChanged += AoMudarModo;
            Ligar(vm);
            _pedidos?.Iniciar(); AjustarLaterais(); vm.AoEntrarEmCena();
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is not AgendaViewModel vm) return;
            _pedidos?.Parar(); vm.PropertyChanged -= AoMudarModo; vm.AoSairDeCena();
        };
    }

    // ==================== Busca e filtros ====================

    private void Ligar(AgendaViewModel vm)
    {
        if (ReferenceEquals(vm, _ligado)) return;
        if (_ligado is not null) _ligado.ColunasPlanejamento.CollectionChanged -= AoMudarColunas;
        _ligado = vm;
        vm.ColunasPlanejamento.CollectionChanged += AoMudarColunas;
        _pedidos = new PedidosEnfermagemViewModel(vm.EscoposPlanejamento, () => vm.Dia, data =>
        {
            vm.MostrarTodosOsProfissionais = true; vm.FiltroProfissional = null; vm.FiltroSala = null;
            vm.ModoSemana = false; vm.Dia = data;
        });
        AvisoEnfermagem.DataContext = _pedidos;
        AplicarFiltro(refazer: false);
    }

    private void AoMudarColunas(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Colunas novas trazem listas de blocos novas: cada uma recebe o filtro já ativo.
        if (e.NewItems is { } novas)
            foreach (ColunaAgendaVisual coluna in novas)
                CollectionViewSource.GetDefaultView(coluna.Blocos).Filter = _filtro.Aceita;
        Recontar();
    }

    private void AoMudarFiltro() => AplicarFiltro(refazer: true);

    private void AplicarFiltro(bool refazer)
    {
        if (_ligado is not { } vm) return;
        foreach (var coluna in vm.ColunasPlanejamento)
        {
            var vista = CollectionViewSource.GetDefaultView(coluna.Blocos);
            if (vista.Filter is null) vista.Filter = _filtro.Aceita;
            else if (refazer) vista.Refresh();
        }
        Recontar();
    }

    private void Recontar()
    {
        if (_ligado is not { } vm) return;
        // Uma linha por horário: na visão por sala o mesmo horário não aparece duas vezes.
        var horarios = vm.ColunasPlanejamento.SelectMany(c => c.Blocos)
            .Where(b => b.Cartao is not null).DistinctBy(b => b.Cartao!.AgendamentoId).Cast<object>().ToList();
        _filtro.Recalcular(horarios, vm.Carregando);
    }

    // ==================== Pedidos da enfermagem ====================

    private async void AoVerPedidos(object sender, RoutedEventArgs e)
    {
        if (_pedidos is not { } pedidos) return;
        try
        {
            PedidosEnfermagemViewModel.Exigir();
            if (_janelaPedidos is not null) { _janelaPedidos.Activate(); return; }
            await pedidos.AtualizarAsync();
            pedidos.VoltarCommand.Execute(null);
            var janela = new Window
            {
                Title = "Pedidos da enfermagem", Width = 1020, Height = 700, MinWidth = 620, MinHeight = 460,
                MaxWidth = SystemParameters.WorkArea.Width, MaxHeight = SystemParameters.WorkArea.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (System.Windows.Media.Brush)FindResource("Brush.Fundo"),
                Content = new PedidosEnfermagemView(pedidos), Owner = JanelaDona.Atual()
            };
            void Fechar() => janela.Close();
            pedidos.PediuFechar += Fechar;
            _janelaPedidos = janela;
            try { janela.ShowDialog(); }
            finally
            {
                pedidos.PediuFechar -= Fechar;
                _janelaPedidos = null;
                pedidos.VoltarCommand.Execute(null);
                await pedidos.AtualizarAsync();
            }
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar("Pedidos da enfermagem na agenda", ex);
            MessageBox.Show(ex.Message, "Pedidos da enfermagem", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AoMudarModo(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgendaViewModel.ModoSemana)) AjustarLaterais();
        if (e.PropertyName == nameof(AgendaViewModel.Carregando)) Recontar();
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
