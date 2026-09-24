using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// Um horário do paciente ainda EM ABERTO no dia (set/2026): o que o Novo atendimento
/// mostra ao escolher o paciente, para lançar SOBRE ele em vez de criar um encaixe ao
/// lado. A frase sai pronta daqui porque quem a lê são dois lugares (o aviso na escolha
/// e o rótulo do botão), e duas montagens divergiriam.
/// </summary>
public sealed record HorarioEmAberto(
    int AgendamentoId, DateTime DataHora, string Modalidade, string? ModalidadeCodigo,
    string? EspecialidadeConsultaCodigo, string? Profissional, bool JaChegou, bool ComGuia,
    bool Importado)
{
    /// <summary>"09h00" — a forma que o balcão fala.</summary>
    public string Hora => DataHora.ToString("HH'h'mm");

    /// <summary>"09h00 · Consulta · Dra. Ana · importado do sistema anterior".</summary>
    public string Descricao =>
        $"{Hora} · {Modalidade}"
        + (Profissional is null ? "" : $" · {Profissional}")
        + (Importado ? " · importado do sistema anterior" : "")
        + (JaChegou ? " · já fez check-in" : "")
        + (ComGuia ? " · guia já gerada na marcação" : "");
}

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

    private async Task<bool> ConsultaDePsicologiaAsync(int? profissionalId, ModalidadeAtendimento modalidade,
        string? modalidadeCodigo, string? especialidadeCodigo, CancellationToken ct)
    {
        if (profissionalId is not { } id) return false;
        var profissional = await _repo.ObterProfissionalAsync(id, ct);
        if (profissional is null || !profissional.Ativo)
            throw new InvalidOperationException("Selecione um profissional ativo para este atendimento.");
        var psicologia = string.Equals(profissional.EspecialidadeCodigo, "Psicologia", StringComparison.OrdinalIgnoreCase);
        if (psicologia && (modalidade != ModalidadeAtendimento.Consulta || especialidadeCodigo is not null
            && !string.Equals(especialidadeCodigo, "Psicologia", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Profissional de Psicologia recebe somente consulta de Psicologia; confira a modalidade e a especialidade.");
        if (profissional.HabilitacoesAtendimento is not null)
        {
            if (!profissional.Atende(modalidadeCodigo ?? modalidade.ToString(), psicologia ? "Psicologia" : especialidadeCodigo))
                throw new InvalidOperationException("Este profissional não está habilitado para a modalidade e especialidade escolhidas. Confira as habilitações com o gerente.");
            return psicologia;
        }
        return psicologia;
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

        if (await ConsultaDePsicologiaAsync(profissionalId, modalidade, modalidadeCodigo,
            especialidadeConsultaCodigo ?? especialidadeConsulta?.ToString(), ct))
        {
            especialidadeConsulta = null;
            especialidadeConsultaCodigo = "Psicologia";
        }

        if (duracaoMinutos is { } d && d <= 0)
            throw new InvalidOperationException("A duração do horário precisa ser maior que zero.");

        // ===== QUEM VAI ATENDER É OBRIGATÓRIO NA MARCAÇÃO (parcela 95) =====
        //
        // O fluxo que a direção pediu é "a secretária marca → cai na agenda do médico
        // respectivo → ele clica em atender". A primeira seta só existe com este campo:
        // o "Meu dia" e a "Minha semana" filtram por `ProfissionalId`, então horário sem
        // dono não aparece na agenda de NINGUÉM — e fica de fora do REPASSE, que lê quem
        // atendeu do agendamento porque `Atendimento` não guarda profissional. Até aqui a
        // tela AVISAVA o custo e deixava salvar (parcela 69); com o atendimento saindo da
        // agenda do profissional, avisar deixou de bastar: o horário sem dono não é um
        // horário pior, é um horário que o fluxo inteiro não alcança.
        //
        // ⚠️ O ENCAIXE continua passando, e é a assimetria deliberada. Encaixe é o
        // paciente que está NO BALCÃO agora (o lançamento avulso vira encaixe desde a
        // parcela 60): recusá-lo travaria quem chegou sem hora marcada quando a
        // recepcionista ainda não sabe quem vai pegar — e ali a tela avisa o custo com a
        // pessoa na frente, que é onde o aviso funciona. O que se exige é do horário
        // MARCADO, que existe justamente para cair na agenda de alguém.
        //
        // A recusa mora aqui, e não nas telas, pela razão de sempre: são CINCO portas de
        // marcação (as duas da Recepção, a série, a lista de espera e a agenda do
        // faturamento), e validar em uma cobre uma e deixa quatro passando.
        if (!encaixe && profissionalId is null)
            throw new InvalidOperationException(
                "Escolha quem vai atender. Sem profissional o horário não aparece na agenda "
                + "de ninguém — nem no \"Meu dia\" de quem atende — e fica de fora do repasse.");

        await GarantirHorarioPermitidoAsync(dataHora, duracaoMinutos, profissionalId, salaId, pacienteId, null, ct);

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
        var psicologia = await ConsultaDePsicologiaAsync(novoProfissional, modalidade,
            modalidadeCodigo ?? ag.ModalidadeCodigo,
            modalidadeCodigo is null ? ag.EspecialidadeConsultaCodigo : especialidadeConsultaCodigo, ct);
        if (psicologia) especialidadeConsultaCodigo = "Psicologia";

        if (novaDuracao is { } d && d <= 0)
            throw new InvalidOperationException("A duração do horário precisa ser maior que zero.");

        // Corrigir observações de um horário existente não invalida fatos antigos.
        // Mudança de intervalo/recurso ou reativação precisa respeitar a trava vigente.
        if (dataHora != ag.DataHora || novoProfissional != ag.ProfissionalId || novaSala != ag.SalaId
            || novaDuracao != ag.DuracaoMinutos || !ag.OcupaAgenda)
            await GarantirHorarioPermitidoAsync(dataHora, novaDuracao, novoProfissional, novaSala, ag.PacienteId, ag.Id, ct);

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
        if (psicologia)
        {
            ag.EspecialidadeConsulta = null;
            ag.EspecialidadeConsultaCodigo = "Psicologia";
        }
        else if (!ehConsulta)
        {
            // A modalidade guardada não é consulta: a especialidade não tem onde morar.
            ag.EspecialidadeConsulta = null;
            ag.EspecialidadeConsultaCodigo = null;
        }
        ag.Observacoes = observacoes;
        // Remarcar um horário cancelado/faltado/substituído o traz de volta para a agenda.
        // O vínculo com a sessão que o substituiu vai embora junto: horário reaberto é
        // horário sem substituto — e a conciliação volta a perguntar por ele.
        ag.Status = StatusAgendamento.Agendado;
        ag.AtendimentoSubstitutoId = null;
        ag.AtendimentoSubstituto = null;

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
            // ⚠️ O ENCERRAMENTO vai junto (parcela 74). Sem ele, o horário remarcado nasceria
            // na quinta com o selo verde "Encerrado às 14h32" de uma sessão que não
            // aconteceu — e um cartão em "Aguardando" dizendo que já terminou é lido pelo
            // balcão como sessão pronta para fechar. Cada carimbo novo da fila tem de entrar
            // NESTE bloco: é a lição da parcela 69, e a parcela 74 a repetiu.
            ag.FimAtendimentoEm = null;
        }

        // GUIA NO AGENDAMENTO (parcela 70): o atendimento acompanha o horário, no MESMO
        // SaveChanges. Reabrir um cancelado/falta devolve as guias suspensas; data nova
        // desloca as previstas; modalidade nova regera (ou recusa, se algo já saiu da
        // clínica). Nada disso salva sozinho — a atomicidade é a do Remarcar.
        if (statusAnterior is StatusAgendamento.Cancelado or StatusAgendamento.Faltou
            or StatusAgendamento.Substituido)
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

    /// <summary>Ocupações que atravessam a abertura do período; complementam a consulta por data inicial.</summary>
    public Task<IReadOnlyList<Agendamento>> OcupacoesNaViradaAsync(DateTime inicio, CancellationToken ct = default)
        => _repo.AgendamentosQueSobrepoemAsync(inicio, inicio.AddTicks(1), ct);

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
    /// Conflitos e impedimentos do candidato. A trava é opcional por profissional e
    /// compartilhada entre postos; encaixe não ignora uma trava ativa.
    /// </summary>
    public async Task<IReadOnlyList<ConflitoAgenda>> ConflitosAsync(
        DateTime dataHora, int? duracaoMinutos = null,
        int? profissionalId = null, int? salaId = null, int? pacienteId = null,
        int? ignorarAgendamentoId = null, CancellationToken ct = default)
    {
        // O profissional é lido UMA vez: dá a duração padrão dele e a jornada (set/2026).
        var profissional = profissionalId is { } pid ? await _repo.ObterProfissionalAsync(pid, ct) : null;
        var fim = dataHora.AddMinutes(
            duracaoMinutos ?? profissional?.DuracaoPadraoMinutos ?? Agendamento.DuracaoPadraoMinutos);

        var doDia = (await _repo.AgendamentosQueSobrepoemAsync(dataHora, fim, ct))
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

        // A jornada gera aviso ou impedimento conforme a trava do profissional.
        if (profissional is { JornadaDeclarada: true } && !profissional.DentroDoExpediente(dataHora, fim))
            conflitos.Add(new ConflitoAgenda(
                RecursoAgenda.Expediente, 0,
                $"{profissional.Rotulo} não atende neste horário — atende {profissional.DescricaoJornada}.",
                dataHora, fim));

        return conflitos.Select(c => c with
        {
            ImpedeMarcar = profissional?.AgendaProtegida == true
                && c.Recurso is RecursoAgenda.Profissional or RecursoAgenda.Bloqueio or RecursoAgenda.Expediente
        }).ToList();
    }

    private async Task GarantirHorarioPermitidoAsync(DateTime inicio, int? duracao, int? profissional,
        int? sala, int paciente, int? ignorar, CancellationToken ct)
    {
        if (profissional is null || (await _repo.ObterProfissionalAsync(profissional.Value, ct))?.AgendaProtegida != true)
            return;
        var impedimentos = (await ConflitosAsync(inicio, duracao, profissional, sala, paciente, ignorar, ct))
            .Where(c => c.ImpedeMarcar).Select(c => c.Descricao).Distinct().ToArray();
        if (impedimentos.Length > 0)
            throw new InvalidOperationException("Agenda protegida. Escolha outro horário. " + string.Join(" ", impedimentos));
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
    /// 2. **Uma data recusada não aborta a série: ela é PULADA e a lista diz qual.**
    ///    ⚠️ Desde set/2026 CHOQUE não recusa mais nada (ver <see cref="ConflitosAsync"/>),
    ///    então a série marca por cima de horário ocupado e de feriado — o que ainda cai
    ///    em <see cref="SerieAgendada.Recusados"/> é a data sem profissional. O
    ///    <c>catch</c> fica: recusar as dez porque uma não deu devolveria a recepção ao
    ///    trabalho manual, e é a próxima recusa nova que ele apanha sem ninguém lembrar.
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
                // O que recusar ESTA data não para a série. Hoje é a falta de
                // profissional; choque e agenda fechada deixaram de recusar em set/2026.
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
    /// ⚠️ ELE TEM PORTA DE NOVO — a agenda do dia da Recepção (set/2026, pedido da
    /// secretária: *"hoje temos Marcado e Concluído, poderíamos colocar um chegou no local
    /// entre os dois"*). E a história vale mais que a porta: semanas antes, a direção
    /// tinha mandado tirar a fila em etapas (*"a secretaria marca e o médico/enfermeiro
    /// atende"*) e os quatro botões saíram das duas listas. O que a prática mostrou é que
    /// a clínica dispensava as ETAPAS INTERMEDIÁRIAS, não o "quem já está aqui" — então
    /// só a chegada voltou.
    ///
    /// ⛔ O <see cref="ChamarAsync"/>, o <see cref="DesfazerChamadaAsync"/> e o
    /// <see cref="VoltarEtapaAsync"/> CONTINUAM sem porta em produção, testados, e é
    /// decisão: a fila em etapas volta a ter tela no dia em que uma clínica a quiser, e
    /// apagar o motor obrigaria a reescrevê-lo. Quem varrer "método sem chamador" leia
    /// isto antes de removê-los.
    ///
    /// ⚠️ E é daqui que volta a sair o TEMPO DE ESPERA. Enquanto não houve porta,
    /// <see cref="Agendamento.EsperaMinutos"/> era nulo em toda sessão — e por isso os
    /// cartões de espera média saíram do painel e da lista. Eles não voltaram junto:
    /// média sobre uma base que a clínica ainda não está de fato colhendo é número com
    /// cara de exato. Ver também o <see cref="IniciarAtendimentoAsync"/>, que deixou de
    /// INVENTAR a chegada — com a porta de volta, inventá-la daria espera zero para todo
    /// mundo, que é o *"ninguém espera nesta clínica"* que aquela mudança evitou.
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

    /// <summary>
    /// O paciente ENTROU na sala: começo da sessão. Desde set/2026 é o ÚNICO carimbo de
    /// fila que as telas produzem — é o que o "Atender" do profissional grava.
    ///
    /// ⚠️ ELE NÃO INVENTA MAIS A CHEGADA NEM A CHAMADA, e a mudança é o preço de a clínica
    /// ter dispensado o check-in no balcão. Até aqui a entrada direta carimbava
    /// <c>ChegadaEm</c> e <c>ChamadoEm</c> no mesmo instante, com o argumento (então
    /// correto) de que o kanban precisava distinguir quem esperou; entrar direto era a
    /// EXCEÇÃO. Sem os botões de fila, ela virou a regra — e a ficção passaria a produzir
    /// um NÚMERO FALSO: chegada igual à entrada dá <c>EsperaMinutos = 0</c> para todo
    /// paciente, e o painel anunciaria "espera média 0 min", isto é, que ninguém espera
    /// nesta clínica. Medida inventada apresentada como exata é a garantia aparente que
    /// este projeto recusa desde a parcela 3; sem chegada, <c>EsperaMinutos</c> devolve
    /// NULO e a tela escreve "—", que é a verdade: não foi medido.
    ///
    /// A etapa continua certa porque <c>Agendamento.Etapa</c> olha
    /// <c>InicioAtendimentoEm</c> PRIMEIRO — entrar sem chegada registrada dá
    /// <c>EmAtendimento</c>, como sempre deu.
    /// </summary>
    public async Task<Agendamento> IniciarAtendimentoAsync(
        int agendamentoId, string operador, DateTime? quando = null, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                "Este horário não está mais em aberto.");

        var agora = quando ?? DateTime.Now;
        var mudou = ag.InicioAtendimentoEm is null;
        ag.InicioAtendimentoEm ??= agora;
        if (mudou) await AuditarFilaAsync(ag, operador, "FilaEntrada",
            $"Entrou na sala às {ag.InicioAtendimentoEm:HH:mm} — horário de {ag.DataHora:dd/MM/yyyy HH:mm}", ct);
        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>
    /// O PROFISSIONAL terminou com o paciente (parcela 74) — ele sai da sala e vai ao
    /// balcão.
    ///
    /// ⚠️ Isto não conclui o atendimento, e a diferença é a decisão da parcela 61:
    /// concluir são QUATRO fatos do mesmo ato e três são do balcão (o pacote debita, o
    /// insumo sai, o dinheiro entra). O que aqui se afirma é só <i>"terminei"</i>, e o
    /// <c>Status</c> continua <c>Agendado</c> de propósito — marcá-lo <c>Realizado</c>
    /// daqui pularia os três fatos e faria o dia fechar com o caixa sem a sessão.
    ///
    /// É o par do <see cref="ChamarAsync"/>: aquele leva o recado do consultório ao
    /// balcão para o paciente ENTRAR, este leva o recado de que ele está SAINDO. Sem o
    /// segundo, a recepcionista só descobre que o médico terminou quando o paciente
    /// aparece na frente dela — e o cartão fica em "Em atendimento" por meia hora depois
    /// de a sala estar vazia, o que faz o quadro do dia mentir sobre quem está ocupado.
    ///
    /// Encerrar de novo NÃO reescreve o carimbo (<c>??=</c>), pela razão do
    /// <see cref="ChamarAsync"/>: quem clica duas vezes precisa continuar vendo a HORA em
    /// que terminou, e o segundo clique esconderia justamente o atendimento demorado.
    /// </summary>
    public async Task<Agendamento> EncerrarAtendimentoAsync(
        int agendamentoId, string operador, DateTime? quando = null, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                "Este horário já foi encerrado.");

        // Encerrar sem ter começado inventaria uma sessão de duração negativa e um
        // cartão que sai da sala sem nunca ter entrado nela.
        if (ag.InicioAtendimentoEm is null)
            throw new InvalidOperationException(
                "O atendimento ainda não começou — o paciente não entrou na sala.");

        var mudou = ag.FimAtendimentoEm is null;
        ag.FimAtendimentoEm ??= quando ?? DateTime.Now;

        if (mudou) await AuditarFilaAsync(ag, operador, "FilaAtendimentoEncerrado",
            $"Atendimento encerrado às {ag.FimAtendimentoEm:HH:mm} "
            + $"(durou {ag.DuracaoDoAtendimento(ag.FimAtendimentoEm!.Value)} min) — "
            + $"horário de {ag.DataHora:dd/MM/yyyy HH:mm}", ct);

        await _repo.SalvarAsync(ct);
        return ag;
    }

    /// <summary>
    /// DESFAZ o encerramento: o paciente volta a estar em atendimento (parcela 74, 2ª
    /// rodada).
    ///
    /// Existe separado do <see cref="VoltarEtapaAsync"/> pela mesma razão que
    /// <see cref="DesfazerChamadaAsync"/> existe desde a parcela 38: são atos diferentes.
    /// Voltar etapa tira o paciente da SALA; isto diz apenas <i>"eu não tinha terminado"</i>
    /// — o caso do profissional que clicou em Finalizar no paciente errado, ou que precisou
    /// chamar a pessoa de volta.
    ///
    /// O carimbo apagado vai ESCRITO na trilha, com o valor: depois do apagamento ele não
    /// existe em mais lugar nenhum, e "quem desfez e o que dizia" é a pergunta de qualquer
    /// conferência.
    /// </summary>
    public async Task<Agendamento> ReabrirAtendimentoAsync(
        int agendamentoId, string operador, CancellationToken ct = default)
    {
        var ag = await ObterParaFilaAsync(agendamentoId, ct);

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                "Este horário já foi encerrado no balcão.");

        if (ag.FimAtendimentoEm is null)
            throw new InvalidOperationException(
                "Este atendimento não está encerrado.");

        var apagado = ag.FimAtendimentoEm;
        ag.FimAtendimentoEm = null;

        await AuditarFilaAsync(ag, operador, "FilaAtendimentoReaberto",
            $"Apagado o encerramento ({apagado:dd/MM/yyyy HH:mm}) — "
            + $"horário de {ag.DataHora:dd/MM/yyyy HH:mm}", ct);

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

            // ⚠️ O ENCERRAMENTO SAI JUNTO, e não como um passo próprio (corrigido na 2ª
            // rodada da parcela 74). Voltar etapa promete "volta o cartão UMA coluna", e
            // FimAtendimentoEm não é coluna nenhuma por decisão da própria parcela — então
            // gastá-lo num passo separado consumia o clique SEM MOVER o cartão, enquanto as
            // duas telas afirmavam que ele tinha voltado. Clique que não faz nada e diz que
            // fez é pior do que botão apagado (parcela 41).
            //
            // Sair da sala é o fato: quem não está mais em atendimento não tem fim de
            // atendimento. Para DESFAZER só o encerramento — o médico que finalizou por
            // engano e quer o paciente de volta na sala — existe
            // <see cref="ReabrirAtendimentoAsync"/>, do mesmo jeito que
            // <see cref="DesfazerChamadaAsync"/> é separado deste método desde a parcela 38.
            if (ag.FimAtendimentoEm is not null)
            {
                apagado += $" e o encerramento ({ag.FimAtendimentoEm:dd/MM/yyyy HH:mm})";
                ag.FimAtendimentoEm = null;
            }

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
        if (ag.Status == StatusAgendamento.Substituido)
            throw new InvalidOperationException(
                "Este horário foi encerrado porque a sessão dele já está lançada por fora "
                + "(conciliação da agenda). Concluir aqui criaria um segundo jogo de guias para a "
                + "mesma sessão. Se a sessão de hoje é OUTRA, reabra o horário pela agenda (Remarcar).");

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
    /// <summary>Conclui a sessão e registra seu fim no mesmo commit das guias. Aceita retomada.</summary>
    public async Task<ResultadoLancamento> ConcluirAtendimentoClinicoAsync(
        int agendamentoId, string operador, CancellationToken ct = default, int? usuarioId = null,
        bool? houveEnfermagem = null, bool permitirEnfermagemPosterior = false,
        bool reprocessarPresenca = true)
    {
        if (permitirEnfermagemPosterior && usuarioId is null)
            throw new UnauthorizedAccessException("Identifique o médico responsável ou o Gerente Geral.");
        if (usuarioId is { } autor)
            await ExigirConclusaoClinicaAsync(agendamentoId, autor, ct);
        var ag = await ObterParaFilaAsync(agendamentoId, ct);
        if (ag.Status == StatusAgendamento.Realizado && ag.FimAtendimentoEm is not null
            && ag.AtendimentoId is { } existente)
            return new ResultadoLancamento(await _repo.ObterAtendimentoAsync(existente, ct)
                ?? throw new InvalidOperationException("O atendimento vinculado não foi encontrado."), []);
        if (ag.Status is not (StatusAgendamento.Agendado or StatusAgendamento.Realizado))
            throw new InvalidOperationException("Este horário não pode ser concluído na situação atual.");
        if (ag.Status == StatusAgendamento.Realizado && ag.AtendimentoId is null)
            throw new InvalidOperationException("Este horário está realizado, mas não tem atendimento vinculado. "
                + "Confira o lançamento original antes de concluir; não crie outro atendimento.");

        if (permitirEnfermagemPosterior && await ExigeConferenciaEnfermagemAsync(agendamentoId, ct)
            && !await _repo.TemEvolucaoEnfermagemVigenteNoHorarioAsync(agendamentoId, ct))
            await AuditarFilaAsync(ag, operador, "EnfermagemPendenteNaConclusao",
                "Conclusão solicitada com evolução de enfermagem ainda não vinculada. Registro posterior permitido na sessão original; o resultado da conclusão é registrado separadamente.", ct);

        if (!permitirEnfermagemPosterior && usuarioId is { } responsavel && await ExigeConferenciaEnfermagemAsync(agendamentoId, ct))
        {
            await ConferirEnfermagemParaConclusaoAsync(agendamentoId, houveEnfermagem, ct);
            ag.HouveAtendimentoEnfermagem = houveEnfermagem;
            ag.EnfermagemConferidaEm = DateTime.Now;
            ag.EnfermagemConferidaPorUsuarioId = responsavel;
            await AuditarFilaAsync(ag, operador, "EnfermagemConferidaNaConclusao",
                houveEnfermagem == true ? "Operador confirmou enfermagem com evolução vinculada." : "Operador declarou que não houve atendimento de enfermagem.", ct);
        }

        // Finalizar é a confirmação clínica explícita. A sessão pode ter sido escrita
        // sem usar o cronômetro; não inventar um horário de início para permitir o fim.

        return await ConfirmarNucleoAsync(ag, operador, ct, encerrarClinico: true,
            reprocessarPresenca: reprocessarPresenca);
    }

    /// <summary>Relê o acesso antes de gravar, inclusive quando a permissão mudou com a tela aberta.</summary>
    public async Task ExigirConclusaoClinicaAsync(int agendamentoId, int usuarioId, CancellationToken ct = default)
    {
        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct);
        if (usuario is null || !usuario.Ativo
            || (usuario.Perfil != PerfilAcesso.Gerente && usuario.Profissional?.Ativo != true)
            || !ConclusaoClinica.Permitida(usuario.Perfil, usuario.Efetivas, usuario.ProfissionalId))
            throw new UnauthorizedAccessException("Seu perfil não permite concluir este atendimento. A enfermagem registra a evolução sem finalizar; o médico responsável ou o Gerente Geral conclui a sessão.");
        var horario = await ObterParaFilaAsync(agendamentoId, ct);
        if (usuario.Perfil != PerfilAcesso.Gerente && horario.ProfissionalId != usuario.ProfissionalId)
            throw new UnauthorizedAccessException("A conclusão deve ser feita pelo profissional responsável por este atendimento.");
    }

    public async Task ConferirEnfermagemParaConclusaoAsync(int agendamentoId, bool? houveEnfermagem, CancellationToken ct = default)
    {
        if (!await ExigeConferenciaEnfermagemAsync(agendamentoId, ct)) return;
        if (houveEnfermagem is null)
            throw new InvalidOperationException("Antes de finalizar, informe se houve atendimento de enfermagem nesta sessão.");
        var temEvolucao = await _repo.TemEvolucaoEnfermagemVigenteNoHorarioAsync(agendamentoId, ct);
        if (houveEnfermagem == false && temEvolucao)
            throw new InvalidOperationException("Há evolução de enfermagem vinculada a esta sessão. Confira o registro e informe que houve atendimento de enfermagem.");
        if (houveEnfermagem == true && !temEvolucao)
            throw new InvalidOperationException("A enfermagem precisa salvar e vincular sua evolução a esta sessão antes de o médico finalizar. O atendimento continua aberto.");
    }

    public async Task<string?> PendenciaEnfermagemAsync(int agendamentoId, CancellationToken ct = default)
        => await ExigeConferenciaEnfermagemAsync(agendamentoId, ct)
            && !await _repo.TemEvolucaoEnfermagemVigenteNoHorarioAsync(agendamentoId, ct)
            ? "Evolução de enfermagem pendente. Registre na sessão BSV original; não é necessário gerar outra guia."
            : null;

    public async Task<bool> ExigeConferenciaEnfermagemAsync(int agendamentoId, CancellationToken ct = default)
        => (await new PoliticaConclusaoService(_repo).ObterAsync(ct))
            .ExigeEnfermagem(await ObterParaFilaAsync(agendamentoId, ct));

    private async Task<ResultadoLancamento> ConfirmarNucleoAsync(
        Agendamento ag, string? operador, CancellationToken ct,
        bool confirmarPresenca = true, bool encerrarClinico = false, bool reprocessarPresenca = true)
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

        // ===== A EVOLUÇÃO NASCE COM O NÚMERO DO ATENDIMENTO (set/2026, pedido da direção) =====
        //
        // No regime atual o médico ESCREVE a sessão (passo 1 do Finalizar) antes de o
        // atendimento existir (passo 3, aqui), então a evolução ficava com `AtendimentoId`
        // nulo para sempre — e nada ligava os dois depois. As evoluções deste horário que
        // ainda não apontam para atendimento nenhum são amarradas AQUI, no MESMO commit em
        // que o atendimento nasce: ou existe tudo amarrado, ou nada. Quando o atendimento já
        // existe (regime "guia no agendamento", avulso), a gravação da evolução já o resolve
        // pelo horário (`ProntuarioService.SalvarAsync`); esta linha é o cinto para o que
        // foi escrito ANTES — inclusive a evolução do colega que cobriu o horário.
        foreach (var evolucao in await _repo.EvolucoesSemAtendimentoDoHorarioAsync(ag.Id, ct))
        {
            if (atendimento.Id != 0) evolucao.AtendimentoId = atendimento.Id;
            else evolucao.Atendimento = atendimento; // a navegação: o Id ainda não existe
        }

        if (confirmarPresenca)
        {
            if (encerrarClinico && ag.FimAtendimentoEm is null)
            {
                ag.FimAtendimentoEm = DateTime.Now;
                await AuditarFilaAsync(ag, operador ?? "?", "FilaAtendimentoEncerrado",
                    $"Atendimento encerrado às {ag.FimAtendimentoEm:HH:mm}", ct);
            }
            ag.Status = StatusAgendamento.Realizado;
            // A âncora de "a sessão ACONTECEU" (parcela 70): com a guia nascendo na marcação,
            // existir atendimento deixou de significar sessão realizada — quem significa é
            // este carimbo, e os leitores de BI/rentabilidade/retenção ancoram nele.
            atendimento.RealizadoEm ??= DateTime.Now;

            // Efeitos de PRESENÇA que entram no mesmo commit (NCs reabertas)…
            // Recuperar uma sessão histórica não significa que o paciente voltou hoje.
            if (reprocessarPresenca)
                avisos.AddRange(await _atendimentos.PrepararPresencaAsync(atendimento, ct));

            // …e a trilha do ato que gera as guias (item 5 da fila da parcela 69), idem.
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
                Acao = "PresencaConfirmada",
                Detalhe = $"{ag.DataHora:dd/MM/yyyy HH:mm} — {atendimento.Codigos.Count} código(s) de faturamento",
                PacienteId = ag.PacienteId
            }, ct);

        }
        else
        {
            await AuditarFilaAsync(ag, operador ?? "?", "AtendimentoPreparado",
                "Atendimento e guias registrados; a sessão permanece aberta para o profissional.", ct);
        }

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
        if (confirmarPresenca && reprocessarPresenca)
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
        int? profissionalId = null, string? operador = null, int? salaId = null,
        bool concluirSessao = true)
    {
        if (modalidadeCodigo is not null)
            modalidade = CatalogoModalidades.Base(modalidadeCodigo);

        if (await ConsultaDePsicologiaAsync(profissionalId, modalidade, modalidadeCodigo,
            especialidadeConsultaCodigo, ct))
            especialidadeConsultaCodigo = "Psicologia";

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

        var lancamento = await ConfirmarNucleoAsync(ag, operador, ct, confirmarPresenca: concluirSessao);
        return (ag, lancamento);
    }

    /// <summary>
    /// Os horários que o paciente ainda tem EM ABERTO num dia (set/2026 — a semana em que
    /// a agenda importada do Smart Clinic e o Novo atendimento passaram a conviver).
    ///
    /// "Em aberto" é o horário que ainda pode virar a sessão daquele dia:
    /// <c>Agendado</c>, presença não confirmada. Cancelado e falta não disputam nada;
    /// realizado já é atendimento — e desse a capa (<see cref="AtendimentoService.CapasDoDiaAsync"/>)
    /// avisa. No regime "guia no agendamento" o horário marcado JÁ tem atendimento e
    /// guias e continua em aberto: lançar sobre ele é confirmar a presença, o mesmo
    /// Concluir da Fila.
    /// </summary>
    public async Task<IReadOnlyList<HorarioEmAberto>> HorariosEmAbertoDoDiaAsync(
        int pacienteId, DateOnly dia, CancellationToken ct = default)
    {
        var doDia = await _repo.AgendamentosDoPacienteNoDiaAsync(pacienteId, dia, ct);
        var abertos = doDia
            .Where(a => a.Status == StatusAgendamento.Agendado)
            .OrderBy(a => a.DataHora).ThenBy(a => a.Id)
            .ToList();
        if (abertos.Count == 0) return [];

        var profissionais = await _repo.ProfissionaisAsync(ct);
        return abertos.Select(a => new HorarioEmAberto(
            a.Id, a.DataHora,
            CatalogoModalidades.Nome(a.ModalidadeCodigo, a.ModalidadePrevista),
            a.ModalidadeCodigo, a.EspecialidadeConsultaCodigo,
            profissionais.FirstOrDefault(p => p.Id == a.ProfissionalId)?.Rotulo,
            JaChegou: a.ChegadaEm is not null,
            ComGuia: a.AtendimentoId is not null,
            Importado: a.ChaveImportacao is not null)).ToList();
    }

    /// <summary>
    /// O LANÇAMENTO SOBRE O HORÁRIO DO DIA (set/2026). O Novo atendimento é a porta que o
    /// balcão usa o dia inteiro, e até aqui ela SEMPRE criava um encaixe — mesmo quando o
    /// paciente já tinha horário marcado naquele dia. A semana da migração expôs o custo:
    /// 227 horários vieram do Smart Clinic, todos como "Consulta" (o sistema antigo não
    /// guardava mais que isso), e lançar por cima deixava DOIS cartões da mesma sessão —
    /// o encaixe concluído e o importado parado em "Aguardando" para sempre. Não era só
    /// ruído: a evolução importada depois do dia (sem vínculo com horário) é distribuída
    /// na ordem da hora marcada, e o horário parado, mais cedo, ficava com ela; a sessão
    /// de verdade continuava em "Sessões sem evolução".
    ///
    /// Este método é o gesto atômico do <see cref="LancarAvulsoAsync"/> aplicado a um
    /// horário que JÁ EXISTE: a modalidade escolhida na tela passa a valer para ele, o
    /// check-in é carimbado e o atendimento com as guias nasce pendurado NESTE horário —
    /// um SaveChanges, nenhum encaixe. Uma sessão, um horário.
    ///
    /// Nulo quer dizer "o chamador não sabe" (a regra da parcela 68): sem código de
    /// modalidade o horário fica com a que tem; sem profissional/sala, os do horário. A
    /// modalidade nova num horário que JÁ tem guias (chave "guia no agendamento") regera
    /// pelo MESMO caminho do Remarcar — duas regras de regeração divergiriam na primeira
    /// correção.
    /// </summary>
    public async Task<(Agendamento Agendamento, ResultadoLancamento Lancamento)> LancarNoHorarioAsync(
        int agendamentoId, string? observacoes, CancellationToken ct = default,
        string? modalidadeCodigo = null, string? especialidadeConsultaCodigo = null,
        TipoCodigo? primeiroCodigo = null, int? profissionalId = null, string? operador = null,
        int? salaId = null, bool concluirSessao = true)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");

        // As duas recusas dizem o que fazer. A tela só oferece horário em aberto, mas a
        // corrida entre as duas máquinas do balcão existe (parcela 86): a outra pode ter
        // concluído ou cancelado este horário depois de a tela ter lido.
        if (ag.Status == StatusAgendamento.Realizado)
            throw new InvalidOperationException(
                $"O horário das {ag.DataHora:HH:mm} já virou atendimento — provavelmente concluído na "
                + "outra máquina. Lançar de novo criaria OUTRO jogo de guias; confira na Fila ou na "
                + "lista de lançados do dia.");
        if (ag.Status is StatusAgendamento.Cancelado or StatusAgendamento.Faltou)
            throw new InvalidOperationException(
                $"O horário das {ag.DataHora:HH:mm} foi cancelado (ou marcado como falta). Reabra-o "
                + "pela agenda (Remarcar) ou lance como encaixe separado.");
        if (ag.Status == StatusAgendamento.Substituido)
            throw new InvalidOperationException(
                $"O horário das {ag.DataHora:HH:mm} já foi encerrado por uma sessão lançada por fora. "
                + "Lançar de novo criaria OUTRO jogo de guias; confira na lista de lançados do dia, "
                + "ou reabra o horário pela agenda (Remarcar) se a sessão é outra.");

        var novaModalidade = modalidadeCodigo is null ? ag.ModalidadePrevista : CatalogoModalidades.Base(modalidadeCodigo);
        var psicologia = await ConsultaDePsicologiaAsync(profissionalId ?? ag.ProfissionalId,
            novaModalidade, modalidadeCodigo ?? ag.ModalidadeCodigo,
            modalidadeCodigo is null ? ag.EspecialidadeConsultaCodigo : especialidadeConsultaCodigo, ct);
        if (psicologia) especialidadeConsultaCodigo = "Psicologia";

        var modalidadeAnterior = ag.ModalidadeCodigo;
        var especialidadeAnterior = ag.EspecialidadeConsultaCodigo;
        var mudouModalidade = false;
        if (modalidadeCodigo is not null)
        {
            var modalidade = CatalogoModalidades.Base(modalidadeCodigo);
            var ehConsulta = modalidade == ModalidadeAtendimento.Consulta;
            ag.ModalidadePrevista = modalidade;
            ag.ModalidadeCodigo = modalidadeCodigo;
            ag.EspecialidadeConsulta = ehConsulta
                ? CatalogoEspecialidades.BaseEnum(especialidadeConsultaCodigo) : null;
            ag.EspecialidadeConsultaCodigo = ehConsulta ? especialidadeConsultaCodigo : null;
            // Qual código sai primeiro anda com a modalidade (a tela só o oferece nas
            // duplas): vai junto, inclusive para limpar.
            ag.PrimeiroCodigo = primeiroCodigo;
            mudouModalidade = modalidadeCodigo != modalidadeAnterior
                              || (ehConsulta && especialidadeConsultaCodigo != especialidadeAnterior);
        }
        if (profissionalId is not null) ag.ProfissionalId = profissionalId;
        if (psicologia)
        {
            ag.EspecialidadeConsulta = null;
            ag.EspecialidadeConsultaCodigo = "Psicologia";
        }
        if (salaId is not null) ag.SalaId = salaId;
        if (!string.IsNullOrWhiteSpace(observacoes))
        {
            // A observação do horário ("Importado do Smart Clinic · Consulta", a nota do
            // balcão ao marcar) não se perde: a da tela entra abaixo dela.
            var junto = string.IsNullOrWhiteSpace(ag.Observacoes)
                ? observacoes.Trim()
                : $"{ag.Observacoes}\n{observacoes.Trim()}";
            ag.Observacoes = junto.Length <= 500 ? junto : junto[..500];
        }

        var agora = DateTime.Now;
        var chegouAgora = ag.ChegadaEm is null;
        // O paciente está no balcão: o check-in é o fato. Quem já fez check-in pela Fila
        // fica com a hora de lá.
        ag.ChegadaEm ??= agora;
        if (chegouAgora)
            await AuditarFilaAsync(ag, operador ?? "?", "FilaChegada",
                $"Check-in às {agora:HH:mm} — lançado pelo Novo atendimento sobre o horário de "
                + $"{ag.DataHora:dd/MM/yyyy HH:mm}", ct);
        if (mudouModalidade)
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
                Acao = "AgendamentoRemarcado",
                Detalhe = $"{ag.DataHora:dd/MM/yyyy HH:mm} — modalidade de "
                          + $"{CatalogoModalidades.Nome(modalidadeAnterior)} para "
                          + $"{CatalogoModalidades.Nome(ag.ModalidadeCodigo)} ao lançar pelo Novo atendimento",
                PacienteId = ag.PacienteId
            }, ct);

        // GUIA NO AGENDAMENTO: o horário marcado com a chave ligada já tem atendimento e
        // guias; a modalidade nova regera (ou recusa, se algo já saiu da clínica) pelo
        // mesmo caminho do Remarcar. Sem atendimento, não faz nada — e o núcleo abaixo o
        // cria pela modalidade que acabou de valer.
        var avisosGuia = await _atendimentos.AjustarAoRemarcarAsync(
            ag, DateOnly.FromDateTime(ag.DataHora), mudouModalidade, operador, ct);

        var lancamento = await ConfirmarNucleoAsync(ag, operador, ct, confirmarPresenca: concluirSessao);
        if (avisosGuia.Count > 0)
            lancamento = lancamento with { Avisos = avisosGuia.Concat(lancamento.Avisos).ToList() };
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

    /// <summary>
    /// A TERCEIRA RESPOSTA da conciliação da agenda (set/2026): a sessão aconteceu e já foi
    /// lançada POR FORA deste horário — encerra o horário apontando para ela.
    ///
    /// Até aqui esta resposta não tinha saída: a tela dizia qual era o atendimento e o botão
    /// de lançar ficava apagado, porque lançar criaria um segundo jogo de guias, e nenhum
    /// status servia (cancelado inflaria o indicador de cancelamento com sessões que
    /// aconteceram; falta culparia o paciente). O horário ficava "Aguardando" para sempre:
    /// inflando a ocupação, entrando no "Meu dia" do médico e capturando a evolução
    /// importada da sessão de verdade.
    ///
    /// As recusas dizem o que fazer, e são as que separam "amarrar" de "amarrar errado":
    /// a sessão tem de ser do MESMO paciente, do MESMO dia e não pode estar estornada —
    /// amarrar ao atendimento de outra pessoa não estoura nada e esconde uma sessão.
    ///
    /// GUIA NO AGENDAMENTO: se o horário tinha atendimento próprio (chave ligada), as guias
    /// abertas dele são suspensas pelo MESMO caminho da falta — elas seriam o segundo jogo.
    /// Reabrir pelo Remarcar as devolve e solta o vínculo. Tudo no mesmo SaveChanges.
    /// </summary>
    public async Task<IReadOnlyList<string>> SubstituirPorSessaoAsync(
        int agendamentoId, int atendimentoId, string? operador = null, CancellationToken ct = default)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException($"Agendamento {agendamentoId} não encontrado.");

        if (ag.Status != StatusAgendamento.Agendado)
            throw new InvalidOperationException(
                $"O horário das {ag.DataHora:dd/MM/yyyy HH:mm} não está em aberto "
                + $"({StatusDaFila.Palavra(ag.Status, ag.Etapa).ToLowerInvariant()}); só um horário "
                + "em aberto pode ser encerrado por uma sessão lançada por fora.");

        var sessao = await _repo.ObterAtendimentoAsync(atendimentoId, ct)
            ?? throw new InvalidOperationException($"Atendimento {atendimentoId} não encontrado.");

        if (sessao.PacienteId != ag.PacienteId)
            throw new InvalidOperationException(
                "A sessão informada é de OUTRO paciente — amarrá-la a este horário esconderia "
                + "uma sessão na ficha errada.");
        if (sessao.Data != DateOnly.FromDateTime(ag.DataHora))
            throw new InvalidOperationException(
                $"A sessão nº {sessao.Numero ?? "#" + sessao.Id} é de {sessao.Data:dd/MM/yyyy}, e o "
                + $"horário é de {ag.DataHora:dd/MM/yyyy}. Só a sessão do MESMO dia encerra o horário; "
                + "se o paciente veio noutro dia, marque falta neste horário.");
        if (sessao.Estornado)
            throw new InvalidOperationException(
                $"A sessão nº {sessao.Numero ?? "#" + sessao.Id} foi ESTORNADA — ela não aconteceu "
                + "por este lançamento. Lance pelo horário, ou marque falta.");
        if (ag.AtendimentoId == sessao.Id)
            throw new InvalidOperationException(
                "Esta sessão já é a DESTE horário — o que falta aqui é concluir a presença, não substituir.");

        ag.Status = StatusAgendamento.Substituido;
        ag.AtendimentoSubstitutoId = sessao.Id;

        // As guias do atendimento PRÓPRIO do horário (chave "guia no agendamento"), se
        // houver: suspensas como na falta, no mesmo commit.
        var avisos = await _atendimentos.RefletirStatusDoHorarioAsync(ag, operador, ct);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AgendamentoSubstituido",
            Detalhe = $"{ag.DataHora:dd/MM/yyyy HH:mm} — encerrado pela sessão nº "
                      + $"{sessao.Numero ?? "#" + sessao.Id} lançada por fora (conciliação da agenda)",
            PacienteId = ag.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return avisos;
    }
}
