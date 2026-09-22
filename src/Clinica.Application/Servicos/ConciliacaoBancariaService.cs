using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Em que situação uma linha do extrato ficou depois do cruzamento.</summary>
public enum SituacaoConciliacao
{
    /// <summary>Achou UM lançamento que bate. É o caso normal e o único que se concilia sozinho.</summary>
    Casada,

    /// <summary>Achou mais de um candidato — a clínica escolhe qual.</summary>
    Ambigua,

    /// <summary>O dinheiro se moveu no banco e não há lançamento no sistema.</summary>
    SoNoBanco,

    /// <summary>Já conciliada numa importação anterior.</summary>
    JaConciliada,

    /// <summary>Um depósito corresponde ao lote completo da adquirente naquela data.</summary>
    DepositoCartao
}

/// <summary>Uma linha do extrato depois de cruzada com o sistema.</summary>
public sealed record LinhaConciliada(
    LinhaExtrato Extrato,
    SituacaoConciliacao Situacao,
    IReadOnlyList<ItemConciliacao> Candidatos)
{
    /// <summary>O único candidato, quando há exatamente um.</summary>
    public ItemConciliacao? Unico => Candidatos.Count == 1 ? Candidatos[0] : null;
}

/// <summary>
/// O resultado do cruzamento.
///
/// <paramref name="SoNoSistema"/> é a metade que o extrato não mostra e que ninguém pede:
/// lançamentos dados como realizados no sistema e que <b>não aparecem no banco</b>. É onde
/// mora o erro caro — a venda marcada como recebida que nunca caiu.
/// </summary>
public sealed record ResultadoConciliacao(
    IReadOnlyList<LinhaConciliada> Linhas,
    IReadOnlyList<ItemConciliacao> SoNoSistema,
    DateOnly? Inicio,
    DateOnly? Fim,
    string? Conta)
{
    public int Casadas => Linhas.Count(l => l.Situacao is SituacaoConciliacao.Casada or SituacaoConciliacao.DepositoCartao);
    public int Pendentes => Linhas.Count(l => l.Situacao is SituacaoConciliacao.Ambigua
                                                        or SituacaoConciliacao.SoNoBanco);
}

/// <summary>
/// CONCILIAÇÃO BANCÁRIA POR OFX (parcela 63).
///
/// Até aqui o extrato do banco era conferido A OLHO contra a tela de recebíveis: o
/// internet banking de um lado, o sistema do outro, linha por linha. Era o último ponto
/// do financeiro em que fechar o mês dependia de alguém não se distrair.
///
/// Três decisões mandam aqui:
///
/// 1. <b>O sistema PROPÕE, a pessoa confirma.</b> Nada é conciliado automaticamente, nem
///    a linha que casa perfeitamente. É a mesma regra do fechamento da sessão e da
///    proposta de preço por convênio: o sistema faz o trabalho (achar o par), o clique
///    continua sendo de quem responde pelo caixa. Casar sozinho um valor que coincide
///    daria conciliação que "bate" sobre um par errado — pior do que não conciliar.
/// 2. <b>Casa por VALOR e por DATA com folga.</b> O banco posta o PIX no mesmo dia, mas
///    o boleto e o TED caem no dia seguinte ou depois do fim de semana; exigir a data
///    exata deixaria de casar a maior parte do extrato de uma clínica. A folga é de
///    poucos dias, e não de um mês — folga grande casaria o aluguel de julho com o de
///    agosto, que têm o mesmo valor.
/// 3. <b>Conciliar NÃO muda o valor nem o status do lançamento.</b> "Foi pago" e
///    "apareceu no extrato" são duas perguntas, e fundi-las faria a conciliação reescrever
///    o caixa — que é justamente o que ela existe para conferir.
/// </summary>
public sealed class ConciliacaoBancariaService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>
    /// Quantos dias de folga entre a data do lançamento e a do extrato.
    ///
    /// Três cobre o fim de semana (sexta → segunda) e o D+1 do boleto. Aumentar isto
    /// parece generoso e é perigoso: com folga de trinta dias, dois aluguéis iguais de
    /// meses diferentes viram candidatos um do outro, e a tela passa a pedir desempate em
    /// tudo — que é como se ensina alguém a clicar sem olhar.
    /// </summary>
    public const int FolgaDias = 3;

    public ConciliacaoBancariaService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Cruza o conteúdo de um arquivo OFX com os lançamentos REALIZADOS do período.
    ///
    /// Só o realizado entra: o previsto é o que ainda vai acontecer, e casá-lo com o
    /// extrato diria que uma conta a vencer já foi paga.
    /// </summary>
    public async Task<ResultadoConciliacao> CruzarAsync(
        string conteudoOfx, CancellationToken ct = default)
    {
        var extrato = LeitorOfx.Ler(conteudoOfx);

        if (extrato.Linhas.Count == 0)
            return new ResultadoConciliacao(
                Array.Empty<LinhaConciliada>(), Array.Empty<ItemConciliacao>(),
                extrato.Inicio, extrato.Fim, extrato.Conta);

        // A janela de busca é a do extrato ESTICADA pela folga dos dois lados — senão o
        // lançamento do dia 31 que caiu no dia 2 ficaria de fora por um dia.
        var de = (extrato.Inicio ?? extrato.Linhas.Min(l => l.Data)).AddDays(-FolgaDias);
        var ate = (extrato.Fim ?? extrato.Linhas.Max(l => l.Data)).AddDays(FolgaDias);

        var lancamentos = (await _repo.LancamentosParaConciliacaoAsync(de, ate, ct))
            .Where(l => l.Status == StatusLancamento.Realizado)
            .Select(l => new ItemConciliacao(l)).ToList();
        var parcelas = await _repo.ParcelasCartaoParaConciliacaoAsync(de, ate, ct);
        lancamentos.AddRange(parcelas.Select(p => new ItemConciliacao(p.Lancamento, p)));

        var linhas = new List<LinhaConciliada>();

        // Lançamento já usado por uma linha não pode ser candidato da seguinte: dois PIX
        // de R$ 150 no mesmo dia são dois recebimentos, e oferecer o mesmo lançamento aos
        // dois faria a clínica conciliar duas entradas contra uma só.
        var usados = new HashSet<string>();

        foreach (var linha in extrato.Linhas.OrderBy(l => l.Data))
        {
            if (!linha.IdentidadeBancariaInformada)
            {
                linhas.Add(new LinhaConciliada(linha, SituacaoConciliacao.SoNoBanco, []));
                continue;
            }
            // Já conciliada antes: a idempotência que permite reimportar o mesmo arquivo
            // sem medo — o extrato do mês costuma ser baixado várias vezes.
            var jaFeitas = lancamentos.Where(l => l.Conciliado && l.IdBancario == linha.Id &&
                (l.ContaBancariaConciliacao == extrato.IdentificacaoConta || l.ContaBancariaConciliacao == null)).ToList();
            if (jaFeitas.Count > 0)
            {
                usados.UnionWith(jaFeitas.Select(l => l.Chave));
                linhas.Add(new LinhaConciliada(
                    linha, SituacaoConciliacao.JaConciliada, jaFeitas));
                continue;
            }

            var candidatos = lancamentos
                .Where(l => !l.Conciliado
                            && !usados.Contains(l.Chave)
                            && Combina(l, linha))
                // O mais PRÓXIMO no tempo primeiro: entre dois candidatos idênticos, o do
                // mesmo dia é quase sempre o certo.
                .OrderBy(l => Math.Abs((DataDoDinheiro(l).DayNumber - linha.Data.DayNumber)))
                .ToList();

            var situacao = candidatos.Count switch
            {
                0 => SituacaoConciliacao.SoNoBanco,
                1 => SituacaoConciliacao.Casada,
                _ => SituacaoConciliacao.Ambigua
            };

            // Só propõe o lote COMPLETO, da mesma adquirente e data, se há um único lote
            // possível e nenhum candidato individual. Não procura combinações arbitrárias.
            if (candidatos.Count == 0 && linha.Entrada)
            {
                var lotes = lancamentos.Where(l => !l.Conciliado && !usados.Contains(l.Chave)
                        && l.Tipo == TipoLancamento.Entrada && l.ModalidadeCartao is not null
                        && !string.IsNullOrWhiteSpace(l.Adquirente)
                        && Math.Abs(DataDoDinheiro(l).DayNumber - linha.Data.DayNumber) <= FolgaDias)
                    .GroupBy(l => (l.Adquirente, Dia: DataDoDinheiro(l)))
                    .Where(g => g.Count() > 1 && g.Sum(ValorBancario) == linha.ValorAbsoluto).ToList();
                if (lotes.Count == 1)
                {
                    candidatos = lotes[0].ToList();
                    situacao = SituacaoConciliacao.DepositoCartao;
                    usados.UnionWith(candidatos.Select(l => l.Chave));
                }
            }

            // Só a casada RESERVA o lançamento. Na ambígua ninguém escolheu ainda, e
            // reservar o primeiro candidato tiraria da linha seguinte a opção certa.
            if (situacao == SituacaoConciliacao.Casada) usados.Add(candidatos[0].Chave);

            linhas.Add(new LinhaConciliada(linha, situacao, candidatos));
        }

        // A metade que o extrato não mostra: dado como recebido no sistema e ausente do
        // banco. É onde mora o erro caro — a venda marcada como paga que nunca caiu.
        var soNoSistema = lancamentos
            .Where(l => !l.Conciliado && !usados.Contains(l.Chave))
            .OrderBy(l => DataDoDinheiro(l))
            .ToList();

        return new ResultadoConciliacao(
            linhas, soNoSistema, extrato.Inicio, extrato.Fim, extrato.Conta);
    }

    // Mantido para integrações anteriores que já fornecem uma identidade bancária.
    public Task ConciliarAsync(int lancamentoId, string idBancario, string? operador = null,
        CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            var item = await IntegralAsync(lancamentoId, ct);
            await ValidarIdentidadeAsync(idBancario, null, ct);
            await ConfirmarItemAsync(item, idBancario.Trim(), null, null, operador, ct);
            await _repo.SalvarAsync(ct);
            return true;
        }, ct);

    public Task ConciliarLinhaAsync(int lancamentoId, LinhaExtrato linha, string? operador = null,
        string? conta = null, CancellationToken ct = default)
        => ConciliarItensAsync([new ItemConciliacao(new LancamentoFinanceiro { Id = lancamentoId })],
            linha, operador, conta, ct);

    public Task ConciliarDepositoAsync(IReadOnlyCollection<int> ids, LinhaExtrato linha,
        string? operador = null, string? conta = null, CancellationToken ct = default)
        => ConciliarItensAsync(ids.Select(id => new ItemConciliacao(new LancamentoFinanceiro { Id = id })).ToList(),
            linha, operador, conta, ct);

    /// <summary>Recarrega cada crédito e confere valor, direção e data dentro da transação.</summary>
    public Task ConciliarItensAsync(IReadOnlyCollection<ItemConciliacao> referencias, LinhaExtrato linha,
        string? operador = null, string? conta = null, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            if (!linha.IdentidadeBancariaInformada)
                throw new InvalidOperationException("O OFX não informou o identificador bancário desta transação. Solicite um extrato com FITID.");
            if (referencias.Count == 0 || referencias.Select(i => i.Chave).Distinct().Count() != referencias.Count)
                throw new InvalidOperationException("Escolha créditos distintos para o depósito.");
            await ValidarIdentidadeAsync(linha.Id, conta, ct);
            var lote = new List<ItemConciliacao>();
            foreach (var r in referencias)
            {
                if (r.ParcelaId is { } id)
                {
                    var p = (await _repo.ParcelasCartaoPorIdsAsync([id], ct)).SingleOrDefault()
                        ?? throw new InvalidOperationException("Parcela não encontrada.");
                    lote.Add(new ItemConciliacao(p.Lancamento, p));
                }
                else lote.Add(await IntegralAsync(r.Id, ct));
            }
            if (lote.Any(i => i.Status != StatusLancamento.Realizado || i.Conciliado
                    || i.Lancamento.FormaPagamento == FormaPagamento.Dinheiro
                    || (i.Tipo == TipoLancamento.Entrada) != linha.Entrada
                    || Math.Abs(i.Data.DayNumber - linha.Data.DayNumber) > FolgaDias)
                || lote.Sum(i => i.ValorBancario) != linha.ValorAbsoluto
                || (lote.Count > 1 && (!linha.Entrada || lote.Any(i => i.ModalidadeCartao is null
                    || string.IsNullOrWhiteSpace(i.Adquirente))
                    || lote.Select(i => (i.Adquirente, i.Data)).Distinct().Count() != 1)))
                throw new InvalidOperationException("Valor líquido, direção ou data divergente do extrato. O lote mudou; atualize e confira os créditos.");
            foreach (var item in lote)
                await ConfirmarItemAsync(item, linha.Id.Trim(), conta, linha.Data, operador, ct);
            await AtualizarVendasAsync(lote.Where(i => i.Parcela is not null).Select(i => i.Id), ct);
            await _repo.SalvarAsync(ct);
            return true;
        }, ct);

    private async Task<ItemConciliacao> IntegralAsync(int id, CancellationToken ct)
    {
        var l = await _repo.ObterLancamentoAsync(id, ct)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");
        if ((await _repo.ParcelasCartaoDoLancamentoAsync(id, ct)).Count > 0)
            throw new InvalidOperationException("Concilie os créditos das parcelas deste contrato, não o total da venda.");
        return new ItemConciliacao(l);
    }

    private async Task ValidarIdentidadeAsync(string id, string? conta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Trim().Length > 100 || conta?.Length > 200)
            throw new InvalidOperationException("Informe uma identificação bancária válida (transação até 100 e conta até 200 caracteres).");
        if (await _repo.IdBancarioUtilizadoAsync(id.Trim(), conta, ct))
            throw new InvalidOperationException("Esta transação bancária já foi conciliada. Atualize o extrato.");
    }

    private async Task ConfirmarItemAsync(ItemConciliacao item, string id, string? conta,
        DateOnly? data, string? operador, CancellationToken ct)
    {
        if (item.Conciliado) throw new InvalidOperationException("Este lançamento já foi conciliado. Desfaça a conciliação antes de continuar.");
        if (item.Status != StatusLancamento.Realizado
            || item.Lancamento.FormaPagamento == FormaPagamento.Dinheiro)
            throw new InvalidOperationException("Concilie apenas pagamentos realizados, ainda não conciliados, que passam pelo banco.");
        if (item.Parcela is { } p)
        {
            p.ConciliadoEm = DateTime.Now;
            p.IdBancario = id;
            p.ContaBancaria = conta;
            p.DataExtrato = data;
            p.RecebimentoAntesDaConciliacao = p.RecebidoEm;
            p.RecebidoEm = data ?? p.RecebidoEm;
        }
        else
        {
            var l = item.Lancamento;
            l.ConciliadoEm = DateTime.Now;
            l.IdBancario = id;
            l.ContaBancariaConciliacao = conta;
            l.DataExtrato = data;
            l.RecebimentoAntesDaConciliacao = l.RecebimentoConfirmadoEm;
            if (l.PrevisaoRecebimento is not null && data is not null) l.RecebimentoConfirmadoEm = data;
        }
        await AuditarAsync("LancamentoConciliado", item, id, operador, ct);
    }

    public Task DesfazerAsync(int lancamentoId, string? operador = null, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            var item = await IntegralAsync(lancamentoId, ct);
            if (!item.Conciliado) return false;
            await DesfazerTransacaoAsync(item.IdBancario!, item.ContaBancariaConciliacao, operador, ct);
            return true;
        }, ct);

    public Task DesfazerParcelaAsync(int parcelaId, string? operador = null, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            var p = (await _repo.ParcelasCartaoPorIdsAsync([parcelaId], ct)).SingleOrDefault()
                ?? throw new InvalidOperationException("Parcela não encontrada.");
            if (p.ConciliadoEm is null) return false;
            await DesfazerTransacaoAsync(p.IdBancario!, p.ContaBancaria, operador, ct);
            return true;
        }, ct);

    private async Task DesfazerTransacaoAsync(string id, string? conta, string? operador, CancellationToken ct)
    {
        var vendas = await _repo.LancamentosDaTransacaoBancariaAsync(id, conta, ct);
        var parcelas = await _repo.ParcelasCartaoDaTransacaoAsync(id, conta, ct);
        foreach (var l in vendas)
        {
            l.ConciliadoEm = null;
            l.IdBancario = null;
            l.ContaBancariaConciliacao = null;
            if (l.DataExtrato is not null && l.PrevisaoRecebimento is not null)
                l.RecebimentoConfirmadoEm = l.RecebimentoAntesDaConciliacao;
            l.DataExtrato = null;
            l.RecebimentoAntesDaConciliacao = null;
            await AuditarAsync("ConciliacaoDesfeita", new ItemConciliacao(l), id, operador, ct);
        }
        foreach (var p in parcelas)
        {
            p.ConciliadoEm = null;
            p.IdBancario = null;
            p.ContaBancaria = null;
            p.RecebidoEm = p.RecebimentoAntesDaConciliacao;
            p.DataExtrato = null;
            p.RecebimentoAntesDaConciliacao = null;
            await AuditarAsync("ConciliacaoDesfeita", new ItemConciliacao(p.Lancamento, p), id, operador, ct);
        }
        await AtualizarVendasAsync(parcelas.Select(p => p.LancamentoFinanceiroId), ct);
        await _repo.SalvarAsync(ct);
    }

    private async Task AtualizarVendasAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        foreach (var id in ids.Distinct())
            await new CalendarioCartaoService(_repo).AtualizarConfirmacaoDaVendaAsync(id, ct);
    }

    private Task AuditarAsync(string acao, ItemConciliacao item, string id, string? operador, CancellationToken ct)
        => _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = acao,
            Detalhe = $"{item.Descricao} — {item.ValorBancario:C2} · extrato {id}",
            PacienteId = item.Lancamento.PacienteId
        }, ct);

    private static bool Combina(ItemConciliacao item, LinhaExtrato linha)
        => item.ValorBancario == linha.ValorAbsoluto && (item.Tipo == TipoLancamento.Entrada) == linha.Entrada
            && Math.Abs(item.Data.DayNumber - linha.Data.DayNumber) <= FolgaDias;
    private static DateOnly DataDoDinheiro(ItemConciliacao item) => item.Data;
    private static decimal ValorBancario(ItemConciliacao item) => item.ValorBancario;
}
