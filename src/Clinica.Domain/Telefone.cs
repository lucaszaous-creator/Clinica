using System.Linq;

namespace Clinica.Domain;

/// <summary>Utilitários de telefone: normalização (só dígitos) e formatação brasileira.</summary>
public static class Telefone
{
    /// <summary>Remove tudo que não é dígito.</summary>
    public static string Normalizar(string? entrada)
        => new string((entrada ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>
    /// Tira o prefixo de DISCAGEM que a ficha às vezes guarda (out/2026, bug visto na clínica: "022998999334"
    /// ia para a Meta como está e a mensagem não era entregue): o 0 do interurbano ("0 22 99899-9334") e, com ele,
    /// o código de operadora ("0 15 22 99899-9334"). O que sobra é DDD + número, como o WhatsApp do celular entende.
    /// "00" é discagem internacional e fica como está; número sem 0 na frente também.
    /// </summary>
    public static string SemPrefixoDeDiscagem(string digitos)
    {
        if (digitos.Length < 11 || digitos[0] != '0' || digitos[1] == '0') return digitos;
        var semZero = digitos[1..];
        return semZero.Length is 12 or 13 ? semZero[2..] : semZero;
    }

    /// <summary>
    /// Formata como (DD) 99999-9999 / (DD) 9999-9999 quando possível;
    /// números fora dos padrões (8/9/10/11 dígitos) voltam como digitados.
    /// </summary>
    public static string Formatar(string? entrada)
    {
        var d = Normalizar(entrada);
        return d.Length switch
        {
            11 => $"({d[..2]}) {d[2..7]}-{d[7..]}",
            10 => $"({d[..2]}) {d[2..6]}-{d[6..]}",
            9 => $"{d[..5]}-{d[5..]}",
            8 => $"{d[..4]}-{d[4..]}",
            _ => entrada?.Trim() ?? string.Empty
        };
    }
}
