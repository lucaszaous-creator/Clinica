namespace Clinica.Domain;

/// <summary>
/// O número de quem é da equipe e pergunta ao sistema pelo WhatsApp da clínica (out/2026). Um só
/// formato para gravar e para comparar: 55 + DDD + 9 + 8 dígitos (13 dígitos, sem "+").
///
/// ⚠️ A Meta entrega o remetente (wa_id) de celular brasileiro às vezes SEM o nono dígito
/// (12 dígitos). Comparar o texto cru faria o médico cadastrado como "22 99942-2253" não ser
/// reconhecido — e a mensagem dele abriria conversa de PACIENTE no CRM. Por isso o 9 é reposto.
/// Fixo não tem WhatsApp aqui: devolve nulo.
/// </summary>
public static class TelefoneDaEquipe
{
    public static string? Chave(string? telefone)
    {
        var d = Telefone.Normalizar(telefone).TrimStart('0');
        if (d.Length is 12 or 13 && d.StartsWith("55")) d = d[2..];
        // Sem o nono dígito: celular antigo começava em 6-9 depois do DDD.
        if (d.Length == 10 && d[2] is >= '6' and <= '9') d = d[..2] + "9" + d[2..];
        if (d.Length != 11 || d[0] == '0' || d[2] != '9') return null;
        return "55" + d;
    }
}
