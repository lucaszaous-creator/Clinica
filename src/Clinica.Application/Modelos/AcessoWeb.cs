using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// O QUE A WEB DE LEITURA DEIXA VER (set/2026) — a regra, onde o <c>dotnet test</c> a
/// alcança.
///
/// Ela responde uma pergunta por página, e a resposta é sempre a MESMA do desktop: os bits
/// da parcela 49 (<see cref="Permissao.VerAgenda"/>, <see cref="Permissao.VerFichaPaciente"/>,
/// <see cref="Permissao.VerProntuario"/>, <see cref="Permissao.VerIndicadores"/>). Uma
/// segunda régua de acesso — "na web todo mundo vê o resumo" — seria a permissão granular
/// desfeita por uma porta nova, que é exatamente o que a parcela 60 achou nas cópias.
///
/// ⚠️ <b>A web NÃO ESCREVE no prontuário.</b> A única escrita que ela faz é a TRILHA DE
/// ACESSO, e ela é obrigatória: o ponto 4 do compromisso de conformidade vale para toda
/// tela que ABRE prontuário, e uma porta de leitura sem trilha é o buraco que só aparece
/// no dia em que alguém precisa investigar.
/// </summary>
public static class AcessoWeb
{
    /// <summary>Uma página da web de leitura e o bit que ela exige.</summary>
    public sealed record Pagina(string Rota, string Titulo, Permissao Exige, string Resposta);

    /// <summary>
    /// As páginas, na ordem do menu. É a lista ÚNICA: o menu, o roteamento e o teste leem
    /// daqui — três listas divergiriam, e a que ficasse para trás ofereceria um link que
    /// leva a uma recusa.
    /// </summary>
    public static IReadOnlyList<Pagina> Paginas { get; } =
    [
        new("/dia", "O dia", Permissao.VerAgenda,
            "quem está marcado hoje, em que pé está cada um e o que já foi lançado"),
        new("/painel", "O mês", Permissao.VerIndicadores,
            "os números do mês e o que está vencendo — o painel da direção, só de leitura"),
        new("/pacientes", "Pacientes", Permissao.VerFichaPaciente,
            "achar a ficha de alguém: contato, convênio e as próximas sessões")
    ];

    /// <summary>As páginas que ESTE usuário alcança — o menu não mostra o que ele não abre.</summary>
    public static IReadOnlyList<Pagina> Do(Permissao efetivas)
        => Paginas.Where(p => efetivas.HasFlag(p.Exige)).ToList();

    /// <summary>
    /// Onde este usuário cai ao entrar: a primeira página que ele alcança.
    ///
    /// Nulo quer dizer que ele não alcança NENHUMA — e aí a porta recusa DIZENDO isso, em
    /// vez de deixar entrar numa tela vazia. É a regra da parcela 45: "deixar entrar e
    /// mostrar sidebar vazia faz a pessoa ligar para o suporte em vez de falar com a
    /// direção".
    /// </summary>
    public static string? AberturaDe(Permissao efetivas) => Do(efetivas).FirstOrDefault()?.Rota;

    /// <summary>
    /// O PRONTUÁRIO na ficha exige o bit dele, sempre — é o corte da parcela 49 (dado de
    /// contato de um lado, dado de saúde do art. 5º, II do outro), e ele não afrouxa por a
    /// tela ser web.
    /// </summary>
    public static bool MostraProntuario(Permissao efetivas)
        => efetivas.HasFlag(Permissao.VerProntuario);
}
