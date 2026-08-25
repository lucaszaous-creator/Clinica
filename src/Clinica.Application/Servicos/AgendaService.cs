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
    private readonly ParametrosService? _parametros;

    public AgendaService(
        IClinicaRepositorio repo, AtendimentoService atendimentos,
        ParametrosService? parametros = null)
    {
        _repo = repo;
        _atendimentos = atendimentos;
        _parametros = parametros;
    }

    /// <summary>O regime "guia no agendamento" está ligado? (parcela 70, atrás da chave.)</summary>
    private async Task<bool> GuiaNaMarcacaoLigadaAsync(CancellationToken ct)
        => _parametros is not null && await _parametros.GuiaNoAgendamentoAsync(ct);

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

        // GUIA NO AGENDAMENTO (parcela 70, atrás da chave): o atendimento e as guias
        // nascem JUNTO do horário, no mesmo grafo — é o pedido da direção ("a guia nasce
        // quando o atendimento entra no sistema") e o que permite à secretária efetivar
        // no portal com antecedência. `RealizadoEm` fica NULO: a sessão ainda não
        // aconteceu — quem carimba é a presença. Guia de sessão futura não vira
        // pendência: `EstaPendente` exige a data prevista alcançada.
        if (await GuiaNaMarcacaoLigadaAsync(ct))
        {
            var montado = await _atendimentos.MontarAsync(
                pacienteId, DateOnly.FromDateTime(dataHora), modalidade, observacoes, ct,
                primeiroCodigo, especialidadeConsulta, modalidadeCodigo,
                especialidadeConsultaCodigo, operador);
            ag.Atendimento = montado.Atendimento;
        }

        await _repo.AdicionarAgendamentoAsync(ag, ct);
        await _repo.SalvarAsync(ct);

        // O número precisa do Id — save cosmético, falha vira log (nunca erro por cima
        // de um horário e guias já gravados).
        if (ag.Atendimento is { } novo && string.IsNullOrEmpty(novo.Numero))
        {
            try
            {
                novo.Numero = $"{novo.Data.Year}-{novo.Id:D6}";
                await _repo.SalvarAsync(ct);
            }
            catch (Exception ex)
            {
                Diagnostico.Registrar("Número do atendimento não pôde ser gravado na marcação", ex);
            }
        }

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
        bool manterRecursos = true, bool encaixe = false,
        ICollection<string>? avisosGuia = null)
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
        var statusAnterior = ag.Status;
        var modalidadeCodigoAnterior = ag.ModalidadeCodigo;
        var especialidadeCodigoAnterior = ag.EspecialidadeConsultaCodigo;

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

        // ⚠️ NULO quer dizer "o chamador não sabe", nunca "desligue" (a regra da parcela
        // 68, aplicada aqui porque foi aqui que ela custou caro).
        //
        // `RemarcarEmLoteAsync` — o "Empurrar" que desloca as sessões de umas férias —
        // chama sem os códigos, porque ele só muda a DATA. Enquanto a atribuição era
        // incondicional, cada sessão empurrada perdia a VARIANTE da modalidade
        // ("Acupuntura (domiciliar)" virava "Acupuntura") e a ESPECIALIDADE da consulta.
        // Nada falhava: o empurrão dizia "30 sessão(ões) empurradas", e o estrago só
        // aparecia semanas depois, uma paciente por vez, na guia que nascia errada.
        //
        // Quem MUDA a modalidade é quem informa o código dela; nesse caso a especialidade
        // que vem junto é a autoridade, inclusive para limpar. Sem código informado, o
        // horário fica com o que já tinha.
        if (modalidadeCodigo is not null)
        {
            ag.ModalidadeCodigo = modalidadeCodigo;
            ag.EspecialidadeConsulta = ehConsulta
                ? CatalogoEspecialidades.BaseEnum(especialidadeConsultaCodigo) : null;
            ag.EspecialidadeConsultaCodigo = ehConsulta ? especialidadeConsultaCodigo : null;
        }
        else if (!ehConsulta)
        {
            // A modalidade guardada não é consulta: a especialidade não tem onde morar.
            ag.EspecialidadeConsulta = null;
            ag.EspecialidadeConsultaCodigo = null;
        }
        ag.Observacoes = observacoes;
        // Remarcar um horário cancelado/faltado o traz de volta para a agenda.
        ag.Status = StatusAgendamento.Agendado;

        // ⚠️ E os CARIMBOS DA FILA vão embora com o horário antigo.
        //
        // A etapa do kanban é DERIVADA deles (`Agendamento.Etapa`), não uma coluna: um
        // horário remarcado que guardasse a chegada de terça nasceria, na quinta, já na
        // raia "Na recepção" — ou em "Em atendimento", se o paciente tinha entrado na sala
        // antes de a sessão ser interrompida. O balcão via alguém esperando desde antes de
        // a clínica abrir (a espera é contada da chegada até agora), e o quadro do médico
        // mostrava na sala um paciente que ainda estava em casa.
        //
        // Só quando a DATA muda: remarcar mexendo apenas em sala, duração ou observação é
        // ajuste do horário de hoje, e apagar o check-in de quem já está sentado no balcão
        // seria destruir o fato pelo caminho errado.
        if (horarioAnterior.Date != dataHora.Date)
        {
            ag.ChegadaEm = null;
            ag.ChamadoEm = null;
            ag.InicioAtendimentoEm = null;
        }

        // GUIA NO AGENDAMENTO (parcela 70): o atendimento acompanha o horário, no MESMO
        // SaveChanges. Reabrir um cancelado/falta devolve as guias suspensas; data nova
        // desloca as previstas; modalidade nova regera (ou recusa, se algo já saiu da
        // clínica). Nada disso salva sozinho — a atomicidade é a do Remarcar.
        if (statusAnterior is StatusAgendamento.Cancelado or StatusAgendamento.Faltou)
            Anexar(avisosGuia, await _atendimentos.RefletirStatusDoHorarioAsync(ag, operador, ct));

        // A ESPECIALIDADE da consulta conta como "mudou" também: ela vai na guia (é a
        // informação que a operadora cobra), e trocar "Consulta/Psiquiatria" por
        // "Consulta/Geriatria" mantém o código "Consulta" — só a modalidade não a vê.
        // Sem isto o horário mostrava a especialidade nova e a guia ia com a antiga.
        var regerarGuias = modalidadeCodigo is not null
                           && (modalidadeCodigo != modalidadeCodigoAnterior
                               || (ehConsulta && especialidadeConsultaCodigo != especialidadeCodigoAnterior));
        Anexar(avisosGuia, await _atendimentos.AjustarAoRemarcarAsync(
            ag, DateOnly.FromDateTime(horarioAnterior), regerarGuias, operador, ct));

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

    /// <summary>Despeja avisos no canal do chamador, quando ele quis um.</summary>
    private static void Anexar(ICollection<string>? destino, List<string> extras)
    {
        if (destino is null) return;
        foreach (var a in extras) destino.Add(a);
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
        CancellationToken ct = default, string? operador = null, TipoCodigo? primeiroCodigo = null)
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
                // O `primeiroCodigo` viaja para CADA sessão: a escolha de qual código o
                // convênio libera primeiro é feita uma vez na tela e vale para a série
                // inteira — descartá-la aqui faria as dez guias nascerem na ordem padrão
                // da regra, contradizendo a prévia que a tela mostrou.
                var agendamento = await AgendarAsync(
                    pacienteId, quando, modalidade, observacoes,
                    origem: OrigemAgendamento.Manual, ct: ct,
                    especialidadeConsulta: especialidadeConsulta,
                    modalidadeCodigo: modalidadeCodigo,
                    especialidadeConsultaCodigo: especialidadeConsultaCodigo,
                    profissionalId: profissionalId, salaId: salaId,
                    duracaoMinutos: duracaoMinutos, operador: operador,
                    primeiroCodigo: primeiroCodigo);

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
    ///
    /// ⚠️ Os cinco movimentos da fila recebem o OPERADOR e gravam trilha (parcela 69):
    /// a parcela 61 criou a permissão do ato e o ato continuava sem autoria — mover a
    /// fila escreve carimbo de hora que alimenta espera, repasse e o fechamento da
    /// sessão, e "quem carimbou isto?" não tinha resposta. O operador vem da TELA
    /// (<c>SessaoUsuario.Atual.Operador</c>), pela razão de sempre: no balcão duas
    /// pessoas dividem a máquina, e o serviço não sabe quem está logado. Movimento
    /// idempotente que não mudou nada (segundo clique no Chamar) NÃO grava linha —
    /// trilha com duplicata a cada clique é trilha que ninguém consegue ler.
    /// </summary>
    public async Task<Agendamento> RegistrarChegadaAsync(
        int agendamentoId, string operador, DateTime? quando = null, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                "Só um horário em aberto aceita check-in. Reabra o agendamento antes.");

        var mudou = ag.ChegadaEm is null;
        ag.ChegadaEm ??= quando ?? DateTime.Now;
        if (mudou) await AuditarFilaAsync(ag, operador, "FilaChegada",
            $"Check-in às {ag.ChegadaEm:HH:mm} — horário de {ag.DataHora:dd/MM/yyyy HH:mm}", ct);
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
        int agendamentoId, string operador, DateTime? quando = null, CancellationToken ct = default)
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
        var mudou = ag.ChamadoEm is null;
        ag.ChamadoEm ??= quando ?? DateTime.Now;
        if (mudou) await AuditarFilaAsync(ag, operador, "FilaChamada",
            $"Chamado às {ag.ChamadoEm:HH:mm} — horário de {ag.DataHora:dd/MM/yyyy HH:mm}", ct);
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
        int agendamentoId, string operador, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.ChamadoEm is null)
            throw new InvalidOperationException("Este paciente não foi chamado.");

        if (ag.InicioAtendimentoEm is not null)
            throw new InvalidOperationException(
                "O atendimento já começou; use voltar etapa.");

        // Desfazer APAGA um carimbo — é justamente o movimento que precisa de rastro:
        // o valor apagado vai escrito na trilha, senão ele deixa de existir em qualquer
        // lugar.
        var apagado = ag.ChamadoEm;
        ag.ChamadoEm = null;
        await AuditarFilaAsync(ag, operador, "FilaChamadaDesfeita",
            $"Apagado o carimbo de chamada ({apagado:dd/MM/yyyy HH:mm}) — horário de {ag.DataHora:dd/MM/yyyy HH:mm}", ct);
        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>O paciente ENTROU na sala: começo da sessão.</summary>
    public async Task<Agendamento> IniciarAtendimentoAsync(
        int agendamentoId, string operador, DateTime? quando = null, CancellationToken ct = default)
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
        var mudou = ag.InicioAtendimentoEm is null;
        ag.ChegadaEm ??= agora;
        ag.ChamadoEm ??= agora;
        ag.InicioAtendimentoEm ??= agora;
        if (mudou) await AuditarFilaAsync(ag, operador, "FilaEntrada",
            $"Entrou na sala às {ag.InicioAtendimentoEm:HH:mm} — horário de {ag.DataHora:dd/MM/yyyy HH:mm}", ct);
        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>
    /// Volta o cartão UMA coluna: em atendimento → chamado → chegou → aguardando. Existe
    /// porque clicar errado no kanban é rotina — e a alternativa (cancelar e remarcar)
    /// falsearia o histórico.
    /// </summary>
    public async Task<Agendamento> VoltarEtapaAsync(
        int agendamentoId, string operador, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                "Este horário já foi encerrado; não dá para voltar a etapa por aqui.");

        // Voltar etapa APAGA um carimbo de hora. O carimbo apagado vai ESCRITO na
        // trilha, com o valor: depois do apagamento ele não existe em mais lugar
        // nenhum, e "quem desfez e o que dizia" é a pergunta de qualquer conferência.
        string apagado;
        if (ag.InicioAtendimentoEm is not null)
        {
            apagado = $"entrada na sala ({ag.InicioAtendimentoEm:dd/MM/yyyy HH:mm})";
            ag.InicioAtendimentoEm = null;
        }
        else if (ag.ChamadoEm is not null)
        {
            apagado = $"chamada ({ag.ChamadoEm:dd/MM/yyyy HH:mm})";
            ag.ChamadoEm = null;
        }
        else if (ag.ChegadaEm is not null)
        {
            apagado = $"check-in ({ag.ChegadaEm:dd/MM/yyyy HH:mm})";
            ag.ChegadaEm = null;
        }
        else throw new InvalidOperationException("O paciente ainda nem fez check-in.");

        await AuditarFilaAsync(ag, operador, "FilaEtapaVoltada",
            $"Apagado o carimbo de {apagado} — horário de {ag.DataHora:dd/MM/yyyy HH:mm}", ct);
        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>
    /// A linha de trilha dos movimentos da fila — no MESMO SaveChanges do carimbo, como
    /// toda auditoria do projeto: ação que possa acontecer sem a linha é ação sem trilha.
    /// </summary>
    private Task AuditarFilaAsync(
        Agendamento ag, string operador, string acao, string detalhe, CancellationToken ct)
        => _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = acao,
            Detalhe = detalhe,
            PacienteId = ag.PacienteId
        }, ct);

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

        // A recusa mora AQUI, não na tela, porque a corrida é entre duas máquinas: o
        // cartão "Em atendimento" da máquina A só relê a cada minuto, e a máquina B pode
        // ter cancelado o horário nesse meio tempo — com as guias já SUSPENSAS. Confirmar
        // por cima carimbaria Realizado sem devolver uma guia sequer: sessão realizada sem
        // guia, calada, que é uma das duas coisas que a direção disse não aceitar.
        if (ag.Status is StatusAgendamento.Cancelado or StatusAgendamento.Faltou)
            throw new InvalidOperationException(
                "Este horário foi cancelado (ou marcado como falta) — provavelmente na outra "
                + "máquina do balcão. Reabra o horário (Remarcar) antes de concluir a sessão: "
                + "é a reabertura que devolve as guias suspensas.");

        return await ConfirmarNucleoAsync(ag, operador, ct);
    }

    /// <summary>
    /// O núcleo ATÔMICO da confirmação (parcela 70). Antes, a criação era uma corrente de
    /// gravações separadas — guias primeiro, número, carimbo por último — e cada vão era
    /// um meio-estado possível: se o carimbo falhasse (conflito de `xmin` entre as duas
    /// máquinas do balcão, queda de conexão), as guias existiam, o agendamento não sabia,
    /// e o segundo clique gerava OUTRO jogo de guias. Foi o incidente de 12/08/2026 uma
    /// camada abaixo.
    ///
    /// Agora o atendimento é MONTADO sem gravar e pendurado no agendamento pela
    /// NAVEGAÇÃO: o EF grava horário + atendimento + códigos + carimbo + NCs reabertas +
    /// trilha numa transação só. Não existe segundo momento de criação — e o que não tem
    /// segundo momento não duplica. Conflito de concorrência agora significa "releia: o
    /// `AtendimentoId` já está lá".
    /// </summary>
    private async Task<ResultadoLancamento> ConfirmarNucleoAsync(
        Agendamento ag, string? operador, CancellationToken ct)
    {
        List<string> avisos;
        Atendimento atendimento;

        if (ag.AtendimentoId is { } idExistente)
        {
            // Regime "guia no agendamento": o atendimento nasceu na MARCAÇÃO — aqui só
            // se confirma a presença e disparam os efeitos dela.
            atendimento = await _repo.ObterAtendimentoAsync(idExistente, ct)
                ?? throw new InvalidOperationException(
                    $"O agendamento {ag.Id} aponta para o atendimento {idExistente}, que não existe mais.");
            avisos = [];
        }
        else
        {
            var montado = await _atendimentos.MontarAsync(
                ag.PacienteId, DateOnly.FromDateTime(ag.DataHora), ag.ModalidadePrevista,
                ag.Observacoes, ct,
                especialidadeConsulta: ag.EspecialidadeConsulta,
                modalidadeCodigo: ag.ModalidadeCodigo,
                especialidadeConsultaCodigo: ag.EspecialidadeConsultaCodigo,
                primeiroCodigo: ag.PrimeiroCodigo,
                operador: operador);
            atendimento = montado.Atendimento;
            avisos = montado.Avisos;

            // A navegação é o que faz o EF inserir e amarrar tudo numa transação só.
            ag.Atendimento = atendimento;
        }

        ag.Status = StatusAgendamento.Realizado;
        // A âncora de "a sessão ACONTECEU" (parcela 70): com a guia nascendo na marcação,
        // existir atendimento deixou de significar sessão realizada — quem significa é
        // este carimbo, e os leitores de BI/rentabilidade/retenção ancoram nele.
        atendimento.RealizadoEm ??= DateTime.Now;

        // Efeitos de PRESENÇA que entram no mesmo commit (NCs reabertas)…
        avisos.AddRange(await _atendimentos.PrepararPresencaAsync(atendimento, ct));

        // …e a trilha do ato que gera as guias (item 5 da fila da parcela 69), idem.
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "PresencaConfirmada",
            Detalhe = $"{ag.DataHora:dd/MM/yyyy HH:mm} — {atendimento.Codigos.Count} código(s) de faturamento",
            PacienteId = ag.PacienteId
        }, ct);

        // ⚠️ O COMMIT ATÔMICO.
        await _repo.SalvarAsync(ct);

        // O número precisa do Id e vai num segundo save COSMÉTICO. Falhar aqui não pode
        // virar exceção: o commit de cima JÁ aconteceu, e a tela diria "não foi lançado"
        // sobre uma guia que existe — o gesto que produziu os três encaixes de 12/08.
        // Nada duplica (o AtendimentoId no agendamento impede a recriação); vira aviso.
        if (string.IsNullOrEmpty(atendimento.Numero))
        {
            try
            {
                atendimento.Numero = $"{atendimento.Data.Year}-{atendimento.Id:D6}";
                await _repo.SalvarAsync(ct);
            }
            catch (Exception ex)
            {
                Diagnostico.Registrar("Número do atendimento não pôde ser gravado", ex);
                avisos.Add("O número/protocolo do atendimento não pôde ser gravado agora — "
                           + "a guia está salva e o faturamento a enxerga normalmente.");
            }
        }

        // Renovação da consulta: gravação própria, falha vira aviso (nunca desfaz).
        avisos.AddRange(await _atendimentos.ConcluirPresencaAsync(atendimento, ct));

        return new ResultadoLancamento(atendimento, avisos);
    }

    /// <summary>
    /// O LANÇAMENTO AVULSO num gesto atômico (parcela 70) — a porta que a clínica usa o
    /// dia inteiro enquanto a agenda mora no Amplimed. Substitui a corrente de três
    /// chamadas (Agendar → RegistrarChegada → ConfirmarPresenca, cinco SaveChanges) cujos
    /// vãos produziram os três encaixes de 12/08 e a guia duplicada: o encaixe nasce na
    /// hora real com o check-in carimbado, o atendimento e as guias — o grafo INTEIRO num
    /// único SaveChanges do núcleo.
    /// </summary>
    public async Task<(Agendamento Agendamento, ResultadoLancamento Lancamento)> LancarAvulsoAsync(
        int pacienteId, DateTime dataHora, ModalidadeAtendimento modalidade, string? observacoes,
        CancellationToken ct = default, string? modalidadeCodigo = null,
        string? especialidadeConsultaCodigo = null, TipoCodigo? primeiroCodigo = null,
        int? profissionalId = null, string? operador = null, int? salaId = null)
    {
        if (modalidadeCodigo is not null)
            modalidade = CatalogoModalidades.Base(modalidadeCodigo);

        // Encaixe: aceita por cima de horário ocupado — o paciente já está aqui.
        await GarantirSemChoqueAsync(
            dataHora, null, profissionalId, null, pacienteId, null, encaixe: true, ct);

        var agora = DateTime.Now;
        var ehConsulta = modalidade == ModalidadeAtendimento.Consulta;
        var ag = new Agendamento
        {
            PacienteId = pacienteId,
            DataHora = dataHora,
            ModalidadePrevista = modalidade,
            ModalidadeCodigo = modalidadeCodigo ?? modalidade.ToString(),
            EspecialidadeConsulta = ehConsulta
                ? CatalogoEspecialidades.BaseEnum(especialidadeConsultaCodigo) : null,
            EspecialidadeConsultaCodigo = ehConsulta ? especialidadeConsultaCodigo : null,
            Observacoes = observacoes,
            Origem = OrigemAgendamento.Manual,
            Status = StatusAgendamento.Agendado,
            ProfissionalId = profissionalId,
            // A sala do encaixe (parcela 70, achado do cliente no teste): a Fila anuncia
            // "anuncie para a sala X" na chamada, e o avulso sem sala saía "sala —".
            SalaId = salaId,
            Encaixe = true,
            PrimeiroCodigo = primeiroCodigo,
            CriadoPor = string.IsNullOrWhiteSpace(operador) ? null : operador.Trim(),
            CriadoEm = agora,
            // O paciente está no balcão: o check-in é o fato, não uma etapa a cumprir.
            ChegadaEm = agora
        };

        await _repo.AdicionarAgendamentoAsync(ag, ct);
        await AuditarFilaAsync(ag, operador ?? "?", "FilaChegada",
            $"Check-in às {agora:HH:mm} — encaixe avulso de {ag.DataHora:dd/MM/yyyy HH:mm}", ct);

        var lancamento = await ConfirmarNucleoAsync(ag, operador, ct);
        return (ag, lancamento);
    }

    public async Task<IReadOnlyList<string>> CancelarAsync(
        int agendamentoId, string? operador = null, CancellationToken ct = default)
        => await AlterarStatusAsync(agendamentoId, StatusAgendamento.Cancelado, operador, ct);

    public async Task<IReadOnlyList<string>> MarcarFaltaAsync(
        int agendamentoId, string? operador = null, CancellationToken ct = default)
        => await AlterarStatusAsync(agendamentoId, StatusAgendamento.Faltou, operador, ct);

    private async Task<IReadOnlyList<string>> AlterarStatusAsync(
        int agendamentoId, StatusAgendamento status, string? operador, CancellationToken ct)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");

        var anterior = ag.Status;
        ag.Status = status;

        // GUIA NO AGENDAMENTO (parcela 70): sessão que não aconteceu não fatura — as
        // guias abertas deste horário são SUSPENSAS no mesmo SaveChanges. Sem isto, o
        // no-show viraria pendência eterna e, dez dias depois, rodada bloqueante por uma
        // guia que nunca vai a operadora nenhuma.
        var avisos = await _atendimentos.RefletirStatusDoHorarioAsync(ag, operador, ct);

        // Falta e cancelamento são sessão não faturada: precisam de rastro de quem marcou.
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = status == StatusAgendamento.Cancelado ? "AgendamentoCancelado" : "AgendamentoFalta",
            Detalhe = $"{ag.DataHora:dd/MM/yyyy HH:mm} — de {anterior} para {status}",
            PacienteId = ag.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return avisos;
    }
}
