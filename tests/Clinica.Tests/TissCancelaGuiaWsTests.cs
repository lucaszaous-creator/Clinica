using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O cancelamento de guia pelo web service TISS (4.01.00), para desfazer o que foi enviado por
/// engano: o pedido (por guia ou do lote inteiro) é válido no XSD oficial e o recibo tem três
/// leituras — cancelado, não cancelado e "não se sabe". "Em processamento" não é cancelado.
/// </summary>
public class TissCancelaGuiaWsTests
{
    private static readonly XNamespace Ans = TissWsDeTeste.Ans;

    private static PedidoCancelaGuiaTiss PorGuia(params GuiaACancelarTiss[] guias) => new()
    {
        Remetente = TissWsDeTeste.Remetente(),
        Sequencial = TissWsDeTeste.Sequencial,
        Momento = TissWsDeTeste.Momento,
        Guias = guias.Length > 0 ? guias : [new GuiaACancelarTiss { NumeroGuiaPrestador = "C1234" }]
    };

    private static PedidoCancelaGuiaTiss PorLote(string? protocolo = "987654321") => new()
    {
        Remetente = TissWsDeTeste.Remetente(),
        Sequencial = TissWsDeTeste.Sequencial,
        Momento = TissWsDeTeste.Momento,
        NumeroLote = "42",
        NumeroProtocoloDoLote = protocolo
    };

    // ---------- o pedido ----------

    [Fact]
    public void Pedido_por_guia_e_valido_no_XSD_com_e_sem_os_numeros_opcionais()
    {
        var soPrestador = TissCancelaGuiaWs.MontarPedido(PorGuia());
        var completo = TissCancelaGuiaWs.MontarPedido(PorGuia(
            new GuiaACancelarTiss { NumeroGuiaPrestador = "C1234", NumeroGuiaOperadora = "OP-777001", NumeroProtocolo = "987654321" },
            new GuiaACancelarTiss { NumeroGuiaPrestador = "C1235" }));

        TissWsDeTeste.ValidarNoXsd(soPrestador).Should().BeEmpty();
        TissWsDeTeste.ValidarNoXsd(completo).Should().BeEmpty();
        completo.Descendants(Ans + "tipoCancelamentoGuia").Should().HaveCount(2);
        completo.Descendants(Ans + "tipoGuia").Select(t => t.Value).Should().AllBe("2", "é guia de faturamento");
    }

    [Fact]
    public void Pedido_do_lote_inteiro_e_valido_no_XSD_com_e_sem_o_protocolo()
    {
        TissWsDeTeste.ValidarNoXsd(TissCancelaGuiaWs.MontarPedido(PorLote())).Should().BeEmpty();
        TissWsDeTeste.ValidarNoXsd(TissCancelaGuiaWs.MontarPedido(PorLote(protocolo: null))).Should().BeEmpty();
    }

    [Fact]
    public void O_XSD_reprova_de_verdade_quando_a_mensagem_esta_errada()
    {
        var pedido = TissCancelaGuiaWs.MontarPedido(PorGuia());
        pedido.Descendants(Ans + "tipoGuia").Single().Value = "9";

        TissWsDeTeste.ValidarNoXsd(pedido).Should().NotBeEmpty();
    }

    [Fact]
    public void Hash_e_o_MD5_do_conteudo_das_tags_na_ordem_sem_a_propria_tag()
    {
        var pedido = PorGuia(new GuiaACancelarTiss { NumeroGuiaPrestador = "C1234", NumeroGuiaOperadora = "OP-777001" });
        var mensagem = TissCancelaGuiaWs.MontarPedido(pedido);

        var esperado = "CANCELA_GUIA" + "261111101500" + "2026-11-11" + "10:15:00"
                       + "12345678000190" + "422631" + "4.01.00" + "clinica.teste" + pedido.Remetente.SenhaMd5
                       + "12345678000190" + "2" + "C1234" + "OP-777001";
        var md5 = Convert.ToHexString(MD5.HashData(Encoding.Latin1.GetBytes(esperado))).ToLowerInvariant();

        mensagem.Element(Ans + "hash")!.Value.Should().Be(md5);
        mensagem.Elements().Select(e => e.Name.LocalName).Should().Equal("cabecalho", "cancelaGuia", "hash");
    }

    [Fact]
    public void Senha_nunca_vai_em_claro_e_o_envelope_leva_o_pedido_no_corpo()
    {
        var pedido = PorGuia() with { Remetente = TissWsDeTeste.Remetente("Senha@Secreta1") };

        var envelope = TissCancelaGuiaWs.MontarEnvelope(pedido);

        envelope.Should().StartWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>").And.NotContain("Senha@Secreta1");
        XDocument.Parse(envelope).Root!.Element(TissWsDeTeste.Soap + "Body")!.Elements().Single().Name
            .Should().Be(Ans + "cancelaGuiaWS");
        TissCancelaGuiaWs.MontarEnvelopeParaExibir(pedido)
            .Should().NotContain(pedido.Remetente.SenhaMd5).And.Contain(new string('*', 32)).And.Contain("C1234");
    }

    [Fact]
    public void Pedido_sem_alvo_com_guia_e_lote_juntos_ou_com_guia_repetida_e_recusado_antes_do_envio()
    {
        var semAlvo = () => TissCancelaGuiaWs.MontarPedido(PorGuia() with { Guias = [] });
        var osDois = () => TissCancelaGuiaWs.MontarPedido(PorGuia() with { NumeroLote = "42" });
        var protocoloSemLote = () => TissCancelaGuiaWs.MontarPedido(PorGuia() with { NumeroProtocoloDoLote = "987654321" });
        var repetida = () => TissCancelaGuiaWs.MontarPedido(PorGuia(
            new GuiaACancelarTiss { NumeroGuiaPrestador = "C1" }, new GuiaACancelarTiss { NumeroGuiaPrestador = "C1" }));

        semAlvo.Should().Throw<ArgumentException>().WithMessage("*por guia OU do lote inteiro*");
        osDois.Should().Throw<ArgumentException>().WithMessage("*por guia OU do lote inteiro*");
        protocoloSemLote.Should().Throw<ArgumentException>().WithMessage("*só vale no cancelamento do lote*");
        repetida.Should().Throw<ArgumentException>().WithMessage("*repetida*");
    }

    [Fact]
    public void Numero_fora_do_campo_e_recusado_antes_do_envio()
    {
        var guiaLonga = () => TissCancelaGuiaWs.MontarPedido(PorGuia(new GuiaACancelarTiss { NumeroGuiaPrestador = new string('9', 21) }));
        var operadoraVazia = () => TissCancelaGuiaWs.MontarPedido(PorGuia(new GuiaACancelarTiss { NumeroGuiaPrestador = "C1", NumeroGuiaOperadora = "" }));
        var loteLongo = () => TissCancelaGuiaWs.MontarPedido(PorLote() with { NumeroLote = new string('1', 13) });

        guiaLonga.Should().Throw<ArgumentException>().WithMessage("*número da guia no prestador*");
        operadoraVazia.Should().Throw<ArgumentException>().WithMessage("*número da guia na operadora*");
        loteLongo.Should().Throw<ArgumentException>().WithMessage("*número do lote*");
    }

    // ---------- leitura do recibo ----------

    private static string Recibo(string retornoStatus) => TissWsDeTeste.Resposta("reciboCancelaGuiaWS", "CANCELAMENTO_GUIA_RECIBO", $"""
        <ans:reciboCancelaGuia><ans:reciboCancelaGuia>
          <ans:registroANS>422631</ans:registroANS>
          <ans:dadosPrestador><ans:codigoPrestadorNaOperadora>{TissWsDeTeste.CodigoPrestador}</ans:codigoPrestadorNaOperadora></ans:dadosPrestador>
          <ans:retornoStatus>{retornoStatus}</ans:retornoStatus>
        </ans:reciboCancelaGuia></ans:reciboCancelaGuia>
        """);

    private static string GuiaDoRecibo(string numero, string status, string? numeroOperadora = null) => $"""
        <ans:dadosGuia>
          <ans:tipoGuia>2</ans:tipoGuia>
          <ans:numeroGuiaPrestador>{numero}</ans:numeroGuiaPrestador>
          {(numeroOperadora is null ? "" : $"<ans:numeroGuiaOperadora>{numeroOperadora}</ans:numeroGuiaOperadora>")}
          <ans:statusCancelamento>{status}</ans:statusCancelamento>
        </ans:dadosGuia>
        """;

    private static string Guias(string guias) => Recibo($"<ans:guiasCanceladas>{guias}</ans:guiasCanceladas>");

    [Fact]
    public void Todas_as_guias_com_status_1_e_cancelado()
    {
        var resposta = Guias(GuiaDoRecibo("C1234", "1", "OP-777001") + GuiaDoRecibo("C1235", "1"));

        var lida = TissCancelaGuiaWs.LerResposta(resposta);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty("a resposta do teste é a que o XSD define");
        lida.Cancelado.Should().BeTrue();
        lida.Guias.Select(g => (g.NumeroGuiaPrestador, g.NumeroGuiaOperadora, g.Cancelada))
            .Should().Equal(("C1234", "OP-777001", true), ("C1235", null, true));
        lida.Falha.Should().BeNull();
        lida.Resumo.Should().Be("2 guia(s) cancelada(s).");
    }

    [Theory]
    [InlineData("2", "Não cancelado")]
    [InlineData("3", "Guia inexistente")]
    [InlineData("6", "Protocolo inexistente")]
    public void Uma_guia_recusada_torna_o_cancelamento_incompleto_e_diz_qual(string status, string nome)
    {
        var resposta = Guias(GuiaDoRecibo("C1234", "1") + GuiaDoRecibo("C1235", status));

        var lida = TissCancelaGuiaWs.LerResposta(resposta);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty();
        lida.Cancelado.Should().BeFalse();
        lida.Guias[0].Cancelada.Should().BeTrue();
        lida.Guias[1].Cancelada.Should().BeFalse();
        lida.Guias[1].StatusDescricao.Should().Be(nome);
        lida.Resumo.Should().Be($"Cancelamento incompleto: C1235 - {nome}.");
    }

    [Fact]
    public void Em_processamento_nao_e_cancelado_nem_recusado_e_nao_se_sabe()
    {
        var lida = TissCancelaGuiaWs.LerResposta(Guias(GuiaDoRecibo("C1234", "1") + GuiaDoRecibo("C1235", "4")));

        lida.Cancelado.Should().BeNull();
        lida.Guias[1].Cancelada.Should().BeFalse();
        lida.Guias[1].StatusDescricao.Should().Be("Em processamento");
        lida.Resumo.Should().Be("Não se sabe se o cancelamento foi feito: a operadora ainda está processando o pedido.");
    }

    [Fact]
    public void Recibo_do_lote_inteiro_le_o_status_e_o_protocolo_na_tag_em_minusculas()
    {
        var cancelado = Recibo("<ans:loteCancelado><ans:numeroLote>42</ans:numeroLote><ans:numeroprotocolo>987654321</ans:numeroprotocolo><ans:statusCancelamento>1</ans:statusCancelamento></ans:loteCancelado>");
        var inexistente = Recibo("<ans:loteCancelado><ans:numeroLote>42</ans:numeroLote><ans:statusCancelamento>5</ans:statusCancelamento></ans:loteCancelado>");

        var a = TissCancelaGuiaWs.LerResposta(cancelado);
        var b = TissCancelaGuiaWs.LerResposta(inexistente);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(cancelado)).Should().BeEmpty();
        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(inexistente)).Should().BeEmpty();
        a.Cancelado.Should().BeTrue();
        a.NumeroLote.Should().Be("42");
        a.NumeroProtocoloDoLote.Should().Be("987654321");
        a.StatusDoLote.Should().Be("1");
        a.Resumo.Should().Be("Lote 42 cancelado.");
        b.Cancelado.Should().BeFalse();
        b.Resumo.Should().Be("Lote 42 não cancelado: Lote inexistente.");
    }

    [Fact]
    public void Mensagem_de_erro_no_lugar_do_recibo_e_cancelamento_recusado_com_o_motivo()
    {
        var resposta = TissWsDeTeste.Resposta("reciboCancelaGuiaWS", "CANCELAMENTO_GUIA_RECIBO",
            "<ans:reciboCancelaGuia><ans:mensagemErro><ans:codigoGlosa>1307</ans:codigoGlosa><ans:descricaoGlosa>NUMERO DA GUIA INVALIDO</ans:descricaoGlosa></ans:mensagemErro></ans:reciboCancelaGuia>");

        var lida = TissCancelaGuiaWs.LerResposta(resposta);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty();
        lida.Cancelado.Should().BeFalse();
        lida.Motivos.Should().Equal(("1307", "NUMERO DA GUIA INVALIDO"));
        lida.Resumo.Should().Be("A operadora recusou o cancelamento: 1307 - NUMERO DA GUIA INVALIDO");
    }

    [Theory]
    [InlineData("LoginInvalido")]
    [InlineData("SchemaInvalido")]
    [InlineData("VersaoInvalida")]
    public void Falha_do_padrao_TISS_e_nao_se_sabe_e_nunca_cancelado_nem_recusado(string falha)
    {
        var lida = TissCancelaGuiaWs.LerResposta(TissWsDeTeste.Falha(falha));

        lida.Cancelado.Should().BeNull();
        lida.Falha.Should().Be(falha);
        lida.Resumo.Should().Be($"Não se sabe se o cancelamento foi feito: {falha}.");
    }

    [Theory]
    [InlineData("<html><body>Servico indisponivel</body></html", "não é XML")]
    [InlineData("", "resposta vazia")]
    [InlineData("<a/>", "sem <reciboCancelaGuia>")]
    public void Resposta_inesperada_nunca_lanca_e_nunca_vira_cancelado(string corpo, string trecho)
    {
        var lida = TissCancelaGuiaWs.LerResposta(corpo);

        lida.Cancelado.Should().BeNull();
        lida.Falha.Should().Contain(trecho);
    }

    [Fact]
    public void Recibo_oco_e_nao_se_sabe()
    {
        var lida = TissCancelaGuiaWs.LerResposta(Recibo(""));

        lida.Cancelado.Should().BeNull();
        lida.Falha.Should().Be("recibo sem lote e sem guias");
    }

    [Fact]
    public void Resposta_bruta_junta_o_HTTP_a_falha_e_a_falta_de_corpo_e_nao_se_sabe()
    {
        var erro500 = TissCancelaGuiaWs.LerResposta(new RespostaBrutaTissWs(TissWsDeTeste.Falha("SchemaInvalido"), 500, null));
        var semRede = TissCancelaGuiaWs.LerResposta(new RespostaBrutaTissWs(null, null, "sem resposta da operadora (sem rota)"));
        var ok = TissCancelaGuiaWs.LerResposta(new RespostaBrutaTissWs(Guias(GuiaDoRecibo("C1234", "1")), 200, null));

        erro500.Cancelado.Should().BeNull();
        erro500.Falha.Should().Be("SchemaInvalido (HTTP 500)");
        semRede.Cancelado.Should().BeNull();
        semRede.Falha.Should().Be("sem resposta da operadora (sem rota)");
        ok.Cancelado.Should().BeTrue();
    }
}
