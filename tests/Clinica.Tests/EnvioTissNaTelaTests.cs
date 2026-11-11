using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O QUE AS TELAS DO ENVIO AFIRMAM (nov/2026). Tudo aqui é frase e cor — e é justamente por isso
/// que tem teste: "pronta para envio" lida como "enviada", ou "não se sabe se chegou" lido como
/// "falhou, envie de novo", não quebra build nem tela; apresenta a mesma guia duas vezes à operadora.
/// </summary>
public sealed class EnvioTissNaTelaTests
{
    private static readonly DateTime Agora = new(2026, 11, 11, 10, 0, 0);

    private static TransacaoTiss Envio(EstadoTransacaoTiss estado, bool saiu = false, string? protocolo = null, params SituacaoGuiaTiss[] guias)
        => new()
        {
            Id = 7, Tipo = TipoTransacaoTiss.EnvioGuia, ConvenioCodigo = "Petrobras", Estado = estado, Protocolo = protocolo,
            EnvioIniciadoEm = saiu ? Agora : null, PedidoEm = Agora, PedidoPor = "secretaria",
            Guias = guias.Select((s, i) => new TransacaoTissGuia { Id = i + 1, CodigoId = 100 + i, NumeroGuiaPrestador = $"C{100 + i}", Situacao = s }).ToList()
        };

    // ==================== a linha do painel ====================

    [Fact]
    public void So_convenio_com_web_service_tem_selo_e_a_familia_Petrobras_e_a_unica_hoje()
    {
        EnvioTissNaTela.TemWebService(Convenio.Petrobras).Should().BeTrue();
        foreach (var outra in Enum.GetValues<Convenio>().Where(c => c != Convenio.Petrobras))
            EnvioTissNaTela.TemWebService(outra).Should().BeFalse($"{outra} não tem web service previsto");

        EnvioTissNaTela.SeloDaLinha(false, null, pronta: true).Should().BeNull();
    }

    [Fact]
    public void Pronta_para_envio_nao_e_enviada_e_cada_estado_tem_o_seu_selo_curto_a_sua_cor_e_o_detalhe()
    {
        EnvioTissNaTela.SeloDaLinha(true, SeloDeEnvioTiss.NaoEnviada, pronta: false).Should().Be(new SeloDaLinhaNaTela("Não enviada", "neutro", null));
        EnvioTissNaTela.SeloDaLinha(true, null, pronta: true).Should().Be(new SeloDaLinhaNaTela("Pronta para envio", "info", null));

        SeloDaLinhaNaTela Selo(EstadoTransacaoTiss estado, SituacaoGuiaTiss situacao, string? motivo = null)
        {
            var envio = Envio(estado, saiu: true, protocolo: "987", situacao);
            envio.Guias[0].Transacao = envio;
            envio.Guias[0].Motivo = motivo;
            return EnvioTissNaTela.SeloDaLinha(true, SeloDeEnvioTiss.Do(envio.Guias[0]), pronta: false)!;
        }

        // O selo é curto; o protocolo e o motivo vão no detalhe, inteiros.
        Selo(EstadoTransacaoTiss.Pendente, SituacaoGuiaTiss.Pendente).Should().Be(new SeloDaLinhaNaTela("Na fila", "info", "Na fila de envio"));
        Selo(EstadoTransacaoTiss.Concluida, SituacaoGuiaTiss.Recebida)
            .Should().Be(new SeloDaLinhaNaTela("Recebida", "sucesso", "Recebida pela operadora, protocolo 987"));
        Selo(EstadoTransacaoTiss.Concluida, SituacaoGuiaTiss.Glosada, "1001 - carteira inválida")
            .Should().Be(new SeloDaLinhaNaTela("Glosada", "perigo", "Glosada: 1001 - carteira inválida"));
        Selo(EstadoTransacaoTiss.Concluida, SituacaoGuiaTiss.Pendente)
            .Should().Match<SeloDaLinhaNaTela>(s => s.Rotulo == "Enviada" && s.Tom == "aviso" && s.Detalhe!.Contains("protocolo 987"));
        Selo(EstadoTransacaoTiss.Falhou, SituacaoGuiaTiss.Falhou, "rede caiu")
            .Should().Be(new SeloDaLinhaNaTela("Envio falhou", "perigo", "Falhou: rede caiu"));
    }

    /// <summary>O pedido cancelado antes de sair deixa a guia conferida: ela volta a ser "pronta", não "não enviada".</summary>
    [Fact]
    public void Guia_de_envio_cancelado_que_continua_conferida_volta_a_aparecer_como_pronta()
    {
        var envio = Envio(EstadoTransacaoTiss.Cancelada, saiu: false, protocolo: null, SituacaoGuiaTiss.Falhou);
        envio.Guias[0].Transacao = envio;
        var selo = SeloDeEnvioTiss.Do(envio.Guias[0]);

        selo.Estado.Should().Be(EstadoDoEnvioDaGuia.NaoEnviada);
        EnvioTissNaTela.SeloDaLinha(true, selo, pronta: true).Should().Be(new SeloDaLinhaNaTela("Pronta para envio", "info", null));
        EnvioTissNaTela.SeloDaLinha(true, selo, pronta: false)!
            .Should().Match<SeloDaLinhaNaTela>(s => s.Rotulo == "Não enviada" && s.Detalhe!.StartsWith("Não enviada:"));
    }

    [Fact]
    public void Os_selos_das_linhas_saem_de_uma_leitura_por_convenio_e_ignoram_quem_nao_tem_web_service()
    {
        PendenciaCodigo P(int id, Convenio familia, string? codigo) => new(
            id, 1, "Paciente", familia, TipoCodigo.Consulta, OrdemCodigo.Primeiro, new DateOnly(2026, 11, 10),
            FormaObtencao.NaoAplica, 1, default, null) { ConvenioCodigo = codigo };

        var porConvenio = EnvioTissNaTela.GuiasComSeloPorConvenio(
            [P(1, Convenio.Petrobras, "Petrobras"), P(2, Convenio.Petrobras, null), P(3, Convenio.Amil, "Amil"), P(4, Convenio.UnimedPadrao, null)]);

        porConvenio.Should().ContainSingle().Which.Key.Should().Be("Petrobras");
        porConvenio["Petrobras"].Should().BeEquivalentTo([1, 2]);

        var selos = EnvioTissNaTela.SelosDasLinhas([1, 2], new Dictionary<int, SeloDeEnvioTiss> { [1] = SeloDeEnvioTiss.NaoEnviada }, new HashSet<int> { 2 });
        selos[1].Should().Be(new SeloDaLinhaNaTela("Não enviada", "neutro", null));
        selos[2].Should().Be(new SeloDaLinhaNaTela("Pronta para envio", "info", null));
    }

    // ==================== o lote ====================

    /// <summary>
    /// O estado que não pode passar batido: a mensagem saiu e não há resposta em que confiar.
    /// Ele tem nome próprio, vai em vermelho e manda conferir no portal ANTES de enviar de novo.
    /// </summary>
    [Fact]
    public void Envio_que_saiu_e_falhou_e_NAO_SE_SABE_SE_CHEGOU_e_o_que_nem_saiu_e_so_nao_enviado()
    {
        var incerto = EnvioTissNaTela.DoEnvio(Envio(EstadoTransacaoTiss.Falhou, saiu: true, protocolo: null, SituacaoGuiaTiss.Falhou));
        incerto.Incerto.Should().BeTrue();
        incerto.Rotulo.Should().Be("Não se sabe se chegou");
        incerto.Tom.Should().Be("perigo");
        incerto.Frase.Should().Contain("Confira no portal da operadora ANTES de enviar");

        var naoSaiu = Envio(EstadoTransacaoTiss.Falhou, saiu: false, protocolo: null, SituacaoGuiaTiss.Falhou);
        naoSaiu.Erro = "Dados recusados antes do envio.";
        var semEnvio = EnvioTissNaTela.DoEnvio(naoSaiu);
        semEnvio.Incerto.Should().BeFalse();
        semEnvio.Rotulo.Should().Be("Não enviado");
        semEnvio.Frase.Should().Be("Dados recusados antes do envio.");

        // Consulta de elegibilidade que falha não escreve na operadora: nunca é "não se sabe se chegou".
        EnvioTissNaTela.NaoSeSabeSeChegou(new TransacaoTiss { Tipo = TipoTransacaoTiss.Elegibilidade, Estado = EstadoTransacaoTiss.Falhou, EnvioIniciadoEm = Agora })
            .Should().BeFalse();
    }

    /// <summary>
    /// A fila devolve às prontas a guia do envio que falhou. A que ficou em DÚVIDA nasce travada —
    /// também quando o envio dela já saiu da lista dos recentes e não dá para saber; a que nem saiu
    /// e a glosada só ganham o aviso.
    /// </summary>
    [Fact]
    public void Guia_pronta_de_envio_em_duvida_pede_a_conferencia_no_portal_antes_de_ser_marcada()
    {
        SeloDeEnvioTiss SeloDe(TransacaoTiss envio, string? motivo = null)
        {
            envio.Guias[0].Transacao = envio;
            envio.Guias[0].Motivo = motivo;
            return SeloDeEnvioTiss.Do(envio.Guias[0]);
        }
        var incerto = Envio(EstadoTransacaoTiss.Falhou, saiu: true, protocolo: null, SituacaoGuiaTiss.Falhou);
        var naoSaiu = Envio(EstadoTransacaoTiss.Falhou, saiu: false, protocolo: null, SituacaoGuiaTiss.Falhou);
        var glosado = Envio(EstadoTransacaoTiss.Concluida, saiu: true, protocolo: "987", SituacaoGuiaTiss.Glosada);

        EnvioTissNaTela.DaPronta(SeloDe(incerto, "tempo esgotado"), incerto)!
            .Should().Match<RessalvaDaPronta>(r => r.PedeConferenciaNoPortal && r.Texto.Contains("Confira no portal ANTES"));
        // O envio não está entre os lidos: não dá para saber — vale a cautela.
        EnvioTissNaTela.DaPronta(SeloDe(incerto, "tempo esgotado"), null)!.PedeConferenciaNoPortal.Should().BeTrue();
        EnvioTissNaTela.DaPronta(SeloDe(naoSaiu, "dados recusados"), naoSaiu)!
            .Should().Match<RessalvaDaPronta>(r => !r.PedeConferenciaNoPortal && r.Texto.Contains("Nada chegou à operadora"));
        EnvioTissNaTela.DaPronta(SeloDe(glosado, "1001 - carteira inválida"), glosado)!
            .Should().Match<RessalvaDaPronta>(r => !r.PedeConferenciaNoPortal && r.Texto.Contains("Glosada: 1001") && r.Texto.Contains("Corrija"));

        // Nunca enviada, ou pedido cancelado antes de sair: sem ressalva.
        EnvioTissNaTela.DaPronta(null, null).Should().BeNull();
        EnvioTissNaTela.DaPronta(SeloDeEnvioTiss.NaoEnviada, null).Should().BeNull();
        EnvioTissNaTela.PerguntaDaLiberacao("Fulana de Teste").Should().Contain("Fulana de Teste").And.Contain("NÃO está lá").And.Contain("duas vezes");
    }

    [Fact]
    public void Na_fila_enviando_recebido_com_glosa_e_protocolado_dizem_coisas_diferentes()
    {
        EnvioTissNaTela.DoEnvio(Envio(EstadoTransacaoTiss.Pendente, guias: SituacaoGuiaTiss.Pendente)).Should()
            .Match<SituacaoDoEnvioNaTela>(s => s.Rotulo == "Na fila" && s.Tom == "info" && s.Frase!.Contains("Ainda não foi à operadora"));
        EnvioTissNaTela.DoEnvio(Envio(EstadoTransacaoTiss.EmEnvio, guias: SituacaoGuiaTiss.Pendente)).Rotulo.Should().Be("Enviando");
        EnvioTissNaTela.DoEnvio(Envio(EstadoTransacaoTiss.Cancelada, guias: SituacaoGuiaTiss.Falhou)).Should()
            .Match<SituacaoDoEnvioNaTela>(s => s.Rotulo == "Não enviado" && s.Tom == "neutro" && !s.Incerto);

        var comGlosa = EnvioTissNaTela.DoEnvio(Envio(EstadoTransacaoTiss.Concluida, true, "555", SituacaoGuiaTiss.Recebida, SituacaoGuiaTiss.Glosada, SituacaoGuiaTiss.Pendente));
        comGlosa.Rotulo.Should().Be("Recebido com glosa");
        comGlosa.Frase.Should().Contain("555").And.Contain("1 guia(s) com glosa");

        var semResposta = EnvioTissNaTela.DoEnvio(Envio(EstadoTransacaoTiss.Concluida, true, "555", SituacaoGuiaTiss.Recebida, SituacaoGuiaTiss.Pendente));
        semResposta.Rotulo.Should().Be("Protocolado");
        semResposta.Frase.Should().Contain("consulte a situação na operadora");

        EnvioTissNaTela.DoEnvio(Envio(EstadoTransacaoTiss.Concluida, true, "555", SituacaoGuiaTiss.Recebida, SituacaoGuiaTiss.Recebida))
            .Should().Match<SituacaoDoEnvioNaTela>(s => s.Rotulo == "Recebido" && s.Tom == "sucesso");
    }

    [Fact]
    public void O_que_cabe_em_cada_estado_do_envio()
    {
        var naFila = Envio(EstadoTransacaoTiss.Pendente, guias: SituacaoGuiaTiss.Pendente);
        var recebido = Envio(EstadoTransacaoTiss.Concluida, true, "555", SituacaoGuiaTiss.Recebida, SituacaoGuiaTiss.Glosada);
        var incerto = Envio(EstadoTransacaoTiss.Falhou, true, null, SituacaoGuiaTiss.Falhou);

        // Cancelar: só o que ainda não saiu. Consultar: só o que tem protocolo. Baixa: só as recebidas.
        EnvioTissNaTela.PodeCancelarPedido(naFila).Should().BeTrue();
        EnvioTissNaTela.PodeCancelarPedido(recebido).Should().BeFalse();
        EnvioTissNaTela.PodeCancelarPedido(incerto).Should().BeFalse();
        EnvioTissNaTela.PodeConsultarSituacao(recebido).Should().BeTrue();
        EnvioTissNaTela.PodeConsultarSituacao(naFila).Should().BeFalse();
        EnvioTissNaTela.PodeConsultarSituacao(incerto).Should().BeFalse("sem protocolo não há o que consultar — confere-se no portal");
        EnvioTissNaTela.Recebidas(recebido).Should().Be(1);
        EnvioTissNaTela.Recebidas(incerto).Should().Be(0);

        EnvioTissNaTela.Acompanha([recebido, incerto]).Should().BeFalse();
        EnvioTissNaTela.Acompanha([recebido, naFila]).Should().BeTrue();
    }

    [Fact]
    public void A_pergunta_do_envio_diz_quantas_para_quem_e_que_e_cobranca_e_o_lote_leva_ate_cem()
    {
        EnvioTissNaTela.PerguntaDoEnvio(3, "Petrobras").Should()
            .Contain("3 guia(s)").And.Contain("Petrobras").And.Contain("COBRANÇA").And.Contain("não há como desfazer");
        EnvioTissNaTela.ImpedimentoDaMarcacao(100).Should().BeNull();
        EnvioTissNaTela.ImpedimentoDaMarcacao(101).Should().Contain("100").And.Contain("101");
        EnvioTissNaTela.AvisoDeEnvioDesligado(true, "Petrobras").Should().BeNull();
        EnvioTissNaTela.AvisoDeEnvioDesligado(false, "Petrobras").Should().Contain("desligado").And.Contain("Configurações do faturamento");
    }

    // ==================== a credencial ====================

    [Fact]
    public void Credencial_pede_login_e_senha_mas_com_senha_gravada_o_branco_e_nao_trocar()
    {
        EnvioTissNaTela.CriticarCredencial("", "", haSenhaGravada: false).Keys.Should().BeEquivalentTo("Login", "Senha");
        EnvioTissNaTela.CriticarCredencial(new string('a', 21), "x", false).Keys.Should().BeEquivalentTo("Login");
        EnvioTissNaTela.CriticarCredencial("clinica", "", haSenhaGravada: true).Should().BeEmpty();
        EnvioTissNaTela.CriticarCredencial("clinica", "segredo", false).Should().BeEmpty();

        // Login e senha são gravados juntos: trocar o login sem digitar a senha não tem como gravar.
        EnvioTissNaTela.TrocaDeLoginPedeSenha("antigo", "novo", null).Should().BeTrue();
        EnvioTissNaTela.TrocaDeLoginPedeSenha("mesmo", " mesmo ", "").Should().BeFalse();
        EnvioTissNaTela.TrocaDeLoginPedeSenha("antigo", "novo", "segredo").Should().BeFalse();

        EnvioTissNaTela.SituacaoDaSenha(true).Should().Contain("não é mostrada");
        EnvioTissNaTela.SituacaoDaSenha(false).Should().Be("Nenhuma senha gravada.");
    }

    // ==================== a elegibilidade ====================

    private static TransacaoTiss Consulta(EstadoTransacaoTiss estado, string? resultado = null, string? erro = null, int segundos = 0)
        => new()
        {
            Tipo = TipoTransacaoTiss.Elegibilidade, Estado = estado, Resultado = resultado, Erro = erro,
            PedidoEm = Agora.AddSeconds(-segundos), PedidoPor = "secretaria"
        };

    /// <summary>Três desfechos, e "não foi possível consultar" nunca tem a cara de "não elegível".</summary>
    [Fact]
    public void Elegivel_nao_elegivel_e_nao_foi_possivel_consultar_sao_tres_coisas()
    {
        EnvioTissNaTela.DaElegibilidade(Consulta(EstadoTransacaoTiss.Concluida, "Elegível. Validade da carteira: 31/12/2027."), Agora)
            .Should().Be(new ElegibilidadeNaTela("Elegível. Validade da carteira: 31/12/2027.", "sucesso", true, false));
        EnvioTissNaTela.DaElegibilidade(Consulta(EstadoTransacaoTiss.Concluida, "Não elegível: 1002 - carteira cancelada"), Agora)
            .Should().Be(new ElegibilidadeNaTela("Não elegível: 1002 - carteira cancelada", "erro", true, false));

        var falhou = EnvioTissNaTela.DaElegibilidade(Consulta(EstadoTransacaoTiss.Falhou, "Falhou: x", erro: "A operadora não avaliou a carteira: LoginInvalido"), Agora);
        falhou.Should().Be(new ElegibilidadeNaTela("Não foi possível consultar: A operadora não avaliou a carteira: LoginInvalido", "atencao", true, false));

        var recusada = EnvioTissNaTela.DaElegibilidade(Consulta(EstadoTransacaoTiss.Cancelada, "Convênio sem credencial cadastrada."), Agora);
        recusada.Texto.Should().StartWith("Não foi possível consultar: Convênio sem credencial");
        recusada.Tom.Should().Be("atencao");
    }

    /// <summary>Sem serviço de envio o pedido fica na fila: passado o prazo a tela diz, e deixa cancelar.</summary>
    [Fact]
    public void Pedido_parado_na_fila_vira_o_servico_nao_respondeu_e_pode_ser_cancelado()
    {
        var cedo = EnvioTissNaTela.DaElegibilidade(Consulta(EstadoTransacaoTiss.Pendente, segundos: 10), Agora);
        cedo.Should().Be(new ElegibilidadeNaTela("Consultando na operadora…", "info", false, true));

        var tarde = EnvioTissNaTela.DaElegibilidade(Consulta(EstadoTransacaoTiss.Pendente, segundos: 61), Agora);
        tarde.Tom.Should().Be("atencao");
        tarde.Terminou.Should().BeFalse();
        tarde.PodeDesistir.Should().BeTrue();
        tarde.Texto.Should().StartWith("O serviço de envio não respondeu");

        // Em andamento: já saiu da fila, não há o que cancelar.
        EnvioTissNaTela.DaElegibilidade(Consulta(EstadoTransacaoTiss.EmEnvio, segundos: 500), Agora)
            .Should().Be(new ElegibilidadeNaTela("Consultando na operadora…", "info", false, false));
    }
}
