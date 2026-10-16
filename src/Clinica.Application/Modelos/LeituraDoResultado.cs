using System.Globalization;
using Clinica.Application.Servicos;

namespace Clinica.Application.Modelos;

/// <summary>A gravidade de um teto, para a tela escolher a cor: o que estourou, o que está perto, o resto.</summary>
public enum GravidadeDoTeto { Normal, Atencao, Estourou }

/// <summary>
/// O que a tela de Resultado do mês e teto de gasto AFIRMA (out/2026, na migração para
/// Blazor). Morava no <c>ResultadoViewModel</c> do WPF; subiu para cá porque são frases que
/// decidem o que a direção lê — "sobrou quanto?", "estourou?" — e decisão em projeto WPF não
/// é alcançada pelo <c>dotnet test</c>.
///
/// As três honestidades que vieram junto, todas do serviço e repetidas aqui de propósito:
/// regime de CAIXA (só o realizado); deduções (taxa e imposto) separadas das despesas; e
/// margem sobre zero NULA — mostrar 0% faria um mês sem faturamento parecer um mês que
/// faturou e não sobrou nada.
/// </summary>
public static class LeituraDoResultado
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>A frase que a tela escreve sob a regra do regime de caixa.</summary>
    public const string RegimeDeCaixa =
        "Regime de caixa: considera o movimento realizado no mês. Taxas e impostos são deduções da receita, separados das despesas.";

    /// <summary>A frase do mês: vazio, no vermelho, ou quanto sobrou (com a margem, quando ela existe).</summary>
    public static string Resumo(ResultadoMensal r) => r.Vazio
        ? "Nenhum movimento realizado neste mês."
        : r.Prejuizo
            ? $"O mês fechou {Dinheiro(Math.Abs(r.Resultado))} no vermelho."
            : $"Sobraram {Dinheiro(r.Resultado)}"
              + (r.Margem is { } margem ? $" — {Margem(margem)} do faturado." : ".");

    /// <summary>"12,5%" — ou "—" sem receita: margem sobre zero não é 0%, é conta que não existe.</summary>
    public static string Margem(double? margem) => margem is { } m ? Margem(m) : "—";

    private static string Margem(double m) => $"{m.ToString("0.#", PtBr)}%";

    /// <summary>
    /// A situação de um teto, em palavras. O COMPROMETIDO é o que muda a decisão: o teto
    /// costuma estourar no que já foi contratado, não no que já foi pago.
    /// </summary>
    public static string Situacao(OrcamentoApurado o)
    {
        if (o.Estourou)
            return $"estourou em {Dinheiro(Math.Abs(o.Saldo))}";

        if (o.VaiEstourar && o.Projetado is { } projetado)
            return $"o já contratado leva a {Dinheiro(projetado)} — passa do teto";

        return o.PercentualUsado is { } p
            ? $"{p.ToString("0.#", PtBr)}% do teto usado"
            : "sem teto para comparar";
    }

    /// <summary>A cor da situação: as mesmas duas marcas do WPF (atenção e estouro).</summary>
    public static GravidadeDoTeto Gravidade(OrcamentoApurado o) =>
        o.Estourou ? GravidadeDoTeto.Estourou : o.EmAtencao ? GravidadeDoTeto.Atencao : GravidadeDoTeto.Normal;

    /// <summary>A barra do teto, de 0 a 1 — cheia quando estourou, nunca além.</summary>
    public static double Fracao(OrcamentoApurado o) => Math.Min((o.PercentualUsado ?? 0) / 100.0, 1.0);

    /// <summary>
    /// A frase acima da lista de tetos. Sem teto nenhum, ela diz o que se perde — o gasto
    /// passa a ser julgado só contra o mês anterior.
    /// </summary>
    public static string ResumoDosTetos(IReadOnlyList<OrcamentoApurado> apurados)
    {
        var estourados = apurados.Count(o => o.Estourou);
        return apurados.Count == 0
            ? "Nenhum teto definido para este mês. Sem teto, o gasto só é julgado contra o mês anterior — e gastar 10% menos que um mês ruim aparece como economia."
            : estourados == 0
                ? $"{apurados.Count} categoria(s) com teto — nenhuma estourou."
                : $"{estourados} de {apurados.Count} categoria(s) passaram do teto.";
    }

    private static string Dinheiro(decimal v) => v.ToString("C", PtBr);
}
