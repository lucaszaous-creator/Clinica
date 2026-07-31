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
