using System.Globalization;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela de CUSTO DE TAXAS E IMPOSTOS do Gerente AFIRMA (out/2026, etapa 6 — a tela saiu
/// do WPF para o Blazor). Era o <c>CustoTransacaoViewModel</c>: a comparação da taxa EFETIVA
/// contra a de tabela, quem passou do teto, a série mensal com buraco no mês sem faturamento e
/// o CSV. Quem lê o banco é a tela, com o MESMO <see cref="CustoTransacaoService.PainelAsync"/>.
/// </summary>
public static class CustoTransacaoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private const string Travessao = "—";

    /// <summary>As janelas oferecidas, em meses — as mesmas do WPF.</summary>
    public static IReadOnlyList<int> Janelas { get; } = [3, 6, 12, 24];

    public const int JanelaPadrao = 12;

    public static PainelDeCusto Montar(
        IReadOnlyList<MesCusto> serie,
        ResumoCustoTransacao resumo,
        IReadOnlyList<CustoAdquirente> adquirentes,
        IReadOnlyDictionary<string, (decimal Menor, decimal Maior)> faixas,
        (DateOnly Inicio, DateOnly Fim) periodo)
    {
        var linhas = adquirentes.Select(a =>
        {
            (decimal Menor, decimal Maior)? faixa = faixas.TryGetValue(a.Adquirente, out var f) ? f : null;
            return new LinhaDeAdquirente(
                a.Adquirente,
                $"{Dinheiro(a.Taxa)} de {Dinheiro(a.Bruto)} ({a.Quantidade})",
                Math.Clamp(a.Fracao, 0, 1),
                Comparacao(faixa, a.TaxaEfetiva),
                AcimaDaTabela(faixa, a.TaxaEfetiva));
        }).ToList();
        var acima = linhas.Count(l => l.AcimaDaTabela);

        return new PainelDeCusto(
            // A mesma janela do PainelAsync (mês corrente inteiro e os N−1 anteriores).
            Periodo: RentabilidadeNaTela.RotuloDoPeriodo(periodo),
            Bruto: Dinheiro(resumo.Bruto),
            Taxa: Dinheiro(resumo.Taxa),
            Imposto: Dinheiro(resumo.Imposto),
            Liquido: Dinheiro(resumo.Liquido),
            PercentualTaxa: Percentual(resumo.PercentualTaxa),
            PercentualImposto: Percentual(resumo.PercentualImposto),
            PercentualDeducoes: Percentual(resumo.PercentualDeducoes),
            MaisCara: resumo.MaisCara is { } m
                ? $"Adquirente que mais descontou: {m.Adquirente} ({Dinheiro(m.Taxa)})"
                : null,
            Resumo: linhas.Count == 0
                ? "Nenhum recebimento no período."
                : acima == 0
                    ? $"{linhas.Count} origem(ns) de recebimento no período."
                    : $"{linhas.Count} origem(ns) · {acima} com taxa efetiva ACIMA do teto de tabela.",
            Adquirentes: linhas,
            Meses: serie.Select(m => new LinhaDeMesCusto(
                m.Rotulo, Dinheiro(m.Bruto), Dinheiro(m.Taxa), Dinheiro(m.Imposto), Dinheiro(m.Liquido),
                // "—" e não "0%": mês sem faturamento não teve dedução de 0%, não teve base.
                Percentual(m.PercentualDeducoes))).ToList(),
            // Mês sem faturamento vira BURACO na série: desenhado como 0% inventaria um mês perfeito.
            SeriePercentual: serie.Select(m => new PontoDaSerie(m.Rotulo, m.PercentualDeducoes is { } p ? (double)p : null)).ToList(),
            SerieDeducoes: serie.Select(m => new PontoDaSerie(m.Rotulo, (double)m.Deducoes)).ToList());
    }

    /// <summary>
    /// A comparação só aparece quando há os dois lados. "Efetiva 3,9%" sozinha não diz se é caro;
    /// inventar uma tabela para comparar seria pior que não comparar.
    /// </summary>
    public static string Comparacao((decimal Menor, decimal Maior)? faixa, decimal? efetiva) => (faixa, efetiva) switch
    {
        (null, { } e) => $"efetiva {Pct(e)} · sem taxa cadastrada para comparar",
        ({ } f, { } e) when f.Menor == f.Maior => $"efetiva {Pct(e)} · tabela {Pct(f.Menor)}",
        ({ } f, { } e) => $"efetiva {Pct(e)} · tabela {Pct(f.Menor)} a {Pct(f.Maior)}",
        _ => "sem base para medir"
    };

    /// <summary>O efetivo passou do TETO da tabela — o único caso em que há o que cobrar da adquirente.</summary>
    public static bool AcimaDaTabela((decimal Menor, decimal Maior)? faixa, decimal? efetiva)
        => faixa is { } f && efetiva is { } e && e > f.Maior;

    /// <summary>A série e a quebra em CSV, com os MESMOS cabeçalhos do WPF.</summary>
    public static (IReadOnlyList<string> Cabecalho, IReadOnlyList<IReadOnlyList<string>> Linhas) Csv(PainelDeCusto painel)
    {
        var linhas = new List<IReadOnlyList<string>>();
        foreach (var m in painel.Meses)
            linhas.Add(["Mes", m.Mes, m.Bruto, m.Taxa, m.Imposto, m.Liquido, m.Percentual]);
        foreach (var a in painel.Adquirentes)
            linhas.Add(["Adquirente", a.Rotulo, a.ValorRotulo, "", "", "", a.Comparacao]);
        return (["Tipo", "Nome", "Bruto", "Taxa", "Imposto", "Liquido", "Deducoes %"], linhas);
    }

    /// <summary>Null vira "—", nunca 0%: não medido e zero são coisas diferentes.</summary>
    private static string Percentual(decimal? valor) => valor is { } v ? Pct(v) : Travessao;
    private static string Pct(decimal v) => $"{v.ToString("0.##", PtBr)}%";
    private static string Dinheiro(decimal v) => v.ToString("C", PtBr);
}

public sealed record LinhaDeAdquirente(string Rotulo, string ValorRotulo, double Fracao, string Comparacao, bool AcimaDaTabela);

public sealed record LinhaDeMesCusto(string Mes, string Bruto, string Taxa, string Imposto, string Liquido, string Percentual);

public sealed record PainelDeCusto(
    string Periodo, string Bruto, string Taxa, string Imposto, string Liquido,
    string PercentualTaxa, string PercentualImposto, string PercentualDeducoes,
    string? MaisCara, string Resumo,
    IReadOnlyList<LinhaDeAdquirente> Adquirentes,
    IReadOnlyList<LinhaDeMesCusto> Meses,
    IReadOnlyList<PontoDaSerie> SeriePercentual,
    IReadOnlyList<PontoDaSerie> SerieDeducoes);
