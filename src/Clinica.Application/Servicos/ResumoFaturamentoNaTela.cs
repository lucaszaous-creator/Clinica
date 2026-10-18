using System.Globalization;
using Clinica.Application.Modelos;

namespace Clinica.Application.Servicos;

/// <summary>
/// O RESUMO do faturamento (a aba "Resumo" do app de faturamento — a visão da direção,
/// SÓ LEITURA), montado fora do Razor (out/2026). Era o <c>FaturamentoGerencialViewModel</c>
/// do Gerente, mais o <c>PeriodoGerencial</c> que morava no projeto WPF (removido na etapa 6, out/2026 — esta é a definição única). Nada aqui lê o
/// banco: quem lê é a tela, com os MESMOS serviços (<see cref="RelatorioService"/>,
/// <see cref="PendenciaService"/>); aqui mora o que a tela AFIRMA.
/// </summary>
public static class ResumoFaturamentoNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public const string EsteMes = "Este mês";
    public const string UltimosTresMeses = "Últimos 3 meses";
    public const string EsteAno = "Este ano";

    /// <summary>As opções, do mais curto ao mais longo (o <c>PeriodoGerencial</c> do Gerente).</summary>
    public static IReadOnlyList<string> Periodos { get; } = [EsteMes, UltimosTresMeses, EsteAno];

    /// <summary>O fim é sempre HOJE: mês que não terminou não aparece como se tivesse terminado.</summary>
    public static (DateOnly Inicio, DateOnly Fim) Intervalo(string periodo, DateOnly hoje) => periodo switch
    {
        UltimosTresMeses => (new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-2), hoje),
        EsteAno => (new DateOnly(hoje.Year, 1, 1), hoje),
        _ => (new DateOnly(hoje.Year, hoje.Month, 1), hoje)
    };

    public static string Rotular((DateOnly Inicio, DateOnly Fim) intervalo)
        => $"{intervalo.Inicio.ToString("dd/MM/yyyy", PtBr)} a {intervalo.Fim.ToString("dd/MM/yyyy", PtBr)}";

    /// <summary>Taxa "12,5%"; sem base, "—" (zero e "não medido" são coisas diferentes).</summary>
    public static string Taxa(double taxa, bool temBase) => temBase ? $"{taxa.ToString("0.#", PtBr)}%" : "—";

    /// <summary>
    /// Monta tudo o que a tela mostra. <paramref name="anterior"/> é o relatório do trecho
    /// anterior equivalente (nulo sem trecho), para o delta dos cartões.
    /// </summary>
    public static PainelDoResumo Montar(
        RelatorioFaturamento relatorio,
        RelatorioFaturamento? anterior,
        (DateOnly Inicio, DateOnly Fim)? trechoAnterior,
        IReadOnlyList<ResumoMensal> meses,
        int pendenciasEmAberto)
    {
        var r = relatorio.Resumo;
        var temBaixa = r.TotalCodigos > 0;
        var temGlosa = r.Baixados > 0;

        const string rotuloDelta = "vs. período anterior";
        var detalhe = trechoAnterior is { } t ? $"Trecho anterior equivalente: {Rotular(t)}" : string.Empty;
        var a = anterior?.Resumo;

        var porConvenio = relatorio.PorConvenio.Select(c => new LinhaConvenioDoResumo(
            c.ConvenioNome, c.TotalCodigos, c.Baixados, c.Pendentes,
            Taxa(c.TaxaBaixa, c.TotalCodigos > 0),
            Taxa(c.TaxaGlosa, c.Baixados > 0),
            c.TempoMedioBaixaDias is { } d ? $"{d.ToString("0.#", PtBr)} d" : "—")).ToList();

        // TODAS as faixas, inclusive as vazias: aging sem a faixa de maior atraso se lê como
        // "não há pendência velha". A fração é do total das faixas — normalizada aqui.
        var totalFaixas = relatorio.Envelhecimento.Sum(f => f.Quantidade);
        var envelhecimento = relatorio.Envelhecimento.Select(f => new FaixaDoResumo(
            f.Faixa,
            totalFaixas == 0
                ? $"{f.Quantidade}"
                : $"{f.Quantidade} · {((double)f.Quantidade / totalFaixas).ToString("P0", PtBr)}",
            totalFaixas == 0 ? 0 : (double)f.Quantidade / totalFaixas)).ToList();

        // Mês SEM guia é BURACO (nulo), nunca 0% — na curva e na lista.
        var serie = meses.Select(m => new PontoDoResumo(m.Rotulo, m.TotalCodigos == 0 ? null : m.TaxaBaixa)).ToList();
        var mesesLinhas = meses.Select(m => new MesDoResumo(
            m.Rotulo, m.TotalCodigos, m.Baixados, Taxa(m.TaxaBaixa, m.TotalCodigos > 0))).ToList();

        return new PainelDoResumo(
            IntervaloTexto: Rotular((relatorio.Inicio, relatorio.Fim)),
            TotalGuias: r.TotalCodigos,
            Baixadas: r.Baixados,
            Pendentes: r.Pendentes,
            NaoConformidades: r.NaoConformidades,
            PendenciasEmAberto: pendenciasEmAberto,
            TaxaBaixa: Taxa(r.TaxaBaixa, temBaixa),
            TaxaBaixaFracao: temBaixa ? Math.Clamp(r.TaxaBaixa / 100.0, 0, 1) : null,
            TaxaGlosa: Taxa(r.TaxaGlosa, temGlosa),
            TaxaGlosaFracao: temGlosa ? Math.Clamp(r.TaxaGlosa / 100.0, 0, 1) : null,
            TempoMedio: r.TempoMedioBaixaDias is { } dias ? $"{dias.ToString("0.#", PtBr)} dias" : "—",
            VariacaoGuias: VariacaoKpi.Relativa(r.TotalCodigos, a?.TotalCodigos, rotuloDelta, detalhe),
            VariacaoTaxaBaixa: VariacaoKpi.EmPontos(
                temBaixa ? r.TaxaBaixa : null,
                a is { TotalCodigos: > 0 } ? a.TaxaBaixa : null,
                rotuloDelta, detalhe, melhorQuandoMenor: false),
            VariacaoTaxaGlosa: VariacaoKpi.EmPontos(
                temGlosa ? r.TaxaGlosa : null,
                a is { Baixados: > 0 } ? a.TaxaGlosa : null,
                rotuloDelta, detalhe, melhorQuandoMenor: true),
            VariacaoTempoMedio: VariacaoKpi.EmValor(
                r.TempoMedioBaixaDias, a?.TempoMedioBaixaDias, "d", rotuloDelta, detalhe, melhorQuandoMenor: true),
            PorConvenio: porConvenio,
            Envelhecimento: envelhecimento,
            Serie: serie,
            // Um ponto só não é curva — é linha de base, e desenhá-la prometeria uma evolução.
            TemSerie: serie.Count(p => p.Valor is not null) >= 2,
            Meses: mesesLinhas,
            MensagemVazio: r.TotalCodigos == 0 ? "Nenhuma guia com atendimento neste período." : null);
    }
}

public sealed record PainelDoResumo(
    string IntervaloTexto,
    int TotalGuias, int Baixadas, int Pendentes, int NaoConformidades, int PendenciasEmAberto,
    string TaxaBaixa, double? TaxaBaixaFracao,
    string TaxaGlosa, double? TaxaGlosaFracao,
    string TempoMedio,
    VariacaoKpi? VariacaoGuias, VariacaoKpi? VariacaoTaxaBaixa, VariacaoKpi? VariacaoTaxaGlosa, VariacaoKpi? VariacaoTempoMedio,
    IReadOnlyList<LinhaConvenioDoResumo> PorConvenio,
    IReadOnlyList<FaixaDoResumo> Envelhecimento,
    IReadOnlyList<PontoDoResumo> Serie,
    bool TemSerie,
    IReadOnlyList<MesDoResumo> Meses,
    string? MensagemVazio);

public sealed record LinhaConvenioDoResumo(string Convenio, int Total, int Baixados, int Pendentes,
    string TaxaBaixa, string TaxaGlosa, string TempoMedio);

/// <summary>Faixa do envelhecimento: rótulo, número escrito e a fração (0..1) da barra.</summary>
public sealed record FaixaDoResumo(string Rotulo, string ValorRotulo, double Fracao);

/// <summary>Ponto da curva da taxa de baixa; <c>Valor</c> nulo = mês sem guia (buraco).</summary>
public sealed record PontoDoResumo(string Rotulo, double? Valor);

public sealed record MesDoResumo(string Rotulo, int Guias, int Baixadas, string TaxaBaixa);
