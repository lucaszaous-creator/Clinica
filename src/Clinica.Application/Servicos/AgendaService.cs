using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Agenda da recepção. Ao confirmar a presença, gera o atendimento (e os códigos de faturamento)
/// e cria automaticamente um retorno sugerido para a obtenção do 2º código (+24h).
/// </summary>
public sealed class AgendaService
{
    private readonly IClinicaRepositorio _repo;
    private readonly AtendimentoService _atendimentos;

    public AgendaService(IClinicaRepositorio repo, AtendimentoService atendimentos)
    {
        _repo = repo;
        _atendimentos = atendimentos;
    }

    public async Task<Agendamento> AgendarAsync(
        int pacienteId, DateTime dataHora, ModalidadeAtendimento modalidade, string? observacoes,
        OrigemAgendamento origem = OrigemAgendamento.Manual, CancellationToken ct = default)
    {
        var ag = new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = dataHora,
            ModalidadePrevista = modalidade,
            Observacoes = observacoes,
            Origem = origem,
            Status = StatusAgendamento.Agendado
        };
        await _repo.AdicionarAgendamentoAsync(ag, ct);
        await _repo.SalvarAsync(ct);
        return ag;
    }

    public Task<IReadOnlyList<Agendamento>> DoDiaAsync(DateOnly dia, CancellationToken ct = default)
        => _repo.AgendamentosNoPeriodoAsync(dia.ToDateTime(TimeOnly.MinValue), dia.ToDateTime(TimeOnly.MaxValue), ct);

    /// <summary>
    /// Confirma a presença: gera o atendimento com os códigos e, havendo 2º código,
    /// cria um retorno sugerido na data prevista (para não esquecer de obtê-lo).
    /// </summary>
    public async Task<ResultadoLancamento> ConfirmarPresencaAsync(int agendamentoId, CancellationToken ct = default)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");

        if (ag.Status == StatusAgendamento.Realizado)
            throw new InvalidOperationException("Este agendamento já teve a presença confirmada.");

        var resultado = await _atendimentos.LancarAsync(
            ag.PacienteId, DateOnly.FromDateTime(ag.DataHora), ag.ModalidadePrevista, ag.Observacoes, ct);

        ag.Status = StatusAgendamento.Realizado;
        ag.AtendimentoId = resultado.Atendimento.Id;

        // Retorno sugerido para o 2º código (obtido 24h depois).
        var segundo = resultado.Atendimento.Codigos
            .FirstOrDefault(c => c.Ordem == OrdemCodigo.Segundo);
        if (segundo is not null)
        {
            var retorno = new Agendamento
            {
                PacienteId = ag.PacienteId,
                DataHora = segundo.DataPrevistaFaturamento.ToDateTime(new TimeOnly(9, 0)),
                ModalidadePrevista = ag.ModalidadePrevista,
                Origem = OrigemAgendamento.RetornoSugerido,
                Status = StatusAgendamento.Agendado,
                Observacoes = "Retorno para obter o 2º código (eletroacupuntura/acupuntura)."
            };
            await _repo.AdicionarAgendamentoAsync(retorno, ct);
        }

        await _repo.SalvarAsync(ct);
        return resultado;
    }

    public async Task CancelarAsync(int agendamentoId, CancellationToken ct = default)
        => await AlterarStatusAsync(agendamentoId, StatusAgendamento.Cancelado, ct);

    public async Task MarcarFaltaAsync(int agendamentoId, CancellationToken ct = default)
        => await AlterarStatusAsync(agendamentoId, StatusAgendamento.Faltou, ct);

    private async Task AlterarStatusAsync(int agendamentoId, StatusAgendamento status, CancellationToken ct)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");
        ag.Status = status;
        await _repo.SalvarAsync(ct);
    }
}
