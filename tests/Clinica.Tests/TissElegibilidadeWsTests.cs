using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A verificação de elegibilidade pelo web service TISS (4.01.00): a mensagem que sai é válida
/// no XSD oficial, o hash é o do padrão, a senha nunca vai em claro, e a resposta da operadora
/// tem TRÊS leituras — elegível, não elegível e "não avaliado" —, porque falha de login não
/// pode chegar à recepção com a cara de carteira inativa.
/// </summary>
public class TissElegibilidadeWsTests
{
    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    private static PedidoElegibilidadeTiss Pedido(string senha = "segredo", DateOnly? validade = null) => new()
    {
        RegistroAnsOperadora = TissWebserviceCliente.RegistroAnsSaudePetrobras,
        CodigoPrestadorNaOperadora = "12345678000190",
        NumeroCarteira = "000123456789",
        ValidadeCarteira = validade,
        Login = "clinica.teste",
        SenhaMd5 = TissElegibilidadeWs.SenhaEmMd5(senha),
        Sequencial = "261111101500",
        Momento = new DateTime(2026, 11, 11, 10, 15, 0)
    };

    [Fact]
    public void Pedido_e_valido_no_XSD_oficial_do_web_service_4_01_00()
    {
        var problemas = ValidarNoXsd(TissElegibilidadeWs.MontarPedido(Pedido(validade: new DateOnly(2027, 1, 31))));

        problemas.Should().BeEmpty();
    }

    [Fact]
    public void Pedido_sem_validade_tambem_e_valido_no_XSD()
        => ValidarNoXsd(TissElegibilidadeWs.MontarPedido(Pedido())).Should().BeEmpty();

    [Fact]
    public void O_XSD_reprova_de_verdade_quando_a_mensagem_esta_errada()
    {
        // Sem esta prova, "lista vazia" poderia ser só um validador que não carregou schema nenhum.
        var pedido = TissElegibilidadeWs.MontarPedido(Pedido());
        pedido.Descendants(Ans + "numeroCarteira").Single().Remove();

        ValidarNoXsd(pedido).Should().NotBeEmpty();
    }

    [Fact]
    public void Hash_e_o_MD5_do_conteudo_das_tags_na_ordem_sem_a_propria_tag()
    {
        var pedido = Pedido(validade: new DateOnly(2027, 1, 31));
        var mensagem = TissElegibilidadeWs.MontarPedido(pedido);

        var esperado = "VERIFICA_ELEGIBILIDADE" + "261111101500" + "2026-11-11" + "10:15:00"
                       + "12345678000190" + "422631" + "4.01.00" + "clinica.teste" + pedido.SenhaMd5
                       + "12345678000190" + "000123456789" + "2027-01-31";
        var md5 = Convert.ToHexString(MD5.HashData(Encoding.Latin1.GetBytes(esperado))).ToLowerInvariant();

        mensagem.Element(Ans + "hash")!.Value.Should().Be(md5);
        mensagem.Elements().Last().Name.Should().Be(Ans + "hash");
    }

    [Fact]
    public void Hash_usa_ISO_8859_1_e_nao_UTF_8()
    {
        // Com acento os dois MD5 diferem, e a operadora confere em ISO-8859-1.
        var mensagem = new XElement(Ans + "x", new XElement(Ans + "nome", "José"));

        TissElegibilidadeWs.CalcularHash(mensagem)
            .Should().Be(Convert.ToHexString(MD5.HashData(Encoding.Latin1.GetBytes("José"))).ToLowerInvariant())
            .And.NotBe(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("José"))).ToLowerInvariant());
    }

    [Fact]
    public void Senha_vai_em_MD5_e_nunca_em_claro()
    {
        var envelope = TissElegibilidadeWs.MontarEnvelope(Pedido("Senha@Secreta1"));

        envelope.Should().NotContain("Senha@Secreta1");
        // MD5 conhecido de "abc", para fixar o formato: 32 hexadecimais minúsculos.
        TissElegibilidadeWs.SenhaEmMd5("abc").Should().Be("900150983cd24fb0d6963f7d28e17f72");
        XDocument.Parse(envelope).Descendants(Ans + "senhaPrestador").Single().Value
            .Should().Be(TissElegibilidadeWs.SenhaEmMd5("Senha@Secreta1"));
    }

    [Fact]
    public void Envelope_para_exibir_mascara_a_senha_e_preserva_o_resto()
    {
        var pedido = Pedido();

        var exibido = TissElegibilidadeWs.MontarEnvelopeParaExibir(pedido);

        exibido.Should().NotContain(pedido.SenhaMd5);
        exibido.Should().Contain(new string('*', 32)).And.Contain("000123456789").And.Contain("clinica.teste");
    }

    [Fact]
    public void Envelope_e_SOAP_1_1_declarado_em_ISO_8859_1_com_o_pedido_no_corpo()
    {
        var envelope = TissElegibilidadeWs.MontarEnvelope(Pedido());

        envelope.Should().StartWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
        var doc = XDocument.Parse(envelope);
        doc.Root!.Name.Should().Be(Soap + "Envelope");
        doc.Root.Element(Soap + "Body")!.Elements().Single().Name.Should().Be(Ans + "pedidoElegibilidadeWS");
    }

    [Theory]
    [InlineData("12345", "registro ANS")]      // 5 dígitos
    [InlineData("42263A", "registro ANS")]     // letra
    public void Registro_ANS_fora_do_padrao_e_recusado_antes_do_envio(string registro, string trecho)
    {
        var acao = () => TissElegibilidadeWs.MontarPedido(Pedido() with { RegistroAnsOperadora = registro });

        acao.Should().Throw<ArgumentException>().WithMessage($"*{trecho}*");
    }

    [Fact]
    public void Carteira_maior_que_o_campo_e_senha_em_claro_sao_recusadas_antes_do_envio()
    {
        var carteiraLonga = () => TissElegibilidadeWs.MontarPedido(Pedido() with { NumeroCarteira = new string('1', 21) });
        var senhaEmClaro = () => TissElegibilidadeWs.MontarPedido(Pedido() with { SenhaMd5 = "segredo" });

        carteiraLonga.Should().Throw<ArgumentException>().WithMessage("*número da carteira*");
        senhaEmClaro.Should().Throw<ArgumentException>().WithMessage("*MD5*");
    }

    // ---------- leitura da resposta ----------

    private static string Resposta(string miolo) => $"""
        <?xml version="1.0" encoding="ISO-8859-1"?>
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ans="http://www.ans.gov.br/padroes/tiss/schemas">
          <soapenv:Body>
            <ans:respostaElegibilidadeWS>
              <ans:cabecalho><ans:Padrao>4.01.00</ans:Padrao></ans:cabecalho>
              <ans:respostaElegibilidade>{miolo}</ans:respostaElegibilidade>
              <ans:hash>0</ans:hash>
            </ans:respostaElegibilidadeWS>
          </soapenv:Body>
        </soapenv:Envelope>
        """.Trim();

    [Fact]
    public void Recibo_com_S_e_elegivel_e_traz_nome_e_validade()
    {
        var lida = TissElegibilidadeWs.LerResposta(Resposta("""
            <ans:reciboElegibilidade>
              <ans:registroANS>422631</ans:registroANS>
              <ans:numeroCarteira>000123456789</ans:numeroCarteira>
              <ans:validadeCarteira>2027-01-31</ans:validadeCarteira>
              <ans:nomeBeneficiario>PACIENTE DE TESTE</ans:nomeBeneficiario>
              <ans:respostaSolicitacao>S</ans:respostaSolicitacao>
            </ans:reciboElegibilidade>
            """));

        lida.Elegivel.Should().BeTrue();
        lida.NomeBeneficiario.Should().Be("PACIENTE DE TESTE");
        lida.NumeroCarteira.Should().Be("000123456789");
        lida.ValidadeCarteira.Should().Be(new DateOnly(2027, 1, 31));
        lida.Falha.Should().BeNull();
        lida.Resumo.Should().Be("Elegível.");
    }

    [Fact]
    public void Recibo_com_N_e_nao_elegivel_com_os_motivos_da_operadora()
    {
        var lida = TissElegibilidadeWs.LerResposta(Resposta("""
            <ans:reciboElegibilidade>
              <ans:registroANS>422631</ans:registroANS>
              <ans:numeroCarteira>000123456789</ans:numeroCarteira>
              <ans:nomeBeneficiario>PACIENTE DE TESTE</ans:nomeBeneficiario>
              <ans:respostaSolicitacao>N</ans:respostaSolicitacao>
              <ans:motivosNegativa>
                <ans:motivoNegativa><ans:codigoGlosa>1016</ans:codigoGlosa><ans:descricaoGlosa>BENEFICIARIO SUSPENSO</ans:descricaoGlosa></ans:motivoNegativa>
                <ans:motivoNegativa><ans:codigoGlosa>1017</ans:codigoGlosa></ans:motivoNegativa>
              </ans:motivosNegativa>
            </ans:reciboElegibilidade>
            """));

        lida.Elegivel.Should().BeFalse();
        lida.Motivos.Should().Equal(("1016", "BENEFICIARIO SUSPENSO"), ("1017", null));
        lida.Resumo.Should().Be("Não elegível: 1016 - BENEFICIARIO SUSPENSO; 1017");
    }

    [Fact]
    public void Glosa_da_mensagem_nao_e_nao_elegivel_e_sim_nao_avaliado()
    {
        var lida = TissElegibilidadeWs.LerResposta(Resposta(
            "<ans:codigoGlosa><ans:codigoGlosa>1001</ans:codigoGlosa><ans:descricaoGlosa>NUMERO DA CARTEIRA INVALIDO</ans:descricaoGlosa></ans:codigoGlosa>"));

        lida.Elegivel.Should().BeNull();
        lida.Motivos.Should().Equal(("1001", "NUMERO DA CARTEIRA INVALIDO"));
        lida.Resumo.Should().StartWith("A operadora recusou a mensagem: 1001");
    }

    [Theory]
    [InlineData("LoginInvalido")]
    [InlineData("HashInvalido")]
    [InlineData("VersaoInvalida")]
    public void Falha_do_padrao_TISS_vira_nao_avaliado_com_o_motivo(string falha)
    {
        var lida = TissElegibilidadeWs.LerResposta($"""
            <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ans="http://www.ans.gov.br/padroes/tiss/schemas">
              <soapenv:Body><soapenv:Fault><faultcode>soapenv:Server</faultcode><faultstring>erro</faultstring>
                <detail><ans:tissFaultWS><ans:tissFault>{falha}</ans:tissFault></ans:tissFaultWS></detail>
              </soapenv:Fault></soapenv:Body>
            </soapenv:Envelope>
            """);

        lida.Elegivel.Should().BeNull();
        lida.Falha.Should().Be(falha);
        lida.Resumo.Should().Be("A operadora não avaliou a carteira: " + falha);
    }

    [Theory]
    [InlineData("<html>fora do ar</html", "não é XML")]
    [InlineData("<a/>", "sem <respostaElegibilidade>")]
    [InlineData("<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body><soapenv:Fault><faultstring>Internal Error</faultstring></soapenv:Fault></soapenv:Body></soapenv:Envelope>", "Internal Error")]
    public void Resposta_inesperada_nunca_lanca_e_nunca_vira_nao_elegivel(string corpo, string trecho)
    {
        var lida = TissElegibilidadeWs.LerResposta(corpo);

        lida.Elegivel.Should().BeNull();
        lida.Falha.Should().Contain(trecho);
    }

    // ---------- transporte ----------

    private sealed class RedeDeMentira(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Recebida { get; private set; }
        public byte[]? CorpoRecebido { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Recebida = request;
            CorpoRecebido = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            return responder(request);
        }
    }

    [Fact]
    public async Task Cliente_envia_POST_text_xml_em_ISO_8859_1_e_le_o_recibo()
    {
        var corpo = Resposta("""
            <ans:reciboElegibilidade><ans:registroANS>422631</ans:registroANS><ans:numeroCarteira>000123456789</ans:numeroCarteira>
            <ans:nomeBeneficiario>JOSÉ DE TESTE</ans:nomeBeneficiario><ans:respostaSolicitacao>S</ans:respostaSolicitacao></ans:reciboElegibilidade>
            """);
        var rede = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            // Bytes em ISO-8859-1 SEM charset no cabeçalho HTTP: quem manda é a declaração do XML.
            Content = new ByteArrayContent(Encoding.Latin1.GetBytes(corpo))
        });

        var lida = await new TissWebserviceCliente(new HttpClient(rede))
            .VerificarElegibilidadeAsync(TissWebserviceCliente.ElegibilidadeSaudePetrobras, Pedido());

        lida.Elegivel.Should().BeTrue();
        lida.NomeBeneficiario.Should().Be("JOSÉ DE TESTE");
        rede.Recebida!.Method.Should().Be(HttpMethod.Post);
        rede.Recebida.RequestUri.Should().Be(TissWebserviceCliente.ElegibilidadeSaudePetrobras);
        rede.Recebida.Content!.Headers.ContentType!.MediaType.Should().Be("text/xml");
        rede.Recebida.Content.Headers.ContentType.CharSet.Should().Be("ISO-8859-1");
        rede.Recebida.Headers.Contains("SOAPAction").Should().BeTrue();
        Encoding.Latin1.GetString(rede.CorpoRecebido!).Should().Be(TissElegibilidadeWs.MontarEnvelope(Pedido()));
    }

    [Fact]
    public async Task Cliente_le_a_falha_no_corpo_de_um_HTTP_500()
    {
        var rede = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""
                <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ans="http://www.ans.gov.br/padroes/tiss/schemas"><soapenv:Body><soapenv:Fault><faultstring>x</faultstring><detail><ans:tissFaultWS><ans:tissFault>LoginInvalido</ans:tissFault></ans:tissFaultWS></detail></soapenv:Fault></soapenv:Body></soapenv:Envelope>
                """)
        });

        var lida = await new TissWebserviceCliente(new HttpClient(rede))
            .VerificarElegibilidadeAsync(TissWebserviceCliente.ElegibilidadeSaudePetrobras, Pedido());

        lida.Elegivel.Should().BeNull();
        lida.Falha.Should().Be("LoginInvalido (HTTP 500)");
    }

    [Fact]
    public async Task Rede_fora_do_ar_e_resposta_vazia_viram_nao_avaliado()
    {
        var semRede = new RedeDeMentira(_ => throw new HttpRequestException("sem rota"));
        var vazia = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new ByteArrayContent([]) });

        var a = await new TissWebserviceCliente(new HttpClient(semRede))
            .VerificarElegibilidadeAsync(TissWebserviceCliente.ElegibilidadeSaudePetrobras, Pedido());
        var b = await new TissWebserviceCliente(new HttpClient(vazia))
            .VerificarElegibilidadeAsync(TissWebserviceCliente.ElegibilidadeSaudePetrobras, Pedido());

        a.Elegivel.Should().BeNull();
        a.Falha.Should().StartWith("sem resposta da operadora");
        b.Elegivel.Should().BeNull();
        b.Falha.Should().Be("resposta vazia da operadora (HTTP 502)");
    }

    [Fact]
    public async Task Cliente_recusa_endereco_sem_HTTPS()
    {
        var rede = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var acao = () => new TissWebserviceCliente(new HttpClient(rede))
            .VerificarElegibilidadeAsync(new Uri("http://webservices.crc.com.br/tiss/ws/40100/tissVerificaElegibilidade"), Pedido());

        await acao.Should().ThrowAsync<ArgumentException>().WithMessage("*HTTPS*");
        rede.Recebida.Should().BeNull();
    }

    // ---------- XSD ----------

    private static List<string> ValidarNoXsd(XElement mensagem)
    {
        var pasta = Path.Combine(AppContext.BaseDirectory, "Tiss", "xsd-4.01.00");
        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        schemas.Add(Ans.NamespaceName, Path.Combine(pasta, "tissWebServicesV4_01_00.xsd"));
        schemas.Compile();

        var problemas = new List<string>();
        new XDocument(mensagem).Validate(schemas, (_, e) => problemas.Add(e.Message));
        return problemas;
    }
}
