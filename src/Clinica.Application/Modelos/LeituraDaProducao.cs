using System.Globalization;

namespace Clinica.Application.Modelos;

/// <summary>Os totais da janela de meses da tela de Produção.</summary>
public sealed record TotaisDaProducao(int Codigos, int Baixados, int Pendentes)
{
    /// <summary>
    /// Efetivados sobre o total. NULA sem código no período: 0% e "não medido" são coisas
    /// diferentes (a regra dos indicadores, parcela 5).
    /// </summary>
    public double? Taxa => Codigos > 0 ? (double)Baixados / Codigos : null;
}

/// <summary>
/// O que a tela de Produção AFIRMA (out/2026, na migração para Blazor). Só CONTAGENS:
/// faturamento não é recebível, e nenhuma linha daqui vira dinheiro.
/// </summary>
public static class LeituraDaProducao
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static TotaisDaProducao Somar(IReadOnlyList<ResumoMensal> meses) => new(
        meses.Sum(m => m.TotalCodigos),
        meses.Sum(m => m.Baixados),
        meses.Sum(m => m.Pendentes));

    /// <summary>"92,5%" ou "—" sem código no período.</summary>
    public static string Taxa(TotaisDaProducao t) => t.Taxa is { } taxa ? taxa.ToString("P1", PtBr) : "—";

    /// <summary>
    /// A taxa do MÊS. ⚠️ <see cref="ResumoMensal.TaxaBaixa"/> já vem em 0..100 do
    /// <c>RelatorioService</c> — formatar com "P" multiplicaria por 100 de novo ("9.250,0%").
    /// E o mês sem código devolve "—", não "0%": o serviço escreve 0 quando não há base.
    /// </summary>
    public static string TaxaDoMes(ResumoMensal m) =>
        m.TotalCodigos == 0 ? "—" : $"{m.TaxaBaixa.ToString("0.#", PtBr)}%";

    /// <summary>
    /// Os que não são nem efetivados nem pendentes: as não conformidades justificadas na
    /// rodada (o <c>RelatorioService</c> as tira das pendentes). Sem esta parcela, as barras
    /// do gráfico não somariam o total da tabela ao lado.
    /// </summary>
    public static int NaoConformidades(ResumoMensal m) => Math.Max(0, m.TotalCodigos - m.Baixados - m.Pendentes);
}
