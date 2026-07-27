using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Agenda da recepção. Ao confirmar a presença, gera o atendimento (e os códigos de faturamento)
/// e cria automaticamente um retorno sugerido para a obtenção do 2º código (+24h).
///
/// A partir da parcela 1 ela é multiprofissional: o horário pode apontar para um
/// <see cref="Profissional"/> e uma <see cref="Sala"/>, e o choque entre dois horários
/// passa a ser calculado por recurso e por INTERVALO (uma sessão de 30 min invade a
/// seguinte mesmo sem bater no mesmo minuto). Quem não informa profissional nem sala
/// — o faturamento — enxerga exatamente o comportamento antigo.
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
        string? especialidadeConsultaCodigo = null,
        int? profissionalId = null, int? salaId = null, int? duracaoMinutos = null,
        bool encaixe = false)
    {
        // Variante do catálogo: a base (comportamento) vem do código. Sem código, usa o enum.
        if (modalidadeCodigo is not null)
            modalidade = CatalogoModalidades.Base(modalidadeCodigo);

        if (duracaoMinutos is { } d && d <= 0)
            throw new InvalidOperationException("A duração do horário precisa ser maior que zero.");

        // Só valida choque quando há recurso disputado. Sem profissional nem sala — o
        // caminho do faturamento — nada muda: ele avisa na tela e marca assim mesmo.
        await GarantirSemChoqueAsync(
            dataHora, duracaoMinutos, profissionalId, salaId, pacienteId, null, encaixe, ct);

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
            Status = StatusAgendamento.Agendado,
            ProfissionalId = profissionalId,
            SalaId = salaId,
            DuracaoMinutos = duracaoMinutos,
            Encaixe = encaixe
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
        string? operador = null, CancellationToken ct = default,
        int? profissionalId = null, int? salaId = null, int? duracaoMinutos = null,
        bool manterRecursos = true, bool encaixe = false)
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
        var horarioAnterior = ag.DataHora;

        // O faturamento remarca sem saber de profissional/sala: por padrão os recursos
        // do horário são preservados. A recepção passa manterRecursos:false quando o
        // formulário de fato traz (ou limpa) esses campos.
        var novoProfissional = manterRecursos ? ag.ProfissionalId : profissionalId;
        var novaSala = manterRecursos ? ag.SalaId : salaId;
        var novaDuracao = manterRecursos ? ag.DuracaoMinutos : duracaoMinutos;
        var novoEncaixe = manterRecursos ? ag.Encaixe : encaixe;

        if (novaDuracao is { } d && d <= 0)
            throw new InvalidOperationException("A duração do horário precisa ser maior que zero.");

        await GarantirSemChoqueAsync(
            dataHora, novaDuracao, novoProfissional, novaSala, ag.PacienteId,
            ignorarAgendamentoId: ag.Id, novoEncaixe, ct);

        ag.ProfissionalId = novoProfissional;
        ag.SalaId = novaSala;
        ag.DuracaoMinutos = novaDuracao;
        ag.Encaixe = novoEncaixe;
        ag.DataHora = dataHora;
        ag.ModalidadePrevista = modalidade;
        ag.ModalidadeCodigo = modalidadeCodigo ?? modalidade.ToString();
        ag.EspecialidadeConsulta = ehConsulta ? CatalogoEspecialidades.BaseEnum(especialidadeConsultaCodigo) : null;
        ag.EspecialidadeConsultaCodigo = ehConsulta ? especialidadeConsultaCodigo : null;
        ag.Observacoes = observacoes;
        // Remarcar um horário cancelado/faltado o traz de volta para a agenda.
        ag.Status = StatusAgendamento.Agendado;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AgendamentoRemarcado",
            Detalhe = $"De {horarioAnterior:dd/MM/yyyy HH:mm} para {dataHora:dd/MM/yyyy HH:mm}",
            PacienteId = ag.PacienteId
        }, ct);

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

    // ==================== Agenda multiprofissional ====================

    /// <summary>
    /// Choques de um horário candidato, por recurso: o profissional já está ocupado, a
    /// sala já está tomada, ou o próprio paciente já tem hora marcada nesse intervalo.
    ///
    /// Compara por INTERVALO, não por igualdade de horário: marcar 14h30 sobre uma
    /// sessão de 30 min que começou às 14h é o mesmo choque, e a comparação antiga
    /// (<see cref="ConflitoAsync"/>, preservada para o faturamento) não pegava.
    /// A sala só acusa choque quando passa da capacidade dela.
    /// </summary>
    public async Task<IReadOnlyList<ConflitoAgenda>> ConflitosAsync(
        DateTime dataHora, int? duracaoMinutos = null,
        int? profissionalId = null, int? salaId = null, int? pacienteId = null,
        int? ignorarAgendamentoId = null, CancellationToken ct = default)
    {
        var fim = dataHora.AddMinutes(
            duracaoMinutos ?? await DuracaoPadraoAsync(profissionalId, ct));

        var doDia = (await DoDiaAsync(DateOnly.FromDateTime(dataHora), ct))
            .Where(a => a.Id != ignorarAgendamentoId && a.OcupaAgenda && a.ColideCom(dataHora, fim))
            .ToList();

        var conflitos = new List<ConflitoAgenda>();

        if (profissionalId is not null)
            conflitos.AddRange(doDia
                .Where(a => a.ProfissionalId == profissionalId)
                .Select(a => new ConflitoAgenda(
                    RecursoAgenda.Profissional, a.Id,
                    $"{a.Profissional?.Rotulo ?? "O profissional"} já atende "
                    + $"{a.Paciente?.Nome ?? "outro paciente"} às {a.DataHora:HH:mm}.",
                    a.DataHora, a.FimPrevisto)));

        if (salaId is not null)
        {
            var naSala = doDia.Where(a => a.SalaId == salaId).ToList();
            var capacidade = (await _repo.ObterSalaAsync(salaId.Value, ct))?.Capacidade ?? 1;
            if (naSala.Count >= capacidade)
                conflitos.AddRange(naSala.Select(a => new ConflitoAgenda(
                    RecursoAgenda.Sala, a.Id,
                    $"A sala {a.Sala?.Nome ?? ""} já está ocupada às {a.DataHora:HH:mm} "
                    + $"({a.Paciente?.Nome ?? "outro paciente"}).",
                    a.DataHora, a.FimPrevisto)));
        }

        if (pacienteId is not null)
            conflitos.AddRange(doDia
                .Where(a => a.PacienteId == pacienteId)
                .Select(a => new ConflitoAgenda(
                    RecursoAgenda.Paciente, a.Id,
                    $"O paciente já tem horário às {a.DataHora:HH:mm}.",
                    a.DataHora, a.FimPrevisto)));

        return conflitos;
    }

    /// <summary>
    /// Barra o choque quando há recurso em disputa — a menos que a recepção tenha
    /// assumido o <paramref name="encaixe"/>. O encaixe existe justamente para o caso
    /// em que a clínica DECIDE atender por cima: a agenda registra em vez de impedir.
    /// </summary>
    private async Task GarantirSemChoqueAsync(
        DateTime dataHora, int? duracaoMinutos, int? profissionalId, int? salaId,
        int? pacienteId, int? ignorarAgendamentoId, bool encaixe, CancellationToken ct)
    {
        if (encaixe) return;
        if (profissionalId is null && salaId is null) return;

        var conflitos = await ConflitosAsync(
            dataHora, duracaoMinutos, profissionalId, salaId,
            pacienteId: pacienteId, ignorarAgendamentoId: ignorarAgendamentoId, ct: ct);

        // Choque com o próprio paciente é aviso da tela, não impedimento: ele pode ter
        // dois procedimentos seguidos. O que trava é recurso disputado.
        var impeditivos = conflitos.Where(c => c.Recurso != RecursoAgenda.Paciente).ToList();
        if (impeditivos.Count == 0) return;

        throw new InvalidOperationException(
            impeditivos[0].Descricao + " Escolha outro horário ou marque como encaixe.");
    }

    /// <summary>Duração padrão do profissional; na falta dele, a da clínica.</summary>
    private async Task<int> DuracaoPadraoAsync(int? profissionalId, CancellationToken ct)
    {
        if (profissionalId is null) return Agendamento.DuracaoPadraoMinutos;
        var prof = await _repo.ObterProfissionalAsync(profissionalId.Value, ct);
        return prof?.DuracaoPadraoMinutos ?? Agendamento.DuracaoPadraoMinutos;
    }

    /// <summary>Agenda do dia de um profissional (coluna da grade).</summary>
    public async Task<IReadOnlyList<Agendamento>> DoDiaPorProfissionalAsync(
        DateOnly dia, int? profissionalId, CancellationToken ct = default)
        => (await DoDiaAsync(dia, ct)).Where(a => a.ProfissionalId == profissionalId).ToList();

    /// <summary>Ocupação do dia, um item por profissional (mais um para "sem profissional").</summary>
    public async Task<IReadOnlyList<OcupacaoProfissional>> OcupacaoDoDiaAsync(
        DateOnly dia, CancellationToken ct = default)
    {
        var doDia = await DoDiaAsync(dia, ct);

        return doDia
            .GroupBy(a => a.ProfissionalId)
            .Select(g => new OcupacaoProfissional(
                g.Key,
                g.First().Profissional?.Rotulo ?? "Sem profissional",
                g.Count(a => a.Status == StatusAgendamento.Agendado),
                g.Count(a => a.Status == StatusAgendamento.Realizado),
                g.Count(a => a.Status == StatusAgendamento.Faltou),
                g.Count(a => a.Status == StatusAgendamento.Cancelado),
                g.Where(a => a.OcupaAgenda).Sum(a => a.DuracaoEfetiva)))
            // Sem profissional por último: é resíduo da agenda antiga, não uma pessoa.
            .OrderBy(o => o.ProfissionalId is null)
            .ThenByDescending(o => o.Total)
            .ThenBy(o => o.Nome)
            .ToList();
    }

    // ==================== Fila em kanban ====================

    /// <summary>
    /// Check-in no balcão: o paciente chegou. É daqui que sai o tempo de espera — sem
    /// carimbo de chegada a fila não tem como dizer há quanto tempo alguém aguarda.
    /// </summary>
    public async Task<Agendamento> RegistrarChegadaAsync(
        int agendamentoId, DateTime? quando = null, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                "Só um horário em aberto aceita check-in. Reabra o agendamento antes.");

        ag.ChegadaEm ??= quando ?? DateTime.Now;
        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>O profissional chamou o paciente: fim da espera, começo da sessão.</summary>
    public async Task<Agendamento> IniciarAtendimentoAsync(
        int agendamentoId, DateTime? quando = null, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                "Este horário não está mais em aberto.");

        var agora = quando ?? DateTime.Now;
        // Chamar direto (paciente que chegou e já entrou) não pode deixar a espera nula:
        // sem chegada, o kanban não saberia distinguir quem esperou de quem não esperou.
        ag.ChegadaEm ??= agora;
        ag.InicioAtendimentoEm ??= agora;
        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>
    /// Volta o cartão uma coluna: de "em atendimento" para "chegou", de "chegou" para
    /// "aguardando". Existe porque clicar errado no kanban é rotina — e a alternativa
    /// (cancelar e remarcar) falsearia o histórico.
    /// </summary>
    public async Task<Agendamento> VoltarEtapaAsync(int agendamentoId, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                "Este horário já foi encerrado; não dá para voltar a etapa por aqui.");

        if (ag.InicioAtendimentoEm is not null) ag.InicioAtendimentoEm = null;
        else if (ag.ChegadaEm is not null) ag.ChegadaEm = null;
        else throw new InvalidOperationException("O paciente ainda nem fez check-in.");

        await _repo.SalvarAsync(ct);
        return ag;
    }

    private async Task<Agendamento> ObterParaFilaAsync(int agendamentoId, CancellationToken ct)
        => await _repo.ObterAgendamentoAsync(agendamentoId, ct)
           ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");

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
                Observacoes = "Retorno para obter o 2º código (eletroacupuntura/acupuntura).",
                // O retorno herda quem e onde: continuidade é o padrão, e o horário
                // sugerido já nasce na coluna certa da agenda.
                ProfissionalId = ag.ProfissionalId,
                SalaId = ag.SalaId,
                DuracaoMinutos = ag.DuracaoMinutos
            };
            await _repo.AdicionarAgendamentoAsync(retorno, ct);
        }

        await _repo.SalvarAsync(ct);
        return resultado;
    }

    public async Task CancelarAsync(int agendamentoId, string? operador = null, CancellationToken ct = default)
        => await AlterarStatusAsync(agendamentoId, StatusAgendamento.Cancelado, operador, ct);

    public async Task MarcarFaltaAsync(int agendamentoId, string? operador = null, CancellationToken ct = default)
        => await AlterarStatusAsync(agendamentoId, StatusAgendamento.Faltou, operador, ct);

    private async Task AlterarStatusAsync(
        int agendamentoId, StatusAgendamento status, string? operador, CancellationToken ct)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");

        var anterior = ag.Status;
        ag.Status = status;

        // Falta e cancelamento são sessão não faturada: precisam de rastro de quem marcou.
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = status == StatusAgendamento.Cancelado ? "AgendamentoCancelado" : "AgendamentoFalta",
            Detalhe = $"{ag.DataHora:dd/MM/yyyy HH:mm} — de {anterior} para {status}",
            PacienteId = ag.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
    }
}
