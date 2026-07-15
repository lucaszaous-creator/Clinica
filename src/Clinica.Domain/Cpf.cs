using System.Linq;

namespace Clinica.Domain;

/// <summary>Utilitários de CPF: normalização (só dígitos), validação (dígito verificador) e formatação.</summary>
public static class Cpf
{
    /// <summary>Remove tudo que não é dígito.</summary>
    public static string Normalizar(string? entrada)
        => new string((entrada ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>Formata como 000.000.000-00 (se tiver 11 dígitos); senão devolve os dígitos.</summary>
    public static string Formatar(string? entrada)
    {
        var d = Normalizar(entrada);
        return d.Length == 11
            ? $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}"
            : d;
    }

    /// <summary>Valida o CPF pelos dígitos verificadores. Vazio é considerado inválido.</summary>
    public static bool Valido(string? entrada)
    {
        var cpf = Normalizar(entrada);
        if (cpf.Length != 11) return false;
        if (cpf.Distinct().Count() == 1) return false; // todos iguais (ex.: 111.111.111-11)

        var numeros = cpf.Select(c => c - '0').ToArray();

        int CalcularDigito(int qtd)
        {
            var soma = 0;
            for (int i = 0; i < qtd; i++)
                soma += numeros[i] * (qtd + 1 - i);
            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        return CalcularDigito(9) == numeros[9] && CalcularDigito(10) == numeros[10];
    }
}
