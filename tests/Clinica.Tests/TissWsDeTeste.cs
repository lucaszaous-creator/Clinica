using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Tests;

/// <summary>
/// O que os testes das mensagens do web service TISS dividem: o XSD oficial 4.01.00, um remetente
/// FICTÍCIO, uma guia conferida fictícia e a resposta da operadora escrita como o XSD a define —
/// e por isso conferida no mesmo XSD antes de servir de prova de leitura.
/// </summary>
internal static class TissWsDeTeste
{
    public static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";
    public static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    public const string CodigoPrestador = "12345678000190";
    public const string Carteira = "000123456789";
    public const string Sequencial = "261111101500";
    public static readonly DateTime Momento = new(2026, 11, 11, 10, 15, 0);

    public static RemetenteTissWs Remetente(string senha = "segredo") => new()
    {
        RegistroAnsOperadora = TissWebserviceCliente.RegistroAnsSaudePetrobras,
        CodigoPrestadorNaOperadora = CodigoPrestador,
        Login = "clinica.teste",
        SenhaMd5 = TissElegibilidadeWs.SenhaEmMd5(senha)
    };

    /// <summary>Um código baixado com a guia ANS conferida, tudo inventado.</summary>
    public static CodigoFaturamento CodigoConferido(int id = 1234) => new()
    {
        Id = id,
        GuiaAns = new GuiaTissConsulta
        {
            CodigoId = id,
            RegistroAns = TissWebserviceCliente.RegistroAnsSaudePetrobras,
            NumeroCarteira = Carteira,
            AtendimentoRn = false,
            NomeBeneficiario = "PACIENTE DE TESTE",
            CodigoPrestadorNaOperadora = CodigoPrestador,
            Cnes = "9999999",
            NomeProfissional = "PROFISSIONAL DE TESTE",
            ConselhoProfissional = "06",
            NumeroConselho = "000000",
            UfConselho = "33",
            Cbos = "225133",
            IndicacaoAcidente = "9",
            RegimeAtendimento = "01",
            DataAtendimento = new DateOnly(2026, 11, 10),
            CodigoTabela = "22",
            TipoConsulta = "1",
            CodigoProcedimento = "10101012",
            ValorProcedimento = 139.00m
        }
    };

    /// <summary>A guia de consulta conferida, com o número que o sistema gera para o Id.</summary>
    public static XElement GuiaConsulta(int id = 1234)
        => TissExportService.GuiaConsultaConferida(CodigoConferido(id), NumeroGuiaPrestadorTiss.Gerar(id));

    /// <summary>
    /// Problemas da mensagem no XSD oficial do web service (<c>tissWebServicesV4_01_00.xsd</c>).
    /// Lista vazia = válida. Serve para o pedido que sai e para a resposta escrita no teste.
    /// </summary>
    public static List<string> ValidarNoXsd(XElement mensagem)
    {
        var pasta = Path.Combine(AppContext.BaseDirectory, "Tiss", "xsd-4.01.00");
        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        schemas.Add(Ans.NamespaceName, Path.Combine(pasta, "tissWebServicesV4_01_00.xsd"));
        schemas.Compile();

        var problemas = new List<string>();
        new XDocument(new XElement(mensagem)).Validate(schemas, (_, e) => problemas.Add(e.Message));
        return problemas;
    }

    /// <summary>
    /// A resposta da operadora num envelope SOAP: o elemento do web service com o cabeçalho de
    /// operadora para prestador, o <paramref name="miolo"/> e o hash.
    /// </summary>
    public static string Resposta(string elementoWs, string tipoTransacao, string miolo) => $"""
        <?xml version="1.0" encoding="ISO-8859-1"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ans="http://www.ans.gov.br/padroes/tiss/schemas">
          <soapenv:Body>
            <ans:{elementoWs}>
              <ans:cabecalho>
                <ans:identificacaoTransacao>
                  <ans:tipoTransacao>{tipoTransacao}</ans:tipoTransacao>
                  <ans:sequencialTransacao>1</ans:sequencialTransacao>
                  <ans:dataRegistroTransacao>2026-11-11</ans:dataRegistroTransacao>
                  <ans:horaRegistroTransacao>10:15:02</ans:horaRegistroTransacao>
                </ans:identificacaoTransacao>
                <ans:origem><ans:registroANS>422631</ans:registroANS></ans:origem>
                <ans:destino><ans:identificacaoPrestador><ans:codigoPrestadorNaOperadora>{CodigoPrestador}</ans:codigoPrestadorNaOperadora></ans:identificacaoPrestador></ans:destino>
                <ans:Padrao>4.01.00</ans:Padrao>
              </ans:cabecalho>
              {miolo}
              <ans:hash>0</ans:hash>
            </ans:{elementoWs}>
          </soapenv:Body>
        </soapenv:Envelope>
        """.Trim();

    /// <summary>O elemento do web service de dentro de uma <see cref="Resposta"/>, para conferir no XSD.</summary>
    public static XElement CorpoDa(string resposta)
        => XDocument.Parse(resposta).Root!.Element(Soap + "Body")!.Elements().Single();

    /// <summary>A falha do padrão como o web service a devolve: erro SOAP com <c>tissFaultWS</c> no detalhe.</summary>
    public static string Falha(string tissFault) => $"""
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ans="http://www.ans.gov.br/padroes/tiss/schemas">
          <soapenv:Body><soapenv:Fault><faultcode>soapenv:Server</faultcode><faultstring>erro</faultstring>
            <detail><ans:tissFaultWS><ans:tissFault>{tissFault}</ans:tissFault></ans:tissFaultWS></detail>
          </soapenv:Fault></soapenv:Body>
        </soapenv:Envelope>
        """;

    /// <summary>Uma guia de <c>ct_guiaDados</c>, como vem no protocolo e na situação do protocolo.</summary>
    public static string GuiaDevolvida(string numeroPrestador, string? numeroOperadora = null, string extras = "")
        => $"""
            <ans:numeroGuiaPrestador>{numeroPrestador}</ans:numeroGuiaPrestador>
            {(numeroOperadora is null ? "" : $"<ans:numeroGuiaOperadora>{numeroOperadora}</ans:numeroGuiaOperadora>")}
            <ans:dadosBeneficiario><ans:numeroCarteira>{Carteira}</ans:numeroCarteira><ans:atendimentoRN>N</ans:atendimentoRN></ans:dadosBeneficiario>
            {extras}
            """;
}
