using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A consulta da situação de um protocolo pelo web service TISS (4.01.00): o pedido é válido no
/// XSD oficial e a resposta tem três leituras — localizado, não localizado e "não se sabe".
/// O padrão não traz situação POR GUIA: a guia volta com números, valores e glosas.
/// </summary>
public class TissStatusProtocoloWsTests
{
    private static readonly XNamespace Ans = TissWsDeTeste.Ans;

    private static PedidoStatusProtocoloTiss Pedido(string senha = "segredo") => new()
    {
        Remetente = TissWsDeTeste.Remetente(senha),
        Sequencial = TissWsDeTeste.Sequencial,
        Momento = TissWsDeTeste.Momento,
        NumeroProtocolo = "987654321"
    };

    // ---------- o pedido ----------

    [Fact]
    public void Pedido_e_valido_no_XSD_oficial_do_web_service()
        => TissWsDeTeste.ValidarNoXsd(TissStatusProtocoloWs.MontarPedido(Pedido())).Should().BeEmpty();

    [Fact]
    public void O_XSD_reprova_de_verdade_quando_a_mensagem_esta_errada()
    {
        var pedido = TissStatusProtocoloWs.MontarPedido(Pedido());
        pedido.Descendants(Ans + "numeroProtocolo").Single().Remove();

        TissWsDeTeste.ValidarNoXsd(pedido).Should().NotBeEmpty();
    }

    [Fact]
    public void Hash_e_o_MD5_do_conteudo_das_tags_na_ordem_sem_a_propria_tag()
    {
        var pedido = Pedido();
        var mensagem = TissStatusProtocoloWs.MontarPedido(pedido);

        var esperado = "SOLIC_STATUS_PROTOCOLO" + "261111101500" + "2026-11-11" + "10:15:00"
                       + "12345678000190" + "422631" + "4.01.00" + "clinica.teste" + pedido.Remetente.SenhaMd5
                       + "12345678000190" + "987654321";
        var md5 = Convert.ToHexString(MD5.HashData(Encoding.Latin1.GetBytes(esperado))).ToLowerInvariant();

        mensagem.Element(Ans + "hash")!.Value.Should().Be(md5);
        mensagem.Elements().Select(e => e.Name.LocalName).Should().Equal("cabecalho", "solicitacaoStatusProtocolo", "hash");
    }

    [Fact]
    public void Senha_nunca_vai_em_claro_e_o_envelope_leva_o_pedido_no_corpo()
    {
        var pedido = Pedido("Senha@Secreta1");

        var envelope = TissStatusProtocoloWs.MontarEnvelope(pedido);

        envelope.Should().StartWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>").And.NotContain("Senha@Secreta1");
        XDocument.Parse(envelope).Root!.Element(TissWsDeTeste.Soap + "Body")!.Elements().Single().Name
            .Should().Be(Ans + "solicitacaoStatusProtocoloWS");
        TissStatusProtocoloWs.MontarEnvelopeParaExibir(pedido)
            .Should().NotContain(pedido.Remetente.SenhaMd5).And.Contain(new string('*', 32)).And.Contain("987654321");
    }

    [Fact]
    public void Protocolo_vazio_ou_maior_que_o_campo_e_recusado_antes_do_envio()
    {
        var vazio = () => TissStatusProtocoloWs.MontarPedido(Pedido() with { NumeroProtocolo = "" });
        var longo = () => TissStatusProtocoloWs.MontarPedido(Pedido() with { NumeroProtocolo = new string('9', 13) });

        vazio.Should().Throw<ArgumentException>().WithMessage("*número do protocolo*");
        longo.Should().Throw<ArgumentException>().WithMessage("*número do protocolo*");
    }

    // ---------- leitura da resposta ----------

    private static string Situacao(string status, string guias) => TissWsDeTeste.Resposta("situacaoProtocoloWS", "SITUACAO_PROTOCOLO", $"""
        <ans:situacaoProtocolo><ans:situacaoDoProtocolo>
          <ans:identificacaoOperadora>422631</ans:identificacaoOperadora>
          <ans:dadosPrestador><ans:codigoPrestadorNaOperadora>{TissWsDeTeste.CodigoPrestador}</ans:codigoPrestadorNaOperadora></ans:dadosPrestador>
          <ans:lote><ans:detalheLote>
            <ans:statusProtocolo>{status}</ans:statusProtocolo>
            <ans:numeroProtocolo>987654321</ans:numeroProtocolo>
            <ans:numeroLote>42</ans:numeroLote>
            <ans:dataEnvioLote>2026-11-11</ans:dataEnvioLote>
            <ans:valorTotalLote><ans:valorProcessado>278.00</ans:valorProcessado><ans:valorGlosa>139.00</ans:valorGlosa><ans:valorLiberado>139.00</ans:valorLiberado></ans:valorTotalLote>
            <ans:guiasTISS>{guias}</ans:guiasTISS>
          </ans:detalheLote></ans:lote>
        </ans:situacaoDoProtocolo></ans:situacaoProtocolo>
        """);

    private static string Guia(string numeroPrestador, string? numeroOperadora = null, string extras = "")
        => $"<ans:guiasMedicas><ans:guias>{TissWsDeTeste.GuiaDevolvida(numeroPrestador, numeroOperadora, extras)}</ans:guias></ans:guiasMedicas>";

    private const string GlosaDaGuia = """
        <ans:vlInformadoGuia><ans:valorProcessado>139.00</ans:valorProcessado><ans:valorGlosa>139.00</ans:valorGlosa><ans:valorLiberado>0.00</ans:valorLiberado></ans:vlInformadoGuia>
        <ans:glosaGuia><ans:motivoGlosa><ans:codigoGlosa>1001</ans:codigoGlosa><ans:descricaoGlosa>NUMERO DA CARTEIRA INVALIDO</ans:descricaoGlosa></ans:motivoGlosa></ans:glosaGuia>
        """;

    [Fact]
    public void Situacao_do_lote_traz_o_status_os_valores_e_cada_guia_com_o_numero_da_operadora_quando_vem()
    {
        var resposta = Situacao("2", Guia("C1234", "OP-777001") + Guia("C1235"));

        var lida = TissStatusProtocoloWs.LerResposta(resposta);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty("a resposta do teste é a que o XSD define");
        lida.Localizado.Should().BeTrue();
        lida.Situacao.Should().Be("2");
        lida.SituacaoDescricao.Should().Be("Em análise");
        lida.Protocolo.Should().Be("987654321");
        lida.NumeroLote.Should().Be("42");
        lida.DataEnvio.Should().Be(new DateOnly(2026, 11, 11));
        lida.ValorProcessado.Should().Be(278.00m);
        lida.ValorGlosa.Should().Be(139.00m);
        lida.ValorLiberado.Should().Be(139.00m);
        lida.Guias.Select(g => (g.NumeroGuiaPrestador, g.NumeroGuiaOperadora))
            .Should().Equal(("C1234", "OP-777001"), ("C1235", null));
        lida.Falha.Should().BeNull();
        lida.Resumo.Should().Be("Protocolo 987654321: Em análise, 2 guia(s).");
    }

    [Fact]
    public void Guia_glosada_na_situacao_traz_o_motivo_e_os_valores()
    {
        var resposta = Situacao("5", Guia("C1234", "OP-777001", GlosaDaGuia) + Guia("C1235", "OP-777002"));

        var lida = TissStatusProtocoloWs.LerResposta(resposta);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty();
        lida.Localizado.Should().BeTrue();
        lida.Guias[0].Glosada.Should().BeTrue();
        lida.Guias[0].Glosas.Should().Equal(("1001", "NUMERO DA CARTEIRA INVALIDO"));
        lida.Guias[0].ValorGlosa.Should().Be(139.00m);
        lida.Guias[1].Glosada.Should().BeFalse();
        lida.Resumo.Should().Be("Protocolo 987654321: Analisado e aguardando liberação para pagamento, 2 guia(s), 1 com glosa.");
    }

    [Theory]
    [InlineData("1", "Recebido")]
    [InlineData("3", "Liberado para pagamento")]
    [InlineData("4", "Encerrado sem pagamento")]
    [InlineData("6", "Pagamento efetuado")]
    [InlineData("8", "Aguardando informação complementar")]
    [InlineData("9", "Cancelado")]
    public void Cada_situacao_do_XSD_tem_o_seu_nome(string codigo, string nome)
    {
        var lida = TissStatusProtocoloWs.LerResposta(Situacao(codigo, Guia("C1234")));

        lida.Localizado.Should().BeTrue();
        lida.SituacaoDescricao.Should().Be(nome);
    }

    [Fact]
    public void Situacao_7_e_protocolo_nao_localizado()
    {
        var lida = TissStatusProtocoloWs.LerResposta(Situacao("7", Guia("C1234")));

        lida.Localizado.Should().BeFalse();
        lida.SituacaoDescricao.Should().Be("Não localizado");
        lida.Resumo.Should().Be("A operadora não localizou o protocolo.");
    }

    [Fact]
    public void Mensagem_de_erro_no_lugar_do_lote_ou_da_situacao_e_nao_localizado_com_o_motivo()
    {
        var noLote = TissWsDeTeste.Resposta("situacaoProtocoloWS", "SITUACAO_PROTOCOLO", $"""
            <ans:situacaoProtocolo><ans:situacaoDoProtocolo>
              <ans:identificacaoOperadora>422631</ans:identificacaoOperadora>
              <ans:dadosPrestador><ans:codigoPrestadorNaOperadora>{TissWsDeTeste.CodigoPrestador}</ans:codigoPrestadorNaOperadora></ans:dadosPrestador>
              <ans:lote><ans:mensagemErro><ans:codigoGlosa>1307</ans:codigoGlosa><ans:descricaoGlosa>NUMERO DA GUIA INVALIDO</ans:descricaoGlosa></ans:mensagemErro></ans:lote>
            </ans:situacaoDoProtocolo></ans:situacaoProtocolo>
            """);
        var naSituacao = TissWsDeTeste.Resposta("situacaoProtocoloWS", "SITUACAO_PROTOCOLO",
            "<ans:situacaoProtocolo><ans:mensagemErro><ans:codigoGlosa>1203</ans:codigoGlosa></ans:mensagemErro></ans:situacaoProtocolo>");

        var a = TissStatusProtocoloWs.LerResposta(noLote);
        var b = TissStatusProtocoloWs.LerResposta(naSituacao);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(noLote)).Should().BeEmpty();
        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(naSituacao)).Should().BeEmpty();
        a.Localizado.Should().BeFalse();
        a.Motivos.Should().Equal(("1307", "NUMERO DA GUIA INVALIDO"));
        a.Resumo.Should().Be("A operadora não devolveu o lote: 1307 - NUMERO DA GUIA INVALIDO");
        b.Localizado.Should().BeFalse();
        b.Motivos.Should().Equal(("1203", null));
    }

    [Theory]
    [InlineData("LoginInvalido")]
    [InlineData("SchemaInvalido")]
    [InlineData("VersaoInvalida")]
    public void Falha_do_padrao_TISS_e_nao_se_sabe_e_nunca_nao_localizado(string falha)
    {
        var lida = TissStatusProtocoloWs.LerResposta(TissWsDeTeste.Falha(falha));

        lida.Localizado.Should().BeNull();
        lida.Falha.Should().Be(falha);
        lida.Resumo.Should().Be($"Não se sabe a situação do protocolo: {falha}.");
    }

    [Theory]
    [InlineData("<html><body>Servico indisponivel</body></html", "não é XML")]
    [InlineData("", "resposta vazia")]
    [InlineData("<a/>", "sem <situacaoProtocolo>")]
    public void Resposta_inesperada_nunca_lanca_e_nunca_vira_nao_localizado(string corpo, string trecho)
    {
        var lida = TissStatusProtocoloWs.LerResposta(corpo);

        lida.Localizado.Should().BeNull();
        lida.Falha.Should().Contain(trecho);
    }

    [Fact]
    public void Situacao_sem_detalhe_e_sem_erro_e_nao_se_sabe()
    {
        var lida = TissStatusProtocoloWs.LerResposta(TissWsDeTeste.Resposta("situacaoProtocoloWS", "SITUACAO_PROTOCOLO",
            "<ans:situacaoProtocolo><ans:situacaoDoProtocolo><ans:lote/></ans:situacaoDoProtocolo></ans:situacaoProtocolo>"));

        lida.Localizado.Should().BeNull();
        lida.Falha.Should().Be("resposta sem detalhe do lote e sem mensagem de erro");
    }

    [Fact]
    public void Resposta_bruta_junta_o_HTTP_a_falha_e_a_falta_de_corpo_e_nao_se_sabe()
    {
        var erro500 = TissStatusProtocoloWs.LerResposta(new RespostaBrutaTissWs(TissWsDeTeste.Falha("LoginInvalido"), 500, null));
        var semRede = TissStatusProtocoloWs.LerResposta(new RespostaBrutaTissWs(null, null, "sem resposta da operadora (sem rota)"));
        var ok = TissStatusProtocoloWs.LerResposta(new RespostaBrutaTissWs(Situacao("1", Guia("C1234")), 200, null));

        erro500.Localizado.Should().BeNull();
        erro500.Falha.Should().Be("LoginInvalido (HTTP 500)");
        semRede.Localizado.Should().BeNull();
        semRede.Falha.Should().Be("sem resposta da operadora (sem rota)");
        ok.Localizado.Should().BeTrue();
    }
}
