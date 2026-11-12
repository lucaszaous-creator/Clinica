using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O QUE O FORMULÁRIO DE BAIXA AFIRMA ENQUANTO ENVIA (nov/2026, correção de fluxo da direção: "o
/// número só existe depois do envio"). Cada desfecho tem a sua frase e as suas saídas — e é isso
/// que se fixa aqui: uma frase trocada não quebra nada, só faz a pessoa reenviar o que não devia
/// ou acreditar numa baixa que não houve.
/// </summary>
public sealed class BaixaComEnvioNaTelaTests
{
    private const string Convenio = "Petrobras";
    private const int Guia = 41;
    private static readonly DateTime Agora = new(2026, 11, 11, 10, 0, 0);

    private static TransacaoTiss Envio(EstadoTransacaoTiss estado, bool saiu = false, string? protocolo = null,
        SituacaoGuiaTiss situacao = SituacaoGuiaTiss.Pendente, string? numeroDaOperadora = null, string? motivo = null)
        => new()
        {
            Id = 9, Tipo = TipoTransacaoTiss.EnvioGuia, ConvenioCodigo = Convenio, Estado = estado, Protocolo = protocolo,
            EnvioIniciadoEm = saiu ? Agora : null, PedidoEm = Agora, PedidoPor = "secretaria",
            Guias = [new TransacaoTissGuia { Id = 1, CodigoId = Guia, NumeroGuiaPrestador = "C41", NumeroGuiaOperadora = numeroDaOperadora, Situacao = situacao, Motivo = motivo }]
        };

    private static LeituraDaBaixaComEnvio Ler(TransacaoTiss envio, int segundosEsperando = 0, string? numeroDaBaixa = null, string? falhaDaBaixa = null)
        => BaixaComEnvioNaTela.Ler(envio, Guia, Convenio, Agora.AddSeconds(-segundosEsperando), Agora, numeroDaBaixa, falhaDaBaixa);

    [Fact]
    public void Os_rotulos_dizem_o_que_acontece_e_levam_o_nome_do_convenio()
    {
        BaixaComEnvioNaTela.AcaoPrincipal(Convenio, reenvio: false).Should().Be("Enviar para Petrobras e dar baixa");
        BaixaComEnvioNaTela.AcaoPrincipal(Convenio, reenvio: true).Should().Be("Corrigir e enviar de novo para Petrobras");
        BaixaComEnvioNaTela.Subtitulo(Convenio).Should().Contain("Petrobras gera o número").And.Contain("baixa é registrada sozinha");
        BaixaComEnvioNaTela.LinhaDoNumero(Convenio).Should().Contain("não é digitado");
        BaixaComEnvioNaTela.PerguntaDoEnvio("Fulana de Teste", Convenio).Should()
            .Contain("Fulana de Teste").And.Contain("Petrobras").And.Contain("COBRANÇA").And.Contain("não há como desfazer");
    }

    [Fact]
    public void Enquanto_o_pedido_anda_a_tela_acompanha_e_os_passos_avancam()
    {
        var naFila = Ler(Envio(EstadoTransacaoTiss.Pendente));
        naFila.Fase.Should().Be(FaseDaBaixaComEnvio.NaFila);
        naFila.Acompanha.Should().BeTrue();
        naFila.PodeCancelarPedido.Should().BeTrue();
        naFila.CamposEditaveis.Should().BeFalse();
        naFila.Passos.Select(p => p.Estado).Should().Equal("feito", "atual", "pendente", "pendente");
        naFila.Passos.Select(p => p.Rotulo).Should().Equal("Guia conferida", "Na fila de envio", "Enviando para Petrobras", "Resposta recebida");

        var enviando = Ler(Envio(EstadoTransacaoTiss.EmEnvio, saiu: true));
        enviando.Fase.Should().Be(FaseDaBaixaComEnvio.Enviando);
        enviando.Acompanha.Should().BeTrue();
        enviando.PodeCancelarPedido.Should().BeFalse("o pedido já saiu da fila");
        enviando.Passos.Select(p => p.Estado).Should().Equal("feito", "feito", "atual", "pendente");
    }

    /// <summary>Sem serviço de envio o pedido fica na fila: passado o prazo, a tela diz — e dá três saídas.</summary>
    [Fact]
    public void Pedido_parado_alem_do_prazo_e_o_servico_que_nao_respondeu_com_tres_saidas()
    {
        Ler(Envio(EstadoTransacaoTiss.Pendente), segundosEsperando: 44).Fase.Should().Be(FaseDaBaixaComEnvio.NaFila);

        var parado = Ler(Envio(EstadoTransacaoTiss.Pendente), segundosEsperando: 45);
        parado.Fase.Should().Be(FaseDaBaixaComEnvio.ServicoSemResposta);
        parado.Titulo.Should().Be("O serviço de envio não respondeu.");
        parado.Tom.Should().Be("atencao");
        parado.Acompanha.Should().BeFalse("quem volta a acompanhar é o 'continuar esperando'");
        parado.PodeCancelarPedido.Should().BeTrue();
        parado.OfereceBaixaManual.Should().BeTrue();
        parado.Texto.Should().Contain("nada foi à operadora");
    }

    [Fact]
    public void Recebida_com_o_numero_da_operadora_e_baixada_mostra_o_numero_gerado_e_o_protocolo()
    {
        var recebida = Envio(EstadoTransacaoTiss.Concluida, true, "20260001", SituacaoGuiaTiss.Recebida, numeroDaOperadora: "PB900101");

        // Antes da baixa: a tela pode registrá-la (sozinha, ou pelo botão de quem reabriu).
        var semBaixa = Ler(recebida);
        semBaixa.Fase.Should().Be(FaseDaBaixaComEnvio.RecebidaSemBaixa);
        semBaixa.PodeRegistrarBaixa.Should().BeTrue();
        semBaixa.Acompanha.Should().BeFalse();

        var baixada = Ler(recebida, numeroDaBaixa: "PB900101");
        baixada.Fase.Should().Be(FaseDaBaixaComEnvio.Baixada);
        baixada.Titulo.Should().Be("Guia nº PB900101 gerada por Petrobras · protocolo 20260001. Baixa registrada.");
        baixada.Tom.Should().Be("sucesso");
        baixada.NumeroDaGuia.Should().Be("PB900101");
        baixada.Passos.Should().OnlyContain(p => p.Estado == "feito");
        baixada.PodeRegistrarBaixa.Should().BeFalse();
        baixada.OfereceBaixaManual.Should().BeFalse();
    }

    /// <summary>A operadora recebeu e NÃO devolveu o número dela: a frase diz a verdade sobre de quem é o número.</summary>
    [Fact]
    public void Recebida_sem_o_numero_da_operadora_diz_que_a_baixa_ficou_com_o_nosso_numero()
    {
        var baixada = Ler(Envio(EstadoTransacaoTiss.Concluida, true, "20260001", SituacaoGuiaTiss.Recebida), numeroDaBaixa: "C41");

        baixada.Fase.Should().Be(FaseDaBaixaComEnvio.Baixada);
        baixada.Titulo.Should().Be("Petrobras recebeu a guia (protocolo 20260001) e não informou o número dela; a baixa ficou com o nosso número C41.");
        baixada.Titulo.Should().NotContain("gerada por");
        baixada.Texto.Should().Contain("estorne a baixa");
    }

    /// <summary>A baixa automática recusada (o formato do número, por exemplo): recebida, NÃO baixada, e a tela diz o que fazer.</summary>
    [Fact]
    public void Baixa_recusada_depois_de_recebida_nao_manda_reenviar_e_oferece_a_baixa_manual()
    {
        var leitura = Ler(Envio(EstadoTransacaoTiss.Concluida, true, "20260001", SituacaoGuiaTiss.Recebida, numeroDaOperadora: "PB-90/01"),
            falhaDaBaixa: "Este convênio usa letras e números na guia, sem pontuação.");

        leitura.Fase.Should().Be(FaseDaBaixaComEnvio.RecebidaComBaixaRecusada);
        leitura.Titulo.Should().Contain("recebeu a guia").And.Contain("a baixa NÃO foi registrada");
        leitura.Texto.Should().Contain("sem pontuação").And.Contain("NÃO precisa ser enviada de novo").And.Contain("PB-90/01");
        leitura.CamposEditaveis.Should().BeFalse();
        leitura.OfereceBaixaManual.Should().BeTrue();
        leitura.PodeRegistrarBaixa.Should().BeFalse("repetir a mesma baixa daria a mesma recusa");
    }

    [Fact]
    public void Glosada_mostra_o_motivo_nao_baixa_nada_e_devolve_os_campos_para_corrigir()
    {
        var glosada = Ler(Envio(EstadoTransacaoTiss.Concluida, true, "20260001", SituacaoGuiaTiss.Glosada, motivo: "1001 - Número da carteira inválido"));

        glosada.Fase.Should().Be(FaseDaBaixaComEnvio.Glosada);
        glosada.Titulo.Should().Be("Petrobras recusou esta guia (glosa).");
        glosada.Texto.Should().Contain("1001 - Número da carteira inválido").And.Contain("Nada foi baixado");
        glosada.Tom.Should().Be("erro");
        glosada.CamposEditaveis.Should().BeTrue();
        glosada.PodeRegistrarBaixa.Should().BeFalse();
        glosada.NumeroDaGuia.Should().BeNull();
    }

    /// <summary>O estado que nunca oferece reenvio: nem campos editáveis, nem ação de enviar — só a conferência no portal e a baixa manual.</summary>
    [Fact]
    public void Nao_se_sabe_se_chegou_manda_conferir_no_portal_e_nao_devolve_a_acao_de_enviar()
    {
        var envio = Envio(EstadoTransacaoTiss.Falhou, saiu: true, situacao: SituacaoGuiaTiss.Falhou);
        envio.Erro = "sem resposta da operadora " + FilaTiss.FraseSemNovaTentativa;
        var incerto = Ler(envio);

        incerto.Fase.Should().Be(FaseDaBaixaComEnvio.NaoSeSabeSeChegou);
        incerto.Titulo.Should().Be("Não se sabe se Petrobras recebeu esta guia.");
        incerto.Texto.Should().Contain("Confira no portal da operadora ANTES de qualquer novo envio");
        incerto.Tom.Should().Be("erro");
        incerto.CamposEditaveis.Should().BeFalse("campos editáveis trariam de volta o botão de enviar");
        incerto.OfereceBaixaManual.Should().BeTrue();
        incerto.Acompanha.Should().BeFalse();
    }

    /// <summary>Nada chegou à operadora (cancelado por quem pediu, recusado pelo serviço, falhou antes de sair): a frase gravada, e a guia volta.</summary>
    [Fact]
    public void Pedido_que_nao_saiu_mostra_a_frase_gravada_e_devolve_a_guia_para_um_novo_envio()
    {
        var recusado = Envio(EstadoTransacaoTiss.Cancelada, situacao: SituacaoGuiaTiss.Falhou);
        recusado.Resultado = "O envio pelo web service está desligado neste servidor (TISS_ENVIO_HABILITADO). Nada foi enviado à operadora.";
        var leitura = Ler(recusado);
        leitura.Fase.Should().Be(FaseDaBaixaComEnvio.NaoEnviada);
        leitura.Texto.Should().Be(recusado.Resultado);
        leitura.CamposEditaveis.Should().BeTrue();

        var falhou = Envio(EstadoTransacaoTiss.Falhou, saiu: false, situacao: SituacaoGuiaTiss.Falhou);
        falhou.Erro = "Dados recusados antes do envio: CBO.";
        Ler(falhou).Should().Match<LeituraDaBaixaComEnvio>(l => l.Fase == FaseDaBaixaComEnvio.NaoEnviada && l.Texto == falhou.Erro && l.CamposEditaveis);
    }

    /// <summary>
    /// A operadora RESPONDEU recusando o lote (versão do padrão, login…): a mensagem saiu e a transação
    /// falhou, mas não há dúvida — o erro não traz a marca. Não é "confira no portal": é corrigir e reenviar.
    /// </summary>
    [Fact]
    public void Recusa_clara_da_operadora_nao_e_duvida_mostra_o_motivo_e_devolve_a_guia()
    {
        var recusado = Envio(EstadoTransacaoTiss.Falhou, saiu: true, situacao: SituacaoGuiaTiss.Falhou);
        recusado.Erro = "A operadora recusou o lote: 5028 - VERSAO DO PADRAO INVALIDA.";
        FilaTiss.NaoSeSabeSeChegou(recusado).Should().BeFalse();

        var leitura = Ler(recusado);
        leitura.Fase.Should().Be(FaseDaBaixaComEnvio.EnvioRecusado);
        leitura.Titulo.Should().Be("Petrobras recusou o envio.");
        leitura.Texto.Should().Contain("5028").And.Contain("Nada foi baixado").And.NotContain("portal");
        leitura.CamposEditaveis.Should().BeTrue();
        leitura.Tom.Should().Be("erro");
    }

    /// <summary>
    /// A dúvida se reconhece pela marca no erro gravado — e a marca vem no FIM do texto. Um erro
    /// comprido cortado no tamanho da coluna não pode perdê-la: a guia em dúvida viraria reenviável.
    /// </summary>
    [Fact]
    public void A_marca_da_duvida_sobrevive_ao_corte_do_erro_e_e_ela_que_separa_duvida_de_recusa()
    {
        FilaTiss.FraseSemNovaTentativa.Should().EndWith(FilaTiss.MarcaDeDuvida);
        FilaTiss.FraseEnvioInterrompido.Should().EndWith(FilaTiss.MarcaDeDuvida);

        var comprido = new string('x', 1500) + " " + FilaTiss.FraseSemNovaTentativa;
        var cortado = FilaTiss.CortarErro(comprido, 1000);
        cortado.Length.Should().BeLessThanOrEqualTo(1000);
        cortado.Should().EndWith(FilaTiss.FraseSemNovaTentativa);
        FilaTiss.CortarErro(new string('x', 1500), 1000).Should().HaveLength(1000).And.NotContain(FilaTiss.MarcaDeDuvida);
        FilaTiss.CortarErro("curto", 1000).Should().Be("curto");

        var t = Envio(EstadoTransacaoTiss.Falhou, saiu: true, situacao: SituacaoGuiaTiss.Falhou);
        t.Erro = cortado;
        FilaTiss.NaoSeSabeSeChegou(t).Should().BeTrue();
        t.Erro = FilaTiss.FraseEnvioInterrompido;
        FilaTiss.NaoSeSabeSeChegou(t).Should().BeTrue("o envio interrompido depois de carimbado também é dúvida");
        t.EnvioIniciadoEm = null;
        FilaTiss.NaoSeSabeSeChegou(t).Should().BeFalse("o que não saiu não deixou nada na operadora");
    }

    [Fact]
    public void Protocolada_sem_resposta_sobre_a_guia_nao_e_recebida_nem_pede_reenvio()
    {
        var leitura = Ler(Envio(EstadoTransacaoTiss.Concluida, true, "20260001", SituacaoGuiaTiss.Pendente));

        leitura.Fase.Should().Be(FaseDaBaixaComEnvio.SemRespostaSobreAGuia);
        leitura.Texto.Should().Contain("não envie de novo");
        leitura.CamposEditaveis.Should().BeFalse();
        leitura.PodeRegistrarBaixa.Should().BeFalse();
    }

    /// <summary>Reabrir a baixa de uma guia com envio: acompanha o pedido em vez de oferecer outro — salvo quando nada saiu.</summary>
    [Fact]
    public void Guia_com_envio_em_curso_recebida_glosada_ou_em_duvida_abre_no_acompanhamento()
    {
        foreach (var estado in new[] { EstadoDoEnvioDaGuia.NaFila, EstadoDoEnvioDaGuia.Enviada, EstadoDoEnvioDaGuia.Recebida, EstadoDoEnvioDaGuia.Glosada, EstadoDoEnvioDaGuia.Falhou })
            BaixaComEnvioNaTela.AbreNoAcompanhamento(new SeloDeEnvioTiss(estado, "x", null, 1)).Should().BeTrue($"{estado} tem pedido a acompanhar");

        BaixaComEnvioNaTela.AbreNoAcompanhamento(null).Should().BeFalse();
        BaixaComEnvioNaTela.AbreNoAcompanhamento(SeloDeEnvioTiss.NaoEnviada).Should().BeFalse();
        BaixaComEnvioNaTela.AbreNoAcompanhamento(new SeloDeEnvioTiss(EstadoDoEnvioDaGuia.Cancelada, "x")).Should().BeFalse();
    }

    // ==================== o nome devolvido na elegibilidade ====================

    [Fact]
    public void O_nome_devolvido_pela_operadora_e_mostrado_ao_lado_do_da_ficha_dizendo_se_confere()
    {
        const string resultado = "Elegível. Validade da carteira: 31/12/2027. Nome na operadora: JOSE DE TESTE.";
        EnvioTissNaTela.NomeNaOperadora(resultado).Should().Be("JOSE DE TESTE");

        // Maiúsculas e acento não são divergência: a operadora devolve "JOSE", a ficha diz "José".
        EnvioTissNaTela.ConferenciaDoNome(resultado, "José de Teste")!.Value
            .Should().Be(("Na operadora: JOSE DE TESTE · na ficha: José de Teste. Os nomes conferem.", true));

        var diferente = EnvioTissNaTela.ConferenciaDoNome(resultado, "Maria de Teste")!.Value;
        diferente.Confere.Should().BeFalse();
        diferente.Texto.Should().Contain("DIFERENTES").And.Contain("A ficha não foi alterada");

        // Sem nome na resposta, não há o que conferir.
        EnvioTissNaTela.NomeNaOperadora("Elegível.").Should().BeNull();
        EnvioTissNaTela.ConferenciaDoNome("Não elegível: 1002", "José").Should().BeNull();
    }
}
