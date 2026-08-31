using System.Globalization;

namespace Clinica.Application.Modelos;

/// <summary>Como ler a variação de um KPI: subir pode ser bom, ruim ou só um fato.</summary>
public enum LeituraKpi
{
    Boa,
    Ruim,
    Neutra
}

/// <summary>
/// A variação de um cartão de KPI contra o trecho anterior equivalente (pedido da
/// direção, ago/2026 — o `KpiCard` do handoff traz o delta como a ÚLTIMA linha do
/// cartão, e o painel da direção já desenha assim desde a parcela 28).
///
/// Mora na Application, e não nas ViewModels, pela regra da casa: o que decide o que a
/// tela AFIRMA precisa morar onde o `dotnet test` alcança. As três regras que os
/// construtores garantem:
///
/// 1. <b>Sem base não há seta</b> — anterior nulo ou zero devolve NULO, e a tela não
///    desenha nada. Zero e "não medido" são coisas diferentes, e uma clínica que começou
///    a medir neste período veria "+∞%" (a mesma recusa do painel da direção).
/// 2. <b>A cor não é da seta, é da MÉTRICA</b>: taxa de glosa subindo é ruim, taxa de
///    baixa subindo é boa, contagem de guias subindo é só um fato (neutra). Quem sabe
///    disso é quem chama, por `melhorQuandoMenor` — nulo = neutra.
/// 3. <b>Taxa compara em PONTOS PERCENTUAIS, nunca em "% do %"</b>: taxa de falta indo
///    de 10% para 12% subiu 2 p.p. — dizer "+20%" leria como um quinto a mais de faltas
///    absolutas, que não é o que aconteceu.
///
/// <see cref="Rotulo"/> é a meia-frase apagada ao lado da seta ("vs. período anterior",
/// como no kit); <see cref="Detalhe"/> é o intervalo exato, para a dica do mouse.
/// </summary>
public sealed record VariacaoKpi(string Texto, LeituraKpi Leitura, string Rotulo, string Detalhe)
{
    /// <summary>Variação relativa de uma contagem/valor (12 → 15 = "↑ 25%").</summary>
    public static VariacaoKpi? Relativa(
        double atual, double? anterior, string rotuloDaBase, string detalheDaBase,
        bool? melhorQuandoMenor = null)
    {
        if (anterior is not > 0) return null;

        var fracao = (atual - anterior.Value) / anterior.Value;
        var pct = Math.Round(fracao * 100);
        return Montar(pct, $"{Math.Abs(pct):0}%", rotuloDaBase, detalheDaBase, melhorQuandoMenor);
    }

    /// <summary>Variação de uma TAXA, em pontos percentuais (10% → 12% = "↑ 2 p.p.").</summary>
    public static VariacaoKpi? EmPontos(
        double? atualPercentual, double? anteriorPercentual, string rotuloDaBase,
        string detalheDaBase, bool? melhorQuandoMenor = null)
    {
        if (atualPercentual is null || anteriorPercentual is null) return null;

        var pontos = Math.Round(atualPercentual.Value - anteriorPercentual.Value, 1);
        var texto = string.Format(CultureInfo.CurrentCulture, "{0:0.#} p.p.", Math.Abs(pontos));
        return Montar(pontos, texto, rotuloDaBase, detalheDaBase, melhorQuandoMenor);
    }

    /// <summary>Variação em valor absoluto com unidade (EVA 4,1 → 4,7 = "↑ 0,6 pt").</summary>
    public static VariacaoKpi? EmValor(
        double? atual, double? anterior, string unidade, string rotuloDaBase,
        string detalheDaBase, bool? melhorQuandoMenor = null)
    {
        if (atual is null || anterior is null) return null;

        var diferenca = Math.Round(atual.Value - anterior.Value, 1);
        var texto = string.Format(CultureInfo.CurrentCulture, "{0:0.#} {1}", Math.Abs(diferenca), unidade);
        return Montar(diferenca, texto, rotuloDaBase, detalheDaBase, melhorQuandoMenor);
    }

    private static VariacaoKpi Montar(
        double delta, string magnitude, string rotuloDaBase, string detalheDaBase,
        bool? melhorQuandoMenor)
    {
        if (delta == 0)
            return new VariacaoKpi($"= {magnitude}", LeituraKpi.Neutra, rotuloDaBase, detalheDaBase);

        var subiu = delta > 0;
        var leitura = melhorQuandoMenor is null
            ? LeituraKpi.Neutra
            : subiu == !melhorQuandoMenor.Value ? LeituraKpi.Boa : LeituraKpi.Ruim;

        return new VariacaoKpi(
            $"{(subiu ? "↑" : "↓")} {magnitude}", leitura, rotuloDaBase, detalheDaBase);
    }
}

/// <summary>
/// O trecho ANTERIOR equivalente a um intervalo — a régua contra a qual o delta é medido.
///
/// A regra é a do painel da direção ("mesmo trecho do mês anterior"), generalizada para
/// os períodos que as telas oferecem:
/// <list type="bullet">
/// <item>ano corrente (01/01 → hoje) compara com o MESMO trecho do ano anterior — é a
/// sazonalidade que interessa, não os doze meses encostados;</item>
/// <item>intervalo ancorado no dia 1 (mês corrente, últimos 3 meses, mês civil) desloca
/// pelo número de MESES que cobre, com o dia do fim preso ao tamanho do mês de destino
/// (31/03 contra fevereiro cai no último dia dele);</item>
/// <item>intervalo corrido (últimos 90 dias) desloca pelo número de DIAS.</item>
/// </list>
/// </summary>
public static class TrechoAnterior
{
    public static (DateOnly Inicio, DateOnly Fim)? De(DateOnly inicio, DateOnly fim)
    {
        if (fim < inicio) return null;

        // Ano corrente: mesmo trecho do ano anterior (clamp para 29/02).
        if (inicio is { Month: 1, Day: 1 } && fim.Year == inicio.Year)
        {
            var fimAnterior = new DateOnly(
                fim.Year - 1, fim.Month,
                Math.Min(fim.Day, DateTime.DaysInMonth(fim.Year - 1, fim.Month)));
            return (new DateOnly(inicio.Year - 1, 1, 1), fimAnterior);
        }

        // Ancorado no dia 1: desloca pelos meses cobertos, preservando o dia do fim.
        if (inicio.Day == 1)
        {
            var meses = (fim.Year * 12 + fim.Month) - (inicio.Year * 12 + inicio.Month) + 1;
            var mesDoFim = new DateOnly(fim.Year, fim.Month, 1).AddMonths(-meses);
            var fimAnterior = new DateOnly(
                mesDoFim.Year, mesDoFim.Month,
                Math.Min(fim.Day, DateTime.DaysInMonth(mesDoFim.Year, mesDoFim.Month)));
            return (inicio.AddMonths(-meses), fimAnterior);
        }

        // Corrido: o mesmo número de dias, terminando na véspera do início.
        var dias = fim.DayNumber - inicio.DayNumber + 1;
        return (inicio.AddDays(-dias), inicio.AddDays(-1));
    }
}
