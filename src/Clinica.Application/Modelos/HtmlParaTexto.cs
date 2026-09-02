using System.Net;
using System.Text.RegularExpressions;

namespace Clinica.Application.Modelos;

/// <summary>
/// O prontuário do Smart Clinic vem em HTML (parágrafos, <c>&lt;br /&gt;</c>, listas,
/// entidades como <c>&amp;ccedil;</c>); o nosso é texto. Esta conversão guarda o CONTEÚDO
/// inteiro — cada quebra vira quebra de linha, cada item de lista vira uma linha com
/// marcador, as entidades voltam a ser letras — e perde só a FORMATAÇÃO (negrito, cor),
/// que não é registro clínico.
/// </summary>
public static partial class HtmlParaTexto
{
    [GeneratedRegex(@"(?i)<\s*br\s*/?\s*>|</\s*(p|div|h[1-6]|tr|table|blockquote)\s*>")]
    private static partial Regex Quebras();

    // O branco em volta do item some junto com a tag: o HTML indenta "<li>" com "\n\t", e
    // manter isso daria uma linha em branco entre cada item da lista.
    [GeneratedRegex(@"(?i)\s*<\s*li[^>]*>\s*")]
    private static partial Regex ItemDeLista();

    [GeneratedRegex(@"(?i)\s*</\s*li\s*>")]
    private static partial Regex FimDeItem();

    [GeneratedRegex(@"(?i)</\s*(ul|ol)\s*>")]
    private static partial Regex FimDeLista();

    [GeneratedRegex(@"(?i)</\s*t[dh]\s*>")]
    private static partial Regex Celula();

    [GeneratedRegex(@"(?is)<\s*(script|style)[^>]*>.*?</\s*\1\s*>")]
    private static partial Regex ScriptOuEstilo();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex QualquerTag();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex EspacosRepetidos();

    [GeneratedRegex(@"[ \t]*\n[ \t]*")]
    private static partial Regex QuebraComEspacos();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex QuebrasRepetidas();

    /// <summary>Texto puro. Texto sem tag nenhuma sai como entrou (com o branco normalizado).</summary>
    public static string? Converter(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var t = html.Replace("\r\n", "\n").Replace('\r', '\n');
        t = ScriptOuEstilo().Replace(t, string.Empty);
        t = ItemDeLista().Replace(t, "\n• ");
        t = FimDeItem().Replace(t, string.Empty);
        t = FimDeLista().Replace(t, "\n");
        t = Celula().Replace(t, " ");
        t = Quebras().Replace(t, "\n");
        t = QualquerTag().Replace(t, string.Empty);
        t = WebUtility.HtmlDecode(t);
        t = EspacosRepetidos().Replace(t, " ");
        t = QuebraComEspacos().Replace(t, "\n");
        t = QuebrasRepetidas().Replace(t, "\n\n");
        t = t.Trim();
        return t.Length == 0 ? null : t;
    }
}
