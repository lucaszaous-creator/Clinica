using System.Text.Json;

namespace Clinica.Application.Servicos;

/// <summary>Quem a recepção quer chamar pelo WhatsApp da clínica, e a mensagem que ela vai revisar.</summary>
/// <param name="PacienteId">A ficha que a recepção escolheu aqui — o CRM a liga à conversa se ela ainda não tiver uma.</param>
public sealed record PedidoDeConversa(string Telefone, string Nome, int? PacienteId, string? Texto);

/// <summary>
/// ESCREVER PELO WHATSAPP DA CLÍNICA (out/2026, fase 2 da unificação Recepção × CRM —
/// docs/crm-na-recepcao.md). Os botões "Escrever no WhatsApp" das telas da Recepção deixam de abrir o
/// wa.me (o WhatsApp do computador, fora do número oficial e sem registro) e levam à aba WhatsApp: o
/// CRM abre a conversa do paciente, liga a ficha e deixa a mensagem como rascunho. Onde a aba não
/// existe (app sem o item, CRM sem endereço, usuário sem o bit), o botão continua no wa.me.
///
/// O pedido viaja no FRAGMENTO da URL da moldura (<c>#entrada=…&amp;abrir=…</c>): não vai a servidor
/// nenhum nem a log, e o CRM o tira da barra de endereço na chegada (modo-recepcao.js).
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

    /// <summary>
    /// O endereço da moldura: a entrada (bilhete), o pedido quando há, e o número da vez — trocar só o
    /// fragmento é o que faz a página já aberta receber o pedido sem recarregar.
    /// </summary>
    public static string Endereco(string enderecoDoCrm, string bilhete, PedidoDeConversa? pedido, int vez)
    {
        var url = EntradaNoCrm.Endereco(enderecoDoCrm, bilhete);
        if (pedido is not null && Telefone(pedido.Telefone) is { } tel)
            url += "&abrir=" + Uri.EscapeDataString(JsonSerializer.Serialize(new
            {
                telefone = tel,
                nome = string.IsNullOrWhiteSpace(pedido.Nome) ? "Paciente" : pedido.Nome.Trim(),
                paciente = pedido.PacienteId,
                texto = pedido.Texto ?? ""
            }));
        return url + "&vez=" + vez.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
