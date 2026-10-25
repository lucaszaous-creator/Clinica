using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>Uma conversa que não voltou à fila na passagem do turno, com a frase do porquê.</summary>
public sealed record FalhaDaPassagem(string Id, string Nome, string Motivo);

/// <summary>O que a passagem do turno fez: quem voltou à fila e quem ficou (com o motivo de cada uma).</summary>
public sealed record ResultadoDaPassagem(IReadOnlyList<string> Passaram, IReadOnlyList<FalhaDaPassagem> Falharam);

/// <summary>
/// A TROCA DE TURNO NUM GESTO (25/10, D12 — decisão da direção): no filtro "Minhas", "Passar o turno" devolve
/// TODAS as conversas abertas de quem está saindo à fila de cada uma, com UMA nota que vai em todas. Antes, a
/// secretária abria uma por uma; esquecida uma, o paciente esperava na conversa de quem já tinha ido embora.
/// Aqui moram a lista, a nota e o relato; a devolução é a MESMA do "Com você → Devolver"
/// (<see cref="WhatsAppDaRecepcaoService.PassarOTurnoAsync"/>).
/// </summary>
public static class TurnoNoWhatsApp
{
    /// <summary>As conversas que passam: as abertas de quem está logado — exatamente o filtro "Minhas", na mesma ordem.</summary>
    public static IReadOnlyList<AppConversationSummary> DoTurno(IEnumerable<AppConversationSummary> conversas, string? meuAgente)
        => ConversasNaTela.Filtrar(conversas, FiltroDasConversas.Minhas, meuAgente);

    /// <summary>A nota obrigatória (o mesmo tamanho do motivo de devolver: ela entra em cada conversa). Nulo = serve.</summary>
    public static string? Impedimento(string? nota)
    {
        var n = nota?.Trim() ?? "";
        return n.Length < WhatsAppDaRecepcaoService.MinimoDoMotivoDeDevolver
            ? $"Escreva a nota da passagem ({WhatsAppDaRecepcaoService.MinimoDoMotivoDeDevolver} letras ou mais): quem assumir cada conversa lê."
            : n.Length > WhatsAppDaRecepcaoService.MaximoDoMotivoDeDevolver
                ? $"A nota passou de {WhatsAppDaRecepcaoService.MaximoDoMotivoDeDevolver} letras."
                : null;
    }

    /// <summary>O botão no filtro "Minhas": "Passar o turno (3)".</summary>
    public static string Rotulo(int conversas) => conversas > 0 ? $"Passar o turno ({conversas})" : "Passar o turno";

    /// <summary>"3 conversas voltaram à fila." · "2 de 3 voltaram à fila. Ficaram com você: Maria (…)." — o que aconteceu, sem esconder a que ficou.</summary>
    public static string Relato(ResultadoDaPassagem r)
    {
        var total = r.Passaram.Count + r.Falharam.Count;
        if (total == 0) return "Nenhuma conversa estava com você.";
        if (r.Falharam.Count == 0)
            return r.Passaram.Count == 1 ? "A conversa voltou à fila. Quem assumir lê a sua nota." : $"{r.Passaram.Count} conversas voltaram à fila. Quem assumir cada uma lê a sua nota.";
        var ficaram = string.Join("; ", r.Falharam.Select(f => $"{f.Nome} ({f.Motivo.TrimEnd('.')})"));
        return r.Passaram.Count == 0
            ? $"Nenhuma conversa voltou à fila. Ficaram com você: {ficaram}."
            : $"{r.Passaram.Count} de {total} voltaram à fila. Ficaram com você: {ficaram}.";
    }

    /// <summary>Quando a conversa já não estava com a pessoa ao repetir (outra a assumiu, ou encerrou).</summary>
    public const string JaNaoEraSua = "já não estava com você";
}
