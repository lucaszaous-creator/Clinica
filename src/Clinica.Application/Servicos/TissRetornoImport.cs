using System.Globalization;
using System.Xml.Linq;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Resultado da leitura de um demonstrativo de análise da operadora.</summary>
public sealed record ResultadoImportacaoRetorno(
    /// <summary>Decisões prontas para a tela de retorno (uma por guia do lote encontrada no XML).</summary>
    IReadOnlyList<RetornoGuiaDecisao> Decisoes,
    /// <summary>Guias citadas no XML que não pertencem ao lote (conferir se o arquivo é do lote certo).</summary>
    IReadOnlyList<string> GuiasForaDoLote,
    /// <summary>Guias do lote que o XML não menciona (a operadora ainda não analisou ou o arquivo está incompleto).</summary>
    IReadOnlyList<string> GuiasSemRetorno,
    /// <summary>Data do demonstrativo, quando presente no XML.</summary>
    DateOnly? DataDemonstrativo);

/// <summary>
/// Lê o demonstrativo de análise de conta (XML TISS devolvido pela operadora) e converte
/// em decisões guia a guia — o que era digitação manual passa a ser conferência.
/// A leitura é tolerante a variações entre operadoras: procura por NOME LOCAL dos
/// elementos (numeroGuiaPrestador, motivoGlosa etc.), sem exigir uma árvore exata.
/// </summary>
public static class TissRetornoImport
{
    /// <summary>
    /// Cruza o XML da operadora com as guias do lote (pelo número real da guia).
    /// Lança <see cref="InvalidOperationException"/> com mensagem amigável quando o
    /// arquivo não é um XML TISS legível.
    /// </summary>
    public static ResultadoImportacaoRetorno Ler(string xml, IReadOnlyList<CodigoFaturamento> codigosDoLote)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"O arquivo não é um XML válido: {ex.Message}");
        }

        if (doc.Root is null || !doc.Root.Name.LocalName.Contains("mensagemTISS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "O arquivo não parece ser uma mensagem TISS (raiz <mensagemTISS> não encontrada).");

        // Índice do lote: número real da guia (como foi enviado no XML do lote) → código.
        var porNumeroGuia = codigosDoLote
            .Where(c => !string.IsNullOrWhiteSpace(c.NumeroGuiaReal))
            .GroupBy(c => c.NumeroGuiaReal!.Trim())
            .ToDictionary(g => g.Key, g => g.ToList());

        var decisoes = new List<RetornoGuiaDecisao>();
        var foraDoLote = new List<string>();
        var vistas = new HashSet<int>();

        foreach (var (numeroGuia, escopo) in EscoposDeGuia(doc))
        {
            if (!porNumeroGuia.TryGetValue(numeroGuia, out var codigos))
            {
                foraDoLote.Add(numeroGuia);
                continue;
            }

            var (motivoCodigo, motivoTexto) = ExtrairGlosa(escopo);
            var glosada = motivoCodigo is not null || motivoTexto is not null;

            // Código fora do catálogo vira "Outros", mas o código original fica no texto.
            var codigoNormalizado = NormalizarMotivo(motivoCodigo);
            if (motivoCodigo is not null && codigoNormalizado == MotivosGlosa.CodigoOutros && motivoCodigo != MotivosGlosa.CodigoOutros)
                motivoTexto = $"Glosa {motivoCodigo}" + (motivoTexto is null ? string.Empty : $" — {motivoTexto}");

            foreach (var codigo in codigos.Where(c => vistas.Add(c.Id)))
                decisoes.Add(new RetornoGuiaDecisao(
                    codigo.Id,
                    glosada,
                    codigoNormalizado,
                    motivoTexto));
        }

        var semRetorno = codigosDoLote
            .Where(c => !vistas.Contains(c.Id))
            .Select(c => c.NumeroGuiaReal ?? $"(guia interna {c.Id})")
            .Distinct()
            .ToList();

        return new ResultadoImportacaoRetorno(
            decisoes,
            foraDoLote.Distinct().ToList(),
            semRetorno,
            ExtrairData(doc));
    }

    /// <summary>
    /// Cada elemento com o número da guia do prestador define um "escopo de guia": o
    /// menor bloco do XML que fala daquela guia (o pai do elemento do número). É nele
    /// que se procuram os dados de glosa daquela guia específica.
    /// </summary>
    private static IEnumerable<(string NumeroGuia, XElement Escopo)> EscoposDeGuia(XDocument doc)
    {
        foreach (var numero in doc.Descendants()
                     .Where(e => e.Name.LocalName is "numeroGuiaPrestador" or "numeroGuiaOrigem"))
        {
            var valor = numero.Value.Trim();
            if (valor.Length == 0) continue;
            yield return (valor, numero.Parent ?? numero);
        }
    }

    /// <summary>Procura, dentro do escopo de uma guia, o código e/ou a descrição da glosa.</summary>
    private static (string? Codigo, string? Texto) ExtrairGlosa(XElement escopo)
    {
        string? codigo = null;
        string? texto = null;

        foreach (var e in escopo.DescendantsAndSelf())
        {
            var nome = e.Name.LocalName;
            if (nome is "codigoGlosa" or "codigoGlosaGuia" or "codigoGlosaProtocolo" or "codigoGlosaItem" or "tipoGlosa")
                codigo ??= Vazio(e.Value);
            else if (nome is "descricaoGlosa" or "motivoGlosa" or "justificativaGlosa")
                texto ??= Vazio(e.Value);
        }

        return (codigo, texto);
    }

    /// <summary>Data do demonstrativo (dataEmissao/dataRegistroTransacao), se houver.</summary>
    private static DateOnly? ExtrairData(XDocument doc)
    {
        var valor = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName is "dataEmissao" or "dataRegistroTransacao")
            ?.Value.Trim();

        return DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var data) ? data : null;
    }

    /// <summary>
    /// Código fora do catálogo interno vira "Outros" (9999) para não quebrar os combos;
    /// o código original segue visível no texto do motivo.
    /// </summary>
    private static string? NormalizarMotivo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        return MotivosGlosa.Todos.Any(m => m.Codigo == codigo) ? codigo : MotivosGlosa.CodigoOutros;
    }

    private static string? Vazio(string valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
