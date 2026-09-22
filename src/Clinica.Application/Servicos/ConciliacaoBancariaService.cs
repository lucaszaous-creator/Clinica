using Clinica.Application.Abstracoes;
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
    IReadOnlyList<LancamentoFinanceiro> Candidatos)
{
    /// <summary>O único candidato, quando há exatamente um.</summary>
    public LancamentoFinanceiro? Unico => Candidatos.Count == 1 ? Candidatos[0] : null;
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
    IReadOnlyList<LancamentoFinanceiro> SoNoSistema,
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
                Array.Empty<LinhaConciliada>(), Array.Empty<LancamentoFinanceiro>(),
                extrato.Inicio, extrato.Fim, extrato.Conta);

        // A janela de busca é a do extrato ESTICADA pela folga dos dois lados — senão o
        // lançamento do dia 31 que caiu no dia 2 ficaria de fora por um dia.
        var de = (extrato.Inicio ?? extrato.Linhas.Min(l => l.Data)).AddDays(-FolgaDias);
        var ate = (extrato.Fim ?? extrato.Linhas.Max(l => l.Data)).AddDays(FolgaDias);

        var lancamentos = (await _repo.LancamentosParaConciliacaoAsync(de, ate, ct))
            .Where(l => l.Status == StatusLancamento.Realizado)
            .ToList();

        var linhas = new List<LinhaConciliada>();

        // Lançamento já usado por uma linha não pode ser candidato da seguinte: dois PIX
        // de R$ 150 no mesmo dia são dois recebimentos, e oferecer o mesmo lançamento aos
        // dois faria a clínica conciliar duas entradas contra uma só.
        var usados = new HashSet<int>();

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
                usados.UnionWith(jaFeitas.Select(l => l.Id));
                linhas.Add(new LinhaConciliada(
                    linha, SituacaoConciliacao.JaConciliada, jaFeitas));
                continue;
            }

            var candidatos = lancamentos
                .Where(l => !l.Conciliado
                            && !usados.Contains(l.Id)
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
                var lotes = lancamentos.Where(l => !l.Conciliado && !usados.Contains(l.Id)
                        && l.Tipo == TipoLancamento.Entrada && l.ModalidadeCartao is not null
                        && !string.IsNullOrWhiteSpace(l.Adquirente)
                        && Math.Abs(DataDoDinheiro(l).DayNumber - linha.Data.DayNumber) <= FolgaDias)
                    .GroupBy(l => (l.Adquirente, Dia: DataDoDinheiro(l)))
                    .Where(g => g.Count() > 1 && g.Sum(ValorBancario) == linha.ValorAbsoluto).ToList();
                if (lotes.Count == 1)
                {
                    candidatos = lotes[0].ToList();
                    situacao = SituacaoConciliacao.DepositoCartao;
                    usados.UnionWith(candidatos.Select(l => l.Id));
                }
            }

            // Só a casada RESERVA o lançamento. Na ambígua ninguém escolheu ainda, e
            // reservar o primeiro candidato tiraria da linha seguinte a opção certa.
            if (situacao == SituacaoConciliacao.Casada) usados.Add(candidatos[0].Id);

            linhas.Add(new LinhaConciliada(linha, situacao, candidatos));
        }

        // A metade que o extrato não mostra: dado como recebido no sistema e ausente do
        // banco. É onde mora o erro caro — a venda marcada como paga que nunca caiu.
        var soNoSistema = lancamentos
            .Where(l => !l.Conciliado && !usados.Contains(l.Id))
            .OrderBy(l => DataDoDinheiro(l))
            .ToList();

        return new ResultadoConciliacao(
            linhas, soNoSistema, extrato.Inicio, extrato.Fim, extrato.Conta);
    }

    /// <summary>
    /// Confirma o casamento de uma linha do extrato com um lançamento.
    ///
    /// Grava o <c>FITID</c>, e é ele que torna a importação idempotente: o mesmo arquivo
    /// relido não oferece de novo o que já foi conciliado.
    /// </summary>
    public Task ConciliarAsync(
        int lancamentoId, string idBancario, string? operador = null,
        CancellationToken ct = default)
        => ConfirmarAsync(lancamentoId, idBancario, null, null, operador, ct);

    private Task ConfirmarAsync(int lancamentoId, string idBancario, string? conta,
        DateOnly? dataExtrato, string? operador, CancellationToken ct, bool validarIdentidade = true)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(idBancario) || idBancario.Trim().Length > 100)
            throw new InvalidOperationException("Informe uma identificação bancária válida, com até 100 caracteres.");
        idBancario = idBancario.Trim();
        var lancamento = await _repo.ObterLancamentoAsync(lancamentoId, ct)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        if (lancamento.Conciliado)
            throw new InvalidOperationException(
                "Este lançamento já foi conciliado. Desfaça a conciliação antes de casá-lo "
                + "com outra linha do extrato.");

        if (lancamento.Status != StatusLancamento.Realizado || lancamento.FormaPagamento == FormaPagamento.Dinheiro)
            throw new InvalidOperationException("Concilie apenas pagamentos realizados que passam pelo banco.");
        if (conta?.Length > 200)
            throw new InvalidOperationException("Identificação bancária da conta excede 200 caracteres.");
        if (validarIdentidade && await _repo.IdBancarioUtilizadoAsync(idBancario, conta, ct))
            throw new InvalidOperationException("Esta transação bancária já foi conciliada. Atualize o extrato.");

        lancamento.ConciliadoEm = DateTime.Now;
        lancamento.IdBancario = idBancario;
        lancamento.ContaBancariaConciliacao = conta;
        lancamento.DataExtrato = dataExtrato;
        lancamento.RecebimentoAntesDaConciliacao = lancamento.RecebimentoConfirmadoEm;
        if (lancamento.PrevisaoRecebimento is not null && dataExtrato is not null)
            lancamento.RecebimentoConfirmadoEm = dataExtrato;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "LancamentoConciliado",
            Detalhe = $"{lancamento.Descricao} — {lancamento.Valor:C2} · extrato {idBancario}",
            PacienteId = lancamento.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return true;
    }, ct);

    /// <summary>Revalida valor, direção e data na confirmação, inclusive após alteração em outra estação.</summary>
    public Task ConciliarLinhaAsync(int lancamentoId, LinhaExtrato linha, string? operador = null,
        string? conta = null, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            if (!linha.IdentidadeBancariaInformada)
                throw new InvalidOperationException("O OFX não informou o identificador bancário desta transação. Solicite um extrato com FITID.");
            var lancamento = await _repo.ObterLancamentoAsync(lancamentoId, ct)
                ?? throw new InvalidOperationException("Lançamento não encontrado.");
            if (!Combina(lancamento, linha))
                throw new InvalidOperationException("Valor líquido, direção ou data divergente do extrato. Atualize e confira o lançamento.");
            await ConfirmarAsync(lancamentoId, linha.Id, conta, linha.Data, operador, ct);
            return true;
        }, ct);

    public Task ConciliarDepositoAsync(IReadOnlyCollection<int> ids, LinhaExtrato linha,
        string? operador = null, string? conta = null, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            if (!linha.IdentidadeBancariaInformada)
                throw new InvalidOperationException("O OFX não informou o identificador bancário desta transação. Solicite um extrato com FITID.");
            var lote = await _repo.LancamentosPorIdAsync(ids, ct);
            if (ids.Count < 2 || ids.Distinct().Count() != ids.Count || lote.Count != ids.Count
                || !linha.Entrada || lote.Any(l => l.Status != StatusLancamento.Realizado || l.Conciliado
                    || l.Tipo != TipoLancamento.Entrada || l.ModalidadeCartao is null
                    || string.IsNullOrWhiteSpace(l.Adquirente)
                    || Math.Abs(DataDoDinheiro(l).DayNumber - linha.Data.DayNumber) > FolgaDias)
                || lote.Select(l => (l.Adquirente, Dia: DataDoDinheiro(l))).Distinct().Count() != 1
                || lote.Sum(ValorBancario) != linha.ValorAbsoluto)
                throw new InvalidOperationException("O lote mudou ou seu total não corresponde ao depósito. Atualize o extrato.");
            if (await _repo.IdBancarioUtilizadoAsync(linha.Id.Trim(), conta, ct))
                throw new InvalidOperationException("Esta transação bancária já foi conciliada. Atualize o extrato.");
            foreach (var l in lote)
                await ConfirmarAsync(l.Id, linha.Id, conta, linha.Data, operador, ct, validarIdentidade: false);
            return true;
        }, ct);

    /// <summary>
    /// Desfaz a conciliação.
    ///
    /// Existe porque conciliar é humano e errar também: casada a linha errada, sem desfazer
    /// a única saída seria mexer no banco. Não apaga o lançamento nem mexe no valor — só
    /// devolve a linha à lista do que falta conferir.
    /// </summary>
    public Task DesfazerAsync(
        int lancamentoId, string? operador = null, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
    {
        var lancamento = await _repo.ObterLancamentoAsync(lancamentoId, ct)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        if (!lancamento.Conciliado) return false;

        var lote = await _repo.LancamentosDaTransacaoBancariaAsync(lancamento.IdBancario!, lancamento.ContaBancariaConciliacao, ct);
        foreach (var item in lote)
            await DesfazerLancamentoAsync(item, operador, ct);
        await _repo.SalvarAsync(ct);
        return true;
    }, ct);

    private async Task DesfazerLancamentoAsync(LancamentoFinanceiro lancamento, string? operador, CancellationToken ct)
    {

        var idAntigo = lancamento.IdBancario;

        lancamento.ConciliadoEm = null;
        lancamento.IdBancario = null;
        lancamento.ContaBancariaConciliacao = null;
        if (lancamento.DataExtrato is not null && lancamento.PrevisaoRecebimento is not null)
            lancamento.RecebimentoConfirmadoEm = lancamento.RecebimentoAntesDaConciliacao;
        lancamento.DataExtrato = null;
        lancamento.RecebimentoAntesDaConciliacao = null;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ConciliacaoDesfeita",
            Detalhe = $"{lancamento.Descricao} — {lancamento.Valor:C2} · era do extrato {idAntigo}",
            PacienteId = lancamento.PacienteId
        }, ct);

    }

    /// <summary>
    /// Este lançamento pode ser esta linha do extrato?
    ///
    /// O VALOR tem de bater ao centavo — é o que dá confiança ao par —, e a DIREÇÃO
    /// também: uma entrada de R$ 150 no sistema não pode casar com uma saída de R$ 150 no
    /// banco, por mais que os números coincidam.
    /// </summary>
    private static bool Combina(LancamentoFinanceiro l, LinhaExtrato linha)
    {
        // Tributos provisionados são recolhidos separadamente; a adquirente desconta sua taxa.
        var valorBancario = ValorBancario(l);
        if (valorBancario != linha.ValorAbsoluto) return false;

        var entradaNoSistema = l.Tipo == TipoLancamento.Entrada;
        if (entradaNoSistema != linha.Entrada) return false;

        return Math.Abs(DataDoDinheiro(l).DayNumber - linha.Data.DayNumber) <= FolgaDias;
    }

    /// <summary>
    /// A data em que o dinheiro se moveu: o PAGAMENTO quando há, a competência como último
    /// recurso. É a mesma escolha do fluxo de caixa — casar pela competência compararia a
    /// data em que a clínica lançou com a data em que o banco creditou.
    /// </summary>
    private static DateOnly DataDoDinheiro(LancamentoFinanceiro l)
        => l.RecebimentoConfirmadoEm ?? l.PrevisaoRecebimento ?? l.DataPagamento ?? l.Data;

    private static decimal ValorBancario(LancamentoFinanceiro l)
        => l.Tipo != TipoLancamento.Entrada ? l.Valor
            : l.FormaPagamento == FormaPagamento.Convenio || l.CodigoFaturamentoId is not null
                ? l.ValorLiquido // A operadora transfere o líquido de suas retenções.
                : l.Valor - (l.ValorTaxa ?? 0m);
}
