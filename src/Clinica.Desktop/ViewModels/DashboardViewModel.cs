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

    public DashboardViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var ops = new List<object> { "Todos" };
        ops.AddRange(Enum.GetValues<Convenio>().Cast<object>());
        OpcoesConvenio = ops;
    }

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();
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

    [RelayCommand]
    private Task Atualizar() => CarregarAsync();

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => AtualizarCommand;
}
