using System.Linq;

namespace Clinica.Domain;

/// <summary>Utilitários de telefone: normalização (só dígitos) e formatação brasileira.</summary>
public static class Telefone
{
    /// <summary>Remove tudo que não é dígito.</summary>
    public static string Normalizar(string? entrada)
        => new string((entrada ?? string.Empty).Where(char.IsDigit).ToArray());

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
