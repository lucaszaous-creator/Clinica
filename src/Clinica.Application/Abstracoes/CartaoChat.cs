using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clinica.Application.Abstracoes;

/// <summary>
/// O cartão estruturado de uma mensagem do chat (out/2026): a resposta do Sistema SemDor (a agenda
/// do dia com a situação de cada horário) e o pedido à recepção (com a situação dele). É UM formato
/// genérico de propósito — título, números de resumo, linhas com selo e botões de sugestão —, para
/// a mesma descrição ser desenhada igual pelo chat dos apps (Blazor) e pelo CRM (JavaScript), sem
/// cada tipo de resposta ganhar um desenho próprio nos dois lados.
/// </summary>
/// <param name="Tipo">"agenda", "lista", "pedido", "ajuda" — o desenho é o mesmo; o tipo só muda o ícone.</param>
/// <param name="Resumo">Os números do topo ("8 confirmaram"), cada um com o seu tom.</param>
/// <param name="Linhas">As linhas (um horário, um paciente, um pedido).</param>
/// <param name="Sugestoes">Perguntas prontas que viram botão ("Amanhã", "Quem não confirmou").</param>
/// <param name="Rodape">Uma linha de rodapé ("Mostrando 40 de 52 — abra a Agenda para ver todos").</param>
/// <param name="PedidoId">No cartão de pedido, o pedido (a tela lê as ações por ele).</param>
/// <param name="Situacao">No cartão de pedido, "Aberto", "EmAndamento", "Concluido" ou "NaoFeito".</param>
public sealed record CartaoChat(
    string Tipo,
    string Titulo,
    string? Subtitulo = null,
    IReadOnlyList<ResumoDoCartao>? Resumo = null,
    IReadOnlyList<LinhaDoCartao>? Linhas = null,
    IReadOnlyList<string>? Sugestoes = null,
    string? Rodape = null,
    int? PedidoId = null,
    string? Situacao = null)
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ParaJson() => JsonSerializer.Serialize(this, Opcoes);

    /// <summary>O cartão gravado; nulo quando não há ou quando o JSON não se lê (a mensagem ainda mostra o texto).</summary>
    public static CartaoChat? DeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<CartaoChat>(json, Opcoes); }
        catch (JsonException) { return null; }
    }
}

/// <summary>Um número do resumo: "8" + "confirmaram", no tom "ok".</summary>
public sealed record ResumoDoCartao(string Rotulo, int Valor, string Tom = TomDoCartao.Neutro);

/// <summary>
/// Uma linha do cartão. <paramref name="PacienteId"/> abre a ficha (para quem pode);
/// <paramref name="AgendamentoId"/> com <paramref name="PodePedir"/> oferece "Pedir à recepção".
/// </summary>
public sealed record LinhaDoCartao(
    string Hora,
    string Titulo,
    string? Detalhe = null,
    string? Selo = null,
    string Tom = TomDoCartao.Neutro,
    int? PacienteId = null,
    int? AgendamentoId = null,
    bool PodePedir = false);

/// <summary>Os tons do selo e do resumo — as mesmas quatro cores do sino e da agenda.</summary>
public static class TomDoCartao
{
    public const string Neutro = "neutro";
    public const string Ok = "ok";
    public const string Atencao = "atencao";
    public const string Alerta = "alerta";
    public const string Info = "info";
}
