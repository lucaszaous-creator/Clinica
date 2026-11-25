using System.Xml.Linq;
using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// As mensagens da AUTORIZAÇÃO (nov/2026, Amil, TISS 4.03.00): o pedido e a pergunta da situação saem
/// válidos no XSD oficial 4.03.00, e a leitura das respostas distingue autorizado, em análise, negado,
/// recusado e "não se sabe" — que nunca tem a cara de "negado". Dados fictícios.
/// </summary>
public sealed class TissAutorizacaoWsTests
{
    private static readonly XNamespace Ans = TissWsDeTeste.Ans;

    private static RemetenteTissWs Remetente() => TissWsDeTeste.Remetente() with { RegistroAnsOperadora = "326305", VersaoTiss = "4.03.00" };

    public static PedidoAutorizacaoTiss Pedido() => new()
    {
        Remetente = Remetente(), Sequencial = "261117100001", Momento = new DateTime(2026, 11, 17, 10, 0, 0),
        NumeroGuiaPrestador = "A41", NumeroCarteira = TissWsDeTeste.Carteira, NomeContratado = "Clínica de Teste Ltda", Cnes = "9999999",
        NomeProfissional = "Dra. Ana de Teste", ConselhoProfissional = "06", NumeroConselho = "123456", UfConselho = "33", Cbos = "225105",
        DataSolicitacao = new DateOnly(2026, 11, 17), CodigoProcedimento = "10101012", DescricaoProcedimento = "Consulta em consultório"
    };

    [Fact]
    public void O_pedido_de_autorizacao_sai_valido_no_XSD_4_03_00_com_o_CBO_e_o_procedimento()
    {
        var mensagem = TissAutorizacaoWs.MontarPedido(Pedido());
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.03.00").Should().BeEmpty();
        mensagem.Descendants(Ans + "tipoTransacao").Single().Value.Should().Be("SOLICITACAO_PROCEDIMENTOS");
        mensagem.Descendants(Ans + "Padrao").Single().Value.Should().Be("4.03.00");
        mensagem.Descendants(Ans + "CBOS").Single().Value.Should().Be("225105");
        mensagem.Descendants(Ans + "codigoProcedimento").Single().Value.Should().Be("10101012");
        mensagem.Descendants(Ans + "numeroGuiaPrestador").Single().Value.Should().Be("A41");
        var semCbo = () => TissAutorizacaoWs.MontarPedido(Pedido() with { Cbos = "" });
        semCbo.Should().Throw<ArgumentException>().WithMessage("*CBO*");
    }

    [Fact]
    public void A_pergunta_da_situacao_sai_valida_no_XSD_4_03_00()
    {
        var mensagem = TissAutorizacaoWs.MontarPedidoStatus(new PedidoStatusAutorizacaoTiss
        {
            Remetente = Remetente(), Sequencial = "261117100002", Momento = new DateTime(2026, 11, 17, 10, 30, 0),
            NumeroGuiaPrestador = "A41", NumeroCarteira = TissWsDeTeste.Carteira
        });
        TissWsDeTeste.ValidarNoXsd(mensagem, "4.03.00").Should().BeEmpty();
        mensagem.Descendants(Ans + "tipoTransacao").Single().Value.Should().Be("SOLICITA_STATUS_AUTORIZACAO");
    }

    [Theory]
    [InlineData("1", true, false, false)]
    [InlineData("7", true, false, false)]
    [InlineData("2", false, true, false)]
    [InlineData("5", false, true, false)]
    [InlineData("3", false, false, true)]
    public void A_resposta_e_lida_pelo_status(string status, bool autorizada, bool emAnalise, bool negada)
    {
        var lida = TissAutorizacaoWs.LerResposta(Resposta(status));
        lida.Autorizada.Should().Be(autorizada);
        lida.EmAnalise.Should().Be(emAnalise);
        lida.Negada.Should().Be(negada);
        if (autorizada)
        {
            lida.Senha.Should().Be("SENHA0001");
            lida.DataAutorizacao.Should().Be(new DateOnly(2026, 11, 17));
            lida.ValidadeSenha.Should().Be(new DateOnly(2027, 1, 16));
            lida.NumeroGuiaOperadora.Should().Be("AMX123");
        }
        if (negada) lida.Motivos.Select(m => m.Codigo).Should().Contain("1801");
    }

    [Fact]
    public void A_resposta_de_teste_e_ela_mesma_valida_no_XSD_e_o_erro_da_mensagem_e_recusa_nao_falha()
    {
        TissWsDeTeste.ValidarNoXsd(XDocument.Parse(Resposta("1")).Root!.Element(TissWsDeTeste.Soap + "Body")!.Elements().Single(), "4.03.00")
            .Should().BeEmpty("a resposta escrita no teste segue o XSD, senão ela não prova a leitura");
        var recusa = TissAutorizacaoWs.LerResposta(TissWsDeTeste.Resposta("autorizacaoProcedimentoWS", "RESPOSTA_SOLICITACAO",
            "<ans:autorizacaoProcedimento><ans:mensagemErro><ans:codigoGlosa>1011</ans:codigoGlosa><ans:descricaoGlosa>IDENTIFICACAO DO BENEFICIARIO NAO CONSISTENTE</ans:descricaoGlosa></ans:mensagemErro></ans:autorizacaoProcedimento>"));
        recusa.Negada.Should().BeTrue();
        recusa.Recusada.Should().BeTrue();
        recusa.Resumo.Should().Contain("1011");

        var naoSei = TissAutorizacaoWs.LerResposta(new RespostaBrutaTissWs(null, null, "sem resposta da operadora (tempo esgotado)"));
        naoSei.Negada.Should().BeFalse("falha de rede é 'não sei', nunca 'negado'");
        naoSei.Autorizada.Should().BeFalse();
        naoSei.Resumo.Should().StartWith("Não se sabe");
    }

    /// <summary>Uma resposta da operadora com o status dado (fictícia, no formato do XSD 4.03.00).</summary>
    public static string Resposta(string status) => TissWsDeTeste.Resposta("autorizacaoProcedimentoWS", "RESPOSTA_SOLICITACAO", $"""
        <ans:autorizacaoProcedimento><ans:autorizacaoServico>
          <ans:dadosAutorizacao>
            <ans:numeroGuiaPrestador>A41</ans:numeroGuiaPrestador>
            <ans:numeroGuiaOperadora>AMX123</ans:numeroGuiaOperadora>
            <ans:dataAutorizacao>2026-11-17</ans:dataAutorizacao>
            <ans:senha>SENHA0001</ans:senha>
            <ans:dataValidadeSenha>2027-01-16</ans:dataValidadeSenha>
          </ans:dadosAutorizacao>
          <ans:tipoEtapaAutorizacao>1</ans:tipoEtapaAutorizacao>
          <ans:dadosBeneficiario><ans:numeroCarteira>{TissWsDeTeste.Carteira}</ans:numeroCarteira><ans:atendimentoRN>N</ans:atendimentoRN></ans:dadosBeneficiario>
          <ans:nomeBeneficiario>PACIENTE DE TESTE</ans:nomeBeneficiario>
          <ans:statusSolicitacao>{status}</ans:statusSolicitacao>
          {(status == "3" ? "<ans:motivosNegativa><ans:codigoGlosa>1801</ans:codigoGlosa></ans:motivosNegativa>" : "")}
        </ans:autorizacaoServico></ans:autorizacaoProcedimento>
        """);
}
