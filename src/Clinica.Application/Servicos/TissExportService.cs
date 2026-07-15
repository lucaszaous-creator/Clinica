using System.Xml.Linq;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Gera um lote de guias no formato TISS (XML, padrão ANS 3.05) a partir das guias faturadas.
/// É uma base estruturada com os dados disponíveis + configuração do prestador — pode exigir
/// ajustes conforme a operadora (códigos TUSS, registro ANS, regras específicas).
/// </summary>
public sealed class TissExportService
{
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";

    public string GerarLoteXml(IReadOnlyList<CodigoFaturamento> codigos, DadosPrestador prestador, string numeroLote)
    {
        var agora = DateTime.Now;

        var guias = codigos
            .Where(c => c.Baixado)
            .GroupBy(c => c.AtendimentoId)
            .Select((g, i) => CriarGuia(g, prestador, i + 1))
            .ToList();

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ans + "mensagemTISS",
                new XAttribute(XNamespace.Xmlns + "ans", Ans.NamespaceName),
                new XElement(Ans + "cabecalho",
                    new XElement(Ans + "identificacaoTransacao",
                        new XElement(Ans + "tipoTransacao", "ENVIO_LOTE_GUIAS"),
                        new XElement(Ans + "sequencialTransacao", numeroLote),
                        new XElement(Ans + "dataRegistroTransacao", agora.ToString("yyyy-MM-dd")),
                        new XElement(Ans + "horaRegistroTransacao", agora.ToString("HH:mm:ss"))),
                    new XElement(Ans + "origem",
                        new XElement(Ans + "identificacaoPrestador",
                            new XElement(Ans + "codigoPrestadorNaOperadora", prestador.CodigoNaOperadora ?? string.Empty))),
                    new XElement(Ans + "destino",
                        new XElement(Ans + "registroANS", prestador.RegistroAnsOperadora ?? string.Empty)),
                    new XElement(Ans + "versaoPadrao", "3.05.00")),
                new XElement(Ans + "prestadorParaOperadora",
                    new XElement(Ans + "loteGuias",
                        new XElement(Ans + "numeroLote", numeroLote),
                        new XElement(Ans + "guiasTISS", guias)))));

        return doc.ToString();
    }

    private static XElement CriarGuia(IGrouping<int, CodigoFaturamento> grupo, DadosPrestador prestador, int numeroGuia)
    {
        var primeiro = grupo.First();
        var paciente = primeiro.Atendimento?.Paciente;

        var procedimentos = grupo.Select(c => new XElement(Ans + "procedimentoExecutado",
            new XElement(Ans + "procedimento",
                new XElement(Ans + "codigoTabela", "22"),
                new XElement(Ans + "codigoProcedimento", prestador.CodigoTuss(c.Tipo)),
                new XElement(Ans + "descricaoProcedimento", c.Tipo.ToString())),
            new XElement(Ans + "quantidadeExecutada", "1")));

        return new XElement(Ans + "guiaConsulta",
            new XElement(Ans + "numeroGuiaPrestador", primeiro.NumeroGuiaReal ?? numeroGuia.ToString()),
            new XElement(Ans + "dataAtendimento", primeiro.Atendimento?.Data.ToString("yyyy-MM-dd") ?? string.Empty),
            new XElement(Ans + "dadosBeneficiario",
                new XElement(Ans + "nomeBeneficiario", paciente?.Nome ?? string.Empty),
                new XElement(Ans + "numeroCarteira", paciente?.Documento ?? string.Empty)),
            new XElement(Ans + "procedimentosExecutados", procedimentos));
    }
}
