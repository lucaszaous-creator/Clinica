using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Lança um atendimento. O sistema gera automaticamente os códigos (inclusive o 2º código +24h).</summary>
public partial class NovoAtendimentoViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<Paciente> Pacientes { get; } = new();
    public ObservableCollection<CodigoFaturamento> CodigosGerados { get; } = new();
    public ObservableCollection<string> Avisos { get; } = new();

    public Array Modalidades => Enum.GetValues(typeof(ModalidadeAtendimento));

    [ObservableProperty] private Paciente? _pacienteSelecionado;
    [ObservableProperty] private DateTime _data = DateTime.Today;
    [ObservableProperty] private ModalidadeAtendimento _modalidade = ModalidadeAtendimento.AcupunturaComEletro;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private bool _lancado;

    public NovoAtendimentoViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task CarregarAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
        Pacientes.Clear();
        foreach (var p in await db.Pacientes.OrderBy(p => p.Nome).ToListAsync())
            Pacientes.Add(p);
    }

    [RelayCommand]
    private async Task Lancar()
    {
        if (PacienteSelecionado is null)
            return;

        CodigosGerados.Clear();
        Avisos.Clear();

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AtendimentoService>();
        var resultado = await service.LancarAsync(
            PacienteSelecionado.Id, DateOnly.FromDateTime(Data), Modalidade, Observacoes);

        foreach (var c in resultado.Atendimento.Codigos)
            CodigosGerados.Add(c);
        foreach (var a in resultado.Avisos)
            Avisos.Add(a);

        Lancado = true;
    }
}
