using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Formatacao;

/// <summary>
/// O que o formulário de CONCILIAÇÃO DA AGENDA afirma e decide (out/2026, migrado do
/// <c>ConciliacaoAgendaViewModel</c> WPF; o mapa é <c>docs/conciliacao-da-agenda.md</c>).
///
/// A pergunta tem TRÊS respostas — faltou · aconteceu e ninguém lançou · aconteceu e já foi
/// lançada por fora —, e a guarda que vale por toda a tela é a do meio: com sessão no dia, o
/// botão de LANÇAR fica apagado COM a frase ao lado (lançar criaria um segundo jogo de guias
/// para a mesma sessão; botão cinza sem explicação vira "o sistema travou", parcela 41).
///
/// Ela não decide nada sozinha: ausência de clique não é prova de falta.
/// </summary>
public static class ConciliacaoAgendaNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"Foi falta": o bit EXATO do <c>Exigir</c> do VM WPF.</summary>
    public const Permissao BitFalta = Permissao.EditarAgenda;

    /// <summary>
    /// "Aconteceu — lançar" e "Já foi lançada — encerrar": os DOIS bits juntos (o <c>Exigir</c>
    /// do VM WPF com bits somados é um E, não um OU).
    /// </summary>
    public const Permissao BitsLancarOuEncerrar = Permissao.EditarAgenda | Permissao.LancarAtendimento;

    public const string Titulo = "Conferir atendimentos";

    public const string Subtitulo =
        "Horários cuja data já passou e que ninguém resolveu. Enquanto o check-in pela agenda não é usado, é o estado normal de quem foi atendido por fora — mas o horário parado infla a ocupação, põe no “Meu dia” do médico um paciente que não vem, e fica com a evolução da sessão de verdade.";

    public const string FalhaTitulo = "A conciliação não pôde ser lida";

    public const string FalhaDetalhe =
        "Não foi possível levantar a conciliação agora. A lista NÃO está vazia por não haver pendência — ela não pôde ser lida.";

    /// <summary>A guarda escrita, e não só o botão apagado (parcela 41).</summary>
    public const string TextoJaLancada =
        "A sessão deste dia já está lançada — lançar de novo criaria um segundo jogo de guias. Use “Já foi lançada — encerrar”: o horário aponta para a sessão e sai da agenda sem contar como falta nem cancelamento.";

    public const string TituloOrfaos = "Marcados como realizados, sem atendimento";

    public const string TextoOrfaos =
        "Estes horários dizem “Finalizado” e não apontam para atendimento nenhum: não há guia, e o repasse do profissional não conta a sessão. Não há ação automática — anote o caso e confira com quem atendeu.";

    public const string Vazio = "Nada pendurado entre a agenda e os atendimentos.";

    public static string Periodo(ConciliacaoDaAgenda c) => $"De {Data(c.Desde)} a {Data(c.Ate)}.";

    public static string Resumo(ConciliacaoDaAgenda c) => c.Vazio
        ? Vazio
        : $"{c.Parados.Count} horário(s) parado(s) — {c.ComSessaoNoDia} com sessão já lançada no dia, "
          + $"{c.SemSessaoNoDia} sem sessão nenhuma."
          + (c.Orfaos.Count > 0
              ? $" E {c.Orfaos.Count} horário(s) marcado(s) como realizados SEM atendimento."
              : string.Empty);

    public static string Cabecalho(DateTime quando, string paciente) => $"{DataHora(quando)} · {paciente}";

    public static string Origem(bool importado) => importado ? "Importado do sistema anterior" : "Marcado aqui";

    public static string Parado(int dias) => dias == 1 ? "parado há 1 dia" : $"parado há {dias} dias";

    /// <summary>Sem profissional no horário, o selo diz "—" — selo vazio não se lê.</summary>
    public static string Profissional(string? nome) => string.IsNullOrWhiteSpace(nome) ? "—" : nome;

    /// <summary>LANÇAR só sem sessão no dia — a guarda mais importante desta tela.</summary>
    public static bool PodeLancar(HorarioParado h, bool resolvida) => !h.TemSessaoNoDia && !resolvida;

    public static bool PodeMarcarFalta(bool resolvida) => !resolvida;

    /// <summary>ENCERRAR pela sessão lançada só COM sessão no dia, pela razão inversa do lançar.</summary>
    public static bool PodeSubstituir(HorarioParado h, bool resolvida) => h.TemSessaoNoDia && !resolvida;

    // ---------- As perguntas (diálogos) ----------

    public const string TituloFalta = "Marcar falta?";

    public static string PerguntaFalta(HorarioParado h)
        => $"{h.Paciente} — horário de {DataHora(h.DataHora)}.\n\n"
           + "Marcar como FALTA registra que o paciente não veio: entra nos indicadores "
           + "de falta e no histórico de relacionamento dele.\n\nConfirma?";

    public const string TituloLancar = "Lançar atendimento retroativo?";

    public static string PerguntaLancar(HorarioParado h)
        => $"{h.Paciente} — horário de {DataHora(h.DataHora)}.\n\n"
           + "O atendimento nasce datado do DIA DO HORÁRIO, não de hoje — é a data em "
           + "que a sessão aconteceu. As guias nascem com a data prevista daquele dia, "
           + "então já entram no painel de pendências do faturamento.\n\n"
           + "A modalidade usada é a prevista no horário. Se ela estiver errada, "
           + "corrija pela agenda antes.\n\nLançar mesmo assim?";

    public const string TituloQualSessao = "Qual sessão encerra este horário?";

    public static string PerguntaQualSessao(HorarioParado h)
        => $"{h.Paciente} tem {h.SessoesDoDia.Count} sessões lançadas em {Data(h.DataHora)}:\n\n"
           + string.Join("\n", h.SessoesDoDia.Select(s => "• " + s.Resumo))
           + $"\n\nDigite o número da sessão ({Numeros(h)}):";

    public const string TituloSubstituir = "Encerrar o horário pela sessão lançada?";

    public static string PerguntaSubstituir(HorarioParado h, SessaoLancadaNoDia sessao)
        => $"{h.Paciente} — horário de {DataHora(h.DataHora)}.\n\n"
           + $"O horário será encerrado apontando para a sessão {sessao.Resumo}. Ele sai da "
           + "ocupação, do dia do profissional e da conciliação — sem contar como "
           + "cancelamento nem como falta, porque a sessão aconteceu.\n\n"
           + "Se depois se descobrir que a sessão é OUTRA, o Remarcar da agenda reabre o "
           + "horário.\n\nConfirma?";

    // ---------- As recusas (a segunda barreira, escrita) ----------

    public static string JaTemSessao(HorarioParado h)
        => $"{h.Paciente} JÁ tem sessão lançada em {Data(h.DataHora)}. "
           + "Lançar de novo criaria um segundo jogo de guias para a mesma sessão.";

    public static string SemSessao(HorarioParado h)
        => $"{h.Paciente} NÃO tem sessão lançada em {Data(h.DataHora)}. "
           + "Sem sessão, a resposta é falta ou lançar retroativo.";

    public static string NumeroQueNaoExiste(HorarioParado h, string escolhido)
        => $"\"{escolhido}\" não é o número de nenhuma das sessões do dia ({Numeros(h)}).";

    /// <summary>
    /// A sessão que encerra o horário: a única, ou a digitada (sem diferença de caixa nem de
    /// espaço). Nulo = o número digitado não é de nenhuma — a tela diz quais são.
    /// </summary>
    public static SessaoLancadaNoDia? SessaoEscolhida(HorarioParado h, string? escolhido)
    {
        if (h.SessoesDoDia.Count == 1) return h.SessoesDoDia[0];
        if (escolhido is null) return null;
        return h.SessoesDoDia.FirstOrDefault(s =>
            string.Equals(s.Numero.Trim(), escolhido.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Convênio "a definir" (parcela 92): o lançamento é recusado até alguém escolher. Esta
    /// tela não abre a janela do shell; sem a ponte, ela diz ONDE se escolhe.
    /// </summary>
    public static string ConvenioADefinirSemPonte(string mensagem)
        => mensagem + " Escolha o convênio na ficha do paciente e volte para lançar.";

    public const string ConvenioNaoEscolhido =
        "O convênio não foi escolhido — o atendimento NÃO foi lançado. Sem convênio a sessão não gera guia.";

    // ---------- Os desfechos ----------

    /// <summary>
    /// Os avisos do serviço (guia já baixada que a falta não suspende, recado do 2º código)
    /// viajam no desfecho — descartá-los era o defeito da parcela 62 nesta porta.
    /// </summary>
    public static string DesfechoFalta(IReadOnlyList<string> avisos) => ComAvisos("Falta registrada.", avisos);

    public static string DesfechoLancamento(Atendimento atendimento, IReadOnlyList<string> avisos)
        => ComAvisos($"Atendimento nº {atendimento.Numero} lançado com {atendimento.Codigos.Count} código(s).", avisos);

    public static string DesfechoSubstituicao(SessaoLancadaNoDia sessao, IReadOnlyList<string> avisos)
        => ComAvisos($"Horário encerrado pela sessão nº {sessao.Numero}.", avisos);

    private static string ComAvisos(string frase, IReadOnlyList<string> avisos)
        => avisos.Count > 0 ? frase + " " + string.Join(" ", avisos) : frase;

    private static string Numeros(HorarioParado h) => string.Join(" ou ", h.SessoesDoDia.Select(s => s.Numero));

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);

    private static string Data(DateTime d) => d.ToString("dd/MM/yyyy", PtBr);

    private static string DataHora(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", PtBr);
}
