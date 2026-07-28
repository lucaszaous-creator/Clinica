using Clinica.Application.Abstracoes;
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

        var destino = dados.Id == 0 ? null : await _repo.ObterRegraRepasseAsync(dados.Id, ct);
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
        destino.VigenteDe = dados.VigenteDe;
        destino.VigenteAte = dados.VigenteAte;
        destino.Ativa = dados.Ativa;
        destino.Observacoes = Limpar(dados.Observacoes);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    public async Task ExcluirRegraAsync(int regraId, CancellationToken ct = default)
    {
        await _repo.RemoverRegraRepasseAsync(regraId, ct);
        await _repo.SalvarAsync(ct);
    }

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
            .Where(l => l.Tipo == TipoLancamento.Entrada && l.AtendimentoId is not null)
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
            .Where(r => r.ProfissionalId == profissionalId && r.VigenteEm(dia))
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
