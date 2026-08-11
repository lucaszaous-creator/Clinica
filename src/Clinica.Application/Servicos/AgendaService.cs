using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Agenda da recepção. Ao confirmar a presença, gera o atendimento e os códigos de
/// faturamento — e SÓ isso: a obtenção do 2º código (+24h) é trabalho da secretária no
/// sistema do convênio, e vive no painel de pendências, não na agenda (ver
/// <see cref="ConfirmarPresencaAsync"/>).
///
/// A partir da parcela 1 ela é multiprofissional: o horário pode apontar para um
/// <see cref="Profissional"/> e uma <see cref="Sala"/>, e o choque entre dois horários
/// passa a ser calculado por recurso e por INTERVALO (uma sessão de 30 min invade a
/// seguinte mesmo sem bater no mesmo minuto). Quem não informa profissional nem sala
/// — o faturamento — enxerga exatamente o comportamento antigo.
/// </summary>
public sealed class AgendaService
{
    /// <summary>
    /// Teto de sessões numa série só. Não é limitação técnica: acima disso a agenda vira
    /// um calendário inteiro marcado de uma vez, e o que se ganha em cliques se perde na
    /// hora de remarcar tudo porque o paciente mudou de horário.
    /// </summary>
    public const int MaximoSessoesPorSerie = 52;

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
        bool encaixe = false, string? operador = null, TipoCodigo? primeiroCodigo = null)
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
            Encaixe = encaixe,
            // Nas modalidades duplas, qual código o convênio libera primeiro. Atravessa o
            // horário para chegar ao motor na confirmação da presença — é o que permitiu o
            // avulso virar encaixe sem perder a escolha que a tela dele sempre ofereceu.
            PrimeiroCodigo = primeiroCodigo,
            // Quem marcou, e quando. O operador chega da TELA (`SessaoUsuario.Atual`) —
            // este serviço não lê a sessão, pela mesma razão dos demais: é o chamador que
            // sabe quem está logado.
            CriadoPor = string.IsNullOrWhiteSpace(operador) ? null : operador.Trim(),
            CriadoEm = DateTime.Now
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

        // Agenda fechada (férias, feriado, folga, sala em manutenção). Vem por último
        // porque é o choque menos frequente — mas é o único que não tem outro paciente
        // do outro lado, e por isso precisava existir: sem ele, o que segurava a
        // marcação em cima da folga era a memória de quem está no balcão.
        var bloqueios = await _repo.BloqueiosNoPeriodoAsync(dataHora, fim, ct);
        conflitos.AddRange(bloqueios
            .Where(b => b.AlcancaRecurso(profissionalId, salaId))
            .Select(b => new ConflitoAgenda(
                RecursoAgenda.Bloqueio, b.Id, b.Descricao, b.Inicio, b.Fim)));

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

        // Sem profissional e sem sala não há recurso disputado — mas o bloqueio DA
        // CLÍNICA (feriado) vale mesmo assim, e por isso a conferência não pode mais
        // sair aqui como saía antes.
        var soBloqueioDaClinica = profissionalId is null && salaId is null;

        var conflitos = await ConflitosAsync(
            dataHora, duracaoMinutos, profissionalId, salaId,
            pacienteId: pacienteId, ignorarAgendamentoId: ignorarAgendamentoId, ct: ct);

        // Choque com o próprio paciente é aviso da tela, não impedimento: ele pode ter
        // dois procedimentos seguidos. O que trava é recurso disputado — e agenda fechada.
        var impeditivos = conflitos
            .Where(c => c.Recurso != RecursoAgenda.Paciente)
            .Where(c => !soBloqueioDaClinica || c.Recurso == RecursoAgenda.Bloqueio)
            .ToList();
        if (impeditivos.Count == 0) return;

        throw new InvalidOperationException(
            impeditivos[0].Descricao + " Escolha outro horário ou marque como encaixe.");
    }

    /// <summary>
    /// Marca várias sessões de uma vez — o pacote de dez, o tratamento de oito semanas.
    ///
    /// Até aqui o Financeiro vendia dez sessões e a agenda marcava UMA por vez: dez
    /// aberturas do mesmo formulário, dez conferências de conflito, dez chances de
    /// digitar a hora errada. Era o atrito que a recepção sentia todo dia.
    ///
    /// Três regras que valem a pena escrever:
    ///
    /// 1. **A série sai da PRIMEIRA data mais N períodos**, nunca da ocorrência anterior
    ///    mais um. Encadear faria uma sessão adiada empurrar todas as seguintes — e a
    ///    recepção perderia o horário fixo do paciente, que é o motivo de marcar em série.
    /// 2. **Conflito não aborta a série: ele PULA a data e diz qual.** Recusar as dez
    ///    porque a sexta caiu em feriado devolveria a recepção ao trabalho manual; marcar
    ///    nove e dizer "a de 25/12 não deu" é o que ela faria à mão.
    /// 3. **Todas compartilham o <see cref="Agendamento.SerieId"/>**, que é o que permite
    ///    tratar o resto do bloco depois — cancelar as que sobraram quando o paciente
    ///    desiste no meio do tratamento.
    /// </summary>
    public async Task<SerieAgendada> AgendarSerieAsync(
        int pacienteId, DateTime primeira, ModalidadeAtendimento modalidade, int quantidade,
        int intervaloDias = 7, string? observacoes = null,
        Especialidade? especialidadeConsulta = null, string? modalidadeCodigo = null,
        string? especialidadeConsultaCodigo = null,
        int? profissionalId = null, int? salaId = null, int? duracaoMinutos = null,
        CancellationToken ct = default, string? operador = null)
    {
        if (quantidade < 2)
            throw new InvalidOperationException(
                "Série é a partir de duas sessões. Para uma só, marque o horário normal.");

        if (quantidade > MaximoSessoesPorSerie)
            throw new InvalidOperationException(
                $"No máximo {MaximoSessoesPorSerie} sessões por série — acima disso a agenda "
                + "vira um calendário inteiro marcado de uma vez, e o que se ganha em "
                + "cliques se perde na hora de remarcar.");

        if (intervaloDias < 1)
            throw new InvalidOperationException("O intervalo entre as sessões é de pelo menos 1 dia.");

        var serieId = Guid.NewGuid().ToString("N")[..32];
        var marcados = new List<Agendamento>();
        var recusados = new List<SessaoRecusada>();

        for (var i = 0; i < quantidade; i++)
        {
            // Da PRIMEIRA data mais N períodos — nunca da anterior mais um.
            var quando = primeira.AddDays((long)intervaloDias * i);

            try
            {
                var agendamento = await AgendarAsync(
                    pacienteId, quando, modalidade, observacoes,
                    origem: OrigemAgendamento.Manual, ct: ct,
                    especialidadeConsulta: especialidadeConsulta,
                    modalidadeCodigo: modalidadeCodigo,
                    especialidadeConsultaCodigo: especialidadeConsultaCodigo,
                    profissionalId: profissionalId, salaId: salaId,
                    duracaoMinutos: duracaoMinutos, operador: operador);

                agendamento.SerieId = serieId;
                marcados.Add(agendamento);
            }
            catch (InvalidOperationException ex)
            {
                // Choque de recurso e agenda fechada param ESTA data, não a série.
                recusados.Add(new SessaoRecusada(quando, ex.Message));
            }
        }

        if (marcados.Count > 0) await _repo.SalvarAsync(ct);

        return new SerieAgendada(serieId, marcados, recusados);
    }

    /// <summary>
    /// Cancela o que ainda está marcado de uma série — o "o paciente desistiu no meio".
    ///
    /// Só alcança horário AINDA em aberto: sessão já atendida é fato, e cancelá-la
    /// apagaria da agenda um atendimento que aconteceu.
    /// </summary>
    public async Task<int> CancelarSerieAsync(
        string serieId, string? operador = null, CancellationToken ct = default)
    {
        var daSerie = await _repo.AgendamentosDaSerieAsync(serieId, ct);
        var cancelados = 0;

        foreach (var a in daSerie.Where(a => a.Status == StatusAgendamento.Agendado))
        {
            a.Status = StatusAgendamento.Cancelado;
            cancelados++;
        }

        if (cancelados == 0) return 0;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "SerieCancelada",
            Detalhe = $"{cancelados} sessão(ões) da série {serieId} canceladas"
        }, ct);

        await _repo.SalvarAsync(ct);
        return cancelados;
    }

    /// <summary>Sessões de uma série, na ordem em que acontecem.</summary>
    public Task<IReadOnlyList<Agendamento>> DaSerieAsync(
        string serieId, CancellationToken ct = default)
        => _repo.AgendamentosDaSerieAsync(serieId, ct);

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

    /// <summary>
    /// O PROFISSIONAL avisa que quer este paciente agora (parcela 38).
    ///
    /// É o recado que atravessa os dois módulos, e ele existe porque quem chama pelo nome
    /// na sala de espera é a RECEPÇÃO — o médico está na sala, com a porta fechada. Sem
    /// isto, a alternativa real da clínica é o profissional abrir a porta e gritar, ou
    /// ligar para o balcão a cada paciente.
    ///
    /// <b>Só se chama quem já CHEGOU.</b> Chamar quem não fez check-in faria a recepção
    /// anunciar um nome para uma sala de espera onde a pessoa não está — e a fila
    /// perderia a única informação que a torna confiável, que é a de quem está no prédio.
    /// </summary>
    public async Task<Agendamento> ChamarAsync(
        int agendamentoId, DateTime? quando = null, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException("Este horário não está mais em aberto.");

        if (ag.ChegadaEm is null)
            throw new InvalidOperationException(
                "Este paciente ainda não fez check-in no balcão — não há quem chamar na "
                + "sala de espera.");

        if (ag.InicioAtendimentoEm is not null)
            throw new InvalidOperationException("Este paciente já está na sala.");

        // Idempotente: chamar de novo não reinicia o cronômetro da chamada. Quem quer
        // insistir com alguém que não veio precisa ver há QUANTO tempo chamou — e um
        // segundo clique zerando esse número esconderia justamente o caso.
        ag.ChamadoEm ??= quando ?? DateTime.Now;
        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>
    /// Desfaz a chamada: o profissional se enganou de paciente, ou vai demorar mais.
    ///
    /// Some da coluna "chamado" e volta a esperar — a recepção para de anunciar. Não é o
    /// mesmo que <see cref="VoltarEtapaAsync"/> porque este é o caminho de quem CHAMOU;
    /// o outro é o do balcão desfazendo um clique errado, e serve à fila inteira.
    /// </summary>
    public async Task<Agendamento> DesfazerChamadaAsync(
        int agendamentoId, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.ChamadoEm is null)
            throw new InvalidOperationException("Este paciente não foi chamado.");

        if (ag.InicioAtendimentoEm is not null)
            throw new InvalidOperationException(
                "O atendimento já começou; use voltar etapa.");

        ag.ChamadoEm = null;
        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>O paciente ENTROU na sala: começo da sessão.</summary>
    public async Task<Agendamento> IniciarAtendimentoAsync(
        int agendamentoId, DateTime? quando = null, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                "Este horário não está mais em aberto.");

        var agora = quando ?? DateTime.Now;
        // Entrar direto (paciente que chegou e já foi levado) não pode deixar a espera
        // nula: sem chegada, o kanban não saberia distinguir quem esperou de quem não
        // esperou. Pelo mesmo motivo a chamada é carimbada junto — o paciente entrou,
        // logo foi chamado, e uma linha do tempo com entrada sem chamada não existe.
        ag.ChegadaEm ??= agora;
        ag.ChamadoEm ??= agora;
        ag.InicioAtendimentoEm ??= agora;
        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>
    /// Volta o cartão UMA coluna: em atendimento → chamado → chegou → aguardando. Existe
    /// porque clicar errado no kanban é rotina — e a alternativa (cancelar e remarcar)
    /// falsearia o histórico.
    /// </summary>
    public async Task<Agendamento> VoltarEtapaAsync(int agendamentoId, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                "Este horário já foi encerrado; não dá para voltar a etapa por aqui.");

        if (ag.InicioAtendimentoEm is not null) ag.InicioAtendimentoEm = null;
        else if (ag.ChamadoEm is not null) ag.ChamadoEm = null;
        else if (ag.ChegadaEm is not null) ag.ChegadaEm = null;
        else throw new InvalidOperationException("O paciente ainda nem fez check-in.");

        await _repo.SalvarAsync(ct);
        return ag;
    }

    private async Task<Agendamento> ObterParaFilaAsync(int agendamentoId, CancellationToken ct)
        => await _repo.ObterAgendamentoAsync(agendamentoId, ct)
           ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");

    /// <summary>
    /// Confirma a presença: gera o atendimento com os códigos pelas regras do convênio.
    ///
    /// ⚠️ NÃO cria mais "retorno sugerido" para o 2º código (parcela 58).
    ///
    /// GUIA NÃO É ATENDIMENTO, e confundir os dois é o defeito mais caro que este serviço
    /// já teve. O 2º código é obtido +24h depois PELA SECRETÁRIA, no sistema do convênio —
    /// o paciente não volta para nada. Materializá-lo como `Agendamento` punha na fila do
    /// balcão e na agenda dos MÉDICOS uma pessoa que não tem horário marcado e não vai
    /// aparecer.
    ///
    /// E não era só ruído visual. O cartão fantasma vinha com "Chegou / Entrou / Falta /
    /// Cancelar": um clique em Entrou → Finalizar lança um atendimento NOVO e gera guias
    /// NOVAS para uma sessão que nunca aconteceu. Faturamento inventado a partir de uma
    /// pendência de faturamento.
    ///
    /// O 2º código já tem o lugar dele, e é o coração do produto: ele nasce como
    /// <c>CodigoFaturamento</c> com `DataPrevistaFaturamento`, aparece no painel de
    /// pendências com semáforo, entra na rodada bloqueante quando vence o prazo e é
    /// mostrado ao balcão pelo `PainelRecepcaoService` junto dos pacientes do dia. Não
    /// faltava lembrete — sobrava um, no lugar errado.
    /// </summary>
    public async Task<ResultadoLancamento> ConfirmarPresencaAsync(
        int agendamentoId, CancellationToken ct = default, string? operador = null)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");

        if (ag.Status == StatusAgendamento.Realizado)
            throw new InvalidOperationException("Este agendamento já teve a presença confirmada.");

        var resultado = await _atendimentos.LancarAsync(
            ag.PacienteId, DateOnly.FromDateTime(ag.DataHora), ag.ModalidadePrevista, ag.Observacoes, ct,
            especialidadeConsulta: ag.EspecialidadeConsulta,
            modalidadeCodigo: ag.ModalidadeCodigo,
            especialidadeConsultaCodigo: ag.EspecialidadeConsultaCodigo,
            primeiroCodigo: ag.PrimeiroCodigo,
            operador: operador);

        ag.Status = StatusAgendamento.Realizado;
        ag.AtendimentoId = resultado.Atendimento.Id;

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
