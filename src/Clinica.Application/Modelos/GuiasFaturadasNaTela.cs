using System.Globalization;
using Clinica.Application.Formatacao;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O filtro da tela "Guias faturadas" (out/2026, migração do FaturadosViewModel do WPF).
/// Em memória sobre o período carregado — refazer a consulta a cada tecla seria pagar o
/// banco remoto para responder o que a tela já sabe.
///
/// O convênio casa pela OPERADORA (o nome que a coluna mostra), nunca pela família: com
/// ela, filtrar por "Porto Saúde" devolveria as guias da Sul América junto.
/// </summary>
public sealed record FiltroFaturados(
    string Convenio = GuiasFaturadasNaTela.TodosConvenios,
    string Paciente = "",
    string Guia = "")
{
    public static readonly FiltroFaturados Nenhum = new();

    /// <summary>
    /// Modalidade do atendimento (nov/2026 — o mesmo combo da Consulta de guias, pedido da
    /// gerência para padronizar pendências e baixas com ela).
    /// </summary>
    public ModalidadeAtendimento? Modalidade { get; init; }

    /// <summary>
    /// Especialidade da GUIA, com o atendimento como caminho de baixo — a mesma regra da coluna
    /// e do filtro do repositório na Consulta de guias.
    /// </summary>
    public Especialidade? Especialidade { get; init; }

    public bool Ativo =>
        Convenio != GuiasFaturadasNaTela.TodosConvenios
        || !string.IsNullOrWhiteSpace(Paciente)
        || !string.IsNullOrWhiteSpace(Guia)
        || Modalidade is not null
        || Especialidade is not null;

    /// <summary>Paciente sem acento e sem caixa (a mesma <see cref="Busca"/> da Conciliação); guia por trecho.</summary>
    public bool Casa(CodigoFaturamento c)
        => (Convenio == GuiasFaturadasNaTela.TodosConvenios || ConsultaDeGuiasNaTela.Convenio(c) == Convenio)
           && Busca.Casa(c.Atendimento?.Paciente?.Nome, Paciente)
           && Busca.Casa(c.NumeroGuiaReal, Guia)
           && (Modalidade is not { } m || c.Atendimento?.Modalidade == m)
           && (Especialidade is not { } e || (c.Especialidade ?? c.Atendimento?.EspecialidadeConsulta) == e);
}

/// <summary>Os atalhos de período da tela de baixas: o histórico de UM paciente raramente cabe no mês corrente.</summary>
public enum AtalhoPeriodoFaturados { EsteMes, MesPassado, Ultimos3Meses, Ultimos12Meses }

public static class GuiasFaturadasNaTela
{
    public const string TodosConvenios = "Todos os convênios";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>As operadoras DO PERÍODO — oferecer as sem guia daria filtro que só leva ao vazio.</summary>
    public static IReadOnlyList<string> Convenios(IEnumerable<CodigoFaturamento> doPeriodo)
        => new[] { TodosConvenios }
            .Concat(doPeriodo.Select(ConsultaDeGuiasNaTela.Convenio).Distinct().OrderBy(n => n, StringComparer.Create(PtBr, true)))
            .ToList();

    /// <summary>
    /// A escolha de convênio sobrevive à recarga enquanto ela ainda existir no período:
    /// atualizar não pode desfazer o filtro de quem está trabalhando nele.
    /// </summary>
    public static FiltroFaturados Preservar(FiltroFaturados filtro, IReadOnlyList<string> convenios)
        => convenios.Contains(filtro.Convenio) ? filtro : filtro with { Convenio = TodosConvenios };

    /// <summary>"12 de 90 guia(s) no filtro" × "90 guia(s) no período" — perguntas diferentes.</summary>
    public static string Resumo(FiltroFaturados filtro, int noFiltro, int noPeriodo)
        => filtro.Ativo ? $"{noFiltro} de {noPeriodo} guia(s) no filtro" : $"{noPeriodo} guia(s) no período";

    /// <summary>A guia descrita na pergunta da glosa: quem, o quê e o número.</summary>
    public static string DescricaoParaGlosa(CodigoFaturamento c)
        => $"{c.Atendimento?.Paciente?.Nome} — {RotulosEnum.De(c.Tipo)} (guia {(string.IsNullOrWhiteSpace(c.NumeroGuiaReal) ? "sem número" : c.NumeroGuiaReal)})";

    public static readonly IReadOnlyList<string> CabecalhoCsv =
        ["Paciente", "Convênio", "Código", "Ordem", "Nº guia", "Baixado em", "Glosa"];

    /// <summary>
    /// Uma linha do CSV para conferência com o convênio. O WPF gravava o identificador do
    /// enum ("ConsultaEspecialidade", "SemGlosa") — a planilha que vai à operadora sai com
    /// as palavras da tela.
    /// </summary>
    public static IReadOnlyList<string> LinhaCsv(CodigoFaturamento c) =>
    [
        c.Atendimento?.Paciente?.Nome ?? string.Empty,
        ConsultaDeGuiasNaTela.Convenio(c),
        RotulosEnum.De(c.Tipo),
        RotulosEnum.De(c.Ordem),
        c.NumeroGuiaReal ?? string.Empty,
        c.DataBaixa?.ToString("dd/MM/yyyy", PtBr) ?? string.Empty,
        ConsultaDeGuiasNaTela.Glosa(c.Glosa)?.Texto ?? "Sem glosa"
    ];

    public static string NomeArquivoCsv(DateOnly inicio, DateOnly fim)
        => $"Faturados-{inicio:yyyy-MM-dd}-a-{fim:yyyy-MM-dd}.csv";

    public static string Rotulo(AtalhoPeriodoFaturados atalho) => atalho switch
    {
        AtalhoPeriodoFaturados.EsteMes => "Este mês",
        AtalhoPeriodoFaturados.MesPassado => "Mês passado",
        AtalhoPeriodoFaturados.Ultimos3Meses => "Últimos 3 meses",
        _ => "Últimos 12 meses"
    };

    /// <summary>
    /// O período de cada atalho, contado a partir de hoje. "Últimos N meses" vai do dia 1º do
    /// mês de N-1 meses atrás até o fim do mês corrente — o mês corrente inteiro entra, e o
    /// período nunca começa no meio de um mês (a conferência com o demonstrativo é mensal).
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fim) Periodo(AtalhoPeriodoFaturados atalho, DateOnly hoje)
    {
        var (inicioMes, fimMes) = MesDe(hoje);
        return atalho switch
        {
            AtalhoPeriodoFaturados.MesPassado => (inicioMes.AddMonths(-1), inicioMes.AddDays(-1)),
            AtalhoPeriodoFaturados.Ultimos3Meses => (inicioMes.AddMonths(-2), fimMes),
            AtalhoPeriodoFaturados.Ultimos12Meses => (inicioMes.AddMonths(-11), fimMes),
            _ => (inicioMes, fimMes)
        };
    }

    /// <summary>
    /// O aviso de quem procura UM paciente num período curto: o histórico dele está em meses
    /// que a tela não leu. Nulo quando não há paciente digitado ou o período já é longo.
    /// </summary>
    public static string? DicaHistoricoDoPaciente(FiltroFaturados filtro, DateOnly inicio, DateOnly fim)
        => string.IsNullOrWhiteSpace(filtro.Paciente) || fim.DayNumber - inicio.DayNumber > 120
            ? null
            : "Procurando o histórico de um paciente? Use “Últimos 12 meses” — a lista só traz as baixas do período escolhido.";

    /// <summary>O mês corrente — o período com que a tela abre, como no WPF.</summary>
    public static (DateOnly Inicio, DateOnly Fim) MesDe(DateOnly hoje)
    {
        var inicio = new DateOnly(hoje.Year, hoje.Month, 1);
        return (inicio, inicio.AddMonths(1).AddDays(-1));
    }
}
