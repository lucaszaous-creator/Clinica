using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Um depósito esperado: tudo o que uma adquirente deve depositar num dia.
///
/// O agrupamento é por (adquirente, data) porque é assim que o dinheiro chega — a Stone
/// não deposita venda por venda, deposita o lote do dia. Conferir linha a linha contra um
/// extrato que traz um valor só faria a conciliação nunca fechar.
/// </summary>
public sealed record DepositoEsperado(
    string Adquirente,
    DateOnly Previsao,
    decimal Bruto,
    decimal Taxa,
    decimal Liquido,
    int Quantidade,
    IReadOnlyList<int> LancamentoIds)
{
    public IReadOnlyList<int> ParcelaIds { get; init; } = [];
    /// <summary>Passou da data e não caiu.</summary>
    public bool Atrasado(DateOnly hoje) => Previsao < hoje;
}

/// <summary>
/// Um depósito que já caiu: o mesmo lote, visto depois da confirmação.
///
/// Ele guarda as DUAS datas — a prevista e a real —, que é justamente o motivo de
/// <c>RecebimentoConfirmadoEm</c> ser campo separado da previsão. Sobrescrever a
/// previsão apagaria a prova do atraso, e é o atraso que dá conversa com a adquirente.
/// </summary>
public sealed record DepositoConfirmado(
    string Adquirente,
    DateOnly Creditado,
    DateOnly? Previsto,
    decimal Bruto,
    decimal Taxa,
    decimal Liquido,
    int Quantidade,
    IReadOnlyList<int> LancamentoIds)
{
    public IReadOnlyList<int> ParcelaIds { get; init; } = [];
    /// <summary>Dias entre o prometido e o pago. Negativo = caiu adiantado.</summary>
    public int? DiasDeAtraso => Previsto is { } p ? Creditado.DayNumber - p.DayNumber : null;
}

/// <summary>Totais do que a clínica tem para receber das maquininhas.</summary>
public sealed record ResumoRecebiveis(
    decimal LiquidoAVencer,
    decimal LiquidoAtrasado,
    int QuantidadeAtrasada,
    DateOnly? ProximoDeposito)
{
    public decimal Total => LiquidoAVencer + LiquidoAtrasado;

    public bool TemAtraso => QuantidadeAtrasada > 0;
}

/// <summary>
/// Recebíveis de cartão (parcela 16).
///
/// `PrevisaoRecebimento` era gravada desde a parcela 9 e **nenhuma tela a lia**. É o mesmo
/// defeito que já apareceu duas vezes neste projeto — o pacote que debitava e o insumo que
/// baixava, ambos testados e sem chamador: dado gravado sem leitor passa no CI e não faz
/// nada na clínica.
///
/// O que ele responde, e que ninguém respondia: **quanto a clínica tem para receber das
/// maquininhas, quando cai, e o que a adquirente deveria ter depositado e não depositou.**
/// A última é a que importa: dinheiro de cartão que não caiu não aparece em lugar nenhum
/// do sistema, e some sem ninguém notar até a conciliação bancária do fim do mês — se
/// houver.
///
/// Duas decisões:
///
/// 1. **A conciliação é por DEPÓSITO, não por venda.** A adquirente deposita o lote do dia;
///    conferir venda a venda contra um extrato que traz um valor só nunca fecharia.
/// 2. **A data real fica separada da prevista.** Quando a adquirente atrasa, as duas
///    divergem — e sobrescrever a previsão apagaria a prova de que houve atraso.
/// </summary>
public sealed class RecebiveisService
{
    private readonly IClinicaRepositorio _repo;

    public RecebiveisService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Depósitos esperados até <paramref name="ate"/>, do mais antigo para o mais novo —
    /// a ordem em que devem cair.
    ///
    /// O atrasado entra mesmo estando fora do período, pela mesma razão da conta vencida:
    /// esconder o problema porque ele é antigo é esconder justamente o que importa.
    /// </summary>
    public async Task<IReadOnlyList<DepositoEsperado>> EsperadosAsync(
        DateOnly ate, CancellationToken ct = default)
    {
        var vendas = await _repo.RecebiveisEmAbertoAsync(ate, ct);
        var parcelas = await _repo.ParcelasCartaoAbertasAsync(ate, ct);
        var abertos = vendas.Where(l => l.PrevisaoRecebimento is not null).Select(l => new ItemConciliacao(l))
            .Concat(parcelas.Select(p => new ItemConciliacao(p.Lancamento, p)));
        return abertos.GroupBy(i => (Adquirente: Adquirente(i), Previsao: i.Data))
            .Select(g => new DepositoEsperado(g.Key.Adquirente, g.Key.Previsao,
                g.Sum(i => i.Valor), g.Sum(i => i.ValorTaxa ?? 0), g.Sum(i => i.ValorBancario),
                g.Count(), g.Where(i => i.Parcela is null).Select(i => i.Id).ToList())
                { ParcelaIds = g.Where(i => i.ParcelaId is not null).Select(i => i.ParcelaId!.Value).ToList() })
            .OrderBy(d => d.Previsao).ThenBy(d => d.Adquirente).ToList();
    }

    /// <summary>Depósitos que já passaram da data e não caíram — a única linha que pede ação.</summary>
    public async Task<IReadOnlyList<DepositoEsperado>> AtrasadosAsync(
        DateOnly hoje, CancellationToken ct = default)
        => (await EsperadosAsync(hoje, ct)).Where(d => d.Atrasado(hoje)).ToList();

    /// <summary>Totais do que há para receber, separando o atrasado do que ainda vai cair.</summary>
    public async Task<ResumoRecebiveis> ResumoAsync(
        DateOnly hoje, DateOnly ate, CancellationToken ct = default)
    {
        var esperados = await EsperadosAsync(ate, ct);
        var atrasados = esperados.Where(d => d.Atrasado(hoje)).ToList();

        return new ResumoRecebiveis(
            esperados.Where(d => !d.Atrasado(hoje)).Sum(d => d.Liquido),
            atrasados.Sum(d => d.Liquido),
            atrasados.Sum(d => d.Quantidade),
            esperados.Where(d => !d.Atrasado(hoje))
                .Select(d => (DateOnly?)d.Previsao).FirstOrDefault());
    }

    /// <summary>
    /// Confirma que o depósito caiu, na data real.
    ///
    /// A data real é informada e não assumida como hoje: a clínica confere o extrato na
    /// segunda-feira e o depósito caiu na sexta, e gravar "hoje" transformaria todo
    /// depósito num atraso de três dias — o número ficaria errado justamente na métrica
    /// que a tela existe para medir.
    /// </summary>
    public Task<int> ConfirmarAsync(IReadOnlyCollection<int> lancamentoIds, DateOnly dataReal,
        string? operador = null, CancellationToken ct = default, IReadOnlyCollection<int>? parcelaIds = null)
        => AlterarConfirmacaoAsync(lancamentoIds, parcelaIds ?? [], dataReal, operador, ct);

    /// <summary>
    /// Depósitos que já caíram, agrupados como caíram — por adquirente e dia do crédito.
    ///
    /// Existe para a confirmação poder ser DESFEITA: quem marcou o dia errado precisa
    /// achar o lote, e sem esta leitura o <see cref="DesfazerConfirmacaoAsync"/> não tinha
    /// como ser chamado de tela nenhuma. A previsão vai junto, mais antiga do lote, para
    /// o atraso continuar visível depois de pago.
    /// </summary>
    public async Task<IReadOnlyList<DepositoConfirmado>> ConfirmadosAsync(
        DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var vendas = await _repo.RecebiveisConfirmadosAsync(de, ate, ct);
        var parcelas = await _repo.ParcelasCartaoConfirmadasAsync(de, ate, ct);
        var creditados = vendas.Select(l => new ItemConciliacao(l))
            .Concat(parcelas.Select(p => new ItemConciliacao(p.Lancamento, p)));
        return creditados.GroupBy(i => (Adquirente: Adquirente(i), Creditado: i.Data))
            .Select(g => new DepositoConfirmado(g.Key.Adquirente, g.Key.Creditado,
                g.Min(i => i.Parcela is { } p ? p.Previsao : i.Lancamento.PrevisaoRecebimento),
                g.Sum(i => i.Valor), g.Sum(i => i.ValorTaxa ?? 0), g.Sum(i => i.ValorBancario),
                g.Count(), g.Where(i => i.Parcela is null).Select(i => i.Id).ToList())
                { ParcelaIds = g.Where(i => i.ParcelaId is not null).Select(i => i.ParcelaId!.Value).ToList() })
            .OrderByDescending(d => d.Creditado).ThenBy(d => d.Adquirente).ToList();
    }

    /// <summary>
    /// Desfaz a confirmação de um depósito — para quando alguém marcou o dia errado.
    /// Não apaga o lançamento: só devolve o recebível à lista de espera.
    /// </summary>
    public Task<int> DesfazerConfirmacaoAsync(IReadOnlyCollection<int> lancamentoIds,
        string? operador = null, CancellationToken ct = default, IReadOnlyCollection<int>? parcelaIds = null)
        => AlterarConfirmacaoAsync(lancamentoIds, parcelaIds ?? [], null, operador, ct);

    private Task<int> AlterarConfirmacaoAsync(IReadOnlyCollection<int> ids, IReadOnlyCollection<int> parcelaIds,
        DateOnly? data, string? operador, CancellationToken ct)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            if (data > DateOnly.FromDateTime(DateTime.Today))
                throw new InvalidOperationException("Não é possível confirmar um depósito futuro.");
            var vendas = await _repo.LancamentosPorIdAsync(ids, ct);
            var parcelas = await _repo.ParcelasCartaoPorIdsAsync(parcelaIds, ct);
            if (vendas.Count != ids.Distinct().Count() || parcelas.Count != parcelaIds.Distinct().Count())
                throw new InvalidOperationException("Um crédito não foi encontrado. Atualize a lista.");
            foreach (var l in vendas)
                if ((await _repo.ParcelasCartaoDoLancamentoAsync(l.Id, ct)).Count > 0)
                    throw new InvalidOperationException("Confirme cada crédito do contrato mensal pela lista de recebíveis.");
            if (vendas.Any(l => l.Conciliado) || parcelas.Any(p => p.ConciliadoEm is not null))
                throw new InvalidOperationException("Desfaça primeiro a conciliação no extrato bancário; o depósito conferido não pode ser alterado isoladamente.");
            if (parcelas.Any(p => p.Lancamento.Status != StatusLancamento.Realizado))
                throw new InvalidOperationException("Este pagamento não está realizado. Atualize a lista.");
            var alterados = 0;
            foreach (var l in vendas)
            {
                if (data is not null ? !l.RecebivelEmAberto : l.RecebimentoConfirmadoEm is null) continue;
                l.RecebimentoConfirmadoEm = data;
                alterados++;
            }
            foreach (var p in parcelas)
            {
                if (data is not null ? p.RecebidoEm is not null : p.RecebidoEm is null) continue;
                p.RecebidoEm = data;
                alterados++;
            }
            if (alterados == 0) return 0;
            foreach (var id in parcelas.Select(p => p.LancamentoFinanceiroId).Distinct())
                await new CalendarioCartaoService(_repo).AtualizarConfirmacaoDaVendaAsync(id, ct);
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Acao = data is null ? "RecebimentoDesfeito" : "RecebimentoConfirmado",
                Detalhe = data is null ? $"{alterados} crédito(s) devolvido(s) à espera"
                    : $"{alterados} crédito(s) recebido(s) em {data:dd/MM/yyyy}",
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador
            }, ct);
            await _repo.SalvarAsync(ct);
            return alterados;
        }, ct);

    private static string Adquirente(ItemConciliacao i)
        => string.IsNullOrWhiteSpace(i.Adquirente) ? "(não informado)" : i.Adquirente;
}
