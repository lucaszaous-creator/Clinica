using System.Windows.Input;
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
public partial class AgendaViewModel : ObservableObject, IAtalhosDeTela
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Controls.IDialogoService _dialogo;

    public ObservableCollection<Agendamento> Agendamentos { get; } = new();
    public ObservableCollection<Paciente> Pacientes { get; } = new();
    public Array Modalidades => Enum.GetValues(typeof(ModalidadeAtendimento));

    [ObservableProperty] private DateTime _dia = DateTime.Today;

    /// <summary>false = visão de dia; true = visão de semana (segunda a domingo do dia atual).</summary>
    [ObservableProperty] private bool _modoSemana;

    // Formulário de novo agendamento
    [ObservableProperty] private string? _busca;
    [ObservableProperty] private Paciente? _pacienteSelecionado;
    [ObservableProperty] private DateTime _dataNovo = DateTime.Today;
    [ObservableProperty] private string _hora = "09:00";
    [ObservableProperty] private ModalidadeAtendimento _modalidade = ModalidadeAtendimento.AcupunturaComEletro;
    [ObservableProperty] private string? _observacoes;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private bool _ocupado;

    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>Segunda-feira da semana do dia selecionado.</summary>
    private DateTime InicioSemana => Dia.AddDays(-(((int)Dia.DayOfWeek + 6) % 7));

    public string TituloDia => ModoSemana
        ? $"Semana de {InicioSemana:dd/MM} a {InicioSemana.AddDays(6):dd/MM/yyyy}"
        : Dia.ToString("dddd, dd/MM/yyyy", PtBr);

    public AgendaViewModel(IServiceScopeFactory scopeFactory, Controls.IDialogoService dialogo)
    {
        _scopeFactory = scopeFactory;
        _dialogo = dialogo;
    }

    partial void OnDiaChanged(DateTime value) => OnPropertyChanged(nameof(TituloDia));

    partial void OnModoSemanaChanged(bool value)
    {
        OnPropertyChanged(nameof(TituloDia));
        _ = RecarregarDia();
    }
    partial void OnBuscaChanged(string? value) => _ = BuscarPacientes();

    // Pré-preenche a modalidade com a habitual do paciente (definida no cadastro).
    partial void OnPacienteSelecionadoChanged(Paciente? value)
    {
        if (value is not null)
            Modalidade = value.ModalidadePreferida;
    }

    public async Task CarregarAsync()
    {
        await BuscarPacientes();
        await RecarregarDia();
    }

    private async Task RecarregarDia()
    {
        using var scope = _scopeFactory.CreateScope();
        var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
        var lista = ModoSemana
            ? await agenda.NoPeriodoAsync(DateOnly.FromDateTime(InicioSemana), DateOnly.FromDateTime(InicioSemana.AddDays(6)))
            : await agenda.DoDiaAsync(DateOnly.FromDateTime(Dia));

        Agendamentos.Clear();
        foreach (var a in lista)
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

    [RelayCommand] private async Task DiaAnterior() { Dia = Dia.AddDays(ModoSemana ? -7 : -1); await RecarregarDia(); }
    [RelayCommand] private async Task ProximoDia() { Dia = Dia.AddDays(ModoSemana ? 7 : 1); await RecarregarDia(); }
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

        if (Ocupado) return;
        Ocupado = true;
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();

                // Choque de horário: avisa quem já ocupa o slot e pede confirmação.
                var conflito = await agenda.ConflitoAsync(dataHora);
                if (conflito is not null &&
                    !_dialogo.Confirmar("Horário ocupado",
                        $"{conflito.Paciente?.Nome} já está agendado em {dataHora:dd/MM} às {dataHora:HH:mm}.\n" +
                        "Agendar mesmo assim (encaixe)?"))
                    return;

                await agenda.AgendarAsync(PacienteSelecionado.Id, dataHora, Modalidade, Observacoes);
            }

            Mensagem = "Agendamento criado.";
            Observacoes = null;
            Dia = DataNovo; // pula para o dia agendado
            await RecarregarDia();
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível agendar: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmarPresenca(Agendamento? ag)
    {
        if (ag is null) return;
        if (!_dialogo.Confirmar("Confirmar presença",
            $"Confirmar presença de {ag.Paciente?.Nome} e gerar o atendimento (códigos de faturamento)?")) return;

        if (Ocupado) return;
        Ocupado = true;
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var agenda = scope.ServiceProvider.GetRequiredService<AgendaService>();
                var resultado = await agenda.ConfirmarPresencaAsync(ag.Id);
                Mensagem = $"Atendimento gerado com {resultado.Atendimento.Codigos.Count} código(s).";
            }

            await RecarregarDia();
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível confirmar a presença: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
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

    // Atalhos globais do shell (IAtalhosDeTela)
    public ICommand? AtalhoAtualizar => HojeCommand;
}
