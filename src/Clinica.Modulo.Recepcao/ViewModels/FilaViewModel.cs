using System.Collections.ObjectModel;
using Clinica.Application.Servicos;
using Clinica.Desktop.Controls;
using Clinica.Domain.Entities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clinica.Recepcao.ViewModels;

/// <summary>Uma linha da fila do dia (agendamento + o que a recepção precisa ver).</summary>
public sealed partial class LinhaFila : ObservableObject
{
    public required int AgendamentoId { get; init; }
    public required string Horario { get; init; }
    public required string Paciente { get; init; }
    public required string Modalidade { get; init; }
    public required StatusAgendamento Status { get; init; }
    public required string StatusRotulo { get; init; }
    public string? Observacoes { get; init; }

    /// <summary>Retorno sugerido automaticamente para obter o 2º código.</summary>
    public required bool EhRetornoDoSegundoCodigo { get; init; }

    /// <summary>Só agendamentos ainda em aberto aceitam check-in/falta/cancelamento.</summary>
    public bool EmAberto => Status == StatusAgendamento.Agendado;
}

/// <summary>
/// Fila de hoje: a primeira tela da Recepção. Lista os agendamentos do dia e permite
/// confirmar presença (check-in), marcar falta e cancelar.
///
/// Usa apenas APIs que já existiam no faturamento — sem migration e sem mudança de
/// domínio. O check-in (<see cref="AgendaService.ConfirmarPresencaAsync"/>) já gera o
/// atendimento com os códigos e cria o retorno do 2º código quando aplicável.
/// </summary>
public sealed partial class FilaViewModel : ObservableObject
{
    private readonly AgendaService _agenda;
    private readonly ISnackbarService _snackbar;

    public ObservableCollection<LinhaFila> Linhas { get; } = [];

    [ObservableProperty]
    private DateTime _dia = DateTime.Today;

    [ObservableProperty]
    private bool _carregando;

    [ObservableProperty]
    private string _resumo = string.Empty;

    public FilaViewModel(AgendaService agenda, ISnackbarService snackbar)
    {
        _agenda = agenda;
        _snackbar = snackbar;
        _ = CarregarAsync();
    }

    partial void OnDiaChanged(DateTime value) => _ = CarregarAsync();

    [RelayCommand]
    public async Task CarregarAsync()
    {
        try
        {
            Carregando = true;
            var agendamentos = await _agenda.DoDiaAsync(DateOnly.FromDateTime(Dia));

            Linhas.Clear();
            foreach (var a in agendamentos)
                Linhas.Add(new LinhaFila
                {
                    AgendamentoId = a.Id,
                    Horario = a.DataHora.ToString("HH:mm"),
                    Paciente = a.Paciente?.Nome ?? "(paciente removido)",
                    Modalidade = a.ModalidadePrevista.ToString(),
                    Status = a.Status,
                    StatusRotulo = Rotular(a.Status),
                    Observacoes = a.Observacoes,
                    EhRetornoDoSegundoCodigo = a.Origem == OrigemAgendamento.RetornoSugerido
                });

            var aguardando = Linhas.Count(l => l.EmAberto);
            var atendidos = Linhas.Count(l => l.Status == StatusAgendamento.Realizado);
            Resumo = $"{Linhas.Count} agendamento(s) · {aguardando} aguardando · {atendidos} atendido(s)";
        }
        catch (Exception ex)
        {
            _snackbar.Erro($"Não foi possível carregar a fila: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    /// <summary>Check-in: confirma a presença e gera o atendimento com os códigos.</summary>
    [RelayCommand]
    private async Task ConfirmarPresencaAsync(LinhaFila? linha)
    {
        if (linha is null) return;
        try
        {
            var resultado = await _agenda.ConfirmarPresencaAsync(linha.AgendamentoId);
            var codigos = resultado.Atendimento.Codigos.Count;
            _snackbar.Sucesso(
                $"Presença de {linha.Paciente} confirmada — {codigos} código(s) gerado(s).");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            _snackbar.Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task MarcarFaltaAsync(LinhaFila? linha)
    {
        if (linha is null) return;
        try
        {
            await _agenda.MarcarFaltaAsync(linha.AgendamentoId);
            _snackbar.Info($"{linha.Paciente} marcado como falta.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            _snackbar.Erro(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CancelarAsync(LinhaFila? linha)
    {
        if (linha is null) return;
        try
        {
            await _agenda.CancelarAsync(linha.AgendamentoId);
            _snackbar.Info($"Agendamento de {linha.Paciente} cancelado.");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            _snackbar.Erro(ex.Message);
        }
    }

    [RelayCommand]
    private void DiaAnterior() => Dia = Dia.AddDays(-1);

    [RelayCommand]
    private void ProximoDia() => Dia = Dia.AddDays(1);

    [RelayCommand]
    private void Hoje() => Dia = DateTime.Today;

    private static string Rotular(StatusAgendamento status) => status switch
    {
        StatusAgendamento.Agendado => "Aguardando",
        StatusAgendamento.Realizado => "Atendido",
        StatusAgendamento.Cancelado => "Cancelado",
        StatusAgendamento.Faltou => "Faltou",
        _ => status.ToString()
    };
}
