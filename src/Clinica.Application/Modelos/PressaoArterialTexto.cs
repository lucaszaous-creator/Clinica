using System.Text.RegularExpressions;

namespace Clinica.Application.Modelos;

/// <summary>
/// A pressão arterial como se ANOTA no papel — "120/80" —, num campo só (set/2026, "uma
/// folha, dois lados"). A tira da passagem de enfermagem tinha oito caixas, duas delas
/// "PA sistólica" e "PA diastólica"; a técnica escreve "120x80" há vinte anos. O serviço
/// continua gravando DOIS números — o que muda é o que a tela pede.
///
/// Aceita os separadores que aparecem na prática: barra, "x" (maiúsculo ou minúsculo),
/// hífen e espaço. Um número só é a sistólica (a diastólica fica em branco, e é a
/// entidade que decide se meia pressão vale — não vale, parcela 37). Texto que não é
/// número devolve os dois em branco, nunca um chute.
///
/// Mora na Application pela regra de sempre: o que decide o que a tela GRAVA precisa
/// morar onde o <c>dotnet test</c> alcança.
/// </summary>
public static partial class PressaoArterialTexto
{
    [GeneratedRegex(@"^\s*(\d{2,3})\s*(?:[/xX\-]|\s)\s*(\d{2,3})\s*$")]
    private static partial Regex Par();

    [GeneratedRegex(@"^\s*(\d{2,3})\s*[/xX\-]?\s*$")]
    private static partial Regex Sozinha();

    /// <summary>"120/80" → ("120", "80"); "120" → ("120", ""); lixo → ("", "").</summary>
    public static (string Sistolica, string Diastolica) Separar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (string.Empty, string.Empty);

        var par = Par().Match(texto);
        if (par.Success) return (par.Groups[1].Value, par.Groups[2].Value);

        var so = Sozinha().Match(texto);
        return so.Success ? (so.Groups[1].Value, string.Empty) : (string.Empty, string.Empty);
    }

    /// <summary>O caminho de volta, para a correção recarregar o campo: ("120","80") → "120/80".</summary>
    public static string Juntar(string? sistolica, string? diastolica)
    {
        var s = (sistolica ?? string.Empty).Trim();
        var d = (diastolica ?? string.Empty).Trim();
        if (s.Length == 0 && d.Length == 0) return string.Empty;
        if (d.Length == 0) return s;
        if (s.Length == 0) return $"/{d}";
        return $"{s}/{d}";
    }
}
