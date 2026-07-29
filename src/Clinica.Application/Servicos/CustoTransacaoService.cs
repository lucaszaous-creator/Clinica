using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Quanto uma adquirente custou no período, e o que isso dá em percentual.
///
/// <paramref name="TaxaEfetiva"/> é o que de fato foi descontado sobre o que de fato
/// passou — não a taxa de tabela. As duas divergem sempre que a clínica parcela mais do
/// que imagina, e é essa divergência que dá argumento na renegociação: "a tabela diz 3,1%,
/// e eu paguei 3,9%".
/// </summary>
public sealed record CustoAdquirente(
    string Adquirente,
    decimal Bruto,
    decimal Taxa,
    int Quantidade)
{
    public decimal Liquido => Bruto - Taxa;

    /// <summary>
    /// Percentual efetivo. Null quando nada passou pela adquirente — e null não é zero:
    /// "não custou nada" e "não passou nada" são leituras diferentes.
    /// </summary>
    public decimal? TaxaEfetiva => Bruto > 0m ? Taxa / Bruto * 100m : null;

    /// <summary>Fatia do custo total de maquininha, de 0 a 1 — o tamanho da barra na tela.</summary>
    public double Fracao { get; init; }
}

/// <summary>Um mês na série do custo de transação.</summary>
public sealed record MesCusto(
    int Ano,
    int Mes,
    string Rotulo,
    decimal Bruto,
    decimal Taxa,
    decimal Imposto)
{
    public decimal Deducoes => Taxa + Imposto;

    public decimal Liquido => Bruto - Deducoes;

    /// <summary>
    /// Quanto por cento do faturamento foi embora em taxa e imposto. Null sem faturamento
    /// no mês — a tela mostra "—", nunca 0%, porque não medido e zero são coisas
    /// diferentes.
    /// </summary>
    public decimal? PercentualDeducoes => Bruto > 0m ? Deducoes / Bruto * 100m : null;
}

/// <summary>O retrato do período para a direção.</summary>
public sealed record ResumoCustoTransacao(
    decimal Bruto,
    decimal Taxa,
    decimal Imposto,
    CustoAdquirente? MaisCara)
{
    public decimal Deducoes => Taxa + Imposto;

    public decimal Liquido => Bruto - Deducoes;

    public decimal? PercentualDeducoes => Bruto > 0m ? Deducoes / Bruto * 100m : null;

    public decimal? PercentualTaxa => Bruto > 0m ? Taxa / Bruto * 100m : null;

    public decimal? PercentualImposto => Bruto > 0m ? Imposto / Bruto * 100m : null;
}

/// <summary>
/// Custo de transação na visão da DIREÇÃO (parcela 17).
///
/// A cliente pediu taxas e impostos "no Financeiro e no Gerente", e as duas telas
/// respondem perguntas diferentes: o Financeiro CADASTRA (qual é a taxa da Cielo, qual é
/// a alíquota do ISS), a direção quer saber **quanto isso custou**.
///
/// Duas leituras que só existem aqui:
///
/// 1. **A taxa EFETIVA contra a de tabela.** A tabela diz 3,1%; se a clínica parcela mais
///    do que imagina, o efetivo do ano é 3,9%. É a diferença entre as duas que dá
///    argumento na renegociação — e ninguém a calculava.
/// 2. **A dedução como percentual do faturamento.** "Foram R$ 14.200 em taxa" é um número
///    grande sem referência; "9,4% de tudo o que entrou" é uma decisão.
///
/// Trabalha só sobre ENTRADA REALIZADA: taxa de recebimento que ainda não aconteceu é
/// desconto de receita que não existe.
/// </summary>
public sealed class CustoTransacaoService
{
    private readonly IClinicaRepositorio _repo;

    public CustoTransacaoService(IClinicaRepositorio repo) => _repo = repo;

    private static readonly string[] Meses =
        ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

    /// <summary>
    /// Custo por adquirente no período, do mais caro para o mais barato.
    ///
    /// Recebimento sem adquirente informado (dinheiro, Pix, convênio) entra como
    /// "(sem maquininha)" em vez de sumir: ele compõe o faturamento sobre o qual a taxa
    /// efetiva geral é medida, e escondê-lo faria o percentual do total parecer maior do
    /// que é.
    /// </summary>
    public async Task<IReadOnlyList<CustoAdquirente>> PorAdquirenteAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var recebimentos = await _repo.RecebimentosComDeducaoAsync(inicio, fim, ct);

        var linhas = recebimentos
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Adquirente) ? "(sem maquininha)" : l.Adquirente!)
            .Select(g => new CustoAdquirente(
                g.Key,
                g.Sum(l => l.Valor),
                g.Sum(l => l.ValorTaxa ?? 0m),
                g.Count()))
            .ToList();

        // A fração é do custo de MAQUININHA, não do faturamento: a pergunta desta barra é
        // "qual adquirente come mais", e incluir o que não tem taxa daria uma fatia
        // gigante para "(sem maquininha)", que por definição custa zero.
        var totalTaxa = linhas.Sum(l => l.Taxa);

        return linhas
            .Select(l => l with { Fracao = totalTaxa > 0m ? (double)(l.Taxa / totalTaxa) : 0d })
            .OrderByDescending(l => l.Taxa)
            .ThenByDescending(l => l.Bruto)
            .ToList();
    }

    /// <summary>
    /// Série mensal do custo, do mês mais antigo para o mais novo — a ordem do gráfico.
    /// Mês sem faturamento entra zerado em vez de sumir: o mês foi medido e não teve nada.
    /// </summary>
    public async Task<IReadOnlyList<MesCusto>> SerieAsync(
        DateOnly referencia, int meses = 12, CancellationToken ct = default)
    {
        if (meses < 1) meses = 1;

        var primeiro = new DateOnly(referencia.Year, referencia.Month, 1).AddMonths(-(meses - 1));
        var ultimo = new DateOnly(referencia.Year, referencia.Month, 1).AddMonths(1).AddDays(-1);

        var recebimentos = await _repo.RecebimentosComDeducaoAsync(primeiro, ultimo, ct);

        var serie = new List<MesCusto>(meses);
        for (var i = 0; i < meses; i++)
        {
            var mes = primeiro.AddMonths(i);
            var doMes = recebimentos
                .Where(l => l.Dia.Year == mes.Year && l.Dia.Month == mes.Month)
                .ToList();

            serie.Add(new MesCusto(
                mes.Year, mes.Month,
                $"{Meses[mes.Month - 1]}/{mes.Year % 100:00}",
                doMes.Sum(l => l.Valor),
                doMes.Sum(l => l.ValorTaxa ?? 0m),
                doMes.Sum(l => l.ValorImposto ?? 0m)));
        }

        return serie;
    }

    /// <summary>Retrato do período: bruto, deduções, líquido e a adquirente mais cara.</summary>
    public async Task<ResumoCustoTransacao> ResumoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var recebimentos = await _repo.RecebimentosComDeducaoAsync(inicio, fim, ct);
        var porAdquirente = await PorAdquirenteAsync(inicio, fim, ct);

        return new ResumoCustoTransacao(
            recebimentos.Sum(l => l.Valor),
            recebimentos.Sum(l => l.ValorTaxa ?? 0m),
            recebimentos.Sum(l => l.ValorImposto ?? 0m),
            // "Mais cara" é a que mais DESCONTOU em dinheiro, não a de maior percentual:
            // uma taxa de 5% em duas vendas não é o problema da clínica.
            porAdquirente.FirstOrDefault(a => a.Taxa > 0m));
    }

    /// <summary>
    /// A taxa de TABELA que valeria hoje para uma adquirente, para comparar com a efetiva.
    ///
    /// Devolve a faixa (menor e maior percentual vigente), porque uma adquirente tem várias
    /// taxas — débito, crédito à vista, parcelado. Um número só teria de escolher uma
    /// modalidade e a comparação com o efetivo do período, que mistura todas, seria
    /// enganosa. Null quando não há taxa cadastrada.
    /// </summary>
    public async Task<(decimal Menor, decimal Maior)?> FaixaDeTabelaAsync(
        string adquirente, DateOnly dia, CancellationToken ct = default)
    {
        var vigentes = (await _repo.TaxasCartaoAsync(somenteAtivas: true, ct))
            .Where(t => string.Equals(t.Adquirente, adquirente, StringComparison.OrdinalIgnoreCase))
            .Where(t => t.VigenteEm(dia))
            .Select(t => t.Percentual)
            .ToList();

        return vigentes.Count == 0 ? null : (vigentes.Min(), vigentes.Max());
    }
}
