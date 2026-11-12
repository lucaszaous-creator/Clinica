using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O envio de lote de guias pelo web service TISS (4.01.00): o <c>loteGuiasWS</c> que sai é
/// válido no XSD oficial, o hash é o do padrão, a senha nunca vai em claro, o lote que o padrão
/// recusaria é recusado ANTES do envio, e a resposta tem TRÊS leituras — recebido, recusado e
/// "não se sabe" —, porque um lote sem resposta pode ter chegado.
/// </summary>
public class TissLoteGuiasWsTests
{
    private static readonly XNamespace Ans = TissWsDeTeste.Ans;

    private static PedidoLoteGuiasTiss Pedido(IReadOnlyList<XElement>? guias = null, string senha = "segredo") => new()
    {
        Remetente = TissWsDeTeste.Remetente(senha),
        Sequencial = TissWsDeTeste.Sequencial,
        Momento = TissWsDeTeste.Momento,
        NumeroLote = "42",
        Guias = guias ?? [TissWsDeTeste.GuiaConsulta()]
    };

    private static List<XElement> Guias(int quantas)
        => Enumerable.Range(1, quantas).Select(i => TissWsDeTeste.GuiaConsulta(i)).ToList();

    // ---------- o pedido ----------

    [Fact]
    public void Lote_com_a_guia_conferida_e_valido_no_XSD_oficial_do_web_service()
        => TissWsDeTeste.ValidarNoXsd(TissLoteGuiasWs.MontarPedido(Pedido())).Should().BeEmpty();

    [Fact]
    public void Lote_com_cem_guias_e_valido_no_XSD()
        => TissWsDeTeste.ValidarNoXsd(TissLoteGuiasWs.MontarPedido(Pedido(Guias(100)))).Should().BeEmpty();

    [Fact]
    public void O_XSD_reprova_de_verdade_quando_a_mensagem_esta_errada()
    {
        // Sem esta prova, "lista vazia" poderia ser só um validador que não carregou schema nenhum.
        var semLote = TissLoteGuiasWs.MontarPedido(Pedido());
        semLote.Descendants(Ans + "numeroLote").Single().Remove();
        var ufEmSigla = TissLoteGuiasWs.MontarPedido(Pedido());
        ufEmSigla.Descendants(Ans + "UF").Single().Value = "RJ";

        TissWsDeTeste.ValidarNoXsd(semLote).Should().NotBeEmpty();
        TissWsDeTeste.ValidarNoXsd(ufEmSigla).Should().NotBeEmpty();
    }

    [Fact]
    public void Hash_e_o_MD5_do_conteudo_das_tags_na_ordem_sem_a_propria_tag()
    {
        var pedido = Pedido();
        var mensagem = TissLoteGuiasWs.MontarPedido(pedido);

        var esperado = "ENVIO_LOTE_GUIAS" + "261111101500" + "2026-11-11" + "10:15:00"
                       + "12345678000190" + "422631" + "4.01.00" + "clinica.teste" + pedido.Remetente.SenhaMd5
                       + "42"
                       + "422631" + "C1234" + "000123456789" + "N" + "12345678000190" + "9999999"
                       + "PROFISSIONAL DE TESTE" + "06" + "000000" + "33" + "225133" + "9"
                       + "01" + "2026-11-10" + "1" + "22" + "10101012" + "139.00";
        var md5 = Convert.ToHexString(MD5.HashData(Encoding.Latin1.GetBytes(esperado))).ToLowerInvariant();

        mensagem.Element(Ans + "hash")!.Value.Should().Be(md5);
        mensagem.Elements().Last().Name.Should().Be(Ans + "hash");
        mensagem.Elements().Select(e => e.Name.LocalName).Should().Equal("cabecalho", "loteGuias", "hash");
    }

    [Fact]
    public void Hash_do_lote_usa_ISO_8859_1_quando_a_guia_tem_acento()
    {
        var codigo = TissWsDeTeste.CodigoConferido();
        codigo.GuiaAns!.Observacao = "Retorno do José";
        var mensagem = TissLoteGuiasWs.MontarPedido(Pedido([TissExportService.GuiaConsultaConferida(codigo, "C1234")]));

        var hash = mensagem.Element(Ans + "hash")!.Value;
        mensagem.Element(Ans + "hash")!.Remove();
        var conteudo = string.Concat(mensagem.DescendantNodes().OfType<XText>().Select(t => t.Value));

        conteudo.Should().Contain("José");
        hash.Should().Be(Convert.ToHexString(MD5.HashData(Encoding.Latin1.GetBytes(conteudo))).ToLowerInvariant())
            .And.NotBe(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(conteudo))).ToLowerInvariant());
    }

    [Fact]
    public void Senha_vai_em_MD5_e_nunca_em_claro_e_o_envelope_para_exibir_a_mascara()
    {
        var pedido = Pedido(senha: "Senha@Secreta1");

        var envelope = TissLoteGuiasWs.MontarEnvelope(pedido);
        var exibido = TissLoteGuiasWs.MontarEnvelopeParaExibir(pedido);

        envelope.Should().NotContain("Senha@Secreta1");
        XDocument.Parse(envelope).Descendants(Ans + "senhaPrestador").Single().Value
            .Should().Be(TissElegibilidadeWs.SenhaEmMd5("Senha@Secreta1"));
        exibido.Should().NotContain(pedido.Remetente.SenhaMd5).And.NotContain("Senha@Secreta1");
        exibido.Should().Contain(new string('*', 32)).And.Contain("C1234");
    }

    [Fact]
    public void Envelope_e_SOAP_1_1_declarado_em_ISO_8859_1_com_o_loteGuiasWS_no_corpo()
    {
        var envelope = TissLoteGuiasWs.MontarEnvelope(Pedido());

        envelope.Should().StartWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
        var doc = XDocument.Parse(envelope);
        doc.Root!.Name.Should().Be(TissWsDeTeste.Soap + "Envelope");
        doc.Root.Element(TissWsDeTeste.Soap + "Body")!.Elements().Single().Name.Should().Be(Ans + "loteGuiasWS");
    }

    [Fact]
    public void Cabecalho_e_de_envio_de_lote_com_login_e_o_lote_leva_o_numero_e_as_guias()
    {
        var mensagem = TissLoteGuiasWs.MontarPedido(Pedido(Guias(3)));

        mensagem.Descendants(Ans + "tipoTransacao").Single().Value.Should().Be("ENVIO_LOTE_GUIAS");
        mensagem.Descendants(Ans + "loginPrestador").Single().Value.Should().Be("clinica.teste");
        mensagem.Element(Ans + "loteGuias")!.Element(Ans + "numeroLote")!.Value.Should().Be("42");
        mensagem.Element(Ans + "loteGuias")!.Element(Ans + "guiasTISS")!.Elements(Ans + "guiaConsulta")
            .Select(TissLoteGuiasWs.NumeroGuiaPrestadorDa).Should().Equal("C1", "C2", "C3");
    }

    [Fact]
    public void As_guias_de_quem_chamou_nao_sao_levadas_para_dentro_da_mensagem()
    {
        var guia = TissWsDeTeste.GuiaConsulta();

        TissLoteGuiasWs.MontarPedido(Pedido([guia]));

        // Levada (e não copiada), a segunda montagem com a mesma guia a arrancaria do primeiro lote.
        guia.Parent.Should().BeNull();
    }

    [Fact]
    public void Lote_vazio_e_lote_com_mais_de_cem_guias_sao_recusados_antes_do_envio()
    {
        var vazio = () => TissLoteGuiasWs.MontarPedido(Pedido([]));
        var cento_e_uma = () => TissLoteGuiasWs.MontarPedido(Pedido(Guias(101)));

        vazio.Should().Throw<ArgumentException>().WithMessage("*pelo menos uma guia*");
        cento_e_uma.Should().Throw<ArgumentException>().WithMessage("*no máximo 100 guias*101*");
    }

    [Fact]
    public void Lote_com_dois_tipos_de_guia_e_recusado_antes_do_envio()
    {
        var spSadt = new XElement(Ans + "guiaSP-SADT",
            new XElement(Ans + "cabecalhoGuia", new XElement(Ans + "registroANS", "422631"), new XElement(Ans + "numeroGuiaPrestador", "C9")));

        var misturado = () => TissLoteGuiasWs.MontarPedido(Pedido([TissWsDeTeste.GuiaConsulta(), spSadt]));

        misturado.Should().Throw<ArgumentException>().WithMessage("*um só tipo de guia*");
    }

    [Fact]
    public void Elemento_que_nao_e_guia_do_padrao_numero_repetido_e_guia_sem_numero_sao_recusados()
    {
        var estranho = () => TissLoteGuiasWs.MontarPedido(Pedido([new XElement(Ans + "guiaQualquer")]));
        var foraDoNamespace = () => TissLoteGuiasWs.MontarPedido(Pedido([new XElement("guiaConsulta")]));
        var repetido = () => TissLoteGuiasWs.MontarPedido(Pedido([TissWsDeTeste.GuiaConsulta(7), TissWsDeTeste.GuiaConsulta(7)]));
        var semNumero = TissWsDeTeste.GuiaConsulta();
        semNumero.Descendants(Ans + "numeroGuiaPrestador").Single().Remove();
        var guiaSemNumero = () => TissLoteGuiasWs.MontarPedido(Pedido([semNumero]));

        estranho.Should().Throw<ArgumentException>().WithMessage("*guias do padrão TISS*");
        foraDoNamespace.Should().Throw<ArgumentException>().WithMessage("*guias do padrão TISS*");
        repetido.Should().Throw<ArgumentException>().WithMessage("*repetido*");
        guiaSemNumero.Should().Throw<ArgumentException>().WithMessage("*numeroGuiaPrestador*");
    }

    [Fact]
    public void Numero_do_lote_maior_que_o_campo_e_senha_em_claro_sao_recusados_antes_do_envio()
    {
        var loteLongo = () => TissLoteGuiasWs.MontarPedido(Pedido() with { NumeroLote = new string('1', 13) });
        var senhaEmClaro = () => TissLoteGuiasWs.MontarPedido(Pedido() with { Remetente = TissWsDeTeste.Remetente() with { SenhaMd5 = "segredo" } });

        loteLongo.Should().Throw<ArgumentException>().WithMessage("*número do lote*");
        senhaEmClaro.Should().Throw<ArgumentException>().WithMessage("*MD5*");
    }

    // ---------- a guia que entra no lote ----------

    [Fact]
    public void Guia_conferida_sai_com_o_numero_que_quem_chama_escolheu_e_nao_com_o_da_baixa()
    {
        var codigo = TissWsDeTeste.CodigoConferido(55);
        codigo.NumeroGuiaReal = "GUIA-DA-BAIXA";

        var guia = TissExportService.GuiaConsultaConferida(codigo, NumeroGuiaPrestadorTiss.Gerar(codigo.Id));

        TissLoteGuiasWs.NumeroGuiaPrestadorDa(guia).Should().Be("C55");
    }

    [Fact]
    public void Guia_sem_conferencia_e_numero_fora_do_campo_sao_recusados()
    {
        var semConferencia = () => TissExportService.GuiaConsultaConferida(new CodigoFaturamento { Id = 1 }, "C1");
        var numeroLongo = () => TissExportService.GuiaConsultaConferida(TissWsDeTeste.CodigoConferido(), new string('9', 21));
        var numeroVazio = () => TissExportService.GuiaConsultaConferida(TissWsDeTeste.CodigoConferido(), " ");

        semConferencia.Should().Throw<ArgumentException>().WithMessage("*conferida na baixa*");
        numeroLongo.Should().Throw<ArgumentException>().WithMessage("*até 20 caracteres*");
        numeroVazio.Should().Throw<ArgumentException>().WithMessage("*obrigatório*");
    }

    // ---------- leitura da resposta ----------

    private static string Protocolo(string guias, string glosaDoProtocolo = "") => TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO", $"""
        <ans:recebimentoLote><ans:protocoloRecebimento>
          <ans:registroANS>422631</ans:registroANS>
          <ans:dadosPrestador><ans:codigoPrestadorNaOperadora>{TissWsDeTeste.CodigoPrestador}</ans:codigoPrestadorNaOperadora></ans:dadosPrestador>
          <ans:numeroLote>42</ans:numeroLote>
          <ans:dataEnvioLote>2026-11-11</ans:dataEnvioLote>
          <ans:detalheProtocolo>
            <ans:numeroProtocolo>987654321</ans:numeroProtocolo>
            <ans:valorTotalProtocolo>278.00</ans:valorTotalProtocolo>
            {glosaDoProtocolo}
            <ans:dadosGuiasProtocolo>{guias}</ans:dadosGuiasProtocolo>
          </ans:detalheProtocolo>
        </ans:protocoloRecebimento></ans:recebimentoLote>
        """);

    private static string Guia(string numeroPrestador, string? numeroOperadora = null, string extras = "")
        => $"<ans:dadosGuias>{TissWsDeTeste.GuiaDevolvida(numeroPrestador, numeroOperadora, extras)}</ans:dadosGuias>";

    private static string Recusa(string codigo, string descricao) => TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO",
        $"<ans:recebimentoLote><ans:mensagemErro><ans:codigoGlosa>{codigo}</ans:codigoGlosa><ans:descricaoGlosa>{descricao}</ans:descricaoGlosa></ans:mensagemErro></ans:recebimentoLote>");

    [Fact]
    public void Protocolo_com_o_numero_da_guia_na_operadora_e_recebido_e_casa_pelo_numero_do_prestador()
    {
        var resposta = Protocolo(Guia("C1234", "OP-777001") + Guia("C1235", "OP-777002"));

        var lida = TissLoteGuiasWs.LerResposta(resposta);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty("a resposta do teste é a que o XSD define");
        lida.Recebido.Should().BeTrue();
        lida.Protocolo.Should().Be("987654321");
        lida.NumeroLote.Should().Be("42");
        lida.DataEnvio.Should().Be(new DateOnly(2026, 11, 11));
        lida.ValorTotal.Should().Be(278.00m);
        lida.Falha.Should().BeNull();
        lida.GlosasDoLote.Should().BeEmpty();
        lida.Guias.Select(g => (g.NumeroGuiaPrestador, g.NumeroGuiaOperadora))
            .Should().Equal(("C1234", "OP-777001"), ("C1235", "OP-777002"));
        NumeroGuiaPrestadorTiss.TentarLer(lida.Guias[0].NumeroGuiaPrestador, out var id, out _).Should().BeTrue();
        id.Should().Be(1234);
        lida.Resumo.Should().Be("Lote recebido: protocolo 987654321, 2 guia(s).");
    }

    [Fact]
    public void Protocolo_sem_o_numero_da_guia_na_operadora_e_recebido_com_o_numero_nulo()
    {
        var resposta = Protocolo(Guia("C1234"));

        var lida = TissLoteGuiasWs.LerResposta(resposta);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty();
        lida.Recebido.Should().BeTrue();
        lida.Guias.Should().ContainSingle();
        lida.Guias[0].NumeroGuiaPrestador.Should().Be("C1234");
        lida.Guias[0].NumeroGuiaOperadora.Should().BeNull();
        lida.Guias[0].Glosada.Should().BeFalse();
    }

    [Fact]
    public void Lote_glosado_no_recebimento_continua_recebido_e_traz_a_glosa_do_lote()
    {
        var resposta = Protocolo(Guia("C1234", "OP-777001"), """
            <ans:glosaProtocolo>
              <ans:motivosGlosa><ans:motivoGlosa><ans:codigoGlosa>1705</ans:codigoGlosa><ans:descricaoGlosa>VALOR APRESENTADO A MAIOR</ans:descricaoGlosa></ans:motivoGlosa></ans:motivosGlosa>
              <ans:vlGlosaProtocolo>139.00</ans:vlGlosaProtocolo>
            </ans:glosaProtocolo>
            """);

        var lida = TissLoteGuiasWs.LerResposta(resposta);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty();
        lida.Recebido.Should().BeTrue();
        lida.GlosasDoLote.Should().Equal(("1705", "VALOR APRESENTADO A MAIOR"));
        lida.ValorGlosado.Should().Be(139.00m);
        lida.Resumo.Should().Be("Lote recebido: protocolo 987654321, 1 guia(s); glosa do lote: 1705 - VALOR APRESENTADO A MAIOR.");
    }

    [Fact]
    public void Guia_glosada_traz_as_glosas_da_guia_e_as_dos_procedimentos_separadas()
    {
        var glosada = Guia("C1234", "OP-777001", """
            <ans:vlInformadoGuia><ans:valorProcessado>139.00</ans:valorProcessado><ans:valorGlosa>139.00</ans:valorGlosa><ans:valorLiberado>0.00</ans:valorLiberado></ans:vlInformadoGuia>
            <ans:glosaGuia><ans:motivoGlosa><ans:codigoGlosa>1308</ans:codigoGlosa><ans:descricaoGlosa>GUIA JA APRESENTADA</ans:descricaoGlosa></ans:motivoGlosa></ans:glosaGuia>
            <ans:procedimentosRealizados><ans:procedimentoRealizado>
              <ans:sequencialItem>1</ans:sequencialItem>
              <ans:dataExecucao>2026-11-10</ans:dataExecucao>
              <ans:procedimento><ans:codigoTabela>22</ans:codigoTabela><ans:codigoProcedimento>10101012</ans:codigoProcedimento><ans:descricaoProcedimento>CONSULTA</ans:descricaoProcedimento></ans:procedimento>
              <ans:quantidadeExecutada>1</ans:quantidadeExecutada>
              <ans:valorUnitario>139.00</ans:valorUnitario>
              <ans:valorTotal>139.00</ans:valorTotal>
              <ans:glosasProcedimento><ans:motivoGlosa><ans:codigoGlosa>1801</ans:codigoGlosa></ans:motivoGlosa><ans:valorGlosaProcedimento>139.00</ans:valorGlosaProcedimento></ans:glosasProcedimento>
            </ans:procedimentoRealizado></ans:procedimentosRealizados>
            """);
        var resposta = Protocolo(glosada + Guia("C1235", "OP-777002"));

        var lida = TissLoteGuiasWs.LerResposta(resposta);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty();
        lida.Recebido.Should().BeTrue();
        var guia = lida.Guias[0];
        guia.Glosada.Should().BeTrue();
        guia.Glosas.Should().Equal(("1308", "GUIA JA APRESENTADA"));
        guia.GlosasDeProcedimento.Should().Equal(("1801", null));
        guia.ValorProcessado.Should().Be(139.00m);
        guia.ValorGlosa.Should().Be(139.00m);
        guia.ValorLiberado.Should().Be(0m);
        lida.Guias[1].Glosada.Should().BeFalse();
        lida.Resumo.Should().Be("Lote recebido: protocolo 987654321, 2 guia(s), 1 com glosa.");
    }

    [Fact]
    public void Mensagem_de_erro_no_lugar_do_protocolo_e_lote_recusado_com_o_motivo()
    {
        var resposta = Recusa("1308", "GUIA JA APRESENTADA");

        var lida = TissLoteGuiasWs.LerResposta(resposta);

        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty();
        lida.Recebido.Should().BeFalse();
        lida.Protocolo.Should().BeNull();
        lida.GlosasDoLote.Should().Equal(("1308", "GUIA JA APRESENTADA"));
        lida.Falha.Should().BeNull();
        lida.Resumo.Should().Be("A operadora recusou o lote: 1308 - GUIA JA APRESENTADA");
    }

    [Theory]
    [InlineData("LoginInvalido")]
    [InlineData("SchemaInvalido")]
    [InlineData("VersaoInvalida")]
    [InlineData("HashInvalido")]
    public void Falha_do_padrao_TISS_e_nao_se_sabe_e_nunca_lote_recusado(string falha)
    {
        var lida = TissLoteGuiasWs.LerResposta(TissWsDeTeste.Falha(falha));

        lida.Recebido.Should().BeNull();
        lida.Falha.Should().Be(falha);
        lida.Resumo.Should().Be($"Não se sabe se o lote foi recebido: {falha}. Confira com a operadora antes de reenviar.");
    }

    [Theory]
    [InlineData("<html><body>Servico indisponivel</body></html", "não é XML")]
    [InlineData("", "resposta vazia")]
    [InlineData("   ", "resposta vazia")]
    [InlineData("<a/>", "sem <recebimentoLote>")]
    [InlineData("<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body><soapenv:Fault><faultstring>Internal Error</faultstring></soapenv:Fault></soapenv:Body></soapenv:Envelope>", "Internal Error")]
    public void Resposta_inesperada_nunca_lanca_e_nunca_vira_recusado(string corpo, string trecho)
    {
        var lida = TissLoteGuiasWs.LerResposta(corpo);

        lida.Recebido.Should().BeNull();
        lida.Falha.Should().Contain(trecho);
    }

    [Fact]
    public void Recebimento_sem_protocolo_e_sem_erro_e_protocolo_sem_numero_sao_nao_se_sabe()
    {
        var oco = TissLoteGuiasWs.LerResposta(TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO", "<ans:recebimentoLote/>"));
        var semNumero = TissLoteGuiasWs.LerResposta(TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO",
            "<ans:recebimentoLote><ans:protocoloRecebimento><ans:detalheProtocolo/></ans:protocoloRecebimento></ans:recebimentoLote>"));

        oco.Recebido.Should().BeNull();
        oco.Falha.Should().Be("resposta sem protocolo e sem mensagem de erro");
        semNumero.Recebido.Should().BeNull();
        semNumero.Falha.Should().Be("protocolo de recebimento sem número");
    }

    [Fact]
    public void Resposta_bruta_junta_o_HTTP_a_falha_e_a_falta_de_corpo_e_nao_se_sabe()
    {
        var erro500 = TissLoteGuiasWs.LerResposta(new RespostaBrutaTissWs(TissWsDeTeste.Falha("LoginInvalido"), 500, null));
        var semRede = TissLoteGuiasWs.LerResposta(new RespostaBrutaTissWs(null, null, "sem resposta da operadora (sem rota)"));
        var ok = TissLoteGuiasWs.LerResposta(new RespostaBrutaTissWs(Protocolo(Guia("C1234")), 200, null));
        // Recusa da operadora com HTTP 500 continua sendo recusa: o status não entra onde não há falha.
        var recusaCom500 = TissLoteGuiasWs.LerResposta(new RespostaBrutaTissWs(Recusa("1308", "GUIA JA APRESENTADA"), 500, null));

        erro500.Recebido.Should().BeNull();
        erro500.Falha.Should().Be("LoginInvalido (HTTP 500)");
        semRede.Recebido.Should().BeNull();
        semRede.Falha.Should().Be("sem resposta da operadora (sem rota)");
        ok.Recebido.Should().BeTrue();
        ok.Falha.Should().BeNull();
        recusaCom500.Recebido.Should().BeFalse();
        recusaCom500.Falha.Should().BeNull();
    }
}
