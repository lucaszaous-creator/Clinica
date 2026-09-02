using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Uma sessão ANTERIOR resumida, como ela aparece ao lado de quem está escrevendo a de hoje
/// (parcela 77).
///
/// O que ela conserta
/// ------------------
/// O painel das últimas sessões nasceu com QUATRO campos e a sessão passou a ter doze
/// (parcelas 73, 75 e 77): ele mostrava data, EVA, queixa e conduta, e ficava cego para a
/// hipótese, o plano, o encaminhamento e — o que mais dói — o <b>retorno sugerido</b>, que é
/// literalmente a resposta para "por que este paciente está aqui hoje". O profissional
/// escrevia "voltar em 7 dias para reavaliar a EVA", o paciente voltava, e a tela não dizia.
/// A EVOLUÇÃO, o texto mais escrito do sistema, estava no modelo e não estava na tela.
///
/// É o mesmo defeito do modelo de evolução (parcela 76) num segundo leitor, e a lição vale
/// para o próximo campo: <b>campo novo de evolução entra também em quem MOSTRA a sessão
/// anterior.</b>
///
/// Por que mora AQUI e não na ViewModel
/// ------------------------------------
/// ⚠️ A composição tem decisões — a hipótese com o CID entre parênteses, o CID sozinho
/// quando é só ele, a linha vazia quando não há o que dizer —, e decisão que mora em projeto
/// WPF não é alcançada pelo <c>dotnet test</c>. É a lição da grade da semana (parcela 69):
/// tudo o que decide um desenho precisa morar onde o teste chega, senão a regra vive de
/// inspeção visual.
///
/// As linhas vêm PRONTAS e vazias quando não há conteúdo: a coluna tem ~350 px e três
/// sessões, e uma linha escrita "—" gasta o mesmo espaço de uma que informa. O texto inteiro
/// continua na aba "Histórico de sessões", que é onde se lê com calma.
///
/// ⚠️ É a MESMA composição que o Histórico de sessões lê. Ele nasceu com os quatro campos
/// originais e ficou assim por mais quatro parcelas depois de este resumo ter sido corrigido
/// — ou seja, a frase acima ("o texto inteiro continua no Histórico") era promessa que o
/// código não cumpria: a história da doença, o exame físico, a hipótese, o plano, o retorno
/// e o encaminhamento não eram legíveis por inteiro em lugar NENHUM. Duas telas que
/// respondem à mesma pergunta sobre o mesmo dado leem uma definição só; a diferença entre
/// elas é o CORTE (o painel corta, o histórico quebra a linha), e corte é decisão de tela.
/// </summary>
public sealed record ResumoSessaoAnterior(
    int EvolucaoId,
    string Data,
    string Eva,
    string Queixa,
    string Historia,
    string ExameFisico,
    string Hipotese,
    string Conduta,
    string Evolucao,
    string Orientacoes,
    string Plano,
    string Retorno,
    string Encaminhamento)
{
    public static ResumoSessaoAnterior De(Evolucao e) => new(
        EvolucaoId: e.Id,
        Data: e.Data.ToString("dd/MM/yyyy"),
        Eva: e.TemParEva ? $"EVA {e.EvaAntes} \u2192 {e.EvaDepois}" : "EVA n\u00E3o medida",
        Queixa: Rotular("Queixa", e.QueixaPrincipal),
        Historia: Rotular("Hist\u00F3ria da doen\u00E7a atual", e.HistoriaDoencaAtual),
        ExameFisico: Rotular("Exame f\u00EDsico", e.ExameFisico),
        Hipotese: DaHipotese(e),
        Conduta: Rotular("Conduta", e.Conduta),
        Evolucao: Rotular("Evolu\u00E7\u00E3o", e.TextoEvolucao),
        Orientacoes: Rotular("Orienta\u00E7\u00F5es", e.Orientacoes),
        Plano: Rotular("Plano", e.PlanoTerapeutico),
        Retorno: DoRetorno(e),
        Encaminhamento: Rotular("Encaminhado", e.Encaminhamento));

    private static string Rotular(string rotulo, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? string.Empty : $"{rotulo}: {valor.Trim()}";

    /// <summary>A hipótese com o CID entre parênteses — e o CID sozinho quando é só ele.</summary>
    private static string DaHipotese(Evolucao e)
    {
        var tem = !string.IsNullOrWhiteSpace(e.HipoteseDiagnostica);
        var cid = !string.IsNullOrWhiteSpace(e.CidSessao);

        return (tem, cid) switch
        {
            (true, true) => $"Hip\u00F3tese: {e.HipoteseDiagnostica!.Trim()} ({e.CidSessao!.Trim()})",
            (true, false) => $"Hip\u00F3tese: {e.HipoteseDiagnostica!.Trim()}",
            (false, true) => $"CID: {e.CidSessao!.Trim()}",
            _ => string.Empty
        };
    }

    /// <summary>
    /// "↩ Voltar em 27/08/2026 — reavaliar a EVA". É o único campo do painel que a tela NÃO
    /// corta: ele é curto, e é a razão da consulta de hoje.
    /// </summary>
    private static string DoRetorno(Evolucao e)
    {
        if (e.RetornoSugeridoEm is not { } quando) return string.Empty;

        return string.IsNullOrWhiteSpace(e.RetornoSugeridoNota)
            ? $"\u21A9 Voltar em {quando:dd/MM/yyyy}"
            : $"\u21A9 Voltar em {quando:dd/MM/yyyy} \u2014 {e.RetornoSugeridoNota!.Trim()}";
    }
}
