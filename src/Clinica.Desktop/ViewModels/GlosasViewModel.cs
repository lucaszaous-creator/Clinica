using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Controle de glosas: acompanha guias recusadas, reapresenta e marca recuperadas.</summary>
public partial class GlosasViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    public ObservableCollection<CodigoFaturamento> Glosas { get; } = new();

    /// <summary>Quando true, mostra só as glosas ainda não recuperadas.</summary>
    [ObservableProperty] private bool _somenteEmAberto = true;

    public GlosasViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    public Task CarregarAsync() => Buscar();

    partial void OnSomenteEmAbertoChanged(bool value) => _ = Buscar();

    [RelayCommand]
    private async Task Buscar()
    {
        using var scope = _scopeFactory.CreateScope();
        var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
        Glosas.Clear();
        foreach (var g in await glosas.ListarAsync(SomenteEmAberto))
            Glosas.Add(g);
    }

    [RelayCommand]
    private async Task Reapresentar(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;
        using (var scope = _scopeFactory.CreateScope())
        {
            var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
            await glosas.ReapresentarAsync(codigo.Id, DateOnly.FromDateTime(DateTime.Today));
        }
        await Buscar();
    }

    [RelayCommand]
    private async Task Recuperar(CodigoFaturamento? codigo)
    {
        if (codigo is null) return;
        if (!_dialogo.Confirmar("Confirmar",
            "Marcar esta glosa como recuperada (aceita pelo convênio)?")) return;

        using (var scope = _scopeFactory.CreateScope())
        {
            var glosas = scope.ServiceProvider.GetRequiredService<GlosaService>();
            await glosas.MarcarRecuperadaAsync(codigo.Id);
        }
        await Buscar();
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => BuscarCommand;
}
