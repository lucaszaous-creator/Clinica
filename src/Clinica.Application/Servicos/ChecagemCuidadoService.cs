using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Um cuidado do plano, com o que foi registrado dele NAQUELE dia.</summary>
/// <param name="Checagens">Só as VIGENTES, em ordem de hora. A retificada fica na base.</param>
public sealed record CuidadoDoDia(
    int CuidadoId, string Redacao, bool SeNecessario,
    IReadOnlyList<ChecagemCuidado> Checagens)
{
    public bool Realizado => Checagens.Any(c => c.Realizado);

    /// <summary>
    /// Cobra uma palavra da técnica. O <b>se necessário</b> fica de fora, e é a única
    /// sutileza da conta — a mesma do SOS da folha de infusão: cuidado condicional sem
    /// registro não é trabalho atrasado, é a condição que não aconteceu. Contá-lo deixaria
    /// todo plano com um SOS eternamente pendente, e o contador passaria a apontar para
    /// nada. Na LINHA ele continua aparecendo sem registro, porque ali a informação é sobre
    /// aquele cuidado e não sobre o que resta do dia.
    /// </summary>
    public bool Pendente => !SeNecessario && Checagens.Count == 0;
}

/// <summary>O plano de cuidados de um paciente visto por um DIA.</summary>
public sealed record PlanoDeCuidadosDoDia(
    DateOnly Data, int EvolucaoId, DateOnly PrescritoEm, string PrescritoPor,
    IReadOnlyList<CuidadoDoDia> Cuidados)
{
    public int Pendentes => Cuidados.Count(c => c.Pendente);

    public int Registrados => Cuidados.Count(c => c.Checagens.Count > 0);
}

/// <summary>
/// A EXECUÇÃO DO CUIDADO DE ENFERMAGEM — a etapa 4 da COFEN 358/2009 (parcela 76).
///
/// O buraco
/// --------
/// A Resolução COFEN 358/2009 torna o Processo de Enfermagem obrigatório em CINCO etapas.
/// O sistema cobria histórico, diagnóstico e resultado esperado, e a quarta —
/// implementação — existia só como TEXTO: a enfermeira escrevia "curativo a cada 24h" e
/// <b>nada registrava que foi feito</b>. Implementação sem registro é intenção; e cuidado
/// que não se registra é, para qualquer fiscalização, cuidado que não aconteceu.
///
/// Tudo o que este serviço faz já foi pago caro na parcela 42
/// ----------------------------------------------------------
/// A hora é <b>INFORMADA</b> e o relógio vai <b>ao lado</b>; hora futura é <b>recusada</b>;
/// não realizado <b>exige justificativa</b>; nada se apaga, <b>retifica-se</b>; e quem checa
/// é quem fez <b>login</b>, com o COREN copiado no ato. O relógio é INJETADO pela mesma
/// razão de lá: regra de segurança que não dá para testar apodrece sem ninguém notar.
///
/// ⚠️ A regra que NÃO se copia
/// ---------------------------
/// Na folha de infusão, "item já checado não se edita" — o item é de administração única.
/// Aqui o cuidado tem FREQUÊNCIA e é executado de novo a cada turno: copiar aquela guarda
/// impediria a segunda troca de curativo do dia. Uma execução por linha, quantas houver.
/// </summary>
public sealed class ChecagemCuidadoService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Ver <see cref="ChecagemPrescricaoService"/>: existe para a recusa de hora
    /// futura poder ser testada sem virar loteria perto da meia-noite.</summary>
    private readonly Func<DateTime> _agora;

    public ChecagemCuidadoService(IClinicaRepositorio repo, Func<DateTime>? agora = null)
    {
        _repo = repo;
        _agora = agora ?? (() => DateTime.Now);
    }

    // ==================== Leitura ====================

    /// <summary>
    /// O plano de cuidados VIGENTE do paciente, com o que foi registrado no dia pedido.
    ///
    /// ⚠️ "Vigente" é a evolução de enfermagem mais recente que TEM cuidados e não foi
    /// cancelada nem retificada — resolvido por <see cref="EvolucaoEnfermagem.Vigentes"/>,
    /// que é a mesma função que a curva de pressão e os sinais vitais do atendimento usam.
    /// Duas definições de "o registro que vale" divergem na primeira correção.
    ///
    /// Devolve nulo quando o paciente não tem plano nenhum — e nulo é diferente de plano
    /// vazio, como em todo indicador deste projeto.
    /// </summary>
    public async Task<PlanoDeCuidadosDoDia?> PlanoDoDiaAsync(
        int pacienteId, DateOnly data, CancellationToken ct = default)
    {
        var evolucoes = await _repo.EvolucoesEnfermagemDoPacienteAsync(pacienteId, 60, ct);

        var vigente = EvolucaoEnfermagem.Vigentes(evolucoes)
            .FirstOrDefault(e => e.Cuidados.Count > 0);

        if (vigente is null) return null;

        var ids = vigente.Cuidados.Select(c => c.Id).ToList();
        var checagens = await _repo.ChecagensDosCuidadosAsync(ids, ct);

        var doDia = ChecagemCuidado.Vigentes(checagens)
            .Where(c => c.Data == data)
            .ToLookup(c => c.CuidadoEnfermagemId);

        var linhas = vigente.Cuidados
            .OrderBy(c => c.Ordem).ThenBy(c => c.Id)
            .Select(c => new CuidadoDoDia(
                c.Id, c.Redacao, c.SeNecessario,
                doDia[c.Id].OrderBy(x => x.HoraRealizacao).ThenBy(x => x.Id).ToList()))
            .ToList();

        return new PlanoDeCuidadosDoDia(
            data, vigente.Id, vigente.Data, vigente.AutorNome, linhas);
    }

    /// <summary>Todas as execuções de um cuidado, para a folha e para a conferência.</summary>
    public async Task<IReadOnlyList<ChecagemCuidado>> HistoricoDoCuidadoAsync(
        int cuidadoId, CancellationToken ct = default)
        => (await _repo.ChecagensDosCuidadosAsync([cuidadoId], ct)).ToList();

    // ==================== Escrita ====================

    public async Task<ChecagemCuidado> ChecarAsync(
        int cuidadoId, SituacaoChecagem situacao, DateOnly data, TimeOnly hora,
        IdentificacaoExecutante executante, string? justificativa = null,
        string? observacao = null, CancellationToken ct = default)
    {
        executante.Exigir("registrar a execução do cuidado");

        var cuidado = await _repo.ObterCuidadoEnfermagemAsync(cuidadoId, ct)
            ?? throw new InvalidOperationException("Cuidado não encontrado.");

        Conferir(cuidado, situacao, data, hora, justificativa);

        var checagem = Montar(cuidadoId, situacao, data, hora, executante, justificativa, observacao);

        await _repo.AdicionarChecagemCuidadoAsync(checagem, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = executante.Nome,
            Acao = situacao == SituacaoChecagem.Realizado
                ? "CuidadoEnfermagemRealizado"
                : "CuidadoEnfermagemNaoRealizado",
            PacienteId = cuidado.Evolucao?.PacienteId,
            Detalhe = Descrever(cuidado, checagem)
        }, ct);

        // Auditoria no MESMO SaveChanges do ato: execução que possa acontecer sem a linha
        // correspondente é execução sem trilha.
        await _repo.SalvarAsync(ct);
        return checagem;
    }

    /// <summary>
    /// Corrige uma execução. A anterior NÃO é apagada — fica na base, apontada por esta,
    /// e a folha mostra as duas. Apagar e regravar é exatamente o gesto que a auditoria
    /// de enfermagem procura.
    /// </summary>
    public async Task<ChecagemCuidado> RetificarAsync(
        int checagemId, SituacaoChecagem situacao, DateOnly data, TimeOnly hora,
        IdentificacaoExecutante executante, string motivo, string? justificativa = null,
        string? observacao = null, CancellationToken ct = default)
    {
        executante.Exigir("retificar a execução do cuidado");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Diga por que o registro anterior estava errado. Ele não é apagado — fica "
                + "na folha, marcado, com esta explicação ao lado.");

        var anterior = await _repo.ObterChecagemCuidadoAsync(checagemId, ct)
            ?? throw new InvalidOperationException("Registro de execução não encontrado.");

        var cuidado = anterior.Cuidado
            ?? throw new InvalidOperationException("Registro sem o cuidado correspondente.");

        // Retificar uma já retificada bifurcaria a cadeia, e a folha passaria a ter duas
        // versões "atuais" do mesmo fato. Quem corrige a correção retifica a ÚLTIMA.
        var todas = await _repo.ChecagensDosCuidadosAsync([cuidado.Id], ct);
        if (todas.Any(c => c.RetificaChecagemId == checagemId))
            throw new InvalidOperationException(
                "Este registro já foi corrigido por outro. Retifique o mais recente — "
                + "corrigir uma correção antiga deixaria duas versões válidas do mesmo fato.");

        Conferir(cuidado, situacao, data, hora, justificativa);

        var checagem = Montar(cuidado.Id, situacao, data, hora, executante, justificativa, observacao);
        checagem.RetificaChecagemId = checagemId;
        checagem.MotivoRetificacao = motivo.Trim();

        await _repo.AdicionarChecagemCuidadoAsync(checagem, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = executante.Nome,
            Acao = "CuidadoEnfermagemRetificado",
            PacienteId = cuidado.Evolucao?.PacienteId,
            Detalhe = $"{Descrever(cuidado, checagem)} — corrige o registro de "
                    + $"{anterior.HoraRealizacao:HH\\:mm} ({anterior.Situacao}). "
                    + $"Motivo: {checagem.MotivoRetificacao}"
        }, ct);

        await _repo.SalvarAsync(ct);
        return checagem;
    }

    // ==================== As recusas ====================

    private void Conferir(
        CuidadoEnfermagem cuidado, SituacaoChecagem situacao, DateOnly data, TimeOnly hora,
        string? justificativa)
    {
        if (cuidado.Evolucao is { Cancelada: true })
            throw new InvalidOperationException(
                "O registro de enfermagem que prescreveu este cuidado foi cancelado. "
                + "Escreva a evolução nova antes de registrar a execução.");

        // A rodela do papel: circular o horário sem dizer por quê é a mesma coisa que não
        // registrar nada. É a recusa que dá valor ao "não realizado".
        if (situacao == SituacaoChecagem.NaoRealizado && string.IsNullOrWhiteSpace(justificativa))
            throw new InvalidOperationException(
                "Diga por que o cuidado não foi realizado (paciente recusou, ausente, "
                + "material em falta, condição não ocorreu). Sem isso a linha só diz que "
                + "alguém abriu a folha.");

        // ⚠️ Regra de SEGURANÇA, não de formulário: registrar adiantado é o hábito que faz
        // aparecer como executado um cuidado num paciente que saiu antes de recebê-lo.
        var momento = data.ToDateTime(hora);
        if (momento > _agora())
            throw new InvalidOperationException(
                $"O horário informado ({data:dd/MM} {hora:HH\\:mm}) está no futuro. Registre "
                + "o cuidado DEPOIS de executá-lo — a folha diz o que aconteceu, não o que "
                + "vai acontecer.");

        // O cuidado não pode ter sido executado antes de ter sido prescrito.
        if (cuidado.Evolucao is { } evolucao && data < evolucao.Data)
            throw new InvalidOperationException(
                $"Este cuidado foi prescrito em {evolucao.Data:dd/MM/yyyy} e não pode ter "
                + $"sido executado em {data:dd/MM/yyyy}.");
    }

    private ChecagemCuidado Montar(
        int cuidadoId, SituacaoChecagem situacao, DateOnly data, TimeOnly hora,
        IdentificacaoExecutante executante, string? justificativa, string? observacao)
        => new()
        {
            CuidadoEnfermagemId = cuidadoId,
            Data = data,
            HoraRealizacao = hora,
            Situacao = situacao,
            Justificativa = string.IsNullOrWhiteSpace(justificativa) ? null : justificativa.Trim(),
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            ExecutanteUsuarioId = executante.UsuarioId,
            ExecutanteNome = executante.Nome,
            ExecutanteConselho = executante.Conselho,
            // O relógio ao lado da hora informada — e é o INJETADO, pela razão da parcela 76:
            // o carimbo que a auditoria compara não pode escapar do relógio que a testa.
            RegistradoEm = _agora()
        };

    private static string Descrever(CuidadoEnfermagem cuidado, ChecagemCuidado checagem)
        => $"{cuidado.Redacao} — {checagem.Linha}"
           + (string.IsNullOrWhiteSpace(checagem.Justificativa)
               ? string.Empty
               : $" ({checagem.Justificativa})");
}
