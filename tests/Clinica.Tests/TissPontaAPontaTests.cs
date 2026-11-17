using System.Net;
using System.Text;
using System.Xml.Linq;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Tiss.Conector;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O caminho inteiro do envio pelo web service TISS, com as três frentes ligadas (nov/2026):
/// conferir a guia sem baixar → montar o lote → o conector envia → a operadora recebe uma e
/// glosa outra → a baixa em lote alcança só a recebida → a situação do protocolo atualiza →
/// o cancelamento solta a guia.
///
/// A REDE É DE MENTIRA: nenhuma chamada sai da máquina. As respostas da "operadora" são escritas
/// como o XSD 4.01.00 as define e conferidas nele; o XML que o conector manda também é.
/// Login, senha, carteira, CNPJ e nomes são fictícios.
/// </summary>
public sealed class TissPontaAPontaTests : IDisposable
{
    private const string P = TissBancoDeTeste.Petrobras, Op = TissBancoDeTeste.Operador;
    private const string Senha = "senha-ficticia-de-teste";
    private static readonly string Md5 = TissElegibilidadeWs.SenhaEmMd5(Senha);
    private const string Protocolo = "987654321";

    private readonly TissBancoDeTeste _b = new();
    private readonly Operadora _rede = new();

    public void Dispose() => _b.Dispose();

    // ---------- montagem ----------

    private async Task ProntoAsync(bool envioDoConvenio = true)
    {
        await _b.PrestadorAsync();
        await _b.Credenciais().SalvarAsync(P, "LOGINTESTE", Senha, Op);
        if (envioDoConvenio) await _b.Credenciais().DefinirEnvioAsync(P, true, Op);
    }

    private ExecutorDeTransacoesTiss Conector(bool envioNoServidor = true)
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        var credenciais = _b.Credenciais(repo);
        var parametros = new ParametrosService(repo, _b.Protecao);
        IProcessadorTransacaoTiss[] processadores =
        [
            new ProcessadorDeElegibilidade(repo, parametros, credenciais, () => _b.Agora),
            new ProcessadorDeEnvioDeGuia(repo, parametros, credenciais, () => _b.Agora),
            new ProcessadorDeStatusDeProtocolo(repo, parametros, credenciais, () => _b.Agora),
            new ProcessadorDeCancelamentoDeGuia(repo, parametros, credenciais, () => _b.Agora)
        ];
        return new ExecutorDeTransacoesTiss(_b.Fila(repo), credenciais, processadores, new TissWebserviceCliente(new HttpClient(_rede)),
            new OpcoesDoConector { EnvioHabilitado = envioNoServidor, Consumidor = "conector-de-teste" },
            NullLogger<ExecutorDeTransacoesTiss>.Instance);
    }

    private FaturamentoService Faturamento(ClinicaRepositorio? repo = null) => new(repo ?? new ClinicaRepositorio(_b.NovoContexto()));

    private BaixaDasGuiasEnviadasService Baixa()
    {
        var repo = new ClinicaRepositorio(_b.NovoContexto());
        return new BaixaDasGuiasEnviadasService(repo, _b.Fila(repo), new FaturamentoService(repo));
    }

    private FilaTissService Fila() => _b.Fila(new ClinicaRepositorio(_b.NovoContexto()));

    private Task<TransacaoTiss> GravadaAsync(int id)
        => _b.NovoContexto().TransacoesTiss.AsNoTracking().Include(t => t.Guias).SingleAsync(t => t.Id == id);

    private Task<CodigoFaturamento> GuiaGravadaAsync(int id)
        => _b.NovoContexto().Codigos.AsNoTracking().Include(c => c.GuiaAns).SingleAsync(c => c.Id == id);

    /// <summary>Duas guias em aberto, conferidas pelo caminho de verdade (sem baixa).</summary>
    private async Task<List<int>> ConferidasAsync(int quantas = 2)
    {
        var ids = new List<int>();
        for (var i = 0; i < quantas; i++)
        {
            var (_, guia) = await _b.GuiaAsync(conferida: false);
            await Faturamento().ConferirGuiaAnsAsync(guia.Id, _b.DadosDaGuia(), Op);
            ids.Add(guia.Id);
        }
        return ids;
    }

    private async Task<TransacaoTiss> LoteRecebidoAsync(IReadOnlyList<int> ids, string resposta)
    {
        var pedido = await Fila().EnfileirarEnvioAsync(P, ids, Op);
        _rede.Responder(HttpStatusCode.OK, resposta);
        await Conector().ProcessarProximaAsync();
        return await GravadaAsync(pedido.Id);
    }

    // ---------- as respostas da operadora, como o XSD as define ----------

    private static string ProtocoloDeRecebimento(string guias) => Conferida(TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO", $"""
        <ans:recebimentoLote><ans:protocoloRecebimento>
          <ans:registroANS>422631</ans:registroANS>
          <ans:dadosPrestador><ans:codigoPrestadorNaOperadora>00000000000191</ans:codigoPrestadorNaOperadora></ans:dadosPrestador>
          <ans:numeroLote>900000000001</ans:numeroLote>
          <ans:dataEnvioLote>2026-11-12</ans:dataEnvioLote>
          <ans:detalheProtocolo>
            <ans:numeroProtocolo>{Protocolo}</ans:numeroProtocolo>
            <ans:valorTotalProtocolo>278.00</ans:valorTotalProtocolo>
            <ans:dadosGuiasProtocolo>{guias}</ans:dadosGuiasProtocolo>
          </ans:detalheProtocolo>
        </ans:protocoloRecebimento></ans:recebimentoLote>
        """));

    private static string GuiaDoProtocolo(int codigoId, string? numeroOperadora = null, bool glosada = false, int vez = 1)
        => $"<ans:dadosGuias>{TissWsDeTeste.GuiaDevolvida(NumeroGuiaPrestadorTiss.Gerar(codigoId, vez), numeroOperadora, glosada ? Glosa : "")}</ans:dadosGuias>";

    private const string Glosa = """
        <ans:vlInformadoGuia><ans:valorProcessado>139.00</ans:valorProcessado><ans:valorGlosa>139.00</ans:valorGlosa><ans:valorLiberado>0.00</ans:valorLiberado></ans:vlInformadoGuia>
        <ans:glosaGuia><ans:motivoGlosa><ans:codigoGlosa>1001</ans:codigoGlosa><ans:descricaoGlosa>NUMERO DA CARTEIRA INVALIDO</ans:descricaoGlosa></ans:motivoGlosa></ans:glosaGuia>
        """;

    private static string LoteRecusado(string codigo, string descricao) => Conferida(TissWsDeTeste.Resposta("protocoloRecebimentoWS", "PROTOCOLO_RECEBIMENTO",
        $"<ans:recebimentoLote><ans:mensagemErro><ans:codigoGlosa>{codigo}</ans:codigoGlosa><ans:descricaoGlosa>{descricao}</ans:descricaoGlosa></ans:mensagemErro></ans:recebimentoLote>"));

    private static string SituacaoDoProtocolo(string status, string guias) => Conferida(TissWsDeTeste.Resposta("situacaoProtocoloWS", "SITUACAO_PROTOCOLO", $"""
        <ans:situacaoProtocolo><ans:situacaoDoProtocolo>
          <ans:identificacaoOperadora>422631</ans:identificacaoOperadora>
          <ans:dadosPrestador><ans:codigoPrestadorNaOperadora>00000000000191</ans:codigoPrestadorNaOperadora></ans:dadosPrestador>
          <ans:lote><ans:detalheLote>
            <ans:statusProtocolo>{status}</ans:statusProtocolo>
            <ans:numeroProtocolo>{Protocolo}</ans:numeroProtocolo>
            <ans:numeroLote>900000000001</ans:numeroLote>
            <ans:dataEnvioLote>2026-11-12</ans:dataEnvioLote>
            <ans:valorTotalLote><ans:valorProcessado>278.00</ans:valorProcessado><ans:valorGlosa>0.00</ans:valorGlosa><ans:valorLiberado>278.00</ans:valorLiberado></ans:valorTotalLote>
            <ans:guiasTISS>{guias}</ans:guiasTISS>
          </ans:detalheLote></ans:lote>
        </ans:situacaoDoProtocolo></ans:situacaoProtocolo>
        """));

    private static string GuiaDaSituacao(int codigoId, string? numeroOperadora = null, bool glosada = false)
        => $"<ans:guiasMedicas><ans:guias>{TissWsDeTeste.GuiaDevolvida(NumeroGuiaPrestadorTiss.Gerar(codigoId), numeroOperadora, glosada ? Glosa : "")}</ans:guias></ans:guiasMedicas>";

    private static string ReciboDeCancelamento(int codigoId, string status) => Conferida(TissWsDeTeste.Resposta("reciboCancelaGuiaWS", "CANCELAMENTO_GUIA_RECIBO", $"""
        <ans:reciboCancelaGuia><ans:reciboCancelaGuia>
          <ans:registroANS>422631</ans:registroANS>
          <ans:dadosPrestador><ans:codigoPrestadorNaOperadora>00000000000191</ans:codigoPrestadorNaOperadora></ans:dadosPrestador>
          <ans:retornoStatus><ans:guiasCanceladas>
            <ans:dadosGuia>
              <ans:tipoGuia>2</ans:tipoGuia>
              <ans:numeroGuiaPrestador>{NumeroGuiaPrestadorTiss.Gerar(codigoId)}</ans:numeroGuiaPrestador>
              <ans:statusCancelamento>{status}</ans:statusCancelamento>
            </ans:dadosGuia>
          </ans:guiasCanceladas></ans:retornoStatus>
        </ans:reciboCancelaGuia></ans:reciboCancelaGuia>
        """));

    /// <summary>A resposta do teste só serve de prova se for a que o XSD define.</summary>
    private static string Conferida(string resposta)
    {
        TissWsDeTeste.ValidarNoXsd(TissWsDeTeste.CorpoDa(resposta)).Should().BeEmpty("a resposta de mentira tem de ser válida no XSD 4.01.00");
        return resposta;
    }

    private static XElement MensagemEnviada(string corpo)
        => XDocument.Parse(corpo).Root!.Element(TissWsDeTeste.Soap + "Body")!.Elements().Single();

    // ==================== o caminho inteiro ====================

    [Fact]
    public async Task Conferir_enviar_lote_de_duas_receber_uma_glosar_outra_baixar_so_a_recebida_e_o_status_atualiza()
    {
        await ProntoAsync();

        // 1. Conferir SEM baixar.
        var ids = await ConferidasAsync(2);
        (await GuiaGravadaAsync(ids[0])).Should().Match<CodigoFaturamento>(c => c.GuiaAns != null && !c.Baixado && c.Status == StatusCodigo.Aberto);
        (await Fila().ProntasParaEnvioAsync(P)).Select(g => g.CodigoId).Should().Equal(ids);
        (await Fila().SelosDeEnvioAsync(ids)).Values.Should().OnlyContain(s => s.Estado == EstadoDoEnvioDaGuia.NaoEnviada);

        // 2. Montar o lote: é o pedido de quem está logado que autoriza estas guias.
        var pedido = await Fila().EnfileirarEnvioAsync(P, ids, Op);
        (await Fila().ProntasParaEnvioAsync(P)).Should().BeEmpty("as duas estão num envio");
        (await Fila().SelosDeEnvioAsync(ids)).Values.Should().OnlyContain(s => s.Estado == EstadoDoEnvioDaGuia.NaFila && s.TransacaoId == pedido.Id);

        // 3. O conector envia; a operadora recebe a primeira e glosa a segunda.
        _rede.Responder(HttpStatusCode.OK, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0], "OP777001") + GuiaDoProtocolo(ids[1], glosada: true)));
        (await Conector().ProcessarProximaAsync()).Should().BeTrue();

        var ida = _rede.Idas.Should().ContainSingle().Subject;
        ida.Endereco.Should().Be(TissWebserviceCliente.LoteGuiasSaudePetrobras, "ligar o envio não exige digitar URL");
        var mensagem = MensagemEnviada(ida.Corpo);
        mensagem.Name.LocalName.Should().Be("loteGuiasWS");
        TissWsDeTeste.ValidarNoXsd(mensagem).Should().BeEmpty("o XML que sai do conector é válido no XSD 4.01.00");
        mensagem.Descendants(TissWsDeTeste.Ans + "guiaConsulta").Should().HaveCount(2);
        mensagem.Descendants(TissWsDeTeste.Ans + "numeroGuiaPrestador").Select(e => e.Value).Should().Equal($"C{ids[0]}", $"C{ids[1]}");
        mensagem.Descendants(TissWsDeTeste.Ans + "numeroLote").Single().Value.Should().Be(DadosDoConvenio.NumeroDoLote(pedido)).And.MatchRegex("^9[0-9]{11}$");
        mensagem.Descendants(TissWsDeTeste.Ans + "senhaPrestador").Single().Value.Should().Be(Md5);

        var lote = await GravadaAsync(pedido.Id);
        lote.Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        lote.Protocolo.Should().Be(Protocolo);
        lote.Resultado.Should().Contain(Protocolo).And.Contain("1 com glosa");
        lote.XmlEnviado.Should().Contain("loteGuiasWS").And.NotContain(Md5);
        var itens = lote.Guias.ToDictionary(g => g.CodigoId!.Value);
        itens[ids[0]].Should().BeEquivalentTo(new { Situacao = SituacaoGuiaTiss.Recebida, NumeroGuiaOperadora = "OP777001", Motivo = (string?)null });
        itens[ids[1]].Situacao.Should().Be(SituacaoGuiaTiss.Glosada);
        itens[ids[1]].Motivo.Should().Be("1001 - NUMERO DA CARTEIRA INVALIDO");
        var selos = await Fila().SelosDeEnvioAsync(ids);
        selos[ids[0]].Should().BeEquivalentTo(new { Estado = EstadoDoEnvioDaGuia.Recebida, Protocolo, Texto = $"Recebida pela operadora, protocolo {Protocolo}" });
        selos[ids[1]].Texto.Should().Be("Glosada: 1001 - NUMERO DA CARTEIRA INVALIDO");
        // O conector NÃO deu baixa em nada.
        (await _b.NovoContexto().Codigos.AsNoTracking().CountAsync(c => c.DataBaixa != null)).Should().Be(0);

        // 4. A baixa em lote, pedida por quem está logado, alcança só a recebida — com o número devolvido.
        var baixa = await Baixa().BaixarRecebidasAsync(pedido.Id, new DateOnly(2026, 11, 12), "faturista.que.clicou");
        baixa.Guias.Should().ContainSingle().Which.Should().Be(new BaixaDeGuiaEnviada(ids[0], "OP777001", null));
        baixa.Resumo.Should().Be("1 guia(s) baixada(s).");
        var baixada = await GuiaGravadaAsync(ids[0]);
        baixada.Status.Should().Be(StatusCodigo.Baixado);
        baixada.NumeroGuiaReal.Should().Be("OP777001");
        baixada.DataBaixa.Should().Be(new DateOnly(2026, 11, 12));
        baixada.UsuarioBaixa.Should().Be("faturista.que.clicou", "quem dá baixa é quem clicou, nunca o conector");
        baixada.ObservacaoBaixa.Should().Contain("web service").And.Contain(Protocolo).And.Contain($"C{ids[0]}");
        baixada.GuiaAns.Should().NotBeNull("a guia conferida antes do envio continua gravada");
        (await GuiaGravadaAsync(ids[1])).Baixado.Should().BeFalse("guia glosada não recebe baixa sozinha");

        // 5. Chamar de novo não baixa de novo.
        var segunda = await Baixa().BaixarRecebidasAsync(pedido.Id, new DateOnly(2026, 11, 13), "faturista.que.clicou");
        segunda.Guias.Should().BeEmpty();
        segunda.Resumo.Should().Contain("Não há guia recebida");
        (await _b.NovoContexto().Auditoria.AsNoTracking().CountAsync(a => a.Acao == "BaixaGuia")).Should().Be(1);
        (await GuiaGravadaAsync(ids[0])).DataBaixa.Should().Be(new DateOnly(2026, 11, 12));

        // 6. A situação do protocolo atualiza as MESMAS guias: a operadora acatou a segunda.
        var status = await Fila().EnfileirarStatusDoEnvioAsync(pedido.Id, Op);
        _rede.Responder(HttpStatusCode.OK, SituacaoDoProtocolo("2", GuiaDaSituacao(ids[0], "OP777001") + GuiaDaSituacao(ids[1], "OP777002")));
        await Conector().ProcessarProximaAsync();

        var idaDoStatus = _rede.Idas[1];
        idaDoStatus.Endereco.Should().Be(TissWebserviceCliente.StatusProtocoloSaudePetrobras);
        TissWsDeTeste.ValidarNoXsd(MensagemEnviada(idaDoStatus.Corpo)).Should().BeEmpty();
        MensagemEnviada(idaDoStatus.Corpo).Descendants(TissWsDeTeste.Ans + "numeroProtocolo").Single().Value.Should().Be(Protocolo);
        (await GravadaAsync(status.Id)).Should().Match<TransacaoTiss>(t => t.Estado == EstadoTransacaoTiss.Concluida && t.Resultado!.Contains(Protocolo));
        var atualizado = (await GravadaAsync(pedido.Id)).Guias.Single(g => g.CodigoId == ids[1]);
        atualizado.Should().BeEquivalentTo(new { Situacao = SituacaoGuiaTiss.Recebida, NumeroGuiaOperadora = "OP777002", Motivo = (string?)null });

        var terceira = await Baixa().BaixarRecebidasAsync(pedido.Id, new DateOnly(2026, 11, 14), "faturista.que.clicou");
        terceira.Guias.Should().ContainSingle().Which.Should().Be(new BaixaDeGuiaEnviada(ids[1], "OP777002", null));
        (await Fila().SelosDeEnvioAsync(ids)).Values.Should().OnlyContain(s => s.Estado == EstadoDoEnvioDaGuia.Recebida);
        var recentes = await Fila().EnviosRecentesAsync(P);
        recentes.Should().ContainSingle().Which.Guias.Should().HaveCount(2).And.OnlyContain(g => g.Situacao == SituacaoGuiaTiss.Recebida);
        recentes[0].XmlEnviado.Should().BeNull();
    }

    // ==================== variações da baixa ====================

    [Fact]
    public async Task Operadora_que_nao_devolve_o_numero_dela_tem_a_baixa_com_o_numero_enviado_e_a_observacao_diz()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(1);
        var lote = await LoteRecebidoAsync(ids, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0])));
        lote.Guias.Single().NumeroGuiaOperadora.Should().BeNull();

        var baixa = await Baixa().BaixarRecebidasAsync(lote.Id, new DateOnly(2026, 11, 12), Op);

        baixa.Guias.Single().Should().Be(new BaixaDeGuiaEnviada(ids[0], $"C{ids[0]}", null));
        var guia = await GuiaGravadaAsync(ids[0]);
        guia.NumeroGuiaReal.Should().Be($"C{ids[0]}");
        guia.ObservacaoBaixa.Should().Contain(Protocolo).And.Contain("não devolveu o número dela");
    }

    [Fact]
    public async Task Numero_devolvido_fora_do_formato_do_convenio_e_falha_so_daquela_linha_e_a_guia_continua_pendente()
    {
        // A Petrobras está cadastrada como "letras e números, sem pontuação" (RegraNumeroGuia): um
        // número devolvido com hífen é recusado pela MESMA regra de qualquer baixa.
        await ProntoAsync();
        var ids = await ConferidasAsync(2);
        var lote = await LoteRecebidoAsync(ids, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0], "OP-777/001") + GuiaDoProtocolo(ids[1], "OP777002")));

        var baixa = await Baixa().BaixarRecebidasAsync(lote.Id, new DateOnly(2026, 11, 12), Op);

        baixa.Baixadas.Should().Be(1);
        var falha = baixa.Falhas.Should().ContainSingle().Subject;
        falha.CodigoId.Should().Be(ids[0]);
        falha.NumeroUsado.Should().Be("OP-777/001");
        falha.Falha.Should().Contain("sem pontuação");
        baixa.Resumo.Should().StartWith("1 guia(s) baixada(s); 1 NÃO:");
        (await GuiaGravadaAsync(ids[0])).Baixado.Should().BeFalse("a recusada continua pendente, para a baixa manual com o número certo");
        (await GuiaGravadaAsync(ids[1])).NumeroGuiaReal.Should().Be("OP777002", "a falha de uma não aborta a outra");
        // E continua na lista: resolver é dar baixa nela pela tela, com o número conferido no portal.
        (await Fila().GuiasRecebidasSemBaixaAsync(lote.Id)).Select(g => g.CodigoId).Should().Equal(ids[0]);
    }

    [Fact]
    public async Task Guia_baixada_a_mao_antes_da_baixa_em_lote_nao_e_baixada_de_novo()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(2);
        var lote = await LoteRecebidoAsync(ids, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0], "OP777001") + GuiaDoProtocolo(ids[1], "OP777002")));
        // Alguém deu baixa na primeira pela tela de baixa, com a guia já conferida.
        await Faturamento().DarBaixaAsync(ids[0], new DateOnly(2026, 11, 12), "MANUAL1", "outra.faturista", null, default, _b.DadosDaGuia());

        var baixa = await Baixa().BaixarRecebidasAsync(lote.Id, new DateOnly(2026, 11, 13), Op);

        baixa.Guias.Select(g => g.CodigoId).Should().Equal(ids[1]);
        (await GuiaGravadaAsync(ids[0])).Should().Match<CodigoFaturamento>(c => c.NumeroGuiaReal == "MANUAL1" && c.UsuarioBaixa == "outra.faturista");
    }

    [Fact]
    public async Task A_baixa_em_lote_exige_quem_a_fez_e_nao_alcanca_lote_que_falhou()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(1);
        var pedido = await Fila().EnfileirarEnvioAsync(P, ids, Op);
        _rede.Cair();
        await Conector().ProcessarProximaAsync();

        var semOperador = () => Baixa().BaixarRecebidasAsync(pedido.Id, new DateOnly(2026, 11, 12), " ");
        var doLoteQueFalhou = await Baixa().BaixarRecebidasAsync(pedido.Id, new DateOnly(2026, 11, 12), Op);

        (await semOperador.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*login*");
        doLoteQueFalhou.Guias.Should().BeEmpty();
        (await GuiaGravadaAsync(ids[0])).Baixado.Should().BeFalse();
    }

    // ==================== o que a operadora pode responder ====================

    [Fact]
    public async Task Falha_do_padrao_no_envio_e_nao_se_sabe_nao_reenvia_e_manda_conferir_no_portal()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(2);
        var pedido = await Fila().EnfileirarEnvioAsync(P, ids, Op);
        _rede.Responder(HttpStatusCode.InternalServerError, TissWsDeTeste.Falha("LoginInvalido"));
        _rede.Responder(HttpStatusCode.OK, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0]) + GuiaDoProtocolo(ids[1]))); // uma segunda ida "daria certo"

        await Conector().ProcessarProximaAsync();
        _b.Agora = _b.Agora.AddDays(1);
        (await Conector().ProcessarProximaAsync()).Should().BeFalse("nada volta para a fila");

        _rede.Idas.Should().HaveCount(1);
        var lote = await GravadaAsync(pedido.Id);
        lote.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        lote.Erro.Should().Contain("LoginInvalido").And.Contain("HTTP 500").And.Contain("não é reenviada sozinha");
        lote.XmlRecebido.Should().Contain("LoginInvalido");
        lote.Guias.Should().OnlyContain(g => g.Situacao == SituacaoGuiaTiss.Falhou);
        (await Fila().SelosDeEnvioAsync(ids)).Values.Should().OnlyContain(s => s.Estado == EstadoDoEnvioDaGuia.Falhou && s.Texto.StartsWith("Falhou: "));
        (await Baixa().BaixarRecebidasAsync(pedido.Id, new DateOnly(2026, 11, 12), Op)).Guias.Should().BeEmpty();
    }

    [Fact]
    public async Task Pagina_de_erro_no_lugar_do_xml_tambem_e_nao_se_sabe()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(1);
        var pedido = await Fila().EnfileirarEnvioAsync(P, ids, Op);
        _rede.Responder(HttpStatusCode.BadGateway, "<html><body>Bad Gateway</body></html>");

        await Conector().ProcessarProximaAsync();

        var lote = await GravadaAsync(pedido.Id);
        lote.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        lote.Erro.Should().Contain("não é reenviada sozinha");
        _rede.Idas.Should().HaveCount(1);
    }

    [Fact]
    public async Task Lote_recusado_pela_operadora_e_recusa_clara_as_guias_ficam_livres_e_o_novo_envio_sai_com_outro_numero()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(1);
        var pedido = await Fila().EnfileirarEnvioAsync(P, ids, Op);
        _rede.Responder(HttpStatusCode.OK, LoteRecusado("5028", "VERSAO DO PADRAO INVALIDA"));

        await Conector().ProcessarProximaAsync();

        var lote = await GravadaAsync(pedido.Id);
        lote.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        lote.Erro.Should().Contain("5028").And.NotContain("não é reenviada sozinha", "a operadora RESPONDEU que não: não há dúvida a conferir no portal");
        lote.Guias.Single().Motivo.Should().Contain("5028");
        // Reenviar é pedido novo, de gente — e a operadora não vê o mesmo número duas vezes.
        var novo = await Fila().EnfileirarEnvioAsync(P, ids, Op);
        _rede.Responder(HttpStatusCode.OK, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0], "OP777009", vez: 2)));
        await Conector().ProcessarProximaAsync();

        MensagemEnviada(_rede.Idas[1].Corpo).Descendants(TissWsDeTeste.Ans + "numeroGuiaPrestador").Single().Value.Should().Be($"C{ids[0]}R2");
        MensagemEnviada(_rede.Idas[1].Corpo).Descendants(TissWsDeTeste.Ans + "numeroLote").Single().Value
            .Should().NotBe(MensagemEnviada(_rede.Idas[0].Corpo).Descendants(TissWsDeTeste.Ans + "numeroLote").Single().Value);
        (await GravadaAsync(novo.Id)).Guias.Single().Should().BeEquivalentTo(new { Situacao = SituacaoGuiaTiss.Recebida, NumeroGuiaOperadora = "OP777009" });
    }

    [Fact]
    public async Task Rede_que_cai_depois_do_carimbo_nao_reenvia_o_lote_nem_guia_alguma()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(2);
        var pedido = await Fila().EnfileirarEnvioAsync(P, ids, Op);
        _rede.Cair();
        _rede.Responder(HttpStatusCode.OK, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0]) + GuiaDoProtocolo(ids[1])));

        await Conector().ProcessarProximaAsync();
        _b.Agora = _b.Agora.AddDays(2);
        (await Conector().ProcessarProximaAsync()).Should().BeFalse();

        _rede.Idas.Should().HaveCount(1, "o lote pode ter chegado: reenviar é arriscar as guias em duplicidade");
        var lote = await GravadaAsync(pedido.Id);
        lote.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        lote.EnvioIniciadoEm.Should().NotBeNull("o carimbo é gravado ANTES da ida à rede");
        lote.XmlEnviado.Should().Contain("loteGuiasWS").And.NotContain(Md5);
        lote.Resultado.Should().Contain("portal da operadora");
        lote.Guias.Should().OnlyContain(g => g.Situacao == SituacaoGuiaTiss.Falhou && g.Motivo!.Contains("não é reenviada sozinha"));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Com_qualquer_das_duas_chaves_desligada_nada_sai_e_as_guias_voltam_a_ficar_prontas(bool noServidor, bool noConvenio)
    {
        await ProntoAsync(envioDoConvenio: noConvenio);
        var ids = await ConferidasAsync(2);
        var pedido = await Fila().EnfileirarEnvioAsync(P, ids, Op);

        await Conector(envioNoServidor: noServidor).ProcessarProximaAsync();

        _rede.Idas.Should().BeEmpty();
        var lote = await GravadaAsync(pedido.Id);
        lote.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        lote.Resultado.Should().Contain("desligado").And.Contain("Nada foi enviado");
        (await Fila().ProntasParaEnvioAsync(P)).Select(g => g.CodigoId).Should().Equal(ids);
        (await Fila().SelosDeEnvioAsync(ids)).Values.Should().OnlyContain(s => s.Estado == EstadoDoEnvioDaGuia.NaoEnviada && s.Texto.Contains("desligado"));
    }

    [Fact]
    public async Task Guia_que_recebeu_baixa_entre_o_pedido_e_o_envio_recusa_o_lote_antes_de_qualquer_coisa_sair()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(2);
        var pedido = await Fila().EnfileirarEnvioAsync(P, ids, Op);
        await Faturamento().DarBaixaAsync(ids[1], new DateOnly(2026, 11, 12), "MANUAL1", Op, null, default, _b.DadosDaGuia());

        await Conector().ProcessarProximaAsync();

        _rede.Idas.Should().BeEmpty();
        var lote = await GravadaAsync(pedido.Id);
        lote.Estado.Should().Be(EstadoTransacaoTiss.Cancelada);
        lote.Resultado.Should().Contain($"C{ids[1]}").And.Contain("recebeu baixa").And.Contain("Nada foi enviado");
    }

    // ==================== situação do protocolo e cancelamento ====================

    [Fact]
    public async Task Protocolo_nao_localizado_e_resposta_clara_e_nao_mexe_nas_guias()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(1);
        var lote = await LoteRecebidoAsync(ids, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0], "OP777001")));
        var status = await Fila().EnfileirarStatusDoEnvioAsync(lote.Id, Op);
        _rede.Responder(HttpStatusCode.OK, Conferida(TissWsDeTeste.Resposta("situacaoProtocoloWS", "SITUACAO_PROTOCOLO",
            "<ans:situacaoProtocolo><ans:mensagemErro><ans:codigoGlosa>1307</ans:codigoGlosa><ans:descricaoGlosa>NUMERO DA GUIA INVALIDO</ans:descricaoGlosa></ans:mensagemErro></ans:situacaoProtocolo>")));

        await Conector().ProcessarProximaAsync();

        (await GravadaAsync(status.Id)).Should().Match<TransacaoTiss>(t => t.Estado == EstadoTransacaoTiss.Falhou && t.Erro!.Contains("1307"));
        (await GravadaAsync(lote.Id)).Guias.Single().Situacao.Should().Be(SituacaoGuiaTiss.Recebida);
    }

    [Fact]
    public async Task Situacao_do_protocolo_sem_resposta_tenta_de_novo_porque_so_pergunta()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(1);
        var lote = await LoteRecebidoAsync(ids, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0])));
        var status = await Fila().EnfileirarStatusDoEnvioAsync(lote.Id, Op);
        _rede.Cair();
        _rede.Responder(HttpStatusCode.OK, SituacaoDoProtocolo("2", GuiaDaSituacao(ids[0], "OP777001")));

        await Conector().ProcessarProximaAsync();
        (await GravadaAsync(status.Id)).Estado.Should().Be(EstadoTransacaoTiss.Pendente);
        _b.Agora = _b.Agora.AddMinutes(1);
        await Conector().ProcessarProximaAsync();

        (await GravadaAsync(status.Id)).Should().Match<TransacaoTiss>(t => t.Estado == EstadoTransacaoTiss.Concluida && t.Tentativas == 2);
        (await GravadaAsync(lote.Id)).Guias.Single().NumeroGuiaOperadora.Should().Be("OP777001");
    }

    [Fact]
    public async Task Cancelamento_aceito_tira_a_guia_da_baixa_e_a_deixa_livre_para_um_envio_novo()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(1);
        var lote = await LoteRecebidoAsync(ids, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0], "OP777001")));
        var cancelamento = await Fila().EnfileirarCancelamentoGuiaAsync(P, ids[0], $"C{ids[0]}", Protocolo, Op);
        _rede.Responder(HttpStatusCode.OK, ReciboDeCancelamento(ids[0], "1"));

        await Conector().ProcessarProximaAsync();

        var ida = _rede.Idas[1];
        ida.Endereco.Should().Be(TissWebserviceCliente.CancelaGuiaSaudePetrobras);
        TissWsDeTeste.ValidarNoXsd(MensagemEnviada(ida.Corpo)).Should().BeEmpty();
        (await GravadaAsync(cancelamento.Id)).Estado.Should().Be(EstadoTransacaoTiss.Concluida);
        var item = (await GravadaAsync(lote.Id)).Guias.Single();
        item.Situacao.Should().Be(SituacaoGuiaTiss.Cancelada);
        item.Motivo.Should().StartWith("Cancelada na operadora em ");
        (await Fila().GuiasRecebidasSemBaixaAsync(lote.Id)).Should().BeEmpty("guia cancelada na operadora não recebe baixa");
        (await Fila().SelosDeEnvioAsync(ids))[ids[0]].Estado.Should().Be(EstadoDoEnvioDaGuia.Cancelada);
        (await Fila().ConferirEnvioAsync(P, ids)).PodeEnviar.Should().BeTrue();
        (await _b.NovoContexto().Auditoria.AsNoTracking().CountAsync(a => a.Acao == "CancelamentoGuiaTissConcluido")).Should().Be(1);
    }

    [Theory]
    [InlineData("4", true)]   // em processamento: não se sabe
    [InlineData("2", false)]  // não cancelado: a operadora respondeu que não
    public async Task Cancelamento_em_processamento_ou_recusado_nao_mexe_na_guia_e_nunca_e_repetido(string status, bool incerto)
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(1);
        var lote = await LoteRecebidoAsync(ids, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0], "OP777001")));
        var cancelamento = await Fila().EnfileirarCancelamentoGuiaAsync(P, ids[0], $"C{ids[0]}", Protocolo, Op);
        _rede.Responder(HttpStatusCode.OK, ReciboDeCancelamento(ids[0], status));

        await Conector().ProcessarProximaAsync();
        _b.Agora = _b.Agora.AddDays(1);
        (await Conector().ProcessarProximaAsync()).Should().BeFalse();

        _rede.Idas.Should().HaveCount(2, "o lote e UMA ida do cancelamento");
        var gravado = await GravadaAsync(cancelamento.Id);
        gravado.Estado.Should().Be(EstadoTransacaoTiss.Falhou);
        gravado.Erro!.Contains("não é reenviada sozinha").Should().Be(incerto);
        (await GravadaAsync(lote.Id)).Guias.Single().Situacao.Should().Be(SituacaoGuiaTiss.Recebida, "sem o recibo de cancelada, a guia continua recebida");
    }

    // ==================== conferir sem baixar ====================

    [Fact]
    public async Task Conferir_grava_a_guia_ANS_e_a_trilha_sem_dar_baixa_e_reconferir_substitui()
    {
        var (paciente, guia) = await _b.GuiaAsync(conferida: false);

        await Faturamento().ConferirGuiaAnsAsync(guia.Id, _b.DadosDaGuia(), "quem.conferiu");
        await Faturamento().ConferirGuiaAnsAsync(guia.Id, _b.DadosDaGuia(cbos: "225105"), "quem.reconferiu");

        var gravada = await GuiaGravadaAsync(guia.Id);
        gravada.Baixado.Should().BeFalse();
        gravada.Status.Should().Be(StatusCodigo.Aberto);
        gravada.NumeroGuiaReal.Should().BeNull();
        gravada.GuiaAns!.Cbos.Should().Be("225105");
        gravada.GuiaAns.ConferidaPor.Should().Be("quem.reconferiu");
        (await _b.NovoContexto().GuiasTissConsulta.CountAsync()).Should().Be(1, "reconferir substitui, não acumula");
        var trilha = await _b.NovoContexto().Auditoria.AsNoTracking().Where(a => a.Acao == "GuiaAnsConferida").OrderBy(a => a.Id).ToListAsync();
        trilha.Select(a => a.Operador).Should().Equal("quem.conferiu", "quem.reconferiu");
        trilha.Should().OnlyContain(a => a.CodigoId == guia.Id && a.PacienteId == paciente.Id);
        (await _b.NovoContexto().Auditoria.AsNoTracking().AnyAsync(a => a.Acao == "BaixaGuia")).Should().BeFalse();
    }

    [Fact]
    public async Task Conferir_e_recusado_para_guia_baixada_para_convenio_que_nao_pede_para_guia_invalida_e_sem_operador()
    {
        var (_, baixada) = await _b.GuiaAsync(conferida: false);
        await Faturamento().DarBaixaAsync(baixada.Id, new DateOnly(2026, 11, 12), "ABC123", Op, null, default, _b.DadosDaGuia());
        var (_, deOutroConvenio) = await _b.GuiaAsync(conferida: false, convenio: Convenio.Amil);
        var (_, aberta) = await _b.GuiaAsync(conferida: false);

        var jaBaixada = () => Faturamento().ConferirGuiaAnsAsync(baixada.Id, _b.DadosDaGuia(), Op);
        var naoPede = () => Faturamento().ConferirGuiaAnsAsync(deOutroConvenio.Id, _b.DadosDaGuia(), Op);
        var invalida = () => Faturamento().ConferirGuiaAnsAsync(aberta.Id, _b.DadosDaGuia(cbos: null), Op);
        var semOperador = () => Faturamento().ConferirGuiaAnsAsync(aberta.Id, _b.DadosDaGuia(), " ");
        var inexistente = () => Faturamento().ConferirGuiaAnsAsync(999_999, _b.DadosDaGuia(), Op);

        (await jaBaixada.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*já tem baixa*");
        (await naoPede.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*não pede a conferência*");
        (await invalida.Should().ThrowAsync<InvalidOperationException>()).WithMessage("A guia ANS não pode ser gravada:*");
        (await semOperador.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*login*");
        await inexistente.Should().ThrowAsync<InvalidOperationException>();
        (await GuiaGravadaAsync(aberta.Id)).GuiaAns.Should().BeNull("recusa não grava nada");
        (await _b.NovoContexto().Auditoria.AsNoTracking().AnyAsync(a => a.Acao == "GuiaAnsConferida")).Should().BeFalse();
    }

    [Fact]
    public async Task A_baixa_de_sempre_continua_exigindo_a_guia_e_aceita_a_que_foi_conferida_antes()
    {
        // DarBaixaAsync não mudou de assinatura nem de regra: sem a guia no pedido, recusa — mesmo
        // com a conferência gravada. Quem a reapresenta é quem chama (a tela, ou a baixa das enviadas).
        var ids = await ConferidasAsync(1);

        var semGuia = () => Faturamento().DarBaixaAsync(ids[0], new DateOnly(2026, 11, 12), "ABC123", Op, null);
        (await semGuia.Should().ThrowAsync<InvalidOperationException>()).WithMessage(GuiaAnsNaBaixa.RecusaSemGuia);

        var gravada = (await GuiaGravadaAsync(ids[0])).GuiaAns!;
        await Faturamento().DarBaixaAsync(ids[0], new DateOnly(2026, 11, 12), "ABC123", Op, null, default, GuiaAnsNaBaixa.DaGuia(gravada));
        (await GuiaGravadaAsync(ids[0])).NumeroGuiaReal.Should().Be("ABC123");
    }

    [Fact]
    public void A_guia_gravada_volta_para_os_campos_do_formulario_sem_perder_nenhum()
    {
        var dados = _b.DadosDaGuia() with
        {
            ValidadeCarteira = new DateOnly(2027, 12, 31), AtendimentoRn = true, NomeSocial = "Nome Social de Teste",
            NomeContratado = "Clínica de Teste Ltda", CoberturaEspecial = "01", SaudeOcupacional = "03", Observacao = "observação de teste"
        };
        var codigo = new CodigoFaturamento { Id = 7 };
        GuiaAnsNaBaixa.Aplicar(codigo, dados, Op, _b.Agora);

        GuiaAnsNaBaixa.DaGuia(codigo.GuiaAns!).Should().BeEquivalentTo(dados);
        GuiaAnsNaBaixa.Validar(GuiaAnsNaBaixa.DaGuia(codigo.GuiaAns!)).Should().BeEmpty();
    }

    // ==================== as leituras para as telas ====================

    [Fact]
    public async Task Prontas_para_envio_sao_as_conferidas_sem_baixa_do_convenio_fora_de_envio_que_as_prenda()
    {
        await ProntoAsync();
        var prontas = await ConferidasAsync(2);
        var (_, semConferencia) = await _b.GuiaAsync(conferida: false);
        var (_, baixada) = await _b.GuiaAsync();
        await Faturamento().DarBaixaAsync(baixada.Id, new DateOnly(2026, 11, 12), "ABC123", Op, null, default, _b.DadosDaGuia());
        var (_, deOutroConvenio) = await _b.GuiaAsync(convenio: Convenio.Amil);
        var presa = await ConferidasAsync(1);
        await Fila().EnfileirarEnvioAsync(P, presa, Op);

        var lista = await Fila().ProntasParaEnvioAsync(P);

        lista.Select(g => g.CodigoId).Should().Equal(prontas);
        lista[0].Should().BeEquivalentTo(new
        {
            PacienteNome = "Paciente de Teste", DataAtendimento = DateOnly.FromDateTime(_b.Agora), Tipo = TipoCodigo.Consulta,
            CodigoProcedimento = "10101012", ValorProcedimento = 139m, ConferidaPor = Op
        });
        (await Fila().ProntasParaEnvioAsync("Amil")).Select(g => g.CodigoId).Should().Equal(deOutroConvenio.Id);
        (await Fila().ProntasParaEnvioAsync(P, limite: 1)).Should().HaveCount(1);
        _ = semConferencia;
    }

    [Fact]
    public async Task Guia_glosada_volta_a_ficar_pronta_e_a_recebida_nao()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(2);
        await LoteRecebidoAsync(ids, ProtocoloDeRecebimento(GuiaDoProtocolo(ids[0], "OP777001") + GuiaDoProtocolo(ids[1], glosada: true)));

        (await Fila().ProntasParaEnvioAsync(P)).Select(g => g.CodigoId).Should().Equal(ids[1]);
    }

    [Fact]
    public void O_selo_da_linha_diz_cada_situacao_com_a_frase_dela()
    {
        static TransacaoTissGuia Item(EstadoTransacaoTiss estado, SituacaoGuiaTiss situacao, string? motivo = null, string? protocolo = "987") => new()
        {
            Situacao = situacao, Motivo = motivo, Transacao = new TransacaoTiss { Id = 5, Estado = estado, Protocolo = protocolo }
        };

        SeloDeEnvioTiss.Do(null).Should().Be(new SeloDeEnvioTiss(EstadoDoEnvioDaGuia.NaoEnviada, "Não enviada"));
        SeloDeEnvioTiss.Do(Item(EstadoTransacaoTiss.Pendente, SituacaoGuiaTiss.Pendente, protocolo: null)).Should().Be(new SeloDeEnvioTiss(EstadoDoEnvioDaGuia.NaFila, "Na fila de envio", null, 5));
        SeloDeEnvioTiss.Do(Item(EstadoTransacaoTiss.EmEnvio, SituacaoGuiaTiss.Pendente, protocolo: null)).Estado.Should().Be(EstadoDoEnvioDaGuia.NaFila);
        SeloDeEnvioTiss.Do(Item(EstadoTransacaoTiss.Concluida, SituacaoGuiaTiss.Pendente)).Should().Be(
            new SeloDeEnvioTiss(EstadoDoEnvioDaGuia.Enviada, "Enviada, protocolo 987: sem resposta sobre esta guia", "987", 5));
        SeloDeEnvioTiss.Do(Item(EstadoTransacaoTiss.Concluida, SituacaoGuiaTiss.Recebida)).Texto.Should().Be("Recebida pela operadora, protocolo 987");
        SeloDeEnvioTiss.Do(Item(EstadoTransacaoTiss.Concluida, SituacaoGuiaTiss.Glosada, "1001 - carteira")).Texto.Should().Be("Glosada: 1001 - carteira");
        SeloDeEnvioTiss.Do(Item(EstadoTransacaoTiss.Concluida, SituacaoGuiaTiss.Glosada)).Texto.Should().Be("Glosada: a operadora não informou o motivo");
        SeloDeEnvioTiss.Do(Item(EstadoTransacaoTiss.Concluida, SituacaoGuiaTiss.Cancelada, "Cancelada na operadora em 12/11/2026.")).Estado.Should().Be(EstadoDoEnvioDaGuia.Cancelada);
        SeloDeEnvioTiss.Do(Item(EstadoTransacaoTiss.Falhou, SituacaoGuiaTiss.Falhou, "sem resposta")).Should().Be(
            new SeloDeEnvioTiss(EstadoDoEnvioDaGuia.Falhou, "Falhou: sem resposta", "987", 5));
        SeloDeEnvioTiss.Do(Item(EstadoTransacaoTiss.Cancelada, SituacaoGuiaTiss.Falhou, "O envio está desligado.", null)).Should().Be(
            new SeloDeEnvioTiss(EstadoDoEnvioDaGuia.NaoEnviada, "Não enviada: O envio está desligado.", null, 5));
    }

    [Fact]
    public async Task O_selo_de_uma_guia_e_o_do_ultimo_envio_dela()
    {
        await ProntoAsync();
        var ids = await ConferidasAsync(1);
        _rede.Responder(HttpStatusCode.OK, LoteRecusado("5028", "VERSAO DO PADRAO INVALIDA"));
        await Fila().EnfileirarEnvioAsync(P, ids, Op);
        await Conector().ProcessarProximaAsync();
        (await Fila().SelosDeEnvioAsync(ids))[ids[0]].Estado.Should().Be(EstadoDoEnvioDaGuia.Falhou);

        var novo = await Fila().EnfileirarEnvioAsync(P, ids, Op);

        (await Fila().SelosDeEnvioAsync([ids[0], 999_999])).Should().BeEquivalentTo(new Dictionary<int, SeloDeEnvioTiss>
        {
            [ids[0]] = new(EstadoDoEnvioDaGuia.NaFila, "Na fila de envio", null, novo.Id),
            [999_999] = SeloDeEnvioTiss.NaoEnviada
        });
    }

    // ---------- a "operadora" ----------

    private sealed class Operadora : HttpMessageHandler
    {
        public sealed record Ida(Uri Endereco, string Corpo);

        private readonly Queue<Func<HttpResponseMessage>> _respostas = new();
        public List<Ida> Idas { get; } = [];

        public void Responder(HttpStatusCode status, string corpo)
            => _respostas.Enqueue(() => new HttpResponseMessage(status) { Content = new ByteArrayContent(Encoding.Latin1.GetBytes(corpo)) });

        public void Cair() => _respostas.Enqueue(() => throw new HttpRequestException("conexão recusada"));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Idas.Add(new Ida(request.RequestUri!, Encoding.Latin1.GetString(await request.Content!.ReadAsByteArrayAsync(ct))));
            if (_respostas.Count == 0) throw new InvalidOperationException("O teste não combinou resposta para esta ida.");
            return _respostas.Dequeue()();
        }
    }
}
