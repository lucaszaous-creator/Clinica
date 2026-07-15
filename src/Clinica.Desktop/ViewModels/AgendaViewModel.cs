using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.ViewModels;

/// <summary>Agenda do dia: marca horários e, ao confirmar presença, gera o atendimento.</summary>
public partial class AgendaViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<Agendamento> Agendamentos { get; } = new();
    public ObservableCollection<Paciente> Pacientes { get; } = new();
    public Array Modalidades => Enum.GetValues(typeof(ModalidadeAtendimento));

    [ObservableProperty] private DateTime _dia = DateTime.Today;

    // Formulário de novo agendamento
    [ObservableProperty] private string? _busca;
    [ObservableProperty] private Paciente? _pacienteSelecionado;
    [ObservableProperty] private DateTime _dataNovo = DateTime.Today;
    [ObservableProperty] private string _hora = "09:00";
    [ObservableProperty] private ModalidadeAtendimento _modalidade = ModalidadeAtendimento.AcupunturaComEletro;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private string? _mensagem;

    public string TituloDia => Dia.ToString("dddd, dd/MM/yyyy", new CultureInfo("pt-BR"));

    public AgendaViewModel(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    partial void OnDiaChanged(DateTime value) => OnPropertyChanged(nameof(TituloDia));
    partial void OnBuscaChanged(string? value) => _ = BuscarPacientes();

    public async Task CarregarAsync()
    {
        await BuscarPacientes();
        await RecarregarDia();
    }

    private async Task RecarregarDia()
    {
        using var scope = _scopeFactory.CreateScope();
        var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
        Agendamentos.Clear();
        foreach (var a in await agenda.DoDiaAsync(DateOnly.FromDateTime(Dia)))
            Agendamentos.Add(a);
    }

    [RelayCommand]
    private async Task BuscarPacientes()
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PacienteService>();
        Pacientes.Clear();
        foreach (var p in await service.BuscarAsync(Busca))
            Pacientes.Add(p);
    }

    [RelayCommand] private async Task DiaAnterior() { Dia = Dia.AddDays(-1); await RecarregarDia(); }
    [RelayCommand] private async Task ProximoDia() { Dia = Dia.AddDays(1); await RecarregarDia(); }
    [RelayCommand] private async Task Hoje() { Dia = DateTime.Today; await RecarregarDia(); }

    [RelayCommand]
    private async Task Agendar()
    {
        if (PacienteSelecionado is null)
        {
            Mensagem = "Selecione o paciente.";
            return;
        }
        if (!TimeOnly.TryParse(Hora, out var hora))
        {
            Mensagem = "Hora inválida (use HH:mm, ex.: 14:30).";
            return;
        }

        // Hora de parede (sem fuso) para casar com a coluna 'timestamp without time zone'.
        var dataHora = DateTime.SpecifyKind(DataNovo.Date.Add(hora.ToTimeSpan()), DateTimeKind.Unspecified);

        using (var scope = _scopeFactory.CreateScope())
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.AgendarAsync(PacienteSelecionado.Id, dataHora, Modalidade, Observacoes);
        }

        Mensagem = "Agendamento criado.";
        Observacoes = null;
        Dia = DataNovo; // pula para o dia agendado
        await RecarregarDia();
    }

    [RelayCommand]
    private async Task ConfirmarPresenca(Agendamento? ag)
    {
        if (ag is null) return;
        var confirma = MessageBox.Show(
            $"Confirmar presença de {ag.Paciente?.Nome} e gerar o atendimento (códigos de faturamento)?",
            "Confirmar presença", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirma != MessageBoxResult.Yes) return;

        using (var scope = _scopeFactory.CreateScope())
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            var resultado = await agenda.ConfirmarPresencaAsync(ag.Id);
            Mensagem = $"Atendimento gerado com {resultado.Atendimento.Codigos.Count} código(s).";
        }

        await RecarregarDia();
    }

    [RelayCommand]
    private async Task Cancelar(Agendamento? ag)
    {
        if (ag is null) return;
        using (var scope = _scopeFactory.CreateScope())
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.CancelarAsync(ag.Id);
        }
        await RecarregarDia();
    }

    [RelayCommand]
    private async Task Faltou(Agendamento? ag)
    {
        if (ag is null) return;
        using (var scope = _scopeFactory.CreateScope())
        {
            var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
            await agenda.MarcarFaltaAsync(ag.Id);
        }
        await RecarregarDia();
    }
}
