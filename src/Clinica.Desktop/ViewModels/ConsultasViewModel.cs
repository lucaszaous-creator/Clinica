using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Aba de Consultas: situação da consulta renovável de cada paciente, com alarme e renovação.</summary>
public partial class ConsultasViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<StatusConsultaPaciente> Consultas { get; } = new();

    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private int _totalAlerta;

    public ConsultasViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public Task CarregarAsync() => Recarregar();

    [RelayCommand]
    private async Task Recarregar()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ConsultaService>();
        var hoje = DateOnly.FromDateTime(DateTime.Today);

        Consultas.Clear();
        foreach (var c in await service.ListarAsync(hoje))
            Consultas.Add(c);

        TotalAlerta = Consultas.Count(c => c.PrecisaRenovar);
    }

    /// <summary>Gera/renova a consulta do paciente para hoje (validade conforme os Parâmetros do convênio).</summary>
    [RelayCommand]
    private async Task Renovar(StatusConsultaPaciente? item)
    {
        if (item is null) return;
        if (!item.UsaConsulta)
        {
            Mensagem = $"{item.PacienteNome}: o convênio não usa consulta renovável.";
            return;
        }

        var confirma = MessageBox.Show(
            $"Gerar/renovar a consulta de {item.PacienteNome} para hoje?",
            "Renovar consulta", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirma != MessageBoxResult.Yes) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ConsultaService>();
            await service.RenovarAsync(item.PacienteId, DateOnly.FromDateTime(DateTime.Today));
            Mensagem = $"Consulta de {item.PacienteNome} renovada.";
        }
        catch (Exception ex)
        {
            Mensagem = ex.Message;
        }

        await Recarregar();
    }

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => RecarregarCommand;
}
