namespace Clinica.Application.Servicos;

/// <summary>Quem a recepção quer chamar pelo WhatsApp da clínica, e a mensagem que ela vai revisar.</summary>
/// <param name="PacienteId">A ficha que a recepção escolheu aqui — o CRM a liga à conversa se ela ainda não tiver uma.</param>
public sealed record PedidoDeConversa(string Telefone, string Nome, int? PacienteId, string? Texto);

/// <summary>
/// ESCREVER PELO WHATSAPP DA CLÍNICA (out/2026, fase 2 da unificação Recepção × CRM —
/// docs/crm-na-recepcao.md). Os botões "Escrever no WhatsApp" das telas da Recepção deixam de abrir o
/// wa.me (o WhatsApp do computador, fora do número oficial e sem registro) e levam à aba WhatsApp: a
/// tela abre a conversa do paciente, liga a ficha e deixa a mensagem como rascunho. Onde a aba não
/// existe (app sem o item, CRM sem endereço, usuário sem o bit), o botão continua no wa.me.
/// O pedido vai em memória (<c>PedidoDeConversaNoCrm</c>), nunca em URL (fase 3, 24/10: a moldura saiu).
/// </summary>
public static class ConversaNoCrm
{
    /// <summary>A chave da aba WhatsApp da Recepção (o item do menu e o destino da navegação).</summary>
    public const string ChaveDaAba = "whatsapp-crm";

    /// <summary>Os dígitos do telefone do cadastro, ou <c>null</c> quando ele não serve para mensagem (10 a 13 dígitos).</summary>
    public static string? Telefone(string? cadastro)
    {
        var d = new string((cadastro ?? "").Where(char.IsAsciiDigit).ToArray());
        return d.Length is >= 10 and <= 13 ? d : null;
    }
}
