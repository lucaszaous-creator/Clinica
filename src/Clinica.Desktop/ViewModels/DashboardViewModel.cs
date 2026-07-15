using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Tela inicial: lista de 2º códigos e consultas pendentes com semáforo. Resolve a dor da cliente.</summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<PendenciaCodigo> Codigos { get; } = new();
    public ObservableCollection<PendenciaConsulta> Consultas { get; } = new();

    [ObservableProperty]
    private int _total;

    /// <summary>Notifica o shell para atualizar o badge.</summary>
    public event Action<int>? PendenciasAtualizadas;

    /// <summary>Pede ao shell para abrir a tela de baixa de um código.</summary>
    public event Action<int>? AbrirBaixaSolicitado;

    public DashboardViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        Codigos.Clear();
        foreach (var c in await pendencias.CodigosPendentesAsync(hoje))
            Codigos.Add(c);

        Consultas.Clear();
        foreach (var c in await pendencias.ConsultasAVencerAsync(hoje))
            Consultas.Add(c);

        Total = Codigos.Count + Consultas.Count;
        PendenciasAtualizadas?.Invoke(Total);
    }

    [RelayCommand]
    private void DarBaixa(PendenciaCodigo? codigo)
    {
        if (codigo is not null)
            AbrirBaixaSolicitado?.Invoke(codigo.CodigoId);
    }

    [RelayCommand]
    private Task Atualizar() => CarregarAsync();
}
