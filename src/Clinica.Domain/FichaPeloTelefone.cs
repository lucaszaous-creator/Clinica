namespace Clinica.Domain;

/// <summary>
/// A FICHA PELO TELEFONE (26/11/2026, decisão do dono na "Agenda dentro da conversa"): quando o número da conversa tem
/// exatamente UMA ficha, ela se vincula sozinha (com trilha e "Não é esta pessoa? Trocar"); com duas ou mais, a recepção
/// escolhe; sem ficha, cadastra. A regra mora AQUI, uma só, para os apps (Recepção e Gerente) e para a ponte do CRM
/// (<c>ClinicPatientBridge</c> marca <c>AutoVincular</c>). O telefone continua não sendo identidade: só a ficha ÚNICA do
/// número se vincula sozinha.
/// </summary>
public static class FichaPeloTelefone
{
    /// <summary>
    /// O número como a agenda o compara: só dígitos, sem prefixo de discagem, com o 55 quando vier com DDD (10 ou 11
    /// dígitos). Vazio quando não parece telefone. É a mesma regra da agenda do CRM (<c>ClinicAgendaBridge.Phone</c>).
    /// </summary>
    public static string Normalizar(string? telefone)
    {
        var digitos = Telefone.SemPrefixoDeDiscagem(new string((telefone ?? "").Where(char.IsAsciiDigit).ToArray()));
        if (digitos.Length is 10 or 11) digitos = "55" + digitos;
        return digitos.Length is >= 10 and <= 15 ? digitos : "";
    }

    /// <summary>A ficha que se vincula sozinha: o id da ÚNICA ficha com este número; nulo com nenhuma, com duas ou mais, ou sem número.</summary>
    public static int? Unica(IEnumerable<(int Id, string? Telefone)> fichas, string? telefone)
    {
        var numero = Normalizar(telefone);
        if (numero.Length == 0) return null;
        var ids = fichas.Where(f => Normalizar(f.Telefone) == numero).Select(f => f.Id).Distinct().Take(2).ToList();
        return ids.Count == 1 ? ids[0] : null;
    }
}
