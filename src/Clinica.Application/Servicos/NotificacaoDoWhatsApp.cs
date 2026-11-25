namespace Clinica.Application.Servicos;

/// <summary>O que a notificação do Windows diz. <see cref="ConversaId"/>: a conversa que o clique abre (nula = a aba, na lista).</summary>
public sealed record AvisoDoWindows(string Titulo, string Texto, string? ConversaId);

/// <summary>
/// A NOTIFICAÇÃO DO WINDOWS E O NÚMERO NO ÍCONE (24/11/2026, F1b do "CRM com cara de WhatsApp", aprovado pelo dono): com o
/// app em segundo plano, cada mensagem nova de paciente vira uma notificação do Windows, e o ícone da barra de tarefas
/// leva o número de conversas não lidas — o MESMO do item "WhatsApp" do menu (<see cref="LeituraDaEspera.NaoLidas"/>).
/// Quem lê o CRM é o <see cref="VigiaDoWhatsApp"/> (o que chegou: <see cref="LeituraDaEspera.Chegaram"/>); aqui mora só o
/// texto, puro e com teste; quem desenha é o shell.
/// <para><b>Sem o texto da mensagem</b>: a notificação aparece no canto da tela do balcão, à vista de quem está na
/// recepção, e a mensagem do paciente pode falar de saúde (LGPD, art. 11). Ela diz QUEM escreveu; o que foi escrito se lê
/// na conversa, com o clique.</para>
/// </summary>
public static class NotificacaoDoWhatsApp
{
    public const string Titulo = "WhatsApp da clínica";

    /// <summary>Nulo quando nada chegou. Uma conversa: o nome, e o clique abre a conversa. Várias: quantas e de quem.</summary>
    public static AvisoDoWindows? Aviso(IReadOnlyList<MensagemQueChegou> chegaram)
    {
        if (chegaram.Count == 0) return null;
        var porConversa = chegaram.GroupBy(m => m.ConversaId, StringComparer.Ordinal).Select(g => g.First()).ToList();
        if (porConversa.Count == 1)
            return new(Titulo, $"{porConversa[0].Nome} mandou mensagem.", porConversa[0].ConversaId);
        var nomes = porConversa.Select(m => m.Nome).ToList();
        var quem = nomes.Count switch
        {
            2 => $"{nomes[0]} e {nomes[1]}",
            3 => $"{nomes[0]}, {nomes[1]} e {nomes[2]}",
            _ => $"{nomes[0]}, {nomes[1]} e mais {nomes.Count - 2}"
        };
        return new(Titulo, $"{porConversa.Count} conversas com mensagem nova: {quem}.", null);
    }

    /// <summary>O número no ícone da barra de tarefas: nada com zero, "9+" acima de 9 (a bolinha é pequena).</summary>
    public static string? NoIcone(int naoLidas) => naoLidas switch
    {
        <= 0 => null,
        <= 9 => naoLidas.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => "9+"
    };

    /// <summary>A dica do ícone (passar o mouse no botão da barra de tarefas).</summary>
    public static string? DicaDoIcone(int naoLidas) => naoLidas switch
    {
        <= 0 => null,
        1 => "1 conversa não lida no WhatsApp",
        _ => $"{naoLidas} conversas não lidas no WhatsApp"
    };
}
