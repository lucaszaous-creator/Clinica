using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

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
        OrigemAgendamento origem = OrigemAgendamento.Manual, CancellationToken ct = default,
        Especialidade? especialidadeConsulta = null, string? modalidadeCodigo = null,
        string? especialidadeConsultaCodigo = null)
    {
        // Variante do catálogo: a base (comportamento) vem do código. Sem código, usa o enum.
        if (modalidadeCodigo is not null)
            modalidade = CatalogoModalidades.Base(modalidadeCodigo);

        var ehConsulta = modalidade == ModalidadeAtendimento.Consulta;
        var ag = new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = dataHora,
            ModalidadePrevista = modalidade,
            ModalidadeCodigo = modalidadeCodigo ?? modalidade.ToString(),
            EspecialidadeConsulta = ehConsulta
                ? especialidadeConsulta ?? CatalogoEspecialidades.BaseEnum(especialidadeConsultaCodigo)
                : null,
            EspecialidadeConsultaCodigo = ehConsulta
                ? especialidadeConsultaCodigo ?? especialidadeConsulta?.ToString()
                : null,
            Observacoes = observacoes,
            Origem = origem,
            Status = StatusAgendamento.Agendado
        };
        await _repo.AdicionarAgendamentoAsync(ag, ct);
        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>
    /// Remarca/edita um agendamento já existente. Preserva o registro (e as observações)
    /// em vez de obrigar a cancelar e criar de novo — cancelamento é informação, e um
    /// cancelamento que nunca aconteceu polui o histórico da recepção.
    /// Só vale para horário ainda de pé: presença confirmada já virou atendimento.
    /// </summary>
    public async Task<Agendamento> RemarcarAsync(
        int agendamentoId, DateTime dataHora, string? observacoes,
        string? modalidadeCodigo = null, string? especialidadeConsultaCodigo = null,
        CancellationToken ct = default)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException("Agendamento não encontrado.");

        if (ag.Status == StatusAgendamento.Realizado)
            throw new InvalidOperationException(
                "Este horário já virou atendimento; não é possível remarcá-lo. Estorne o atendimento antes.");

        var modalidade = modalidadeCodigo is not null
            ? CatalogoModalidades.Base(modalidadeCodigo)
            : ag.ModalidadePrevista;
        var ehConsulta = modalidade == ModalidadeAtendimento.Consulta;

        ag.DataHora = dataHora;
        ag.ModalidadePrevista = modalidade;
        ag.ModalidadeCodigo = modalidadeCodigo ?? modalidade.ToString();
        ag.EspecialidadeConsulta = ehConsulta ? CatalogoEspecialidades.BaseEnum(especialidadeConsultaCodigo) : null;
        ag.EspecialidadeConsultaCodigo = ehConsulta ? especialidadeConsultaCodigo : null;
        ag.Observacoes = observacoes;
        // Remarcar um horário cancelado/faltado o traz de volta para a agenda.
        ag.Status = StatusAgendamento.Agendado;

        await _repo.SalvarAsync(ct);
        return ag;
    }

    public Task<Agendamento?> ObterAsync(int agendamentoId, CancellationToken ct = default)
        => _repo.ObterAgendamentoAsync(agendamentoId, ct);

    public Task<IReadOnlyList<Agendamento>> DoDiaAsync(DateOnly dia, CancellationToken ct = default)
        => _repo.AgendamentosNoPeriodoAsync(dia.ToDateTime(TimeOnly.MinValue), dia.ToDateTime(TimeOnly.MaxValue), ct);

    /// <summary>Agendamentos de um intervalo de dias (visão de semana).</summary>
    public Task<IReadOnlyList<Agendamento>> NoPeriodoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => _repo.AgendamentosNoPeriodoAsync(inicio.ToDateTime(TimeOnly.MinValue), fim.ToDateTime(TimeOnly.MaxValue), ct);

    /// <summary>
    /// Agendamento ativo (não cancelado/faltou) que já ocupa exatamente este horário,
    /// ou nulo se o horário está livre — usado para alertar choque de horário.
    /// </summary>
    /// <param name="ignorarAgendamentoId">
    /// Numa remarcação, o próprio agendamento não conta como conflito consigo mesmo.
    /// </param>
    public async Task<Agendamento?> ConflitoAsync(DateTime dataHora, CancellationToken ct = default,
        int? ignorarAgendamentoId = null)
    {
        var doDia = await DoDiaAsync(DateOnly.FromDateTime(dataHora), ct);
        return doDia.FirstOrDefault(a =>
            a.DataHora == dataHora &&
            a.Id != ignorarAgendamentoId &&
            a.Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado);
    }

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
            ag.PacienteId, DateOnly.FromDateTime(ag.DataHora), ag.ModalidadePrevista, ag.Observacoes, ct,
            especialidadeConsulta: ag.EspecialidadeConsulta,
            modalidadeCodigo: ag.ModalidadeCodigo,
            especialidadeConsultaCodigo: ag.EspecialidadeConsultaCodigo);

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
                ModalidadeCodigo = ag.ModalidadeCodigo,
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
