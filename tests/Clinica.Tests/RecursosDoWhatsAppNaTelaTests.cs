using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A v3 E A v4 NA TELA (nov/2026, F3 — contrato da F2): o que o balão, a lista e o campo afirmam sobre formatação, reação,
/// ★, fixar, etiquetas, "escrevendo", rascunho, agendadas, "Tentar de novo" e as mensagens automáticas compactas (pedido do
/// dono, 24/11). Dados fictícios.
/// </summary>
public sealed class RecursosDoWhatsAppNaTelaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 24, 10, 0, 0, TimeSpan.FromHours(-3));
    private static readonly AcessoNoMenu Tudo = new(PodeEscrever: true, PodeVerFicha: true, PodeGuardarNaFicha: true, SemDor: true);

    private static AppMessage Msg(string id, string direcao, string texto, string? agente = null, string status = "delivered", int minutos = 0,
        AppReaction[]? reacoes = null, string? reacaoA = null, string? retryOf = null, AppTemplateSelection? modelo = null)
        => new(id, direcao, texto, "TEXT", agente, Agora.AddMinutes(minutos), status, Template: modelo, Reactions: reacoes, ReactionTo: reacaoA,
            RetryOf: retryOf);

    private static AppConversation Conversa(params AppMessage[] mensagens)
        => new("c1", "Helena Costa", "5522990000000", "whatsapp", "5522333300000", "Recepção", "eu", false, 1, Agora, Agora,
            null, "", mensagens, [], [], [], null);

    // ---------------------------------------------------------------- formatação do WhatsApp

    [Fact]
    public void A_formatacao_do_WhatsApp_vira_negrito_italico_e_tachado_sem_os_simbolos()
    {
        var t = ConversasNaTela.Formatar("Preparo: *venha cedo* e _roupa leve_, ~quarta~ quinta.");
        string.Concat(t.Select(x => x.Texto)).Should().Be("Preparo: venha cedo e roupa leve, quarta quinta.");
        t.Single(x => x.Negrito).Texto.Should().Be("venha cedo");
        t.Single(x => x.Italico).Texto.Should().Be("roupa leve");
        t.Single(x => x.Tachado).Texto.Should().Be("quarta");

        ConversasNaTela.Formatar("2 * 3 = 6 e 4*5").Should().ContainSingle().Which.Negrito.Should().BeFalse("símbolo solto não formata");
        ConversasNaTela.TextoSemFormatacao("encaminhado para *Clínica SemDor*.").Should().Be("encaminhado para Clínica SemDor.");
    }

    // ---------------------------------------------------------------- reação, ★ e "Tentar de novo"

    [Fact]
    public void A_reacao_aparece_no_balao_que_reagiu_e_nunca_sozinha()
    {
        var c = Conversa(
            Msg("m1", "in", "Posso levar o exame?", reacoes: [new("👍", "eu", false, Agora), new("👍", null, true, Agora)]),
            Msg("m2", "in", "Reagiu com 👍", reacaoA: "m9"));
        var itens = ConversasNaTela.LinhaDoTempo(c, recebidasJaLidas: 2, Agora);

        itens.Should().NotContain(i => i.Texto.Contains("Reagiu com"));
        itens.Single(i => i.Id == "m1").Reacoes.Should().ContainSingle().Which.Should().Be(new ReacaoNoBalao("👍", 2, true));
    }

    [Fact]
    public void So_se_reage_na_janela_e_em_mensagem_de_ate_30_dias()
    {
        var m = new ItemDaConversa(TipoNaLinhaDoTempo.Recebida, "oi", Id: "m1", At: Agora.AddDays(-2));
        ConversasNaTela.MotivoParaNaoReagir(m, janelaLivre: true, sms: false, Agora).Should().BeNull();
        ConversasNaTela.MotivoParaNaoReagir(m, janelaLivre: false, sms: false, Agora).Should().Contain("Janela de 24 h");
        ConversasNaTela.MotivoParaNaoReagir(m with { At = Agora.AddDays(-31) }, true, false, Agora).Should().Contain("30 dias");
        ConversasNaTela.MotivoParaNaoReagir(m, true, sms: true, Agora).Should().Contain("SMS");

        var menu = MenuDoWhatsApp.DaMensagem(m, "", janelaLivre: false, sms: false, minha: true, fichaConferida: true, Tudo, Agora);
        menu.Single(i => i.Acao == AcaoNaMensagem.Reagir).Motivo.Should().Contain("Janela de 24 h");
        menu.Single(i => i.Acao == AcaoNaMensagem.Estrelar).Rotulo.Should().Be("Marcar como importante ★");
        MenuDoWhatsApp.DaMensagem(m with { Estrela = true }, "", true, false, true, true, Tudo, Agora)
            .Single(i => i.Acao == AcaoNaMensagem.Estrelar).Rotulo.Should().Be("Tirar a estrela");
    }

    [Fact]
    public void A_que_falhou_pede_tentar_de_novo_ate_a_nova_sair()
    {
        var falhou = Msg("m1", "out", "Confirmado!", agente: "eu", status: "failed");
        ConversasNaTela.PedeTentarDeNovo(falhou, [falhou]).Should().BeTrue();
        ConversasNaTela.PedeTentarDeNovo(falhou, [falhou, Msg("m2", "out", "Confirmado!", agente: "eu", retryOf: "m1")]).Should().BeFalse();
        ConversasNaTela.PedeTentarDeNovo(Msg("m3", "out", "ok", agente: "eu"), []).Should().BeFalse("entregue não pede");
    }

    // ---------------------------------------------------------------- a lista: fixadas, rascunho, etiquetas, escrevendo

    [Fact]
    public void Fixadas_vem_primeiro_na_ordem_em_que_fixou_e_o_maximo_explica()
    {
        ConversasNaTela.FixadasPrimeiro(["a", "b", "c", "d"], x => x, ["d", "b"]).Should().Equal("d", "b", "a", "c");
        ConversasNaTela.MotivoParaNaoFixar(jaFixada: false, fixadas: 3, maximo: 3).Should().Contain("desafixe");
        ConversasNaTela.MotivoParaNaoFixar(jaFixada: true, fixadas: 3, maximo: 3).Should().BeNull();

        var linha = new AppConversationSummary("c1", "Ana", "5522990000000", "whatsapp", "x", "Recepção", "eu", false, 1, Agora, Agora,
            null, "", 0, "oi", false, false, null, null);
        MenuDoWhatsApp.DaConversa(linha, "eu", Tudo, fixadas: 3, maximoDeFixadas: 3).Single(i => i.Acao == AcaoNaConversa.Fixar).Habilitado.Should().BeFalse();
        MenuDoWhatsApp.DaConversa(linha with { Pinned = true }, "eu", Tudo).Should().Contain(i => i.Acao == AcaoNaConversa.Desafixar);
    }

    [Fact]
    public void Rascunho_escrevendo_e_etiquetas_na_tela()
    {
        ConversasNaTela.PreviaDoRascunho("  Posso remarcar\npara sexta?  ").Should().Be("Posso remarcar para sexta?");
        ConversasNaTela.PreviaDoRascunho("/rem").Should().BeNull("o \"/\" é busca, não rascunho");
        ConversasNaTela.PreviaDoRascunho("   ").Should().BeNull();

        var nomes = new Dictionary<string, string> { ["bia"] = "Bia Souza", ["carla"] = "Carla Dias", ["eu"] = "Eu Mesma" };
        ConversasNaTela.QuemEscreve(["eu"], "eu", nomes).Should().BeNull("quem pediu não aparece");
        ConversasNaTela.QuemEscreve(["bia", "eu"], "eu", nomes).Should().Be("Bia está escrevendo…");
        ConversasNaTela.QuemEscreve(["bia", "carla"], "eu", nomes).Should().Be("Bia e Carla estão escrevendo…");

        AppLabel[] daClinica = [new("l1", "Agendar", false, 1), new("l2", "Convênio", false, 1), new("l3", "Antiga", true, 1)];
        ConversasNaTela.EtiquetasDa(["l3", "l1"], daClinica).Select(e => e.Name).Should().Equal("Agendar", "Antiga");
        ConversasNaTela.TomDaEtiqueta("l1").Should().Be(ConversasNaTela.TomDaEtiqueta("l1")).And.BeOneOf("laranja", "verde", "vermelho", "azul");
    }

    // ---------------------------------------------------------------- clipe e enviar mais tarde

    [Fact]
    public void O_clipe_oferece_endereco_contato_e_servicos_so_na_janela()
    {
        var opcoes = new AppShareOptions(new("Clínica SemDor", "Rua Exemplo, 100", -22.3, -41.7),
            [new("p1", "Dr. Exemplo", "5522990000001", "Acupuntura", false, 1)], [new("s1", "Acupuntura", "Sessão", null, false, 1)]);
        var dentro = MenuDoWhatsApp.DoClipe(false, true, Tudo, opcoes, janelaLivre: true);
        dentro.Select(i => i.Acao).Should().Contain([AcaoDoClipe.Endereco, AcaoDoClipe.Contato, AcaoDoClipe.Servicos]);
        dentro.Should().OnlyContain(i => i.Habilitado);
        MenuDoWhatsApp.DoClipe(false, true, Tudo, opcoes, janelaLivre: false).Single(i => i.Acao == AcaoDoClipe.Endereco).Motivo
            .Should().Be("Só dentro da janela de 24 h");
        MenuDoWhatsApp.DoClipe(true, true, Tudo, opcoes).Select(i => i.Acao).Should().NotContain(AcaoDoClipe.Endereco, "SMS não manda localização");
        MenuDoWhatsApp.DoClipe(false, true, Tudo, new AppShareOptions(null, [], [])).Select(i => i.Acao).Should().NotContain(AcaoDoClipe.Endereco);
    }

    [Fact]
    public void Enviar_mais_tarde_so_sugere_horarios_dentro_da_janela_e_a_agendada_diz_quando()
    {
        var fim = Agora.AddHours(23);
        ConversasNaTela.HorariosSugeridos(Agora, fim).Select(h => h.Rotulo).Should().Equal("Hoje, 14:00", "Hoje, 17:00", "Amanhã, 08:00");
        ConversasNaTela.HorariosSugeridos(Agora, null).Should().BeEmpty("fora da janela, só modelo");

        var agendada = new AppScheduledMessage("s1", "eu", "Bom dia!", Agora.AddDays(1).AddHours(-2), "agendada", Agora);
        ConversasNaTela.TextoDaAgendada(agendada, Agora).Should().Be("Agendada para amanhã às 08:00");
        ConversasNaTela.TextoDaAgendada(agendada with { State = "falhou", Error = "Janela fechou" }, Agora).Should().Be("Janela fechou");
    }

    // ---------------------------------------------------------------- automáticas compactas

    [Fact]
    public void O_menu_automatico_vira_uma_linha_com_o_que_o_paciente_escolheu()
    {
        const string menu = "Olá! Escolha o atendimento:\n\n1. Clínica Exemplo\n_Clínica da dor._\n\n2. Exemplo Acupuntura\n_Acupuntura._";
        var enviado = Msg("m1", "out", menu, agente: "automation");
        ConversasNaTela.EhAutomatica(enviado).Should().BeTrue();
        ConversasNaTela.EhAutomatica(Msg("m2", "out", "oi", agente: "bia")).Should().BeFalse("da equipe");

        ConversasNaTela.ResumoDaAutomatica(enviado, Msg("r1", "in", "Exemplo Acupuntura")).Should().Be("Menu enviado · paciente escolheu Exemplo Acupuntura");
        ConversasNaTela.ResumoDaAutomatica(enviado, Msg("r2", "in", "1")).Should().Be("Menu enviado · paciente escolheu Clínica Exemplo");
        ConversasNaTela.ResumoDaAutomatica(enviado, Msg("r3", "in", "Quero marcar uma consulta para amanhã")).Should().Be("Menu enviado");
        ConversasNaTela.ResumoDaAutomatica(Msg("m3", "out", "Certo! Seu atendimento foi encaminhado para *Clínica Exemplo*. Em instantes a equipe continua."), null)
            .Should().Be("Seu atendimento foi encaminhado para Clínica Exemplo");
        ConversasNaTela.ResumoDaAutomatica(Msg("m4", "out", "Lembrete", modelo: new("semdor_lembrete", "pt_BR", [])), null).Should().Be("Lembrete enviado");

        var itens = ConversasNaTela.LinhaDoTempo(Conversa(enviado, Msg("r1", "in", "2", minutos: 1)), 2, Agora);
        itens.Single(i => i.Id == "m1").Automatica.Should().Be("Menu enviado · paciente escolheu Exemplo Acupuntura");
    }
}
