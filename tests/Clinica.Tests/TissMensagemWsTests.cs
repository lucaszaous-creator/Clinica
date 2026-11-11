using System.Xml.Linq;
using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as mensagens do web service TISS têm em comum: o cabeçalho com login na ordem do XSD,
/// as recusas antes do envio, a máscara da senha e a leitura das falhas que impedem qualquer
/// leitura — as mesmas para lote, situação do protocolo e cancelamento.
/// </summary>
public class TissMensagemWsTests
{
    private static readonly XNamespace Ans = TissWsDeTeste.Ans;

    [Fact]
    public void Cabecalho_segue_a_ordem_do_XSD_e_leva_o_login_com_a_senha_em_MD5()
    {
        var remetente = TissWsDeTeste.Remetente();

        var cabecalho = TissMensagemWs.Cabecalho("ENVIO_LOTE_GUIAS", remetente, TissWsDeTeste.Sequencial, TissWsDeTeste.Momento);

        cabecalho.Elements().Select(e => e.Name.LocalName)
            .Should().Equal("identificacaoTransacao", "origem", "destino", "Padrao", "loginSenhaPrestador");
        cabecalho.Element(Ans + "identificacaoTransacao")!.Elements().Select(e => e.Value)
            .Should().Equal("ENVIO_LOTE_GUIAS", "261111101500", "2026-11-11", "10:15:00");
        cabecalho.Element(Ans + "Padrao")!.Value.Should().Be("4.01.00");
        cabecalho.Descendants(Ans + "senhaPrestador").Single().Value.Should().Be(remetente.SenhaMd5).And.HaveLength(32);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("42263A")]
    public void Registro_ANS_fora_do_padrao_e_recusado_antes_do_envio(string registro)
    {
        var acao = () => TissMensagemWs.Cabecalho("CANCELA_GUIA", TissWsDeTeste.Remetente() with { RegistroAnsOperadora = registro },
            TissWsDeTeste.Sequencial, TissWsDeTeste.Momento);

        acao.Should().Throw<ArgumentException>().WithMessage("*registro ANS*");
    }

    [Fact]
    public void Login_codigo_e_sequencial_fora_do_campo_e_senha_em_claro_sao_recusados_antes_do_envio()
    {
        var bom = TissWsDeTeste.Remetente();
        var loginLongo = () => TissMensagemWs.Cabecalho("X", bom with { Login = new string('a', 21) }, "1", TissWsDeTeste.Momento);
        var codigoLongo = () => TissMensagemWs.Cabecalho("X", bom with { CodigoPrestadorNaOperadora = new string('1', 15) }, "1", TissWsDeTeste.Momento);
        var sequencialLongo = () => TissMensagemWs.Cabecalho("X", bom, new string('1', 13), TissWsDeTeste.Momento);
        var senhaEmClaro = () => TissMensagemWs.Cabecalho("X", bom with { SenhaMd5 = "segredo" }, "1", TissWsDeTeste.Momento);

        loginLongo.Should().Throw<ArgumentException>().WithMessage("*login do prestador*");
        codigoLongo.Should().Throw<ArgumentException>().WithMessage("*código do prestador*");
        sequencialLongo.Should().Throw<ArgumentException>().WithMessage("*sequencial*");
        senhaEmClaro.Should().Throw<ArgumentException>().WithMessage("*MD5*");
    }

    [Fact]
    public void Fechar_poe_o_hash_por_ultimo_e_ele_nao_entra_na_propria_conta()
    {
        var mensagem = TissMensagemWs.Fechar("qualquerWS", new XElement(Ans + "cabecalho", new XElement(Ans + "a", "1")),
            new XElement(Ans + "corpo", new XElement(Ans + "b", "2")));

        mensagem.Elements().Last().Name.Should().Be(Ans + "hash");
        // MD5 de "12": o conteúdo das tags, na ordem, sem o hash.
        mensagem.Element(Ans + "hash")!.Value.Should().Be("c20ad4d76fe97759aa27a0c99bff6710");
    }

    [Fact]
    public void Envelope_sai_sem_espaco_entre_tags_para_o_hash_conferir_do_outro_lado()
    {
        var envelope = TissMensagemWs.Envelope(new XElement(Ans + "x", new XElement(Ans + "y", "1")));

        envelope.Should().StartWith("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
        envelope.Should().NotContain("\n").And.NotContain("> <");
    }

    [Fact]
    public void Para_exibir_mascara_toda_senha_do_envelope()
    {
        var md5 = TissElegibilidadeWs.SenhaEmMd5("segredo");
        var envelope = TissMensagemWs.Envelope(new XElement(Ans + "x",
            TissMensagemWs.Cabecalho("X", TissWsDeTeste.Remetente(), "1", TissWsDeTeste.Momento)));

        var exibido = TissMensagemWs.ParaExibir(envelope);

        envelope.Should().Contain(md5);
        exibido.Should().NotContain(md5).And.Contain(new string('*', 32)).And.Contain("clinica.teste");
    }

    [Theory]
    [InlineData(null, "resposta vazia")]
    [InlineData("", "resposta vazia")]
    [InlineData("<html><body>fora do ar</body></html", "resposta que não é XML")]
    public void Abrir_devolve_a_falha_do_que_nao_da_para_ler_sem_lancar(string? xml, string trecho)
    {
        var falha = TissMensagemWs.Abrir(xml, out var doc);

        falha.Should().Contain(trecho);
        doc.Should().BeNull();
    }

    [Theory]
    [InlineData("DestinatarioInvalido")]
    [InlineData("RemetenteInvalido")]
    [InlineData("LoginInvalido")]
    [InlineData("VersaoInvalida")]
    [InlineData("HashInvalido")]
    [InlineData("SchemaInvalido")]
    [InlineData("ErroInesperadoServidor")]
    public void Abrir_devolve_cada_falha_do_padrao_pelo_nome_do_XSD(string tissFault)
        => TissMensagemWs.Abrir(TissWsDeTeste.Falha(tissFault), out _).Should().Be(tissFault);

    [Fact]
    public void Abrir_devolve_o_texto_do_erro_SOAP_e_nulo_quando_ha_o_que_ler()
    {
        const string soap = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body><soapenv:Fault><faultstring>Internal Error</faultstring></soapenv:Fault></soapenv:Body></soapenv:Envelope>";
        const string semTexto = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body><soapenv:Fault/></soapenv:Body></soapenv:Envelope>";

        TissMensagemWs.Abrir(soap, out _).Should().Be("Internal Error");
        TissMensagemWs.Abrir(semTexto, out _).Should().Be("erro SOAP sem descrição");
        TissMensagemWs.Abrir("<a/>", out var doc).Should().BeNull();
        doc.Should().NotBeNull();
    }

    [Fact]
    public void Falha_com_HTTP_so_junta_o_status_quando_ha_falha_e_o_status_e_de_erro()
    {
        TissMensagemWs.FalhaComHttp("LoginInvalido", 500).Should().Be("LoginInvalido (HTTP 500)");
        TissMensagemWs.FalhaComHttp("LoginInvalido", 200).Should().Be("LoginInvalido");
        TissMensagemWs.FalhaComHttp("sem resposta", null).Should().Be("sem resposta");
        TissMensagemWs.FalhaComHttp(null, 500).Should().BeNull();
    }

    [Fact]
    public void Valor_e_data_fora_do_formato_do_padrao_viram_nulo_e_nao_excecao()
    {
        TissMensagemWs.LerValor(new XElement("v", "139.00")).Should().Be(139.00m);
        TissMensagemWs.LerValor(new XElement("v", "139,00")).Should().BeNull();
        TissMensagemWs.LerValor(null).Should().BeNull();
        TissMensagemWs.LerData(new XElement("d", " 2026-11-11 ")).Should().Be(new DateOnly(2026, 11, 11));
        TissMensagemWs.LerData(new XElement("d", "11/11/2026")).Should().BeNull();
    }
}
