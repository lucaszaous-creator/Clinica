using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Clinica.Application.Servicos;

/// <summary>
/// Valida o XML TISS gerado ANTES de ele ir para a operadora: estrutura mínima,
/// campos que as operadoras rejeitam em branco e conferência do hash do epílogo.
/// Um lote rejeitado por erro estrutural é retrabalho de dias — melhor barrar aqui.
/// <see cref="ValidarComXsdOficial"/> valida também contra o esquema oficial da ANS, embutido.
/// </summary>
public static class TissValidador
{
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";

    /// <summary>Validação estrutural do XML. Lista vazia = nenhum problema encontrado.</summary>
    public static IReadOnlyList<string> Validar(string xml)
    {
        var problemas = new List<string>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            return new[] { $"XML malformado: {ex.Message}" };
        }

        var mensagem = doc.Root;
        if (mensagem is null || mensagem.Name != Ans + "mensagemTISS")
        {
            problemas.Add("Raiz do documento não é <ans:mensagemTISS> no namespace da ANS.");
            return problemas;
        }

        // ---- Cabeçalho ----
        var cabecalho = mensagem.Element(Ans + "cabecalho");
        if (cabecalho is null)
            problemas.Add("Cabeçalho (<ans:cabecalho>) ausente.");
        else
        {
            ExigirTexto(problemas, cabecalho.Element(Ans + "identificacaoTransacao")?.Element(Ans + "tipoTransacao"),
                "tipo de transação (tipoTransacao)");
            ExigirTexto(problemas, cabecalho.Element(Ans + "identificacaoTransacao")?.Element(Ans + "sequencialTransacao"),
                "sequencial da transação (sequencialTransacao)");
            ExigirTexto(problemas,
                cabecalho.Element(Ans + "origem")?.Element(Ans + "identificacaoPrestador")?.Element(Ans + "codigoPrestadorNaOperadora"),
                "código do prestador na operadora (origem)");
            ExigirTexto(problemas, cabecalho.Element(Ans + "destino")?.Element(Ans + "registroANS"),
                "registro ANS da operadora (destino)");
            ExigirTexto(problemas, cabecalho.Element(Ans + "Padrao"), "versão do padrão TISS (Padrao)");
        }

        // ---- Corpo: lote de guias ----
        var lote = mensagem.Element(Ans + "prestadorParaOperadora")?.Element(Ans + "loteGuias");
        if (lote is not null)
        {
            ExigirTexto(problemas, lote.Element(Ans + "numeroLote"), "número do lote");

            var guias = lote.Element(Ans + "guiasTISS")?.Elements().ToList() ?? new List<XElement>();
            if (guias.Count == 0)
                problemas.Add("O lote não contém nenhuma guia.");

            for (var i = 0; i < guias.Count; i++)
            {
                var guia = guias[i];
                var rotulo = $"guia {i + 1} de {guias.Count}";

                var carteira = guia.Descendants(Ans + "numeroCarteira").FirstOrDefault();
                if (string.IsNullOrWhiteSpace(carteira?.Value))
                    problemas.Add($"Carteirinha do beneficiário em branco na {rotulo} — a operadora recusa a guia (cadastre a carteirinha ou o CPF do paciente).");

                foreach (var proc in guia.Descendants(Ans + "codigoProcedimento"))
                    if (string.IsNullOrWhiteSpace(proc.Value))
                        problemas.Add($"Código TUSS de procedimento em branco na {rotulo} — configure os Códigos TUSS nas Configurações.");
            }
        }

        // ---- Epílogo: hash confere? ----
        var hashElemento = mensagem.Element(Ans + "epilogo")?.Element(Ans + "hash");
        if (string.IsNullOrWhiteSpace(hashElemento?.Value))
            problemas.Add("Epílogo sem hash — o padrão TISS exige o hash MD5 da mensagem.");
        else
        {
            var recalculado = CalcularHashSemEpilogo(mensagem);
            if (!string.Equals(hashElemento!.Value, recalculado, StringComparison.OrdinalIgnoreCase))
                problemas.Add("Hash do epílogo não confere com o conteúdo da mensagem (arquivo alterado após a geração?).");
        }

        return problemas;
    }

    /// <summary>
    /// Valida contra o ESQUEMA OFICIAL da ANS (TISS 4.01.00), que vai embutido na DLL
    /// (<c>Recursos/Tiss/*.xsd</c>) — nov/2026. Antes disso o esquema só era usado se alguém o
    /// copiasse para uma pasta da máquina, e na prática o lote nunca era conferido contra ele:
    /// a validação estrutural aprovava guias que o esquema reprova.
    /// Lista vazia = o arquivo é aceito pelo esquema.
    /// </summary>
    public static IReadOnlyList<string> ValidarComXsdOficial(string xml)
    {
        var problemas = new List<string>();
        try
        {
            var doc = XDocument.Parse(xml);
            doc.Validate(EsquemaOficial.Value, (_, e) => problemas.Add($"Esquema TISS: {e.Message}"));
        }
        catch (Exception ex)
        {
            problemas.Add($"Não foi possível validar contra o esquema TISS: {ex.Message}");
        }
        return problemas;
    }

    private const string PrefixoDosEsquemas = "Clinica.Application.Recursos.Tiss.";
    private const string EsquemaPrincipal = "tissV4_01_00.xsd";

    // Compilar o esquema (seis arquivos, ~300 KB) custa algumas centenas de ms: uma vez por processo.
    private static readonly Lazy<XmlSchemaSet> EsquemaOficial = new(() =>
    {
        var resolvedor = new EsquemasEmbutidos();
        var schemas = new XmlSchemaSet { XmlResolver = resolvedor };
        var uri = new Uri(EsquemasEmbutidos.Base, EsquemaPrincipal);
        using var leitor = XmlReader.Create(
            (Stream)resolvedor.GetEntity(uri, null, typeof(Stream))!,
            new XmlReaderSettings { XmlResolver = resolvedor, DtdProcessing = DtdProcessing.Ignore }, uri.ToString());
        schemas.Add(Ans.NamespaceName, leitor);
        schemas.Compile();
        return schemas;
    });

    /// <summary>Resolve os <c>include</c>/<c>import</c> do esquema nos recursos embutidos — nunca na rede nem no disco.</summary>
    private sealed class EsquemasEmbutidos : XmlResolver
    {
        public static readonly Uri Base = new("tiss-embutido:///");

        public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
            => new(Base, Path.GetFileName(relativeUri ?? string.Empty));

        public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            var nome = PrefixoDosEsquemas + Path.GetFileName(absoluteUri.AbsolutePath);
            return typeof(TissValidador).Assembly.GetManifestResourceStream(nome)
                ?? throw new FileNotFoundException($"Esquema TISS não embutido: {nome}");
        }
    }

    private static void ExigirTexto(List<string> problemas, XElement? elemento, string rotulo)
    {
        if (string.IsNullOrWhiteSpace(elemento?.Value))
            problemas.Add($"Campo obrigatório em branco: {rotulo}.");
    }

    /// <summary>
    /// Recalcula o hash com a MESMA conta da geração (<see cref="TissExportService.CalcularHash"/>):
    /// os textos de &lt;epilogo&gt; ficam de fora, e os bytes são ISO-8859-1.
    /// </summary>
    private static string CalcularHashSemEpilogo(XElement mensagem) => TissExportService.CalcularHash(mensagem);
}
