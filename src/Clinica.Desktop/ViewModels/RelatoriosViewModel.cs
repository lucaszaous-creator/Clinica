using System.Collections.ObjectModel;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Relatórios: taxa de baixa (métrica-chave), faturamento por convênio e envelhecimento das pendências.</summary>
public partial class RelatoriosViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty] private DateTime _inicio;
    [ObservableProperty] private DateTime _fim;
    [ObservableProperty] private ResumoFaturamento? _resumo;

    public ObservableCollection<FaturamentoPorConvenio> PorConvenio { get; } = new();
    public ObservableCollection<FaixaEnvelhecimento> Envelhecimento { get; } = new();

    public RelatoriosViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var hoje = DateTime.Today;
        _inicio = new DateTime(hoje.Year, hoje.Month, 1); // início do mês corrente
        _fim = _inicio.AddMonths(1).AddDays(-1);           // fim do mês corrente
    }

    public Task CarregarAsync() => Gerar();

    [RelayCommand]
    private async Task Gerar()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RelatorioService>();

        var rel = await service.GerarAsync(
            DateOnly.FromDateTime(Inicio),
            DateOnly.FromDateTime(Fim),
            DateOnly.FromDateTime(DateTime.Today));

        Resumo = rel.Resumo;

        PorConvenio.Clear();
        foreach (var c in rel.PorConvenio) PorConvenio.Add(c);

        Envelhecimento.Clear();
        foreach (var f in rel.Envelhecimento) Envelhecimento.Add(f);
    }
}
