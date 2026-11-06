using System.Text;
using System.Text.RegularExpressions;

namespace Clinica.Application.Formatacao;

/// <summary>
/// O CSV que as telas exportam, para o Excel em português (nov/2026: subiu do shell WPF para a
/// Application, porque a gestão web exporta o MESMO arquivo).
///
/// 1. **Ponto e vírgula** como separador (o Excel em pt-BR usa a vírgula como decimal).
/// 2. **BOM UTF-8** — sem ele "Ocupação" vira "OcupaÃ§Ã£o".
/// 3. **Aspas** quando o campo tem separador, aspas ou quebra de linha.
/// 4. **Fórmula neutralizada** (CSV/formula injection): texto digitado por uma pessoa — nome de
///    paciente, observação — que comece com <c>= + - @</c>, tabulação ou retorno vira FÓRMULA ao
///    abrir no Excel (<c>=HYPERLINK(…)</c> leva o clique para fora). Esses campos ganham um
///    apóstrofo na frente. Número negativo ("-12,50", "-R$ 3.714,30", "-82,9%") continua número.
/// </summary>
public static partial class CsvParaExcel
{
    public static byte[] Montar(IReadOnlyList<string> cabecalho, IEnumerable<IReadOnlyList<string>> linhas)
    {
        var texto = new StringBuilder();
        texto.AppendLine(string.Join(';', cabecalho.Select(Escapar)));
        foreach (var linha in linhas)
            texto.AppendLine(string.Join(';', linha.Select(Escapar)));
        // ⚠️ GetBytes NUNCA escreve o BOM, mesmo com encoderShouldEmitUTF8Identifier: true — só o
        // GetPreamble o devolve. O shell WPF fazia `new UTF8Encoding(true).GetBytes(…)` com o
        // comentário dizendo que gravava o BOM, e os CSVs saíam sem ele (achado pelo teste, nov/2026).
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. utf8.GetPreamble(), .. utf8.GetBytes(texto.ToString())];
    }

    public static string Escapar(string? campo)
    {
        campo = Neutralizar(campo ?? string.Empty);
        if (!campo.Contains(';') && !campo.Contains('"') && !campo.Contains('\n') && !campo.Contains('\r'))
            return campo;
        return $"\"{campo.Replace("\"", "\"\"")}\"";
    }

    /// <summary>O campo que o Excel leria como fórmula ganha um apóstrofo; número com sinal não.</summary>
    public static string Neutralizar(string campo)
    {
        if (campo.Length == 0) return campo;
        var c = campo[0];
        if (c is '=' or '@' or '\t' or '\r') return "'" + campo;
        if (c is '+' or '-' && !NumeroComSinal().IsMatch(campo)) return "'" + campo;
        return campo;
    }

    [GeneratedRegex(@"^[+-]\s*(R\$\s*)?[\d.,]+\s*%?$")]
    private static partial Regex NumeroComSinal();
}
