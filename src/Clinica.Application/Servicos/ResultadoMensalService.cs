using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Uma linha do resultado: a categoria e quanto ela pesou no mês.</summary>
public sealed record LinhaResultado(
    string Categoria,
    decimal Valor,
    double Fracao);

/// <summary>
/// O resultado do mês, no formato em que o contador e a direção conversam.
///
/// <see cref="Margem"/> é nula quando não houve receita — margem sobre zero não é 0%,
/// é conta que não existe, e mostrá-la como zero faria um mês sem faturamento parecer um
/// mês que faturou e não sobrou nada.
/// </summary>
public sealed record ResultadoMensal(
    int Ano,
    int Mes,
    decimal Receita,
    decimal Deducoes,
    decimal Despesas,
    IReadOnlyList<LinhaResultado> Entradas,
    IReadOnlyList<LinhaResultado> Saidas)
{
    /// <summary>Receita menos o que a operadora e o fisco já tiraram dela.</summary>
    public decimal ReceitaLiquida => Receita - Deducoes;

    /// <summary>O que sobrou. Pode ser negativo, e a tela mostra o sinal.</summary>
    public decimal Resultado => ReceitaLiquida - Despesas;

    /// <summary>Quanto sobra de cada real faturado. Nula sem receita.</summary>
    public double? Margem => Receita > 0m
        ? Math.Round((double)(Resultado / Receita) * 100.0, 1)
        : null;

    public bool Prejuizo => Resultado < 0m;

    public bool Vazio => Receita == 0m && Despesas == 0m;
}

/// <summary>
/// O resultado do mês por COMPETÊNCIA (out/2026): o que pertence ao mês pela data do fato,
/// recebido ou não, pago ou não. É o par do <see cref="ResultadoMensal"/> (caixa) e se lê ao
/// lado dele — os dois juntos dizem quanto do mês ainda está para entrar e para sair.
///
/// Só entra o que tem LANÇAMENTO. A produção que ainda não ganhou valor no financeiro (guia
/// baixada sem receita, sessão particular sem pagamento registrado) não é somada a preço de
/// tabela: ela é CONTADA em <see cref="GuiasSemReceita"/> e
/// <see cref="SessoesParticularesSemReceita"/>, e a tela diz que o número fica abaixo do real.
/// Nulo nessas duas contagens é "não pôde ser conferido", nunca "nenhuma".
/// </summary>
public sealed record ResultadoPorCompetencia(
    int Ano,
    int Mes,
    decimal ReceitaRecebida,
    decimal ReceitaAReceber,
    decimal Deducoes,
    decimal DespesasPagas,
    decimal DespesasAPagar,
    int? GuiasSemReceita,
    int? SessoesParticularesSemReceita)
{
    /// <summary>Tudo o que o mês faturou, recebido ou não.</summary>
    public decimal Receita => ReceitaRecebida + ReceitaAReceber;

    /// <summary>Tudo o que o mês gastou, pago ou não.</summary>
    public decimal Despesas => DespesasPagas + DespesasAPagar;

    public decimal ReceitaLiquida => Receita - Deducoes;

    public decimal Resultado => ReceitaLiquida - Despesas;

    /// <summary>Mesma regra do caixa: sem receita, a margem não existe (não é 0%).</summary>
    public double? Margem => Receita > 0m
        ? Math.Round((double)(Resultado / Receita) * 100.0, 1)
        : null;

    public bool Prejuizo => Resultado < 0m;

    public bool Vazio => Receita == 0m && Despesas == 0m;

    /// <summary>Quanto da produção do mês ainda não tem valor no financeiro. Nulo = não conferido.</summary>
    public int? ProducaoSemValor => GuiasSemReceita is { } g && SessoesParticularesSemReceita is { } s ? g + s : null;
}

/// <summary>
/// O resultado do mês — a leitura que faltava entre o extrato e o indicador (parcela 31).
///
/// O Financeiro responde "o que entrou e saiu" (Caixa), "como isso se distribui no tempo"
/// (Fluxo de caixa) e "quanto de imposto" (Apuração). Nenhuma dessas responde a pergunta
/// que a direção faz para o contador todo mês: **sobrou quanto, e de onde veio o que
/// sobrou?**
///
/// Três decisões que mudam o número:
///
/// 1. **É regime de CAIXA, não de competência.** Entra o que se moveu no mês (realizado
///    pela data de pagamento). Um DRE de competência exigiria provisão e rateio que esta
///    clínica não mantém — e um número aproximado apresentado como contábil é pior do que
///    um número honesto apresentado como caixa. A tela diz isso.
/// 2. **Taxa e imposto são DEDUÇÃO, não despesa.** Eles saem da receita antes de ela
///    existir (a maquininha desconta, a operadora retém), e listá-los junto do aluguel
///    faria a clínica achar que pode cortá-los. O valor do lançamento continua sendo o
///    BRUTO, como em todo o resto do sistema.
/// 3. **Lançamento sem categoria aparece como "Sem categoria"**, nunca some — mesma regra
///    do fluxo de caixa. Sumir faria as linhas não fecharem com o total, e o primeiro
///    reflexo de quem confere é desconfiar do total.
/// </summary>
public sealed class ResultadoMensalService
{
    private readonly IClinicaRepositorio _repo;

    public ResultadoMensalService(IClinicaRepositorio repo) => _repo = repo;

    public async Task<ResultadoMensal> DoMesAsync(
        int ano, int mes, CancellationToken ct = default)
    {
        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var lancamentos = (await _repo.LancamentosDatadosNoPeriodoAsync(inicio, fim, ct))
            // Só o REALIZADO: previsto é promessa, e promessa no resultado do mês
            // transformaria o DRE numa projeção sem dizer que é.
            .Where(l => l.Status == StatusLancamento.Realizado)
            .Where(l => l.DataDoFluxo >= inicio && l.DataDoFluxo <= fim)
            .ToList();

        var entradas = Agrupar(lancamentos.Where(l => l.Tipo == TipoLancamento.Entrada));
        var saidas = Agrupar(lancamentos.Where(l => l.Tipo == TipoLancamento.Saida));

        var receita = lancamentos
            .Where(l => l.Tipo == TipoLancamento.Entrada)
            .Sum(l => l.Valor);

        var despesas = lancamentos
            .Where(l => l.Tipo == TipoLancamento.Saida)
            .Sum(l => l.Valor);

        // As deduções vêm do que foi descontado de cada recebimento, não de uma categoria
        // de despesa: elas nunca chegaram à conta da clínica.
        var recebimentos = await _repo.RecebimentosComDeducaoAsync(inicio, fim, ct);
        var deducoes = recebimentos.Sum(r => (r.ValorTaxa ?? 0m) + (r.ValorImposto ?? 0m));

        return new ResultadoMensal(ano, mes, receita, deducoes, despesas, entradas, saidas);
    }

    /// <summary>
    /// O mesmo mês por COMPETÊNCIA (out/2026): a data do FATO (<c>Data</c> do lançamento),
    /// realizado e previsto juntos — cancelado já fica fora na leitura. As deduções são as
    /// copiadas nas entradas do mês do fato, pela mesma razão: a taxa pertence à venda.
    ///
    /// As duas contagens da produção sem valor falham SOZINHAS: não conseguir contar as
    /// guias não apaga o resultado, só tira da tela a frase "fica abaixo do real".
    /// </summary>
    public async Task<ResultadoPorCompetencia> CompetenciaDoMesAsync(
        int ano, int mes, CancellationToken ct = default)
    {
        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var lancamentos = await _repo.LancamentosDatadosNoPeriodoAsync(inicio, fim, ct);

        int? guias = null;
        try
        {
            guias = (await new FinanceiroService(_repo).GuiasSemLancamentoAsync(inicio, fim, ct)).Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Diagnostico.Registrar("Resultado por competência — guias sem receita não puderam ser contadas", ex);
        }

        int? sessoes = null;
        try
        {
            sessoes = (await _repo.SessoesParticularesSemReceitaAsync(inicio, fim, ct)).Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Diagnostico.Registrar("Resultado por competência — sessões particulares sem receita não puderam ser contadas", ex);
        }

        return ApurarCompetencia(ano, mes, lancamentos, guias, sessoes);
    }

    /// <summary>
    /// A conta da competência, pura: só o que tem a data do FATO dentro do mês. A leitura
    /// traz também quem só VENCE ou só foi PAGO no mês (o fluxo precisa deles), e é aqui que
    /// eles saem — senão a conta de julho paga em agosto entraria nos dois meses.
    /// </summary>
    public static ResultadoPorCompetencia ApurarCompetencia(
        int ano, int mes, IReadOnlyList<Modelos.LancamentoDatado> lancamentos,
        int? guiasSemReceita, int? sessoesSemReceita)
    {
        var doMes = lancamentos
            .Where(l => l.Status != StatusLancamento.Cancelado)
            .Where(l => l.Data.Year == ano && l.Data.Month == mes)
            .ToList();

        decimal Somar(TipoLancamento tipo, StatusLancamento status) => doMes
            .Where(l => l.Tipo == tipo && l.Status == status)
            .Sum(l => l.Valor);

        var deducoes = doMes
            .Where(l => l.Tipo == TipoLancamento.Entrada)
            .Sum(l => (l.ValorTaxa ?? 0m) + (l.ValorImposto ?? 0m));

        return new ResultadoPorCompetencia(
            ano, mes,
            Somar(TipoLancamento.Entrada, StatusLancamento.Realizado),
            Somar(TipoLancamento.Entrada, StatusLancamento.Previsto),
            deducoes,
            Somar(TipoLancamento.Saida, StatusLancamento.Realizado),
            Somar(TipoLancamento.Saida, StatusLancamento.Previsto),
            guiasSemReceita,
            sessoesSemReceita);
    }

    private static IReadOnlyList<LinhaResultado> Agrupar(
        IEnumerable<Modelos.LancamentoDatado> lancamentos)
    {
        var lista = lancamentos.ToList();
        var total = lista.Sum(l => l.Valor);

        return lista
            // Sem categoria aparece nomeado, nunca some: sumir faria as linhas não
            // fecharem com o total, e quem confere desconfia do total primeiro.
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Categoria) ? "Sem categoria" : l.Categoria!)
            .Select(g =>
            {
                var valor = g.Sum(l => l.Valor);
                return new LinhaResultado(
                    g.Key, valor,
                    // A fração é do total do MESMO TIPO: dividir despesa por receita daria
                    // uma barra que não soma 100 e não significa nada.
                    total > 0m ? (double)(valor / total) : 0);
            })
            .OrderByDescending(l => l.Valor)
            .ToList();
    }
}
