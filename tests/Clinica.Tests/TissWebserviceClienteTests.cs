using System.Net;
using System.Text;
using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O transporte das mensagens TISS, com a rede trocada por um manipulador de mentira: POST em
/// ISO-8859-1 só por HTTPS, corpo lido mesmo no HTTP 500 (é onde o SOAP põe a falha), e rede
/// fora do ar ou resposta vazia devolvidas como falha — nunca como exceção, nunca como recusa.
/// </summary>
public class TissWebserviceClienteTests
{
    private sealed class RedeDeMentira(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Chamadas { get; private set; }
        public HttpRequestMessage? Recebida { get; private set; }
        public byte[]? CorpoRecebido { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            Recebida = request;
            CorpoRecebido = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            return responder(request);
        }
    }

    private static PedidoLoteGuiasTiss Lote(string? observacao = null)
    {
        var codigo = TissWsDeTeste.CodigoConferido();
        codigo.GuiaAns!.Observacao = observacao;
        return new PedidoLoteGuiasTiss
        {
            Remetente = TissWsDeTeste.Remetente(),
            Sequencial = TissWsDeTeste.Sequencial,
            Momento = TissWsDeTeste.Momento,
            NumeroLote = "42",
            Guias = [TissExportService.GuiaConsultaConferida(codigo, NumeroGuiaPrestadorTiss.Gerar(codigo.Id))]
        };
    }

    private static string Protocolo(string nomeNoErro = "") => TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO", $"""
        <ans:recebimentoLote><ans:protocoloRecebimento>
          <ans:registroANS>422631</ans:registroANS>
          <ans:dadosPrestador><ans:codigoPrestadorNaOperadora>{TissWsDeTeste.CodigoPrestador}</ans:codigoPrestadorNaOperadora></ans:dadosPrestador>
          <ans:numeroLote>42</ans:numeroLote>
          <ans:dataEnvioLote>2026-11-11</ans:dataEnvioLote>
          <ans:detalheProtocolo>
            <ans:numeroProtocolo>987654321</ans:numeroProtocolo>
            <ans:valorTotalProtocolo>139.00</ans:valorTotalProtocolo>
            <ans:dadosGuiasProtocolo><ans:dadosGuias>{TissWsDeTeste.GuiaDevolvida("C1234", "OP-777001", nomeNoErro)}</ans:dadosGuias></ans:dadosGuiasProtocolo>
          </ans:detalheProtocolo>
        </ans:protocoloRecebimento></ans:recebimentoLote>
        """);

    [Fact]
    public void Enderecos_da_Saude_Petrobras_sao_HTTPS_no_mesmo_servidor_um_por_servico()
    {
        Uri[] enderecos =
        [
            TissWebserviceCliente.ElegibilidadeSaudePetrobras,
            TissWebserviceCliente.LoteGuiasSaudePetrobras,
            TissWebserviceCliente.StatusProtocoloSaudePetrobras,
            TissWebserviceCliente.CancelaGuiaSaudePetrobras,
            TissWebserviceCliente.DemonstrativoRetornoSaudePetrobras
        ];

        enderecos.Should().OnlyContain(e => e.Scheme == Uri.UriSchemeHttps && e.Host == "webservices.crc.com.br");
        enderecos.Select(e => e.AbsolutePath).Should().Equal(
            "/tiss/ws/40100/tissVerificaElegibilidade",
            "/tiss/ws/40100/tissLoteGuias",
            "/tiss/ws/40100/tissSolicitacaoStatusProtocolo",
            "/tiss/ws/40100/tissCancelaGuia",
            "/tiss/ws/40100/tissSolicitacaoDemonstrativoRetorno");
    }

    [Fact]
    public async Task Envia_POST_text_xml_com_os_bytes_em_ISO_8859_1_e_devolve_o_corpo_e_o_status()
    {
        var rede = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            // Bytes em ISO-8859-1 SEM charset no cabeçalho HTTP: quem manda é a declaração do XML.
            Content = new ByteArrayContent(Encoding.Latin1.GetBytes(Protocolo("<!-- José -->")))
        });
        var envelope = TissLoteGuiasWs.MontarEnvelope(Lote(observacao: "Retorno do José"));

        var bruta = await new TissWebserviceCliente(new HttpClient(rede))
            .EnviarAsync(TissWebserviceCliente.LoteGuiasSaudePetrobras, envelope);

        bruta.StatusHttp.Should().Be(200);
        bruta.Falha.Should().BeNull();
        bruta.Corpo.Should().Contain("José", "a resposta em ISO-8859-1 é lida na codificação que ela declara");
        rede.Recebida!.Method.Should().Be(HttpMethod.Post);
        rede.Recebida.RequestUri.Should().Be(TissWebserviceCliente.LoteGuiasSaudePetrobras);
        rede.Recebida.Content!.Headers.ContentType!.MediaType.Should().Be("text/xml");
        rede.Recebida.Content.Headers.ContentType.CharSet.Should().Be("ISO-8859-1");
        rede.Recebida.Headers.Contains("SOAPAction").Should().BeTrue();
        // O "é" sai em UM byte (0xE9), como o envelope declara e como o hash foi calculado.
        Encoding.Latin1.GetString(rede.CorpoRecebido!).Should().Be(envelope);
        rede.CorpoRecebido.Should().Contain((byte)0xE9).And.NotContain((byte)0xC3);
    }

    [Fact]
    public async Task Lote_ida_e_volta_pelo_cliente_e_lido_pela_classe_da_transacao()
    {
        var rede = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.Latin1.GetBytes(Protocolo())) });
        var cliente = new TissWebserviceCliente(new HttpClient(rede));

        var lida = TissLoteGuiasWs.LerResposta(
            await cliente.EnviarAsync(TissWebserviceCliente.LoteGuiasSaudePetrobras, TissLoteGuiasWs.MontarEnvelope(Lote())));

        lida.Recebido.Should().BeTrue();
        lida.Protocolo.Should().Be("987654321");
        lida.Guias.Single().NumeroGuiaOperadora.Should().Be("OP-777001");
    }

    [Fact]
    public async Task Resposta_em_UTF_8_tambem_e_lida_na_codificacao_que_declara()
    {
        var corpo = Protocolo("<!-- José -->").Replace("ISO-8859-1", "UTF-8");
        var rede = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(corpo)) });

        var bruta = await new TissWebserviceCliente(new HttpClient(rede))
            .EnviarAsync(TissWebserviceCliente.LoteGuiasSaudePetrobras, TissLoteGuiasWs.MontarEnvelope(Lote()));

        bruta.Corpo.Should().Contain("José");
    }

    [Fact]
    public async Task HTTP_500_devolve_o_corpo_porque_e_nele_que_vem_a_falha()
    {
        var rede = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(TissWsDeTeste.Falha("LoginInvalido"))
        });
        var cliente = new TissWebserviceCliente(new HttpClient(rede));

        var bruta = await cliente.EnviarAsync(TissWebserviceCliente.StatusProtocoloSaudePetrobras, "<x/>");

        bruta.StatusHttp.Should().Be(500);
        bruta.Falha.Should().BeNull();
        bruta.Corpo.Should().Contain("LoginInvalido");
        // E cada transação lê a mesma falha como "não se sabe", com o HTTP ao lado.
        TissLoteGuiasWs.LerResposta(bruta).Should().Match<RespostaLoteGuiasTiss>(r => r.Recebido == null && r.Falha == "LoginInvalido (HTTP 500)");
        TissStatusProtocoloWs.LerResposta(bruta).Should().Match<RespostaStatusProtocoloTiss>(r => r.Localizado == null && r.Falha == "LoginInvalido (HTTP 500)");
        TissCancelaGuiaWs.LerResposta(bruta).Should().Match<RespostaCancelaGuiaTiss>(r => r.Cancelado == null && r.Falha == "LoginInvalido (HTTP 500)");
    }

    [Fact]
    public async Task Pagina_HTML_de_erro_volta_como_corpo_e_a_transacao_a_le_como_nao_se_sabe()
    {
        var rede = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html><body>Bad Gateway<br></body></html>") });

        var bruta = await new TissWebserviceCliente(new HttpClient(rede))
            .EnviarAsync(TissWebserviceCliente.CancelaGuiaSaudePetrobras, "<x/>");
        var lida = TissCancelaGuiaWs.LerResposta(bruta);

        bruta.StatusHttp.Should().Be(502);
        lida.Cancelado.Should().BeNull();
        lida.Falha.Should().StartWith("resposta que não é XML").And.EndWith("(HTTP 502)");
    }

    [Fact]
    public async Task Rede_fora_do_ar_tempo_esgotado_e_resposta_vazia_voltam_como_falha_sem_lancar()
    {
        var semRede = new RedeDeMentira(_ => throw new HttpRequestException("sem rota"));
        var tempoEsgotado = new RedeDeMentira(_ => throw new TaskCanceledException("tempo esgotado"));
        var vazia = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new ByteArrayContent([]) });

        var a = await new TissWebserviceCliente(new HttpClient(semRede)).EnviarAsync(TissWebserviceCliente.LoteGuiasSaudePetrobras, "<x/>");
        var b = await new TissWebserviceCliente(new HttpClient(tempoEsgotado)).EnviarAsync(TissWebserviceCliente.LoteGuiasSaudePetrobras, "<x/>");
        var c = await new TissWebserviceCliente(new HttpClient(vazia)).EnviarAsync(TissWebserviceCliente.LoteGuiasSaudePetrobras, "<x/>");

        a.Should().Be(new RespostaBrutaTissWs(null, null, "sem resposta da operadora (sem rota)"));
        b.Corpo.Should().BeNull();
        b.Falha.Should().StartWith("sem resposta da operadora");
        c.Should().Be(new RespostaBrutaTissWs(null, 502, "resposta vazia da operadora (HTTP 502)"));
        // Lote sem resposta não é lote recusado: pode ter chegado.
        TissLoteGuiasWs.LerResposta(a).Recebido.Should().BeNull();
        TissLoteGuiasWs.LerResposta(c).Should().Match<RespostaLoteGuiasTiss>(r => r.Recebido == null && r.Falha == "resposta vazia da operadora (HTTP 502)");
    }

    [Fact]
    public async Task Endereco_sem_HTTPS_e_recusado_sem_chegar_a_rede()
    {
        var rede = new RedeDeMentira(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var acao = () => new TissWebserviceCliente(new HttpClient(rede))
            .EnviarAsync(new Uri("http://webservices.crc.com.br/tiss/ws/40100/tissLoteGuias"), "<x/>");

        await acao.Should().ThrowAsync<ArgumentException>().WithMessage("*HTTPS*");
        rede.Chamadas.Should().Be(0);
    }
}
