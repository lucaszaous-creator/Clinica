using System.Globalization;
using Clinica.Application.Servicos;

namespace Clinica.Application.Modelos;

/// <summary>Um bloco do detalhe: convênio + referência do pagamento, numa competência.</summary>
public sealed record BlocoDoRelatorioRepasse(
    string Convenio,
    string? ReferenciaPagamento,
    IReadOnlyList<LinhaRelatorioRepasse> Linhas,
    /// <summary>Soma das linhas de fechamentos NÃO cancelados (cancelado aparece, não soma).</summary>
    decimal Total);

/// <summary>Uma competência de um médico, com os blocos por convênio e pagamento.</summary>
public sealed record CompetenciaDoRelatorioRepasse(DateOnly Competencia, IReadOnlyList<BlocoDoRelatorioRepasse> Blocos, decimal Total);

/// <summary>
/// O que a vista "Relatório para os médicos" da tela de Repasses AFIRMA (nov/2026): os atalhos de
/// período, a recusa de filtro, os rótulos da situação, o agrupamento do detalhe e as frases.
/// Razor não decide — a tela só desenha o que sai daqui. A leitura e os arquivos são do
/// <see cref="RelatorioRepasseService"/>.
/// </summary>
public static class RelatorioRepasseNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O teto do serviço: mais que isso ele recusa, e a tela recusa antes, com a mesma frase.</summary>
    public const int MaximoDeCompetencias = 24;

    public const string NomeParticular = "Particular";

    // ==================== Período ====================

    private static DateOnly Mes(DateOnly d) => new(d.Year, d.Month, 1);

    public static (DateOnly De, DateOnly Ate) EsteMes(DateOnly hoje) => (Mes(hoje), Mes(hoje));

    public static (DateOnly De, DateOnly Ate) MesPassado(DateOnly hoje) => (Mes(hoje).AddMonths(-1), Mes(hoje).AddMonths(-1));

    /// <summary>Os três últimos meses, o corrente incluído.</summary>
    public static (DateOnly De, DateOnly Ate) UltimosTresMeses(DateOnly hoje) => (Mes(hoje).AddMonths(-2), Mes(hoje));

    public static (DateOnly De, DateOnly Ate) EsteAno(DateOnly hoje) => (new DateOnly(hoje.Year, 1, 1), Mes(hoje));

    /// <summary>O filtro com que a vista abre: o mês passado — o fechamento costuma ser do mês que acabou.</summary>
    public static FiltroRelatorioRepasse Inicial(DateOnly hoje)
    {
        var (de, ate) = MesPassado(hoje);
        return new FiltroRelatorioRepasse(de, ate);
    }

    /// <summary>Quantas competências o filtro cobre (as duas pontas inclusive).</summary>
    public static int Competencias(FiltroRelatorioRepasse f)
        => (f.CompetenciaAte.Year - f.CompetenciaDe.Year) * 12 + f.CompetenciaAte.Month - f.CompetenciaDe.Month + 1;

    /// <summary>A recusa do filtro ANTES de ir ao banco (a mesma que o serviço faria). Null = pode ler.</summary>
    public static string? Recusa(FiltroRelatorioRepasse f)
    {
        if (f.CompetenciaAte < f.CompetenciaDe) return "A competência inicial é depois da final — troque as duas.";
        if (Competencias(f) > MaximoDeCompetencias)
            return $"No máximo {MaximoDeCompetencias} competências por relatório. Diminua o período.";
        return null;
    }

    /// <summary>"09/2026" ou "09/2026 a 11/2026".</summary>
    public static string Periodo(FiltroRelatorioRepasse f)
        => f.CompetenciaDe == f.CompetenciaAte
            ? Competencia(f.CompetenciaDe)
            : $"{Competencia(f.CompetenciaDe)} a {Competencia(f.CompetenciaAte)}";

    public static string Competencia(DateOnly c) => c.ToString("MM/yyyy", PtBr);

    // ==================== Situação ====================

    public static IReadOnlyList<SituacaoNoRelatorioRepasse> Situacoes { get; } =
    [
        SituacaoNoRelatorioRepasse.ConferidosEPagos,
        SituacaoNoRelatorioRepasse.SoPagos,
        SituacaoNoRelatorioRepasse.SoAPagar,
        SituacaoNoRelatorioRepasse.IncluirCancelados
    ];

    public static string Rotulo(SituacaoNoRelatorioRepasse s) => s switch
    {
        SituacaoNoRelatorioRepasse.SoPagos => "Só pagos",
        SituacaoNoRelatorioRepasse.SoAPagar => "Só a pagar",
        SituacaoNoRelatorioRepasse.IncluirCancelados => "Incluir cancelados",
        _ => "Conferidos e pagos"
    };

    /// <summary>Algum filtro além do período? (acende o "Limpar filtros")</summary>
    public static bool FiltroAtivo(FiltroRelatorioRepasse f)
        => f.ProfissionalId is not null || !string.IsNullOrWhiteSpace(f.ConvenioCodigo)
           || !string.IsNullOrWhiteSpace(f.Paciente) || f.Situacao != SituacaoNoRelatorioRepasse.ConferidosEPagos;

    /// <summary>Volta os filtros ao padrão, mantendo o período.</summary>
    public static FiltroRelatorioRepasse SemFiltros(FiltroRelatorioRepasse f) => new(f.CompetenciaDe, f.CompetenciaAte);

    /// <summary>O mesmo filtro, só daquele médico — o "PDF do médico" da linha.</summary>
    public static FiltroRelatorioRepasse DoMedico(FiltroRelatorioRepasse f, int profissionalId) => f with { ProfissionalId = profissionalId };

    /// <summary>Paciente digitado: aparado; vazio vira nulo (todos).</summary>
    public static string? Paciente(string? digitado) => string.IsNullOrWhiteSpace(digitado) ? null : digitado.Trim();

    // ==================== Frases ====================

    /// <summary>A linha de resumo sob os filtros.</summary>
    public static string Resumo(RelatorioRepasse r)
    {
        var medicos = r.PorProfissional.Count;
        var fechamentos = r.PorProfissional.Sum(p => p.Fechamentos);
        return $"{Plural(medicos, "médico", "médicos")} · {Plural(fechamentos, "fechamento", "fechamentos")} · "
               + $"{Plural(r.Quantidade, "guia", "guias")} · competência {Periodo(r.Filtro)}";
    }

    public static string NotaDoTotal(RelatorioRepasse r)
        => r.Filtro.Situacao == SituacaoNoRelatorioRepasse.IncluirCancelados
            ? "Fechamentos conferidos no filtro. Os cancelados aparecem marcados e não somam."
            : "Fechamentos conferidos no filtro — o que foi entregue aos médicos.";

    public static string NotaDoAPagar(RelatorioRepasse r)
        => r.APagar == 0m ? "Nada pendente no filtro" : "Conferido e ainda sem a saída realizada no caixa";

    public const string TituloDoVazio = "Nenhum fechamento no filtro";

    /// <summary>O vazio explica de onde o relatório lê — e leva ao fechamento.</summary>
    public static string DescricaoDoVazio(FiltroRelatorioRepasse f)
        => "O relatório lê só os fechamentos CONFERIDOS — o que alguém fechou na vista Fechamento. "
           + (FiltroAtivo(f)
               ? "Limpe os filtros ou amplie o período; se o mês ainda não foi fechado, feche-o primeiro."
               : $"Nenhum repasse foi fechado em {Periodo(f)}: feche o mês primeiro na vista Fechamento.");

    /// <summary>Quantos PDFs saíram e onde.</summary>
    public static string PdfsGravados(int quantidade, string pasta)
        => quantidade == 1 ? $"1 PDF gravado em {pasta}." : $"{quantidade} PDFs gravados em {pasta} — um por médico.";

    /// <summary>A situação da linha do detalhe (o serviço dá o texto; a tela dá o tom).</summary>
    public static string TomDaLinha(LinhaRelatorioRepasse l)
        => l.Cancelado ? "neutro" : l.PagoEm is not null ? "sucesso" : "info";

    // ==================== Detalhe ====================

    /// <summary>
    /// O detalhe de UM médico: competência → convênio · referência do pagamento, como no
    /// demonstrativo. As linhas mantêm a ordem do serviço; o total do bloco ignora cancelados.
    /// </summary>
    public static IReadOnlyList<CompetenciaDoRelatorioRepasse> Detalhe(IEnumerable<LinhaRelatorioRepasse> linhas, int profissionalId)
    {
        var cmp = StringComparer.Create(PtBr, true);
        return linhas.Where(l => l.ProfissionalId == profissionalId)
            .GroupBy(l => l.Competencia)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var blocos = g
                    .GroupBy(l => (l.Convenio, Referencia: string.IsNullOrWhiteSpace(l.ReferenciaPagamento) ? null : l.ReferenciaPagamento.Trim()))
                    .OrderBy(b => b.Key.Convenio, cmp)
                    .ThenBy(b => b.Key.Referencia is null ? 1 : 0)
                    .ThenBy(b => b.Key.Referencia, StringComparer.Ordinal)
                    .Select(b => new BlocoDoRelatorioRepasse(b.Key.Convenio, b.Key.Referencia, b.ToList(),
                        b.Where(l => !l.Cancelado).Sum(l => l.Valor)))
                    .ToList();
                return new CompetenciaDoRelatorioRepasse(g.Key, blocos, blocos.Sum(b => b.Total));
            })
            .ToList();
    }

    private static string Plural(int n, string um, string varios) => $"{n.ToString(PtBr)} {(n == 1 ? um : varios)}";
}
