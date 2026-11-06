using System.Globalization;
using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>
/// Uma conversa em que o PACIENTE espera a clínica (25/10, R8 — regra 3 da direção: o que pede ação vira
/// aviso): uma linha da seção do sino e uma unidade do número no item "WhatsApp" do menu.
/// </summary>
/// <param name="Desde">A última mensagem do paciente (o <c>LastIncoming</c> do CRM).</param>
/// <param name="Chave">
/// A marca de leitura do sino: <c>wa-espera:{conversa}:{ticks da última recebida}</c>. A mesma espera é UM
/// aviso (a leitura de 60 s não o repete); a mensagem nova depois da resposta é outra espera, e outro aviso.
/// </param>
public sealed record ConversaQueEspera(string Id, string Nome, DateTimeOffset Desde, int Minutos, string Chave);

public static partial class ConversasNaTela
{
    /// <summary>Depois de quantos minutos sem resposta a espera vira aviso (decisão da direção, 25/10).</summary>
    public const int MinutosDeEspera = 5;

    /// <summary>
    /// Quem espera a clínica há <paramref name="minutos"/> ou mais: conversa ABERTA, fora do menu automático
    /// do CRM, cuja última mensagem é do PACIENTE, e que é de ninguém ("Novas") ou de quem está logado
    /// (<paramref name="meuAgente"/>). A conversa de outra pessoa é dela — avisar a todos ensinaria a ignorar o
    /// aviso; quem saiu sem passar o turno aparece no painel "responsável sem sinal" do CRM, agora que o app
    /// pulsa a presença. A espera conta da ÚLTIMA mensagem do paciente (é o que a lista traz): com três
    /// mensagens seguidas, o aviso sai cinco minutos depois da terceira. Quem espera há mais tempo vem primeiro.
    /// </summary>
    public static IReadOnlyList<ConversaQueEspera> PedemResposta(IEnumerable<AppConversationSummary> conversas,
        DateTimeOffset agora, int minutos = MinutosDeEspera, string? meuAgente = null)
    {
        var limite = TimeSpan.FromMinutes(Math.Max(0, minutos));
        return conversas
            .Where(c => !c.Closed && !c.AwaitingMenu
                && (c.AgentId is null || meuAgente is not null && c.AgentId == meuAgente)
                && string.Equals(c.PreviewDirection, "in", StringComparison.OrdinalIgnoreCase)
                && PacienteJaEscreveu(c.LastIncoming)
                && agora - c.LastIncoming >= limite)
            .OrderBy(c => c.LastIncoming).ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => new ConversaQueEspera(c.Id, NomeDaConversa(c), c.LastIncoming, (int)(agora - c.LastIncoming).TotalMinutes,
                $"wa-espera:{c.Id}:{c.LastIncoming.UtcTicks.ToString(CultureInfo.InvariantCulture)}"))
            .ToList();
    }

    /// <summary>"há 7 min", "há 1 h", "há 2 h 15 min", "há 3 dias" — quanto o paciente espera, na linha do sino.</summary>
    public static string HaQuanto(int minutos) => minutos switch
    {
        < 1 => "agora",
        < 60 => $"há {minutos} min",
        < 24 * 60 => minutos % 60 == 0 ? $"há {minutos / 60} h" : $"há {minutos / 60} h {minutos % 60} min",
        _ => minutos / (24 * 60) == 1 ? "há 1 dia" : $"há {minutos / (24 * 60)} dias"
    };

    /// <summary>A notificação passageira da espera nova: "Maria das Graças espera resposta no WhatsApp há 6 min".</summary>
    public static string FraseDaEspera(ConversaQueEspera e) => $"{e.Nome} espera resposta no WhatsApp {HaQuanto(e.Minutos)}";

    /// <summary>O número no item "WhatsApp" do menu: nada quando ninguém espera, "99+" acima de 99 (o item não cresce).</summary>
    public static string? ContadorDoMenu(int esperando) => esperando switch
    {
        <= 0 => null,
        > 99 => "99+",
        _ => esperando.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>O cabeçalho da seção do sino: "1 espera resposta", "3 esperam resposta".</summary>
    public static string ContadorDoSino(int esperando) => esperando == 1 ? "1 espera resposta" : $"{esperando} esperam resposta";
}
