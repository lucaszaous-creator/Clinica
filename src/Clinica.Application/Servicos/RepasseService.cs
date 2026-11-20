using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O que um profissional produziu no período e quanto disso é repasse dele.</summary>
public sealed record RepasseCalculado(
    int ProfissionalId,
    string Profissional,
    int Atendimentos,
    decimal ReceitaVinculada,
    decimal Valor,
    string RegraDescricao,
    bool TemRegra,
    bool JaApurado)
{
    /// <summary>Sem regra cadastrada não há repasse a pagar — e a tela precisa dizer isso.</summary>
    public string Situacao => !TemRegra
        ? "Sem regra de repasse cadastrada"
        : JaApurado
            ? "Já apurado neste período"
            : "A apurar";
}

/// <summary>
/// Repasse por profissional — a metade que faltava da feature 09.
///
/// Duas coisas que não são óbvias:
///
/// 1. <b>Quem atendeu vem do AGENDAMENTO.</b> O atendimento não guarda profissional (e
///    não vai guardar: é entidade do faturamento congelado). O agendamento guarda os
///    dois — profissional e o atendimento que ele gerou —, e é por essa ponte que a
///    produção de cada um é apurada.
/// 2. <b>A receita considerada é a que ENTROU.</b> Repasse sobre o que foi faturado e
///    não recebido faria a clínica pagar por dinheiro que não tem. Só entram lançamentos
///    de entrada não cancelados ligados àqueles atendimentos.
/// </summary>
public sealed class RepasseService
{
    private readonly IClinicaRepositorio _repo;

    public RepasseService(IClinicaRepositorio repo) => _repo = repo;

    // ==================== Regras ====================

    public Task<IReadOnlyList<RegraRepasse>> RegrasAsync(
        int? profissionalId = null, CancellationToken ct = default)
        => _repo.RegrasRepasseAsync(profissionalId, ct);

    public async Task<RegraRepasse> SalvarRegraAsync(
        RegraRepasse dados, string? operador = null, CancellationToken ct = default)
    {
        if (await _repo.ObterProfissionalAsync(dados.ProfissionalId, ct) is null)
            throw new InvalidOperationException("Profissional não encontrado.");

        if (dados.Base == BaseRepasse.PercentualDaReceita)
        {
            if (dados.Percentual is not { } percentual || percentual <= 0 || percentual > 100)
                throw new InvalidOperationException("O percentual do repasse vai de 0 a 100.");
        }
        else if (dados.ValorPorAtendimento is not { } valor || valor <= 0)
        {
            throw new InvalidOperationException("Informe o valor por atendimento.");
        }

        if (dados.VigenteDe is { } de && dados.VigenteAte is { } ate && ate < de)
            throw new InvalidOperationException("O fim da vigência é anterior ao início.");

        if (dados.TipoGuia is { } tipoGuia && !Enum.IsDefined(tipoGuia))
            throw new InvalidOperationException("Tipo de guia inválido.");

        var destino = dados.Id == 0 ? null : await _repo.ObterRegraRepasseAsync(dados.Id, ct);
        // O que a regra dizia ANTES, para a trilha: "quem mudou a Amil de 65 para 70?".
        var antes = destino is null ? null : Retrato(destino);
        var estavaAtiva = destino?.Ativa;
        if (destino is null)
        {
            destino = new RegraRepasse { CriadoEm = DateTime.Now, CriadoPor = operador };
            await _repo.AdicionarRegraRepasseAsync(destino, ct);
        }

        destino.ProfissionalId = dados.ProfissionalId;
        destino.Base = dados.Base;
        destino.Percentual = dados.Base == BaseRepasse.PercentualDaReceita ? dados.Percentual : null;
        destino.ValorPorAtendimento =
            dados.Base == BaseRepasse.ValorPorAtendimento ? dados.ValorPorAtendimento : null;
        destino.ModalidadeCodigo = Limpar(dados.ModalidadeCodigo);
        destino.ConvenioCodigo = Limpar(dados.ConvenioCodigo);
        // Lugar 3 do CLAUDE.md: a cópia é campo a campo, e o que não estiver aqui é apagado
        // na primeira EDIÇÃO (a criação funciona, o que esconde o defeito).
        destino.TipoGuia = dados.TipoGuia;
        destino.VigenteDe = dados.VigenteDe;
        destino.VigenteAte = dados.VigenteAte;
        destino.Ativa = dados.Ativa;
        destino.Observacoes = Limpar(dados.Observacoes);

        // Mudança de valor de repasse é dinheiro: vai à trilha no MESMO SaveChanges.
        var depois = Retrato(destino);
        if (antes != depois)
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador.Trim(),
                Acao = antes is null ? "RegraRepasseCriada"
                    : estavaAtiva == true && !destino.Ativa ? "RegraRepasseDesativada"
                    : "RegraRepasseAlterada",
                Detalhe = Cortar(antes is null ? depois : $"{antes} → {depois}", 1000)
            }, ct);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    /// <summary>
    /// Exclui uma regra que nunca pagou ninguém. Regra que já está num fechamento de repasse
    /// vivo é RECUSADA: ela é a procedência daquele valor — desative-a em vez de excluir.
    /// </summary>
    public Task ExcluirRegraAsync(int regraId, CancellationToken ct = default)
        => ExcluirRegraAsync(regraId, null, ct);

    /// <inheritdoc cref="ExcluirRegraAsync(int, CancellationToken)"/>
    public async Task ExcluirRegraAsync(int regraId, string? operador, CancellationToken ct = default)
    {
        var regra = await _repo.ObterRegraRepasseAsync(regraId, ct);
        if (regra is null) return;

        if (await _repo.RegraRepasseEmUsoAsync(regraId, ct))
            throw new InvalidOperationException(
                "Esta regra já foi usada num fechamento de repasse e não pode ser excluída — "
                + "desative-a (ou reajuste o valor a partir de uma data) para ela parar de valer.");

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador.Trim(),
            Acao = "RegraRepasseExcluida",
            Detalhe = Cortar(Retrato(regra), 1000)
        }, ct);
        await _repo.RemoverRegraRepasseAsync(regraId, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// REAJUSTA o valor (ou o percentual) de uma regra a partir de uma data, sem reescrever o
    /// passado (nov/2026, pedido do dono: o gerente configura os valores do repasse).
    ///
    /// Num ÚNICO SaveChanges: a regra antiga termina na véspera de <paramref name="aPartirDe"/>
    /// e nasce uma nova, idêntica nos filtros (profissional, convênio, tipo de guia, modalidade,
    /// base), com o valor novo e a vigência que a antiga tinha pela frente. Como a regra do
    /// repasse é a da DATA DO ATENDIMENTO, quem atendeu antes continua recebendo o valor
    /// antigo, mesmo que a guia seja paga depois — e o que já foi fechado nem é relido (o item
    /// guarda o valor congelado).
    /// </summary>
    public async Task<RegraRepasse> ReajustarRegraAsync(
        int regraId, decimal? novoValorPorAtendimento, decimal? novoPercentual,
        DateOnly aPartirDe, string? operador, CancellationToken ct = default)
    {
        var antiga = await _repo.ObterRegraRepasseAsync(regraId, ct);
        if (antiga is null || !antiga.Ativa)
            throw new InvalidOperationException("Regra de repasse não encontrada ou desativada.");

        if (antiga.VigenteDe is { } de && aPartirDe <= de)
            throw new InvalidOperationException(
                $"A regra já começa em {de:dd/MM/yyyy}. Para mudar o valor desde o início, edite a regra; "
                + "o reajuste vale a partir de uma data posterior.");

        if (antiga.VigenteAte is { } ate && aPartirDe > ate)
            throw new InvalidOperationException(
                $"A regra termina em {ate:dd/MM/yyyy}; a data do reajuste está fora da vigência dela.");

        decimal? valor = null, percentual = null;
        if (antiga.Base == BaseRepasse.ValorPorAtendimento)
        {
            if (novoValorPorAtendimento is not { } v || v <= 0 || decimal.Round(v, 2) != v)
                throw new InvalidOperationException("Informe o novo valor por atendimento, em reais e centavos.");
            valor = v;
        }
        else
        {
            if (novoPercentual is not { } p || p <= 0 || p > 100)
                throw new InvalidOperationException("O novo percentual do repasse vai de 0 a 100.");
            percentual = p;
        }

        var nova = new RegraRepasse
        {
            ProfissionalId = antiga.ProfissionalId,
            Base = antiga.Base,
            Percentual = percentual,
            ValorPorAtendimento = valor,
            ModalidadeCodigo = antiga.ModalidadeCodigo,
            ConvenioCodigo = antiga.ConvenioCodigo,
            TipoGuia = antiga.TipoGuia,
            VigenteDe = aPartirDe,
            VigenteAte = antiga.VigenteAte,
            Ativa = true,
            Observacoes = antiga.Observacoes,
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };
        var descricaoAntiga = antiga.Descricao;
        antiga.VigenteAte = aPartirDe.AddDays(-1);
        await _repo.AdicionarRegraRepasseAsync(nova, ct);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador.Trim(),
            Acao = "RegraRepasseReajustada",
            Detalhe = Cortar($"{Filtros(antiga)}: {descricaoAntiga} → {nova.Descricao} "
                             + $"a partir de {aPartirDe:dd/MM/yyyy}", 1000)
        }, ct);

        await _repo.SalvarAsync(ct);
        return nova;
    }

    /// <summary>A regra em uma linha, para a trilha (pt-BR fixo, como a Descricao).</summary>
    private static string Retrato(RegraRepasse r)
        => $"{Filtros(r)}: {r.Descricao}, vigência "
           + $"{(r.VigenteDe is { } de ? de.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) : "sempre")}"
           + $"{(r.VigenteAte is { } ate ? " a " + ate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture) : string.Empty)}"
           + (r.Ativa ? ", ativa" : ", desativada");

    private static string Filtros(RegraRepasse r)
        => $"profissional {r.ProfissionalId}"
           + (r.ConvenioCodigo is { } c ? $" · convênio {c}" : string.Empty)
           + (r.TipoGuia is { } t ? $" · {RotulosEnum.De(t)}" : string.Empty)
           + (r.ModalidadeCodigo is { } m ? $" · modalidade {m}" : string.Empty);

    private static string? Cortar(string? texto, int maximo)
        => texto is null ? null : texto.Length <= maximo ? texto : texto[..maximo];

    // ==================== Apuração ====================

    /// <summary>
    /// Calcula, sem gravar nada, quanto cada profissional tem a receber no período.
    /// Profissional sem regra aparece na lista com valor zero e a situação dizendo o
    /// porquê — sumir com ele esconderia justamente o cadastro que falta fazer.
    /// </summary>
    public async Task<IReadOnlyList<RepasseCalculado>> CalcularAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        if (fim < inicio) throw new InvalidOperationException("O fim do período é anterior ao início.");

        var agendamentos = await _repo.AgendamentosComAtendimentoAsync(inicio, fim, ct);
        var atendimentoIds = agendamentos
            .Where(a => a.AtendimentoId is not null)
            .Select(a => a.AtendimentoId!.Value)
            .Distinct()
            .ToList();

        var lancamentos = await _repo.LancamentosDosAtendimentosAsync(atendimentoIds, ct);
        var receitaPorAtendimento = lancamentos
            // Só o que ENTROU (set/2026): a sessão particular que "fica a receber" é uma
            // entrada PREVISTA ligada ao atendimento, e repassar sobre ela pagaria o
            // profissional por dinheiro que o paciente ainda não trouxe.
            .Where(l => l.Tipo == TipoLancamento.Entrada && l.Status == StatusLancamento.Realizado
                        && l.AtendimentoId is not null)
            .GroupBy(l => l.AtendimentoId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Valor));

        var regras = await _repo.RegrasRepasseAsync(null, ct);
        var apurados = await _repo.RepassesApuradosAsync(null, ct);

        var resultado = new List<RepasseCalculado>();

        foreach (var grupo in agendamentos.GroupBy(a => a.ProfissionalId!.Value))
        {
            var profissional = grupo.First().Profissional;
            var regra = EscolherRegra(regras, grupo.Key, fim);

            var doProfissional = regra is null
                ? grupo.ToList()
                : grupo.Where(a => AtendeAoFiltro(regra, a)).ToList();

            var receita = doProfissional
                .Where(a => a.AtendimentoId is not null)
                .Sum(a => receitaPorAtendimento.TryGetValue(a.AtendimentoId!.Value, out var v) ? v : 0m);

            var valor = regra is null
                ? 0m
                : regra.Base == BaseRepasse.PercentualDaReceita
                    ? Math.Round(receita * (regra.Percentual ?? 0m) / 100m, 2)
                    : Math.Round((regra.ValorPorAtendimento ?? 0m) * doProfissional.Count, 2);

            resultado.Add(new RepasseCalculado(
                grupo.Key,
                profissional?.Rotulo ?? $"Profissional {grupo.Key}",
                doProfissional.Count,
                receita,
                valor,
                regra?.Descricao ?? "—",
                regra is not null,
                apurados.Any(r => !r.Cancelado && r.ProfissionalId == grupo.Key && r.Sobrepoe(inicio, fim))));
        }

        return resultado.OrderByDescending(r => r.Valor).ThenBy(r => r.Profissional).ToList();
    }

    /// <summary>
    /// Fecha o repasse do período: grava a apuração e cria a saída no caixa (prevista,
    /// porque o pagamento ainda vai acontecer).
    ///
    /// Recusa apurar duas vezes o mesmo período — pagar repasse em duplicidade é dinheiro
    /// que sai e não volta, e sem este registro nada impediria duas máquinas de gerar o
    /// mesmo mês.
    /// </summary>
    public async Task<RepasseApurado> ApurarAsync(
        int profissionalId, DateOnly inicio, DateOnly fim, int? categoriaId = null,
        string? operador = null, CancellationToken ct = default)
    {
        var calculados = await CalcularAsync(inicio, fim, ct);
        var calculo = calculados.FirstOrDefault(c => c.ProfissionalId == profissionalId)
            ?? throw new InvalidOperationException(
                "Este profissional não tem atendimento registrado no período.");

        if (!calculo.TemRegra)
            throw new InvalidOperationException(
                "Cadastre a regra de repasse deste profissional antes de apurar.");

        var jaApurados = await _repo.RepassesApuradosAsync(profissionalId, ct);
        if (jaApurados.FirstOrDefault(r => !r.Cancelado && r.Sobrepoe(inicio, fim)) is { } existente)
            throw new InvalidOperationException(
                $"Já existe repasse apurado para este profissional de "
                + $"{existente.Inicio:dd/MM/yyyy} a {existente.Fim:dd/MM/yyyy}. "
                + "Cancele-o antes de apurar de novo.");

        if (calculo.Valor <= 0)
            throw new InvalidOperationException(
                "O repasse calculado ficou em zero — não há o que pagar neste período.");

        var lancamento = new LancamentoFinanceiro
        {
            Data = fim,
            Tipo = TipoLancamento.Saida,
            Descricao = $"Repasse {calculo.Profissional} — {inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}",
            Valor = calculo.Valor,
            Status = StatusLancamento.Previsto,
            CategoriaFinanceiraId = categoriaId,
            Observacoes = $"{calculo.Atendimentos} atendimento(s) · {calculo.RegraDescricao}",
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };
        await _repo.AdicionarLancamentoAsync(lancamento, ct);

        var apurado = new RepasseApurado
        {
            ProfissionalId = profissionalId,
            Inicio = inicio,
            Fim = fim,
            Atendimentos = calculo.Atendimentos,
            BaseCalculo = calculo.ReceitaVinculada,
            Valor = calculo.Valor,
            RegraDescricao = calculo.RegraDescricao,
            Lancamento = lancamento,
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };
        await _repo.AdicionarRepasseApuradoAsync(apurado, ct);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "RepasseApurado",
            Detalhe = $"{calculo.Profissional} — {calculo.Valor:C} "
                      + $"({inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy})"
        }, ct);

        // Apuração e lançamento no MESMO SaveChanges: repasse gravado sem a saída no
        // caixa (ou o contrário) seria pior do que não ter apurado.
        await _repo.SalvarAsync(ct);
        return apurado;
    }

    public Task<IReadOnlyList<RepasseApurado>> ApuradosAsync(
        int? profissionalId = null, CancellationToken ct = default)
        => _repo.RepassesApuradosAsync(profissionalId, ct);

    /// <summary>
    /// Cancela a apuração e, junto, a saída prevista no caixa — deixar o lançamento vivo
    /// faria a clínica pagar um repasse que ela mesma desfez.
    /// </summary>
    public async Task CancelarApuracaoAsync(
        int repasseId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Diga por que a apuração está sendo cancelada.");

        var apurado = await _repo.ObterRepasseApuradoAsync(repasseId, ct)
            ?? throw new InvalidOperationException("Apuração não encontrada.");

        if (apurado.Cancelado)
            throw new InvalidOperationException("Esta apuração já foi cancelada.");

        apurado.CanceladoEm = DateTime.Now;
        apurado.MotivoCancelamento = motivo.Trim();

        if (apurado.LancamentoFinanceiroId is { } lancamentoId
            && await _repo.ObterLancamentoAsync(lancamentoId, ct) is { } lancamento
            && lancamento.Status != StatusLancamento.Cancelado)
        {
            lancamento.Status = StatusLancamento.Cancelado;
            lancamento.Observacoes = string.IsNullOrWhiteSpace(lancamento.Observacoes)
                ? $"Cancelado: {motivo.Trim()}"
                : $"{lancamento.Observacoes} | Cancelado: {motivo.Trim()}";
        }

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "RepasseCancelado",
            Detalhe = $"{apurado.Valor:C} — {motivo.Trim()}"
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// A regra que vale para o profissional no fim do período. Havendo mais de uma
    /// vigente, ganha a mais específica (com filtro) e depois a que começou por último.
    /// </summary>
    private static RegraRepasse? EscolherRegra(
        IReadOnlyList<RegraRepasse> regras, int profissionalId, DateOnly dia)
        => regras
            // A regra por TIPO DE GUIA só vale no repasse por guia recebida
            // (FechamentoRepasseService): aqui não há guia, e ela viraria "geral" sem querer.
            .Where(r => r.ProfissionalId == profissionalId && r.VigenteEm(dia) && r.TipoGuia == null)
            .OrderByDescending(r => r.ModalidadeCodigo is not null || r.ConvenioCodigo is not null)
            .ThenByDescending(r => r.VigenteDe ?? DateOnly.MinValue)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();

    private static bool AtendeAoFiltro(RegraRepasse regra, Agendamento agendamento)
        => (regra.ModalidadeCodigo is null
            || string.Equals(regra.ModalidadeCodigo, agendamento.ModalidadeCodigo,
                StringComparison.OrdinalIgnoreCase))
           && (regra.ConvenioCodigo is null
               || string.Equals(regra.ConvenioCodigo, agendamento.Paciente?.ConvenioCodigo,
                   StringComparison.OrdinalIgnoreCase));

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
