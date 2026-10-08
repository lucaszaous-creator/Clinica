using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;
using System.Text;

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
        // Duplicatas antigas continuam no histórico. Só a apresentação idêntica se
        // repete menos; CID ou observações diferentes continuam visíveis.
        return todos.Where(p => p.EhAlertaDeAtendimento)
            .DistinctBy(p => p.Natureza == NaturezaProblema.Alergia
                ? $"alergia:{NormalizarAlergia(p.Descricao)}\u001f{NormalizarAlergia(p.Cid)}\u001f{NormalizarAlergia(p.Observacoes)}"
                : $"problema:{p.Id}")
            .ToList();
    }

    /// <summary>Uma única entrada de alergias, mesmo quando há relatos legados repetidos.</summary>
    public static string? ResumirAlergias(IEnumerable<ProblemaPaciente> problemas)
    {
        var nomes = AgruparAlergias(problemas).Select(g => LimparEspacos(g.First().Descricao)).ToArray();
        return nomes.Length == 0 ? null : $"ALERGIA — {string.Join("; ", nomes)}";
    }

    /// <summary>Preserva as observações e CIDs distintos para consulta na própria linha.</summary>
    public static string? DetalharAlergias(IEnumerable<ProblemaPaciente> problemas)
    {
        var detalhes = AgruparAlergias(problemas).Select(g =>
        {
            var cids = g.Select(p => LimparEspacos(p.Cid)).Where(c => c.Length > 0)
                .DistinctBy(NormalizarAlergia).Select(c => c.ToUpperInvariant()).ToArray();
            var observacoes = g.SelectMany(p => (p.Observacoes ?? "").Split('\n'))
                .Select(LimparEspacos).Where(o => o.Length > 0).DistinctBy(NormalizarAlergia).ToArray();
            return LimparEspacos(g.First().Descricao)
                + (cids.Length == 0 ? "" : $" (CID: {string.Join(", ", cids)})")
                + (observacoes.Length == 0 ? "" : $": {string.Join("; ", observacoes)}");
        }).ToArray();
        return detalhes.Length == 0 ? null : string.Join(Environment.NewLine, detalhes);
    }

    private static IEnumerable<IGrouping<string, ProblemaPaciente>> AgruparAlergias(
        IEnumerable<ProblemaPaciente> problemas)
        => problemas.Where(p => p.Natureza == NaturezaProblema.Alergia && p.EhAlertaDeAtendimento)
            .GroupBy(p => NormalizarAlergia(p.Descricao));

    private static string LimparEspacos(string? texto)
        => string.Join(" ", (texto ?? "").Normalize(NormalizationForm.FormC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Registra ou atualiza uma linha da lista.</summary>
    public Task<ProblemaPaciente> SalvarAsync(
        ProblemaPaciente dados, string? operador = null, CancellationToken ct = default)
        => dados.Natureza == NaturezaProblema.Alergia
            ? _repo.ExecutarRegistroAlergiaAtomicoAsync(dados.PacienteId,
                () => SalvarConteudoAsync(dados, operador, ct), ct)
            : SalvarConteudoAsync(dados, operador, ct);

    private async Task<ProblemaPaciente> SalvarConteudoAsync(
        ProblemaPaciente dados, string? operador, CancellationToken ct)
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

        if (dados.Id == 0 && dados.Natureza == NaturezaProblema.Alergia)
        {
            var alergia = await RegistrarAlergiaSemSalvarAsync(
                dados, operador, "Lista de problemas", mesclarObservacoes: true, ct: ct);
            await _repo.SalvarAsync(ct);
            return alergia;
        }

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
            // A trava precisa ser a do dono real da linha, nunca a de outro paciente
            // informado por um formulário antigo ou incompatível.
            if (dados.Natureza == NaturezaProblema.Alergia && destino.PacienteId != dados.PacienteId)
                throw new InvalidOperationException("Esta alergia pertence a outro paciente. Reabra a ficha correta.");
            if (dados.Natureza == NaturezaProblema.Alergia
                && destino.Situacao != SituacaoProblema.Descartado
                && (destino.Natureza != NaturezaProblema.Alergia
                    || NormalizarAlergia(destino.Descricao) != NormalizarAlergia(dados.Descricao)))
            {
                var problemas = await _repo.ProblemasDoPacienteAsync(destino.PacienteId, ct: ct);
                if (problemas.Any(p => p.Id != destino.Id
                    && p.Natureza == NaturezaProblema.Alergia
                    && p.Situacao != SituacaoProblema.Descartado
                    && NormalizarAlergia(p.Descricao) == NormalizarAlergia(dados.Descricao)))
                    throw new InvalidOperationException(
                        "Já existe uma alergia com esta descrição para o paciente. Abra a linha existente para atualizá-la.");
            }
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

        return problema.Natureza == NaturezaProblema.Alergia
            ? await _repo.ExecutarRegistroAlergiaAtomicoAsync(problema.PacienteId,
                () => ReabrirConteudoAsync(problema, operador, ct), ct)
            : await ReabrirConteudoAsync(problema, operador, ct);
    }

    private async Task<ProblemaPaciente> ReabrirConteudoAsync(
        ProblemaPaciente problema, string? operador, CancellationToken ct)
    {

        if (problema.Situacao == SituacaoProblema.Ativo)
            throw new InvalidOperationException("Este problema já está ativo.");

        if (problema.Natureza == NaturezaProblema.Alergia)
        {
            var problemas = await _repo.ProblemasDoPacienteAsync(problema.PacienteId, ct: ct);
            if (problemas.Any(p => p.Id != problema.Id
                && p.Natureza == NaturezaProblema.Alergia
                && p.Situacao != SituacaoProblema.Descartado
                && NormalizarAlergia(p.Descricao) == NormalizarAlergia(problema.Descricao)))
                throw new InvalidOperationException(
                    "Já existe uma alergia equivalente ativa ou resolvida para este paciente. "
                    + "Use a linha existente; este registro permanecerá preservado no histórico.");
        }

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

    /// <summary>
    /// Reutiliza a alergia deste paciente sem salvar a unidade de trabalho do chamador.
    /// A observação da enfermagem e sua auditoria continuam na mesma transação.
    /// Descartadas não são reativadas: a decisão clínica anterior fica preservada.
    /// O chamador deve usar ExecutarRegistroAlergiaAtomicoAsync e salvar a unidade
    /// de trabalho dentro dessa mesma operação, junto dos demais registros do relato.
    /// </summary>
    public async Task<ProblemaPaciente> RegistrarAlergiaSemSalvarAsync(
        ProblemaPaciente dados, string? operador, string origem,
        bool mesclarObservacoes = false, CancellationToken ct = default)
    {
        var chave = NormalizarAlergia(dados.Descricao);
        var problemas = await _repo.ProblemasDoPacienteAsync(dados.PacienteId, ct: ct);
        var existente = problemas.FirstOrDefault(p =>
            p.Natureza == NaturezaProblema.Alergia
            && p.Situacao != SituacaoProblema.Descartado
            && NormalizarAlergia(p.Descricao) == chave);

        if (existente is not null)
        {
            // Nunca substitui a procedência, o início ou a descrição pelo relato da
            // sessão atual. A alergia pertence ao paciente, não a cada atendimento.
            if (mesclarObservacoes)
            {
                if (!string.IsNullOrWhiteSpace(dados.Cid)
                    && !string.IsNullOrWhiteSpace(existente.Cid)
                    && NormalizarAlergia(dados.Cid) != NormalizarAlergia(existente.Cid))
                    throw new InvalidOperationException(
                        "Esta alergia já está cadastrada com outro CID. Abra a alergia existente para revisar o código.");

                var observacoes = Limpar(dados.Observacoes);
                var combinadas = existente.Observacoes;
                if (observacoes is not null
                    && NormalizarAlergia(existente.Observacoes) != NormalizarAlergia(observacoes)
                    && !(existente.Observacoes ?? "").Split('\n')
                        .Any(linha => NormalizarAlergia(linha) == NormalizarAlergia(observacoes)))
                    combinadas = string.IsNullOrWhiteSpace(combinadas)
                        ? observacoes : $"{combinadas}\n{observacoes}";
                if (combinadas?.Length > 2000)
                    throw new InvalidOperationException(
                        "Esta alergia já está cadastrada. Revise suas observações na linha existente (limite de 2000 caracteres).");

                var editavel = await _repo.ObterProblemaAsync(existente.Id, ct)
                    ?? throw new InvalidOperationException("Alergia não encontrada.");
                editavel.Observacoes = combinadas;
                editavel.Cid ??= Limpar(dados.Cid)?.ToUpperInvariant();
                editavel.AtualizadoEm = DateTime.Now;
                editavel.AtualizadoPor = operador;
                existente = editavel;
            }

            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
                Acao = "AlergiaRelatadaNovamente",
                PacienteId = dados.PacienteId,
                Detalhe = $"Alergia #{existente.Id}: {existente.Descricao} · {origem}"
            }, ct);
            return existente;
        }

        dados.Descricao = dados.Descricao.Trim();
        dados.Cid = Limpar(dados.Cid)?.ToUpperInvariant();
        dados.Observacoes = Limpar(dados.Observacoes);
        dados.Situacao = SituacaoProblema.Ativo;
        dados.Fim = null;
        dados.MotivoDescarte = null;
        dados.CriadoPor = operador;
        await _repo.AdicionarProblemaAsync(dados, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ProblemaRegistrado",
            PacienteId = dados.PacienteId,
            Detalhe = $"Alergia: {dados.Descricao} · {origem}"
        }, ct);
        return dados;
    }

    // Não aproxima nomes, não remove palavras nem troca marcas por princípios ativos.
    public static string NormalizarAlergia(string? texto)
        => LimparEspacos(texto).ToUpperInvariant();
}
