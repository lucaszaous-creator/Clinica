using System.Windows.Input;
using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Tela inicial: 2º códigos e consultas pendentes com semáforo, filtros por convênio e urgência.</summary>
public partial class DashboardViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;
    private readonly List<PendenciaCodigo> _todos = new();

    public ObservableCollection<PendenciaCodigo> Codigos { get; } = new();
    public ObservableCollection<PendenciaConsulta> Consultas { get; } = new();

    public IReadOnlyList<object> OpcoesConvenio { get; }
    public IReadOnlyList<object> OpcoesUrgencia { get; } =
        new object[] { "Todos", NivelUrgencia.Vermelho, NivelUrgencia.Amarelo, NivelUrgencia.Verde };

    [ObservableProperty] private object _filtroConvenio = "Todos";
    [ObservableProperty] private object _filtroUrgencia = "Todos";
    [ObservableProperty] private int _total;

    /// <summary>Total de códigos/guias pendentes de baixa (para a faixa de alerta do topo).</summary>
    public int TotalCodigos => _todos.Count;
    public bool TemPendencias => _todos.Count > 0;

    // KPIs do painel
    public int CodigosUrgentes => _todos.Count(p => p.Urgencia == NivelUrgencia.Vermelho);
    public int ConsultasARenovar => Consultas.Count;

    public event Action<int>? PendenciasAtualizadas;
    public event Action<int>? AbrirBaixaSolicitado;

    public DashboardViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
        var ops = new List<object> { "Todos" };
        ops.AddRange(Enum.GetValues<Convenio>().Cast<object>());
        OpcoesConvenio = ops;
    }

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();
        pendencias.JanelaAlertaConsultaDias = Configuracao.PreferenciasStore.Carregar().JanelaAlertaConsultaDias;
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        _todos.Clear();
        _todos.AddRange(await pendencias.CodigosPendentesAsync(hoje));

        Consultas.Clear();
        foreach (var c in await pendencias.ConsultasAVencerAsync(hoje))
            Consultas.Add(c);

        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        IEnumerable<PendenciaCodigo> filtrados = _todos;
        if (FiltroConvenio is Convenio cv)
            filtrados = filtrados.Where(p => p.Convenio == cv);
        if (FiltroUrgencia is NivelUrgencia u)
            filtrados = filtrados.Where(p => p.Urgencia == u);

        Codigos.Clear();
        foreach (var c in filtrados) Codigos.Add(c);

        Total = _todos.Count + Consultas.Count;
        OnPropertyChanged(nameof(TotalCodigos));
        OnPropertyChanged(nameof(TemPendencias));
        OnPropertyChanged(nameof(CodigosUrgentes));
        OnPropertyChanged(nameof(ConsultasARenovar));
        PendenciasAtualizadas?.Invoke(Total);
    }

    partial void OnFiltroConvenioChanged(object value) => AplicarFiltro();
    partial void OnFiltroUrgenciaChanged(object value) => AplicarFiltro();

    [RelayCommand]
    private void DarBaixa(PendenciaCodigo? codigo)
    {
        if (codigo is not null)
            AbrirBaixaSolicitado?.Invoke(codigo.CodigoId);
    }

    /// <summary>Baixa em lote das linhas selecionadas (Ctrl/Shift + clique na tabela).</summary>
    [RelayCommand]
    private async Task DarBaixaEmLote(System.Collections.IList? selecionados)
    {
        var itens = selecionados?.OfType<PendenciaCodigo>().ToList() ?? new List<PendenciaCodigo>();
        if (itens.Count == 0)
        {
            _dialogo.Aviso("Baixa em lote",
                "Selecione uma ou mais linhas da tabela (Ctrl+clique ou Shift+clique) antes de usar a baixa em lote.");
            return;
        }

        var janela = new Alertas.BaixaLoteWindow(itens)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (janela.ShowDialog() != true) return;

        var feitas = 0;
        using (var scope = _scopeFactory.CreateScope())
        {
            var faturamento = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
            foreach (var linha in janela.Linhas.Where(l => !string.IsNullOrWhiteSpace(l.NumeroGuia)))
            {
                await faturamento.DarBaixaAsync(linha.CodigoId, janela.DataBaixa,
                    linha.NumeroGuia!.Trim(), Environment.UserName, "baixa em lote");
                feitas++;
            }
        }

        if (feitas == 0)
            _dialogo.Aviso("Baixa em lote", "Nenhuma linha tinha número de guia — nada foi baixado.");

        await CarregarAsync();
    }

    /// <summary>Renova a consulta direto do card "Consultas a renovar" do painel.</summary>
    [RelayCommand]
    private async Task Renovar(PendenciaConsulta? item)
    {
        if (item is null) return;
        if (!_dialogo.Confirmar("Renovar consulta",
                $"Gerar/renovar a consulta de {item.PacienteNome} para hoje?")) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ConsultaService>();
            await service.RenovarAsync(item.PacienteId, DateOnly.FromDateTime(DateTime.Today));
        }
        catch (Exception ex)
        {
            _dialogo.Aviso("Renovar consulta", ex.Message);
        }

        await CarregarAsync();
    }

    [RelayCommand]
    private Task Atualizar() => CarregarAsync();

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => AtualizarCommand;
}
