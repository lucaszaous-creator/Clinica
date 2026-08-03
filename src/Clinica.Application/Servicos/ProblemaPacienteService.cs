using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// A LISTA DE PROBLEMAS do paciente — diagnósticos, alergias, antecedentes e medicação de
/// uso contínuo (parcela 37).
///
/// Por que existe
/// --------------
/// O CID morava só dentro de <see cref="DocumentoClinico"/>: um campo por papel emitido,
/// nunca o diagnóstico da pessoa. Na prática o profissional redigitava "M54.5" a cada
/// atestado, ninguém conseguia responder "o que este paciente tem?" sem ler o prontuário
/// inteiro, e a alergia — a informação que muda conduta em dez segundos — ficava enterrada
/// em texto livre no meio de uma evolução de dois anos atrás. É fundação de prontuário:
/// a lista alimenta os documentos, e não o contrário.
///
/// As regras
/// ---------
/// - <b>Não se apaga: muda-se a situação.</b> Resolvido e Descartado continuam na base,
///   como a não conformidade do faturamento e o documento clínico cancelado. Linha que
///   some leva junto a prova de que a conduta da época era razoável.
/// - <b>Descartar exige motivo escrito.</b> É a única recusa deste serviço, e vale pela
///   mesma razão da justificativa do fechamento de caixa: desdizer sem dizer por quê deixa
///   o próximo leitor sem saber se a linha era falsa ou se o paciente melhorou.
/// - <b>Alergia alerta mesmo resolvida</b> (só o descarte a cala). "Resolvida" numa
///   alergia é quase sempre "não reagiu da última vez", e o dia em que reagir é o dia em
///   que o aviso teria valido.
/// - <b>O CID é opcional.</b> Exigi-lo faria o fisioterapeuta e o acupunturista pararem de
///   usar a lista — e uma lista de problemas pela metade é pior que nenhuma, porque seria
///   lida como completa.
/// </summary>
public sealed class ProblemaPacienteService
{
    private readonly IClinicaRepositorio _repo;

    public ProblemaPacienteService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>A lista do paciente: ativos primeiro, mais recente na frente.</summary>
    public Task<IReadOnlyList<ProblemaPaciente>> DoPacienteAsync(
        int pacienteId, bool somenteAtivos = false, CancellationToken ct = default)
        => _repo.ProblemasDoPacienteAsync(pacienteId, somenteAtivos, ct);

    public Task<ProblemaPaciente?> ObterAsync(int problemaId, CancellationToken ct = default)
        => _repo.ObterProblemaAsync(problemaId, ct);

    /// <summary>
    /// O que tem de aparecer em destaque na hora de atender: alergia e medicação contínua,
    /// em qualquer situação que não seja descartada.
    ///
    /// Sai separado da lista inteira de propósito. A tela de atendimento não tem espaço
    /// para quinze linhas de histórico, e um alerta que divide o lugar com o histórico é um
    /// alerta que ninguém lê — a mesma razão pela qual o <c>ElegibilidadeService</c> não
    /// dispara para todo mundo.
    /// </summary>
    public async Task<IReadOnlyList<ProblemaPaciente>> AlertasAsync(
        int pacienteId, CancellationToken ct = default)
    {
        var todos = await _repo.ProblemasDoPacienteAsync(pacienteId, somenteAtivos: false, ct);
        return todos.Where(p => p.EhAlertaDeAtendimento).ToList();
    }

    /// <summary>Registra ou atualiza uma linha da lista.</summary>
    public async Task<ProblemaPaciente> SalvarAsync(
        ProblemaPaciente dados, string? operador = null, CancellationToken ct = default)
    {
        if (await _repo.ObterPacienteAsync(dados.PacienteId, ct) is null)
            throw new InvalidOperationException("Paciente não encontrado.");

        if (string.IsNullOrWhiteSpace(dados.Descricao))
            throw new InvalidOperationException(
                "Descreva o problema. O CID é opcional; a descrição, não — é ela que o "
                + "próximo profissional lê.");

        if (dados.Inicio is { } inicio && dados.Fim is { } fim && fim < inicio)
            throw new InvalidOperationException("O fim não pode ser anterior ao início.");

        if (dados.Inicio is { } i && i > DateOnly.FromDateTime(DateTime.Today))
            throw new InvalidOperationException("O início não pode ser uma data futura.");

        ProblemaPaciente destino;
        var novo = dados.Id == 0;
        if (novo)
        {
            destino = new ProblemaPaciente
            {
                PacienteId = dados.PacienteId,
                CriadoEm = DateTime.Now,
                CriadoPor = operador
            };
            await _repo.AdicionarProblemaAsync(destino, ct);
        }
        else
        {
            destino = await _repo.ObterProblemaAsync(dados.Id, ct)
                ?? throw new InvalidOperationException("Problema não encontrado.");
            destino.AtualizadoEm = DateTime.Now;
            destino.AtualizadoPor = operador;
        }

        destino.ProfissionalId = dados.ProfissionalId;
        destino.EvolucaoId = dados.EvolucaoId;
        destino.Natureza = dados.Natureza;
        destino.Descricao = dados.Descricao.Trim();
        destino.Cid = Limpar(dados.Cid)?.ToUpperInvariant();
        destino.Inicio = dados.Inicio;
        destino.Observacoes = Limpar(dados.Observacoes);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = novo ? "ProblemaRegistrado" : "ProblemaAlterado",
            Detalhe = $"{destino.Natureza}: {destino.Rotulo}",
            PacienteId = destino.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    /// <summary>
    /// Encerra o problema. O fim é INFORMADO, nunca <c>Today</c>: quem registra na quarta
    /// o que se resolveu no sábado não pode ser obrigado a datar errado — é a mesma regra
    /// da data do crédito na conciliação de recebíveis.
    /// </summary>
    public async Task<ProblemaPaciente> ResolverAsync(
        int problemaId, DateOnly? fim = null, string? operador = null,
        CancellationToken ct = default)
    {
        var problema = await _repo.ObterProblemaAsync(problemaId, ct)
            ?? throw new InvalidOperationException("Problema não encontrado.");

        if (problema.Situacao == SituacaoProblema.Descartado)
            throw new InvalidOperationException(
                "Este problema foi descartado. Reabra antes de marcá-lo como resolvido.");

        var data = fim ?? DateOnly.FromDateTime(DateTime.Today);
        if (problema.Inicio is { } inicio && data < inicio)
            throw new InvalidOperationException("O fim não pode ser anterior ao início.");

        problema.Situacao = SituacaoProblema.Resolvido;
        problema.Fim = data;
        problema.AtualizadoEm = DateTime.Now;
        problema.AtualizadoPor = operador;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ProblemaResolvido",
            Detalhe = $"{problema.Rotulo} — encerrado em {data:dd/MM/yyyy}",
            PacienteId = problema.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return problema;
    }

    /// <summary>
    /// Desdiz a linha (foi registrada por engano). Não apaga — ela esteve no prontuário, e
    /// conduta pode ter sido tomada com base nela.
    /// </summary>
    public async Task<ProblemaPaciente> DescartarAsync(
        int problemaId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Descreva por que a linha está sendo descartada. Sem o motivo, quem ler "
                + "depois não sabe se ela era falsa ou se o paciente melhorou.");

        var problema = await _repo.ObterProblemaAsync(problemaId, ct)
            ?? throw new InvalidOperationException("Problema não encontrado.");

        problema.Situacao = SituacaoProblema.Descartado;
        problema.MotivoDescarte = motivo.Trim();
        problema.AtualizadoEm = DateTime.Now;
        problema.AtualizadoPor = operador;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ProblemaDescartado",
            Detalhe = $"{problema.Rotulo} — {problema.MotivoDescarte}",
            PacienteId = problema.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return problema;
    }

    /// <summary>
    /// Volta o problema para ativo. Limpa o fim e o motivo do descarte — eles descreviam um
    /// encerramento que deixou de valer, e mantê-los faria a linha dizer "ativo desde
    /// sempre, encerrado em março".
    /// </summary>
    public async Task<ProblemaPaciente> ReabrirAsync(
        int problemaId, string? operador = null, CancellationToken ct = default)
    {
        var problema = await _repo.ObterProblemaAsync(problemaId, ct)
            ?? throw new InvalidOperationException("Problema não encontrado.");

        if (problema.Situacao == SituacaoProblema.Ativo)
            throw new InvalidOperationException("Este problema já está ativo.");

        problema.Situacao = SituacaoProblema.Ativo;
        problema.Fim = null;
        problema.MotivoDescarte = null;
        problema.AtualizadoEm = DateTime.Now;
        problema.AtualizadoPor = operador;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ProblemaReaberto",
            Detalhe = problema.Rotulo,
            PacienteId = problema.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return problema;
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
