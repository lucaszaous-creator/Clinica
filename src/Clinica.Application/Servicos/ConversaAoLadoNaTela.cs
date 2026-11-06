using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>As conversas de UM paciente que o painel ao lado oferece, a escolhida primeiro.</summary>
/// <param name="Conversas">A mais recente primeiro. Vazia: o paciente ainda não tem conversa.</param>
/// <param name="PeloTelefone">
/// Nenhuma conversa está LIGADA à ficha; estas são do telefone dela, ainda sem ficha nenhuma. O
/// painel avisa para conferir: o telefone não é identidade (regra 4 das telas de quem atende).
/// </param>
public sealed record ConversasDaFicha(IReadOnlyList<AppConversationSummary> Conversas, bool PeloTelefone)
{
    /// <summary>A que o painel abre: a mais recente.</summary>
    public AppConversationSummary? Escolhida => Conversas.Count > 0 ? Conversas[0] : null;

    /// <summary>Mais de uma: o painel mostra o seletor.</summary>
    public bool TemEscolha => Conversas.Count > 1;
}

/// <summary>
/// A CONVERSA AO LADO (out/2026, fase 2 do WhatsApp nativo — docs/whatsapp-nativo-implementacao.md,
/// "Fase 2 — a conversa ao lado"). A Agenda do dia, a Ficha, as Confirmações e o Acompanhamento abrem a
/// conversa do paciente num painel à direita, sem trocar de tela. O painel é a MESMA tela de conversa
/// (<c>ConversasDoWhatsApp</c> no modo painel); o que ele AFIRMA a mais mora aqui, puro e com teste:
/// <list type="bullet">
/// <item><b>Qual conversa abrir</b> (<see cref="DoPaciente"/>): as LIGADAS à ficha (<c>PatientId</c>), a
/// mais recente primeiro. Sem nenhuma ligada, as do telefone da ficha que ainda não têm ficha — nunca a
/// conversa ligada a OUTRA ficha com o mesmo número (a mãe que divide o celular com o filho): seria
/// mostrar a conversa de outra pessoa.</item>
/// <item><b>As frases</b> do aviso de conferir, do seletor e do paciente sem conversa.</item>
/// </list>
/// Se o painel serve neste app é <see cref="WhatsAppDaRecepcaoService.ConversaAoLadoAsync"/> (a tela
/// nativa escolhida e o CRM com endereço) mais a aba WhatsApp existir para quem entrou (o hospedeiro).
/// </summary>
public static class ConversaAoLadoNaTela
{
    /// <summary>As conversas do paciente para o painel, na ordem em que o seletor as mostra.</summary>
    /// <param name="pacienteId">A ficha de onde se pediu (nula: só o telefone — o pedido sem ficha).</param>
    /// <param name="telefone">O telefone da ficha, como está no cadastro.</param>
    public static ConversasDaFicha DoPaciente(IEnumerable<AppConversationSummary> conversas, int? pacienteId, string? telefone)
    {
        var todas = conversas as IReadOnlyCollection<AppConversationSummary> ?? conversas.ToList();
        if (pacienteId is { } id)
        {
            var ligadas = MaisRecentesPrimeiro(todas.Where(c => c.PatientId == id));
            if (ligadas.Count > 0) return new ConversasDaFicha(ligadas, PeloTelefone: false);
        }
        var doTelefone = MaisRecentesPrimeiro(todas.Where(c => c.PatientId is null && ConversasNaTela.MesmoTelefone(c.Contact, telefone)));
        return new ConversasDaFicha(doTelefone, PeloTelefone: doTelefone.Count > 0);
    }

    private static List<AppConversationSummary> MaisRecentesPrimeiro(IEnumerable<AppConversationSummary> conversas)
        => conversas.OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();

    /// <summary>
    /// O aviso acima da conversa que veio pelo TELEFONE: ela ainda não tem ficha, e quem confere quem
    /// escreve é a recepção — o painel não liga a ficha sozinho.
    /// </summary>
    public static (string Titulo, string Texto) AvisoDoTelefone(string? nome)
        => ("Pelo telefone da ficha:",
            $"esta conversa ainda não está ligada à ficha de {ConversasNaTela.PrimeiroNomeOu(nome, "quem você abriu")}. Confira quem escreve antes de responder.");

    /// <summary>Uma opção do seletor: o canal, o número, a situação e a última atividade.</summary>
    public static string Opcao(AppConversationSummary c, DateTimeOffset agora)
        => $"{(ConversasNaTela.EhSms(c.Channel) ? "SMS" : "WhatsApp")} · {ConversasNaTela.TelefoneNaTela(c.Contact)} · "
           + $"{(c.Closed ? "encerrada" : "aberta")} · {ConversasNaTela.Quando(c.UpdatedAt, agora)}";

    /// <summary>
    /// O paciente ainda sem conversa (e sem caso no Acompanhamento — esse abre o convite da retenção):
    /// quem começa é a clínica, e a primeira mensagem é um modelo aprovado.
    /// </summary>
    public static (string Titulo, string Texto) SemConversa(string? nome)
        => ($"Ainda não há conversa com {ConversasNaTela.PrimeiroNomeOu(nome, "este paciente")}",
            "Quem começa é a clínica: a primeira mensagem é um modelo aprovado pela Meta. Quando a pessoa responder, o campo livre abre.");

    /// <summary>O rótulo do botão que começa a conversa (o formulário "Nova conversa", já preenchido).</summary>
    public const string Comecar = "Começar com um modelo";
}
