using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Um mês do fluxo de caixa.
///
/// Realizado e previsto vêm SEPARADOS de propósito. Somados num número só, o mês que já
/// aconteceu e o mês que ainda vai acontecer ficariam com a mesma cara — e a direção
/// tomaria decisão sobre expectativa achando que era medida. É a mesma regra que faz o
/// painel de pendências mostrar um terceiro estado quando a checagem não rodou.
/// </summary>
public sealed record MesFluxo(
    int Ano,
    int Mes,
    string Rotulo,
    decimal EntradasRealizadas,
    decimal SaidasRealizadas,
    decimal EntradasPrevistas,
    decimal SaidasPrevistas)
{
    /// <summary>O que de fato entrou menos o que de fato saiu.</summary>
    public decimal Realizado => EntradasRealizadas - SaidasRealizadas;

    /// <summary>O que ainda deve entrar menos o que ainda deve sair.</summary>
    public decimal Previsto => EntradasPrevistas - SaidasPrevistas;

    /// <summary>Os dois juntos — o resultado do mês se tudo o que está previsto acontecer.</summary>
    public decimal Total => Realizado + Previsto;

    /// <summary>Houve movimento algum no mês. Mês vazio e mês zerado não são a mesma coisa.</summary>
    public bool Vazio =>
        EntradasRealizadas == 0m && SaidasRealizadas == 0m
        && EntradasPrevistas == 0m && SaidasPrevistas == 0m;
}

/// <summary>
/// Uma linha do resultado por categoria: quanto entrou (ou saiu) sob aquele rótulo.
/// </summary>
public sealed record ResultadoCategoria(
    string Categoria,
    TipoLancamento Tipo,
    decimal Total,
    int Quantidade)
{
    /// <summary>Fatia do total do mesmo tipo, de 0 a 1 — o tamanho da barra na tela.</summary>
    public double Fracao { get; init; }
}

/// <summary>
/// O retrato do período: totais, a maior entrada e a maior saída.
/// </summary>
public sealed record ResumoFluxo(
    decimal Entradas,
    decimal Saidas,
    ResultadoCategoria? MaiorEntrada,
    ResultadoCategoria? MaiorSaida)
{
    public decimal Resultado => Entradas - Saidas;

    /// <summary>
    /// Quanto de cada real que entra sobra depois das saídas, de 0 a 1. Null quando não
    /// entrou nada — e null NÃO é zero: "não sobrou nada" e "não teve receita para
    /// sobrar" são diagnósticos diferentes, e a tela mostra "—" no segundo caso.
    /// </summary>
    public double? Margem => Entradas > 0m ? (double)(Resultado / Entradas) : null;
}

/// <summary>
/// Fluxo de caixa e resultado por categoria (parcela 13).
///
/// Responde às duas perguntas que a direção faz e que nenhuma tela respondia: "como está
/// o mês comparado com os anteriores?" e "para onde está indo o dinheiro?". O Caixa
/// mostra o extrato do período, a Produção mostra o volume do faturamento — nenhum dos
/// dois mostra a SÉRIE, e é a série que revela tendência.
///
/// Duas honestidades embutidas, e as duas custam número bonito:
///
/// 1. **Realizado e previsto nunca viram um número só.** Somá-los daria uma linha lisa e
///    mentirosa: o mês que já fechou é medida, o que ainda vai vencer é expectativa.
/// 2. **Não existe "saldo em conta".** A clínica nunca cadastrou saldo inicial, então o
///    acumulado que este serviço entrega é a VARIAÇÃO no período consultado, não o
///    dinheiro que há no banco. Chamar isso de saldo seria dar à direção um número que
///    ela conferiria com o extrato e não bateria nunca.
/// </summary>
public sealed class FluxoCaixaService
{
    private readonly IClinicaRepositorio _repo;

    public FluxoCaixaService(IClinicaRepositorio repo) => _repo = repo;

    private static readonly string[] Meses =
        ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

    /// <summary>
    /// Os últimos <paramref name="meses"/> meses terminando no mês de
    /// <paramref name="referencia"/>, do mais antigo para o mais novo — a ordem em que o
    /// gráfico desenha.
    ///
    /// Mês sem movimento entra na série ZERADO em vez de ser omitido: buraco na linha
    /// sugeriria "não medido", que aqui seria falso — o mês foi medido e não teve nada.
    /// </summary>
    public async Task<IReadOnlyList<MesFluxo>> ProjecaoAsync(
        DateOnly referencia, int meses = 12, CancellationToken ct = default)
    {
        if (meses < 1) meses = 1;

        var primeiro = new DateOnly(referencia.Year, referencia.Month, 1).AddMonths(-(meses - 1));
        var ultimo = new DateOnly(referencia.Year, referencia.Month, 1)
            .AddMonths(1).AddDays(-1);

        var lancamentos = await _repo.LancamentosDatadosNoPeriodoAsync(primeiro, ultimo, ct);

        var serie = new List<MesFluxo>();
        for (var i = 0; i < meses; i++)
        {
            var mes = primeiro.AddMonths(i);
            var doMes = lancamentos
                .Where(l => l.DataDoFluxo.Year == mes.Year && l.DataDoFluxo.Month == mes.Month)
                .ToList();

            decimal Somar(TipoLancamento tipo, StatusLancamento status) => doMes
                .Where(l => l.Tipo == tipo && l.Status == status)
                .Sum(l => l.Valor);

            serie.Add(new MesFluxo(
                mes.Year, mes.Month,
                $"{Meses[mes.Month - 1]}/{mes.Year % 100:00}",
                Somar(TipoLancamento.Entrada, StatusLancamento.Realizado),
                Somar(TipoLancamento.Saida, StatusLancamento.Realizado),
                Somar(TipoLancamento.Entrada, StatusLancamento.Previsto),
                Somar(TipoLancamento.Saida, StatusLancamento.Previsto)));
        }

        return serie;
    }

    /// <summary>
    /// Variação acumulada ao longo da série — o quanto o caixa subiu ou desceu desde o
    /// início do período consultado.
    ///
    /// NÃO é saldo em conta, e o nome diz isso. A clínica nunca cadastrou saldo inicial;
    /// somar os meses e chamar o resultado de "saldo" daria um número que a direção
    /// conferiria com o extrato do banco e que não bateria nunca — e o erro apareceria
    /// como desconfiança no sistema inteiro, não como um rótulo errado.
    /// </summary>
    public static IReadOnlyList<(MesFluxo Mes, decimal Acumulado)> Acumular(
        IReadOnlyList<MesFluxo> serie)
    {
        var acumulado = 0m;
        var lista = new List<(MesFluxo, decimal)>(serie.Count);
        foreach (var m in serie)
        {
            acumulado += m.Total;
            lista.Add((m, acumulado));
        }
        return lista;
    }

    /// <summary>
    /// Para onde está indo o dinheiro: totais por categoria no período, do maior para o
    /// menor, dentro de cada tipo.
    ///
    /// Cancelado fica de fora (não é dinheiro). Lançamento sem categoria vira "Sem
    /// categoria" em vez de sumir: dinheiro não classificado é justamente o que a direção
    /// precisa ver para mandar classificar, e escondê-lo faria as fatias somarem menos
    /// que o total sem explicação.
    /// </summary>
    public async Task<IReadOnlyList<ResultadoCategoria>> PorCategoriaAsync(
        DateOnly inicio, DateOnly fim, bool incluirPrevisto = false, CancellationToken ct = default)
    {
        var lancamentos = await _repo.LancamentosDatadosNoPeriodoAsync(inicio, fim, ct);

        var considerados = lancamentos
            .Where(l => l.Status == StatusLancamento.Realizado
                        || (incluirPrevisto && l.Status == StatusLancamento.Previsto))
            .ToList();

        var agrupado = considerados
            .GroupBy(l => (l.Tipo, Categoria: string.IsNullOrWhiteSpace(l.Categoria)
                ? "Sem categoria"
                : l.Categoria!))
            .Select(g => new ResultadoCategoria(
                g.Key.Categoria, g.Key.Tipo, g.Sum(l => l.Valor), g.Count()))
            .ToList();

        // A fração é do total DO MESMO TIPO: comparar uma despesa com o total geral
        // (receita + despesa) daria barras que não somam 100% e não querem dizer nada.
        var totalPorTipo = agrupado
            .GroupBy(r => r.Tipo)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Total));

        return agrupado
            .Select(r => r with
            {
                Fracao = totalPorTipo[r.Tipo] > 0m ? (double)(r.Total / totalPorTipo[r.Tipo]) : 0d
            })
            .OrderBy(r => r.Tipo)
            .ThenByDescending(r => r.Total)
            .ToList();
    }

    /// <summary>Retrato do período: totais, maior entrada, maior saída e margem.</summary>
    public async Task<ResumoFluxo> ResumoAsync(
        DateOnly inicio, DateOnly fim, bool incluirPrevisto = false, CancellationToken ct = default)
    {
        var categorias = await PorCategoriaAsync(inicio, fim, incluirPrevisto, ct);

        decimal Total(TipoLancamento tipo) =>
            categorias.Where(c => c.Tipo == tipo).Sum(c => c.Total);

        ResultadoCategoria? Maior(TipoLancamento tipo) => categorias
            .Where(c => c.Tipo == tipo)
            .OrderByDescending(c => c.Total)
            .FirstOrDefault();

        return new ResumoFluxo(
            Total(TipoLancamento.Entrada),
            Total(TipoLancamento.Saida),
            Maior(TipoLancamento.Entrada),
            Maior(TipoLancamento.Saida));
    }
}
