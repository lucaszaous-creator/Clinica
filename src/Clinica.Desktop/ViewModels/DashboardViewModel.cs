using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Tela inicial: 2º códigos e consultas pendentes com semáforo, filtros por convênio e urgência.</summary>
public partial class DashboardViewModel : ObservableObject
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
}
