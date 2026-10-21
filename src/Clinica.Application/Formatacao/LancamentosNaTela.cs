using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Formatacao;

/// <summary>
/// Um atendimento na conferência do balcão (aba "Lançamentos" da Recepção). Convênio,
/// modalidade e operador aparecem DUAS vezes — uma para ler, outra para filtrar — porque o
/// filtro casa por igualdade exata, e casar pelo texto desenhado amarraria o filtro à
/// formatação.
/// </summary>
public sealed record LinhaLancamentoBalcao
{
    /// <summary>O atendimento por trás da linha — a porta do ESTORNO (parcela 94).</summary>
    public required int AtendimentoId { get; init; }
    public required DateOnly Data { get; init; }
    public required string DataRotulo { get; init; }
    public required string Paciente { get; init; }
    public required string Modalidade { get; init; }
    public required string Convenio { get; init; }
    public required string Numero { get; init; }
    public required string Guias { get; init; }
    /// <summary>Quantas guias faturáveis o atendimento gerou (o "sem guia" é zero).</summary>
    public required int QuantidadeGuias { get; init; }
    public required string Pendencia { get; init; }
    public required bool TemPendencia { get; init; }
    /// <summary>"por Ana às 14:32" — quem LANÇOU (parcela 58).</summary>
    public required string Lancamento { get; init; }
    /// <summary>A chave de filtro de "quem lançou": o operador cru, sem a frase em volta.</summary>
    public required string Operador { get; init; }
}

/// <summary>
/// Um horário MARCADO no período (out/2026): o que a recepção fez no balcão. Com a guia
/// nascendo só quando o profissional finaliza, marcar não cria atendimento — sem esta lista a
/// conferência do balcão ficava vazia o dia inteiro, e a secretária não via o próprio trabalho.
/// </summary>
public sealed record LinhaMarcacaoBalcao
{
    public required int AgendamentoId { get; init; }
    /// <summary>Quando foi marcado — a ordem da lista (o mais recente primeiro).</summary>
    public required DateTime MarcadoEm { get; init; }
    /// <summary>"21/10 14:32" — quando o horário foi marcado.</summary>
    public required string MarcadoRotulo { get; init; }
    /// <summary>"28/10 09:00 · Dra. Ana" — para quando e com quem.</summary>
    public required string Sessao { get; init; }
    public required string Paciente { get; init; }
    public required string Modalidade { get; init; }
    public required string Convenio { get; init; }
    /// <summary>"por Ana" — quem marcou.</summary>
    public required string Marcacao { get; init; }
    public required string Operador { get; init; }
    /// <summary>"Aguardando atendimento", "Atendido", "Cancelado"…</summary>
    public required string Situacao { get; init; }
    /// <summary>Tom do selo da situação: neutro, sucesso, aviso ou perigo.</summary>
    public required string TomSituacao { get; init; }
    public required bool Encaixe { get; init; }
}

/// <summary>O recorte dos filtros da conferência. Nulo ou <see cref="LancamentosNaTela.Todos"/> = sem filtro.</summary>
public sealed record FiltroLancamentos(string? Convenio, string? Modalidade, string? Operador, bool SomenteComPendencia)
{
    public static readonly FiltroLancamentos Nenhum = new(null, null, null, false);

    public bool Ativo => !LancamentosNaTela.Livre(Convenio) || !LancamentosNaTela.Livre(Modalidade)
                         || !LancamentosNaTela.Livre(Operador) || SomenteComPendencia;
}

/// <summary>
/// O que a tela "Lançamentos" AFIRMA (out/2026, migrado do <c>LancamentosViewModel</c> WPF
/// para a tela Blazor). Pura, para o <c>dotnet test</c> alcançar: a frase da guia, a
/// pendência, a autoria, o teto do período e o recorte dos filtros.
/// </summary>
public static class LancamentosNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O "sem filtro" dos combos.</summary>
    public const string Todos = "Todos";

    /// <summary>
    /// O operador de quem não ficou registro. É opção de FILTRO como as outras: "quantos
    /// lançamentos ninguém assinou?" é pergunta da direção.
    /// </summary>
    public const string SemRegistroDeOperador = "sem registro";

    /// <summary>
    /// Teto do período, em dias: a conferência do balcão, não o relatório do ano — para
    /// intervalos maiores a pergunta é outra (Consulta de guias, no faturamento).
    /// </summary>
    public const int TetoDeDias = 366;

    /// <summary>Sem filtro: "Todos" escolhido, ou o vazio.</summary>
    public static bool Livre(string? escolhido) => string.IsNullOrEmpty(escolhido) || escolhido == Todos;

    /// <summary>
    /// O período pedido, ORDENADO (datas invertidas não são erro de quem usa: ordenar é mais
    /// útil que recusar) — ou a frase da recusa quando passa do teto.
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fim, string? Recusa) Periodo(DateOnly de, DateOnly ate)
    {
        var inicio = de <= ate ? de : ate;
        var fim = de <= ate ? ate : de;
        if (fim.DayNumber - inicio.DayNumber + 1 > TetoDeDias)
            return (inicio, fim, $"O período pedido passa de {TetoDeDias} dias. Esta é a conferência do balcão — "
                                 + "para intervalos maiores use a Consulta de guias, no faturamento.");
        return (inicio, fim, null);
    }

    /// <summary>
    /// Releitura ao VOLTAR à aba: só quando o período inclui hoje. A sessão lançada na aba do
    /// lado nasce com a data de hoje; um período antigo não pode ter mudado, e reconsultá-lo a
    /// cada troca pagaria até 366 dias de consulta para nada.
    /// </summary>
    public static bool RelerAoVoltar(DateOnly de, DateOnly ate, DateOnly hoje)
    {
        var (inicio, fim, _) = Periodo(de, ate);
        return inicio <= hoje && hoje <= fim;
    }

    public static LinhaLancamentoBalcao Montar(Atendimento atendimento)
    {
        var faturaveis = atendimento.Codigos.Where(c => c.Status != StatusCodigo.NaoAplicavel).ToList();

        // A guia que só libera depois é o assunto do produto. O corte é pela data da PRÓPRIA
        // linha: numa lista de período, "depois" significa depois do atendimento.
        var depois = faturaveis
            .Where(c => !c.Baixado && c.DataPrevistaFaturamento > atendimento.Data)
            .OrderBy(c => c.DataPrevistaFaturamento)
            .ToList();

        var paciente = atendimento.Paciente;
        return new LinhaLancamentoBalcao
        {
            AtendimentoId = atendimento.Id,
            Data = atendimento.Data,
            DataRotulo = atendimento.Data.ToString("dd/MM", PtBr),
            Paciente = paciente?.Nome ?? "—",
            Modalidade = atendimento.ModalidadeCodigo is { } cod
                ? CatalogoModalidades.Nome(cod)
                : ModalidadeInfo.NomeExibicao(atendimento.Modalidade),
            Convenio = paciente is null
                ? "—"
                : CatalogoConvenios.Nome(paciente.ConvenioCodigo ?? paciente.Convenio.ToString()),
            Numero = atendimento.Numero ?? $"#{atendimento.Id}",
            // "0 guias · todas liberadas" seria afirmação falsa sobre zero guias.
            QuantidadeGuias = faturaveis.Count,
            Guias = faturaveis.Count switch { 0 => "sem guia", 1 => "1 guia", var n => $"{n} guias" },
            Lancamento = DescreverLancamento(atendimento),
            Operador = string.IsNullOrWhiteSpace(atendimento.LancadoPor) ? SemRegistroDeOperador : atendimento.LancadoPor!,
            TemPendencia = depois.Count > 0,
            Pendencia = faturaveis.Count == 0
                ? "nada vai ao convênio"
                : depois.Count == 0
                    ? "todas liberadas"
                    : $"{depois.Count} libera(m) a partir de {depois[0].DataPrevistaFaturamento.ToString("dd/MM", PtBr)}"
        };
    }

    /// <summary>
    /// Um horário marcado na conferência. A situação diz o que aconteceu DEPOIS da marcação —
    /// é o que separa "marquei e o paciente veio" de "marquei e ele faltou".
    /// </summary>
    public static LinhaMarcacaoBalcao MontarMarcacao(Agendamento a)
    {
        var paciente = a.Paciente;
        var (situacao, tom) = a.Status switch
        {
            StatusAgendamento.Realizado => ("Atendido", "sucesso"),
            StatusAgendamento.Cancelado => ("Cancelado", "perigo"),
            StatusAgendamento.Faltou => ("Faltou", "perigo"),
            StatusAgendamento.Substituido => ("Lançado por fora", "neutro"),
            _ => ("Aguardando atendimento", "aviso")
        };
        var quem = a.Profissional?.Rotulo ?? "sem profissional";
        return new LinhaMarcacaoBalcao
        {
            AgendamentoId = a.Id,
            MarcadoEm = a.CriadoEm ?? a.DataHora,
            MarcadoRotulo = a.CriadoEm is { } m ? m.ToString("dd/MM HH:mm", PtBr) : "—",
            Sessao = $"{a.DataHora.ToString("dd/MM HH:mm", PtBr)} · {quem}",
            Paciente = paciente?.Nome ?? "—",
            Modalidade = CatalogoModalidades.NomeComEspecialidade(a.ModalidadeCodigo, a.ModalidadePrevista, a.EspecialidadeConsultaCodigo),
            Convenio = paciente is null
                ? "—"
                : CatalogoConvenios.Nome(paciente.ConvenioCodigo ?? paciente.Convenio.ToString()),
            Marcacao = string.IsNullOrWhiteSpace(a.CriadoPor) ? "sem registro de quem marcou" : $"por {a.CriadoPor}",
            Operador = string.IsNullOrWhiteSpace(a.CriadoPor) ? SemRegistroDeOperador : a.CriadoPor!,
            Situacao = situacao,
            TomSituacao = tom,
            Encaixe = a.Encaixe
        };
    }

    /// <summary>O mesmo recorte dos atendimentos, sem o "só com guia por liberar" (marcação não tem guia).</summary>
    public static bool Passa(LinhaMarcacaoBalcao l, FiltroLancamentos f)
        => (Livre(f.Convenio) || l.Convenio == f.Convenio)
           && (Livre(f.Modalidade) || l.Modalidade == f.Modalidade)
           && (Livre(f.Operador) || l.Operador == f.Operador);

    /// <summary>"12 horário(s) marcado(s)" ou "3 de 12" com filtro.</summary>
    public static string ResumoMarcacoes(int mostradas, int carregadas)
        => mostradas == carregadas ? $"{mostradas} horário(s) marcado(s)" : $"{mostradas} de {carregadas} horário(s) marcado(s)";

    /// <summary>"por Ana às 14:32".</summary>
    public static string DescreverLancamento(Atendimento atendimento)
    {
        if (string.IsNullOrWhiteSpace(atendimento.LancadoPor)) return "sem registro de quem lançou";
        return atendimento.LancadoEm is { } quando
            ? $"por {atendimento.LancadoPor} às {quando.ToString("HH:mm", PtBr)}"
            : $"por {atendimento.LancadoPor}";
    }

    public static bool Passa(LinhaLancamentoBalcao l, FiltroLancamentos f)
        => (Livre(f.Convenio) || l.Convenio == f.Convenio)
           && (Livre(f.Modalidade) || l.Modalidade == f.Modalidade)
           && (Livre(f.Operador) || l.Operador == f.Operador)
           && (!f.SomenteComPendencia || l.TemPendencia);

    /// <summary>
    /// As opções de um combo, tiradas do que FOI CARREGADO (não do catálogo — o combo existe
    /// para separar o que está na lista), com "Todos" primeiro.
    /// </summary>
    public static IReadOnlyList<string> Opcoes(IEnumerable<string> valores)
    {
        var comparador = StringComparer.Create(PtBr, ignoreCase: true);
        return [Todos, .. valores.Distinct(comparador).OrderBy(v => v, StringComparer.Create(PtBr, false))];
    }

    /// <summary>
    /// A escolha continua valendo só se ainda existe no período novo — senão volta a "Todos".
    /// Filtro invisível por um valor que sumiu esconderia a lista inteira sem dizer por quê.
    /// </summary>
    public static string Preservar(string? escolhido, IReadOnlyList<string> opcoes)
        => escolhido is not null && opcoes.Contains(escolhido) ? escolhido : Todos;

    /// <summary>"27 atendimento(s)" ou "8 de 27" quando algum filtro está ligado.</summary>
    public static string Resumo(int mostradas, int carregadas)
        => mostradas == carregadas ? $"{mostradas} atendimento(s)" : $"{mostradas} de {carregadas} atendimento(s)";

    /// <summary>Quantas linhas pedem atenção (guia que só libera depois) — o número do cartão.</summary>
    public static int ComPendencia(IEnumerable<LinhaLancamentoBalcao> linhas) => linhas.Count(l => l.TemPendencia);

    /// <summary>O recado depois do estorno, com os avisos das reversões de fora — eles não podem sumir com a janela.</summary>
    public static string RecadoDoEstorno(string numero, IReadOnlyList<string> avisos)
    {
        var recado = $"Atendimento nº {numero} estornado. O horário dele, se houver, voltou para \"Agendado\" e pode ser lançado de novo.";
        return avisos.Count > 0 ? recado + " " + string.Join(" ", avisos) : recado;
    }
}
