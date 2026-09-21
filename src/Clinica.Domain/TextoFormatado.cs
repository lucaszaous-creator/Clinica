using System.Text.Json;

namespace Clinica.Domain;

/// <summary>Somente texto, negrito e itálico. Nunca contém HTML, RTF ou código executável.</summary>
public sealed record TrechoTexto(string Texto, bool Negrito = false, bool Italico = false);

public static class TextoFormatado
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<TrechoTexto> Ler(string? texto, string? formato)
    {
        var esperado = Linhas(texto);
        if (string.IsNullOrWhiteSpace(formato) || formato.Length > 250_000)
            return [new(esperado)];
        try
        {
            var trechos = JsonSerializer.Deserialize<List<TrechoTexto>>(formato, Json);
            if (trechos is null || trechos.Count > 4096 || trechos.Any(t => t is null || t.Texto is null))
                return [new(esperado)];
            var normalizados = trechos.Select(t => t with { Texto = Linhas(t.Texto) }).ToList();
            var completo = string.Concat(normalizados.Select(t => t.Texto));
            if (completo == esperado) return normalizados;
            // Os serviços antigos retiram espaços nas pontas. Rebasear só esse recorte
            // preserva a apresentação; qualquer outra diferença invalida os estilos.
            if (completo.Trim() == esperado)
            {
                var inicio = completo.Length - completo.TrimStart().Length;
                var fim = inicio + esperado.Length;
                var posicao = 0;
                var resultado = new List<TrechoTexto>();
                foreach (var t in normalizados)
                {
                    var de = Math.Max(inicio, posicao);
                    var ate = Math.Min(fim, posicao + t.Texto.Length);
                    if (ate > de) resultado.Add(t with { Texto = t.Texto[(de - posicao)..(ate - posicao)] });
                    posicao += t.Texto.Length;
                }
                return resultado;
            }
        }
        catch (JsonException) { }
        // Um cliente antigo pode editar o texto sem conhecer a formatação. Nesse caso
        // o texto gravado continua sendo a verdade; estilos antigos não mudam palavras.
        return [new(esperado)];
    }

    public static string? Normalizar(string? texto, string? formato) => Guardar(Ler(texto, formato));

    public static string? Guardar(IEnumerable<TrechoTexto> trechos)
    {
        var unidos = new List<TrechoTexto>();
        foreach (var trecho in trechos)
        {
            var t = trecho with { Texto = Linhas(trecho.Texto) };
            if (t.Texto.Length == 0) continue;
            if (unidos.Count > 0 && unidos[^1].Negrito == t.Negrito && unidos[^1].Italico == t.Italico)
                unidos[^1] = unidos[^1] with { Texto = unidos[^1].Texto + t.Texto };
            else unidos.Add(t);
        }
        if (!unidos.Any(t => t.Negrito || t.Italico)) return null;
        if (unidos.Count > 4096) throw new InvalidOperationException("Simplifique a formatação do texto antes de salvar.");
        var json = JsonSerializer.Serialize(unidos, Json);
        if (json.Length > 250_000) throw new InvalidOperationException("Divida o conteúdo em documentos menores antes de salvar.");
        return json;
    }

    public static (string Texto, string? Formato) Juntar(IEnumerable<(string? Texto, string? Formato)> partes, string separador = "\n\n")
    {
        var trechos = new List<TrechoTexto>();
        foreach (var (texto, formato) in partes)
        {
            if (string.IsNullOrWhiteSpace(texto)) continue;
            if (trechos.Count > 0) trechos.Add(new(separador));
            trechos.AddRange(Ler(texto, formato));
        }
        return (string.Concat(trechos.Select(t => t.Texto)), Guardar(trechos));
    }

    public static string Linhas(string? texto) => (texto ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
}
