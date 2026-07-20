using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Clinica.Application.Servicos;

/// <summary>
/// Valida o XML TISS gerado ANTES de ele ir para a operadora: estrutura mínima,
/// campos que as operadoras rejeitam em branco e conferência do hash do epílogo.
/// Um lote rejeitado por erro estrutural é retrabalho de dias — melhor barrar aqui.
/// Havendo o XSD oficial da ANS disponível, valida também contra o schema.
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
    /// Valida contra o XSD oficial da ANS, quando o arquivo estiver disponível na máquina
    /// (o XSD não é distribuído com o app — baixe no portal da ANS e aponte o caminho).
    /// </summary>
    public static IReadOnlyList<string> ValidarComXsd(string xml, string caminhoXsd)
    {
        var problemas = new List<string>();
        try
        {
            var schemas = new XmlSchemaSet();
            schemas.Add(Ans.NamespaceName, caminhoXsd);

            var doc = XDocument.Parse(xml);
            doc.Validate(schemas, (_, e) => problemas.Add($"XSD: {e.Message}"));
        }
        catch (Exception ex)
        {
            problemas.Add($"Não foi possível validar contra o XSD ({caminhoXsd}): {ex.Message}");
        }
        return problemas;
    }

    private static void ExigirTexto(List<string> problemas, XElement? elemento, string rotulo)
    {
        if (string.IsNullOrWhiteSpace(elemento?.Value))
            problemas.Add($"Campo obrigatório em branco: {rotulo}.");
    }

    /// <summary>
    /// Recalcula o hash como o TissExportService o calculou na geração: MD5 da concatenação
    /// dos valores-texto da mensagem, na ordem do documento, ANTES do epílogo existir —
    /// por isso os textos dentro de &lt;epilogo&gt; ficam de fora.
    /// </summary>
    private static string CalcularHashSemEpilogo(XElement mensagem)
    {
        var epilogo = mensagem.Element(Ans + "epilogo");
        var sb = new StringBuilder();
        foreach (var texto in mensagem.DescendantNodes().OfType<XText>())
            if (epilogo is null || !texto.Ancestors().Contains(epilogo))
                sb.Append(texto.Value);

        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
