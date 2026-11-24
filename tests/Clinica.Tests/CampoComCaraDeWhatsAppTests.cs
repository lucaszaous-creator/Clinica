using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A TELA COM CARA DE WHATSAPP (nov/2026, F1 — propostas v2 a v4 aprovadas pelo dono): as regras que a tela nova
/// AFIRMA e que moram na Application — a pílula "Sem resposta" da direção, as pílulas e os números delas, o ⋮ do
/// cabeçalho, o menu do balão com "Perguntar ao profissional", o "/" do campo, o 😊 e os botões da faixa fixada.
/// Dados fictícios.
/// </summary>
public sealed class CampoComCaraDeWhatsAppTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 24, 10, 0, 0, TimeSpan.FromHours(-3));
    private static readonly AcessoNoMenu Tudo = new(PodeEscrever: true, PodeVerFicha: true, PodeGuardarNaFicha: true, SemDor: true);

    private static AppConversationSummary Linha(string id, string? agente = null, bool encerrada = false, string direcao = "in",
        int minutos = 10, bool menu = false, int naoLidas = 0, bool recebeu = true)
        => new(id, "Paciente " + id, "5522990000000", "whatsapp", "5522333300000", "Recepção", agente, encerrada, 1,
            Agora.AddMinutes(-minutos), recebeu ? Agora.AddMinutes(-minutos) : default, null, "", naoLidas, "oi", menu, false,
            null, null, PreviewDirection: direcao);

    private static AppConversation Conversa(string? agente = null, bool encerrada = false, int mensagens = 1, AppPendingTransfer? pedido = null)
        => new("c1", "Helena Costa", "5522990000000", "whatsapp", "5522333300000", "Recepção", agente, encerrada, 1, Agora, Agora,
            null, "", Enumerable.Range(0, mensagens).Select(i => new AppMessage($"m{i}", "in", "oi", "TEXT", null, Agora, "received")).ToArray(),
            [], [], [], null, pedido);

    // ---------------------------------------------------------------- Sem resposta (a direção)

    [Fact]
    public void Sem_resposta_traz_todas_as_que_esperam_a_clinica_de_quem_quer_que_sejam_a_mais_antiga_primeiro()
    {
        var conversas = new[]
        {
            Linha("da-bia", agente: "bia", minutos: 20),
            Linha("minha", agente: "eu", minutos: 40),
            Linha("sem-dono", minutos: 5),
            Linha("respondida", agente: "bia", direcao: "out"),
            Linha("encerrada", encerrada: true),
            Linha("no-menu", menu: true),
            Linha("nunca-escreveu", recebeu: false)
        };

        ConversasNaTela.Filtrar(conversas, FiltroDasConversas.SemResposta, "eu").Select(c => c.Id)
            .Should().Equal("minha", "da-bia", "sem-dono");
        ConversasNaTela.Contagens(conversas, "eu").SemResposta.Should().Be(3);
        ConversasNaTela.Rotulo(FiltroDasConversas.SemResposta).Should().Be("Sem resposta");
        ConversasNaTela.Vazio(FiltroDasConversas.SemResposta, comBusca: false).Titulo.Should().Be("Ninguém esperando resposta");
    }

    [Fact]
    public void As_pilulas_sao_as_do_WhatsApp_e_so_mostram_numero_onde_ha_o_que_fazer()
    {
        ConversasNaTela.Pilulas.Select(p => p.Rotulo).Should().Equal("Tudo", "Não lidas", "Minhas", "Sem resposta");
        var c = new ContagemDasConversas(Novas: 1, Minhas: 3, Todas: 9, NaoLidas: 7, SemResposta: 120);
        ConversasNaTela.Pilulas.Select(p => ConversasNaTela.ContagemDaPilula(p, c)).Should().Equal(null, "7", null, "99+");
        ConversasNaTela.ContagemDaPilula(ConversasNaTela.Pilulas[1], c with { NaoLidas = 0 }).Should().BeNull("zero não vira número");
        ConversasNaTela.PilulaAtual(FiltroDasConversas.Todas, soNaoLidas: true)!.Rotulo.Should().Be("Não lidas");
        ConversasNaTela.PilulaAtual(FiltroDasConversas.Minhas, soNaoLidas: true)!.Rotulo.Should().Be("Minhas");
    }

    // ---------------------------------------------------------------- o ⋮ do cabeçalho

    [Fact]
    public void O_menu_do_cabecalho_reune_os_botoes_que_saíram_e_quem_nao_pode_ve_o_motivo()
    {
        var minha = MenuDoWhatsApp.DoCabecalho(Conversa(agente: "eu"), "eu", 3, Tudo);
        minha.Select(i => i.Acao).Should().Equal(AcaoNaConversa.Devolver, AcaoNaConversa.Transferir, AcaoNaConversa.NotaInterna,
            AcaoNaConversa.Buscar, AcaoNaConversa.DadosDoContato, AcaoNaConversa.Etiquetas, AcaoNaConversa.Resumir);
        minha.Should().OnlyContain(i => i.Habilitado);

        var daBia = MenuDoWhatsApp.DoCabecalho(Conversa(agente: "bia"), "eu", 3, Tudo);
        daBia.Single(i => i.Acao == AcaoNaConversa.Assumir).Motivo.Should().Contain("peça a transferência");
        daBia.Single(i => i.Acao == AcaoNaConversa.Transferir).Motivo.Should().Be(MenuDoWhatsApp.SoComVoce);
        daBia.Single(i => i.Acao == AcaoNaConversa.NotaInterna).Habilitado.Should().BeTrue("todos escrevem: nota é de toda a equipe");

        var semEscrever = MenuDoWhatsApp.DoCabecalho(Conversa(agente: "eu"), "eu", 3, Tudo with { PodeEscrever = false });
        semEscrever.Single(i => i.Acao == AcaoNaConversa.Buscar).Habilitado.Should().BeTrue("buscar só lê");
        semEscrever.Single(i => i.Acao == AcaoNaConversa.DadosDoContato).Habilitado.Should().BeTrue();
        semEscrever.Single(i => i.Acao == AcaoNaConversa.Devolver).Motivo.Should().Contain("Seu acesso");
    }

    [Fact]
    public void Sem_encerrar_nem_reabrir_a_conversa_que_o_CRM_separou_e_igual_as_outras_e_o_caso_sem_conversa_so_tem_os_dados()
    {
        // Pedido do dono (24/11): encerrar some da tela. A conversa que o CRM separou por inatividade tem o mesmo menu de todas.
        var encerrada = MenuDoWhatsApp.DoCabecalho(Conversa(agente: "eu", encerrada: true, mensagens: 0), "eu", 0, Tudo);
        encerrada.Select(i => i.Acao).Should().Equal(AcaoNaConversa.Devolver, AcaoNaConversa.Transferir, AcaoNaConversa.NotaInterna,
            AcaoNaConversa.Buscar, AcaoNaConversa.DadosDoContato, AcaoNaConversa.Etiquetas, AcaoNaConversa.Resumir);
        encerrada.Single(i => i.Acao == AcaoNaConversa.Resumir).Motivo.Should().Be("Ainda não há mensagem para resumir");

        MenuDoWhatsApp.DoCabecalho(Conversa(agente: "eu"), "eu", 3, Tudo with { SemDor = false }).Select(i => i.Acao)
            .Should().NotContain(AcaoNaConversa.Resumir);
        MenuDoWhatsApp.DoCabecalho(null, "eu", 0, Tudo).Select(i => i.Acao).Should().Equal(AcaoNaConversa.DadosDoContato);
        MenuDoWhatsApp.DoCabecalho(Conversa(agente: "eu", pedido: new("t1", "eu", "bia", "", Agora)), "eu", 3, Tudo)
            .Single(i => i.Acao == AcaoNaConversa.Transferir).Motivo.Should().Contain("aguardando o aceite");
    }

    [Fact]
    public void O_clipe_junta_arquivo_modelos_e_respostas_e_o_SMS_so_tem_as_respostas()
    {
        MenuDoWhatsApp.DoClipe(sms: false, anexosLigados: true, Tudo).Select(i => i.Acao)
            .Should().Equal(AcaoDoClipe.Arquivo, AcaoDoClipe.Modelos, AcaoDoClipe.Rapidas);
        MenuDoWhatsApp.DoClipe(sms: true, anexosLigados: true, Tudo).Select(i => i.Acao).Should().Equal(AcaoDoClipe.Rapidas);
        MenuDoWhatsApp.DoClipe(false, anexosLigados: false, Tudo).Single(i => i.Acao == AcaoDoClipe.Arquivo).Motivo.Should().Contain("desligado");
        MenuDoWhatsApp.DoClipe(false, true, Tudo with { PodeEscrever = false }).Single(i => i.Acao == AcaoDoClipe.Rapidas).Habilitado
            .Should().BeTrue("a resposta rápida só vai para o campo");
    }

    // ---------------------------------------------------------------- o menu do balão

    [Fact]
    public void O_balao_do_paciente_abre_com_os_quatro_do_WhatsApp_e_pergunta_ao_profissional()
    {
        var recebida = new ItemDaConversa(TipoNaLinhaDoTempo.Recebida, "Posso levar o exame?", "09:02", Id: "wamid.1", At: Agora);
        var menu = MenuDoWhatsApp.DaMensagem(recebida, "", janelaLivre: true, sms: false, minha: true, fichaConferida: true, Tudo, Agora);
        menu.Take(4).Select(i => i.Rotulo).Should().Equal("Responder", "Copiar", "Nota para a equipe", "Perguntar ao profissional");
        menu.Should().OnlyContain(i => i.Habilitado);

        var enviada = new ItemDaConversa(TipoNaLinhaDoTempo.Enviada, "Pode sim", "09:03", "Bia", Id: "m2", At: Agora);
        MenuDoWhatsApp.DaMensagem(enviada, "", true, false, true, true, Tudo).Select(i => i.Acao)
            .Should().NotContain(AcaoNaMensagem.PerguntarAoProfissional, "só a mensagem do paciente vai ao profissional");
        MenuDoWhatsApp.DaMensagem(recebida, "", true, false, true, true, Tudo with { PodeEscrever = false })
            .Single(i => i.Acao == AcaoNaMensagem.PerguntarAoProfissional).Motivo.Should().Contain("Seu acesso");
    }

    // ---------------------------------------------------------------- o "/" do campo

    private static readonly AppQuickReply[] Rapidas =
    [
        new("q1", "Endereço", "Rua Exemplo, 100 · Centro"),
        new("q2", "Remarcação", "Podemos remarcar para outro dia?"),
        new("q3", "Antiga", "não usar", Archived: true)
    ];

    private static AppTemplate Modelo(string nome, bool liberado = true, string motivo = "")
        => new(nome, "pt_BR", "APPROVED", "Olá {{1}}, sua consulta foi remarcada.", 1, liberado, motivo, [], "UTILITY");

    [Fact]
    public void O_termo_da_barra_so_existe_com_a_barra_no_comeco_e_numa_linha_so()
    {
        ConversasNaTela.TermoDaBarra("/rem").Should().Be("rem");
        ConversasNaTela.TermoDaBarra("/").Should().Be("");
        ConversasNaTela.TermoDaBarra("Volte dia 15/12").Should().BeNull();
        ConversasNaTela.TermoDaBarra("/rem\nmais").Should().BeNull();
        ConversasNaTela.TermoDaBarra(null).Should().BeNull();
    }

    [Fact]
    public void A_barra_junta_respostas_e_modelos_sem_acento_e_respeita_o_SMS()
    {
        var modelos = new[] { Modelo("remarcacao_consulta"), Modelo("lembrete", liberado: false, motivo: "Tem cabeçalho de mídia") };

        var achou = ConversasNaTela.OpcoesDaBarra("remarcacao", Rapidas, modelos, sms: false);
        achou.Select(o => o.Tipo).Should().Equal(TipoNaBarra.Rapida, TipoNaBarra.Modelo);
        achou[0].Rapida!.Id.Should().Be("q2", "sem acento: \"remarcacao\" acha \"Remarcação\"");

        var tudo = ConversasNaTela.OpcoesDaBarra("", Rapidas, modelos, sms: false);
        tudo.Select(o => o.Titulo).Should().NotContain("Antiga", "resposta arquivada não aparece");
        tudo.Last().Motivo.Should().Be("Tem cabeçalho de mídia");
        tudo.Last().Habilitada.Should().BeFalse();

        ConversasNaTela.OpcoesDaBarra("", Rapidas, modelos, sms: true).Should().OnlyContain(o => o.Tipo == TipoNaBarra.Rapida, "SMS não tem modelo");
        ConversasNaTela.VazioDaBarra("xyz", true, false).Should().Contain("\"xyz\"");
    }

    [Fact]
    public void A_barra_mostra_no_maximo_oito()
    {
        var muitas = Enumerable.Range(1, 12).Select(i => new AppQuickReply($"q{i}", $"Resposta {i}", "texto")).ToArray();
        ConversasNaTela.OpcoesDaBarra("", muitas, null, sms: false).Should().HaveCount(ConversasNaTela.OpcoesNaBarra).And.HaveCount(8);
    }

    [Fact]
    public void O_emoji_entra_no_fim_com_um_espaco_so()
    {
        ConversasNaTela.ComEmoji("", "😊").Should().Be("😊");
        ConversasNaTela.ComEmoji("Até quarta", "😊").Should().Be("Até quarta 😊");
        ConversasNaTela.ComEmoji("Até quarta ", "😊").Should().Be("Até quarta 😊");
        ConversasNaTela.EmojisDoCampo.Should().HaveCount(16).And.OnlyHaveUniqueItems();
    }

    // ---------------------------------------------------------------- a faixa fixada

    [Fact]
    public void A_faixa_mostra_dois_botoes_o_primario_primeiro_e_o_resto_fica_no_mais()
    {
        BotaoPedido B(BotaoDaAcao a, bool primario = false) => new(a, a.ToString(), Primario: primario);
        var dois = new AcaoPedida(TipoDaAcaoPedida.NaoVem, "atencao", "Avisou que não vem", "", [B(BotaoDaAcao.Remarcar, true), B(BotaoDaAcao.ManterHorario)]);
        ConversasNaTela.DividirBotoes(dois).NoMais.Should().BeEmpty();

        var cinco = new AcaoPedida(TipoDaAcaoPedida.RetencaoRespondeu, "info", "Como foi?", "",
            [B(BotaoDaAcao.Desfecho), B(BotaoDaAcao.JaLiguei), B(BotaoDaAcao.Remarcar, true), B(BotaoDaAcao.Desfecho), B(BotaoDaAcao.NotaInterna)]);
        var faixa = ConversasNaTela.DividirBotoes(cinco);
        faixa.AVista.Select(b => b.Acao).Should().Equal(BotaoDaAcao.Desfecho, BotaoDaAcao.Remarcar);
        faixa.AVista.Should().Contain(b => b.Primario);
        faixa.NoMais.Select(b => b.Acao).Should().Equal(BotaoDaAcao.JaLiguei, BotaoDaAcao.Desfecho, BotaoDaAcao.NotaInterna);
        (faixa.AVista.Count + faixa.NoMais.Count).Should().Be(5, "nenhum botão do aviso some");
    }
}
