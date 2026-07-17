using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Alertas;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Guias já baixadas no período, com a opção de ESTORNAR (reabrir a pendência).</summary>
public partial class FaturadosViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<CodigoFaturamento> Baixados { get; } = new();

    [ObservableProperty] private DateTime _inicio;
    [ObservableProperty] private DateTime _fim;

    public FaturadosViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        var hoje = DateTime.Today;
        _inicio = new DateTime(hoje.Year, hoje.Month, 1);
        _fim = _inicio.AddMonths(1).AddDays(-1);
    }

    public Task CarregarAsync() => Buscar();

    [RelayCommand]
    private async Task Buscar()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        var lista = await db.Codigos
            .Include(c => c.Atendimento!).ThenInclude(a => a.Paciente!)
            .Where(c => c.DataBaixa != null
                        && c.DataBaixa >= DateOnly.FromDateTime(Inicio)
                        && c.DataBaixa <= DateOnly.FromDateTime(Fim))
            .OrderByDescending(c => c.DataBaixa)
            .ToListAsync();

        Baixados.Clear();
        foreach (var c in lista) Baixados.Add(c);
    }

    [RelayCommand]
    private async Task Estornar(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;

        var confirma = MessageBox.Show(
            $"Estornar a baixa desta guia de {codigo.Atendimento?.Paciente?.Nome}?\n\n" +
            "A pendência voltará a aparecer no painel para ser faturada novamente.",
            "Confirmar estorno", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirma != MessageBoxResult.Yes) return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FaturamentoService>();
        await service.EstornarBaixaAsync(codigo.Id, "estorno pela tela de Faturados", Environment.UserName);

        await Buscar();
    }

    [RelayCommand]
    private async Task Glosar(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;

        var descricao = $"{codigo.Atendimento?.Paciente?.Nome} — {codigo.Tipo} (guia {codigo.NumeroGuiaReal})";
        var dialog = new GlosaWindow(descricao) { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;

        using var scope = _scopeFactory.CreateScope();
        var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
        await glosas.RegistrarAsync(codigo.Id, dialog.DataGlosa, dialog.Motivo);

        await Buscar();
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => BuscarCommand;
}
