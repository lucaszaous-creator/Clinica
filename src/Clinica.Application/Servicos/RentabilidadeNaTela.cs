using System.Globalization;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a tela de RENTABILIDADE POR CONVÊNIO AFIRMA (out/2026, etapa 6 — a tela saiu do WPF
/// para o Blazor). Era o <c>RentabilidadeConvenioViewModel</c> com a <c>LinhaConvenio</c>: a
/// janela de meses, as frases de pendência, a comparação "rende mais / rende menos" e o CSV.
/// Quem lê o banco é a tela, com o MESMO <see cref="RentabilidadeConvenioService"/>.
/// </summary>
public static class RentabilidadeNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private const string Travessao = "—";

    /// <summary>As janelas oferecidas, em meses — as mesmas do WPF.</summary>
    public static IReadOnlyList<int> Janelas { get; } = [1, 3, 6, 12];

    public const int JanelaPadrao = 6;

    /// <summary>
    /// Do primeiro dia do mês (N−1 meses atrás) ao último dia do mês corrente — o período é o
    /// do ATENDIMENTO, e o mês corrente entra inteiro (a guia de hoje pode já ter sido baixada).
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fim) Intervalo(DateOnly hoje, int meses)
    {
        meses = Math.Max(1, meses);
        var primeiroDoMes = new DateOnly(hoje.Year, hoje.Month, 1);
        return (primeiroDoMes.AddMonths(-(meses - 1)), primeiroDoMes.AddMonths(1).AddDays(-1));
    }

    public static string RotuloDoPeriodo((DateOnly Inicio, DateOnly Fim) p)
        => $"{p.Inicio.ToString("MM/yyyy", PtBr)} a {p.Fim.ToString("MM/yyyy", PtBr)}";

    public static PainelDeRentabilidade Montar(IReadOnlyList<RentabilidadeConvenio> porConvenio, (DateOnly Inicio, DateOnly Fim) periodo)
    {
        var r = RentabilidadeConvenioService.Resumir(porConvenio);
        var linhas = porConvenio.Select(LinhaDe).ToList();
        var pendentes = linhas.Count(l => l.TemPendencia);

        return new PainelDeRentabilidade(
            Periodo: RotuloDoPeriodo(periodo),
            Guias: r.Guias,
            Bruto: Dinheiro(r.Bruto),
            Retido: Dinheiro(r.Retido),
            Liquido: Dinheiro(r.Liquido),
            PercentualRetido: r.PercentualRetido is { } p ? $"{p.ToString("0.##", PtBr)}%" : Travessao,
            TaxaGlosa: r.TaxaGlosa is { } g ? $"{g.ToString("0.#", PtBr)}%" : Travessao,
            // A comparação só aparece com DUAS operadoras medidas: comparar uma consigo mesma
            // não diz nada, e escrever "a melhor é a única" seria ruído. E com EMPATE no líquido
            // por guia o serviço devolve a mesma operadora nas duas pontas — "rende mais: X ·
            // rende menos: X" é a mesma frase sem informação nenhuma.
            MelhorEPior: r.MaisRentavel is { } melhor && r.MenosRentavel is { } pior
                         && melhor.LiquidoPorGuia != pior.LiquidoPorGuia
                ?$"Rende mais por atendimento: {melhor.Convenio} ({Dinheiro(melhor.LiquidoPorGuia!.Value)}/guia) · "
                  + $"rende menos: {pior.Convenio} ({Dinheiro(pior.LiquidoPorGuia!.Value)}/guia)"
                : null,
            Resumo: linhas.Count == 0
                ? "Nenhuma guia no período."
                : pendentes == 0
                    ? $"{linhas.Count} convênio(s) no período."
                    : $"{linhas.Count} convênio(s) · {pendentes} com guia efetivada e SEM receita lançada.",
            ComPendencia: pendentes,
            Linhas: linhas);
    }

    public static LinhaDeRentabilidade LinhaDe(RentabilidadeConvenio c) => new(
        // `Convenio` JÁ vem resolvido pelo serviço (CatalogoConvenios.Nome(codigo)): resolver de
        // novo devolveria a família padrão para toda operadora.
        Convenio: c.Convenio,
        Guias: c.Guias,
        Bruto: Dinheiro(c.Bruto),
        // Zero retido vira "—": a operadora que não retém e a que não pagou nada apareceriam
        // iguais escritas como "R$ 0,00".
        Retido: c.Retido > 0m ? Dinheiro(c.Retido) : Travessao,
        Liquido: Dinheiro(c.Liquido),
        // Média sobre nenhuma guia recebida é invenção.
        PorGuia: c.LiquidoPorGuia is { } m ? Dinheiro(m) : Travessao,
        Prazo: c.PrazoMedioDias is { } d ? $"{d.ToString("0.#", PtBr)} dias" : Travessao,
        Pendencias: Pendencias(c),
        TemPendencia: c.SemReceita > 0,
        Fracao: Math.Clamp(c.Fracao, 0, 1));

    /// <summary>Guia efetivada sem receita é trabalho feito e não cobrado; glosa vem ao lado.</summary>
    public static string Pendencias(RentabilidadeConvenio c) => c.SemReceita == 0
        ? (c.Glosadas == 0 ? Travessao : $"{c.Glosadas} glosada(s)")
        : c.Glosadas == 0
            ? $"{c.SemReceita} sem receita"
            : $"{c.SemReceita} sem receita · {c.Glosadas} glosada(s)";

    /// <summary>O CSV com os MESMOS cabeçalhos do WPF — é com ele que a clínica renegocia tabela.</summary>
    public static (IReadOnlyList<string> Cabecalho, IReadOnlyList<IReadOnlyList<string>> Linhas) Csv(PainelDeRentabilidade painel)
        => (["Convenio", "Guias", "Bruto", "Retido", "Liquido", "Liquido/guia", "Prazo medio", "Pendencias"],
            painel.Linhas.Select(c => (IReadOnlyList<string>)
                [c.Convenio, c.Guias.ToString(PtBr), c.Bruto, c.Retido, c.Liquido, c.PorGuia, c.Prazo, c.Pendencias]).ToList());

    private static string Dinheiro(decimal v) => v.ToString("C", PtBr);
}

public sealed record LinhaDeRentabilidade(
    string Convenio, int Guias, string Bruto, string Retido, string Liquido, string PorGuia,
    string Prazo, string Pendencias, bool TemPendencia, double Fracao);

public sealed record PainelDeRentabilidade(
    string Periodo, int Guias, string Bruto, string Retido, string Liquido,
    string PercentualRetido, string TaxaGlosa, string? MelhorEPior, string Resumo,
    int ComPendencia, IReadOnlyList<LinhaDeRentabilidade> Linhas);
