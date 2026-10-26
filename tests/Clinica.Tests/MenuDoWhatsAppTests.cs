using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O BOTÃO DIREITO da aba WhatsApp (out/2026): qual item aparece, em que ordem, e por que um deles está desabilitado —
/// a regra do <see cref="MenuDoWhatsApp"/>. A tela só liga cada item ao handler que já existe
/// (<c>RegrasDoWhatsAppNaTelaTests</c> clica dois deles).
/// </summary>
public sealed class MenuDoWhatsAppTests
{
    private static readonly AcessoNoMenu Tudo = new(PodeEscrever: true, PodeVerFicha: true, PodeGuardarNaFicha: true, SemDor: true);
    private static readonly DateTimeOffset Agora = new(2026, 10, 25, 10, 0, 0, TimeSpan.FromHours(-3));

    private static AppConversationSummary Linha(string? agente = null, bool encerrada = false, int naoLidas = 0, int? ficha = null,
        AppPendingTransfer? pedido = null, string contato = "5522998999334", bool recebeu = true)
        => new("c1", "Jamili", contato, "whatsapp", "5522333300000", "Recepção", agente, encerrada, 3, Agora,
            recebeu ? Agora.AddMinutes(-5) : default, ficha, ficha is null ? "" : "Jamile Leandro Barcelos", naoLidas, "oi", false, false,
            null, null, PendingTransfer: pedido);

    private static IEnumerable<AcaoNaConversa> Acoes(IReadOnlyList<ItemDeMenu<AcaoNaConversa>> itens) => itens.Select(i => i.Acao);

    private static ItemDeMenu<T> Item<T>(IReadOnlyList<ItemDeMenu<T>> itens, T acao) => itens.Single(i => EqualityComparer<T>.Default.Equals(i.Acao, acao));

    [Fact]
    public void Conversa_da_fila_oferece_assumir_e_o_que_so_quem_esta_com_ela_faz_vem_desabilitado_com_o_motivo()
    {
        var menu = MenuDoWhatsApp.DaConversa(Linha(), "ana", Tudo);

        Acoes(menu).Should().Equal(AcaoNaConversa.Abrir, AcaoNaConversa.Assumir, AcaoNaConversa.Transferir, AcaoNaConversa.NotaInterna,
            AcaoNaConversa.MarcarNaoLida, AcaoNaConversa.CopiarTelefone, AcaoNaConversa.ConferirFicha, AcaoNaConversa.Resumir, AcaoNaConversa.Encerrar);
        Item(menu, AcaoNaConversa.Assumir).Habilitado.Should().BeTrue();
        Item(menu, AcaoNaConversa.Transferir).Motivo.Should().Be(MenuDoWhatsApp.SoComVoce);
        Item(menu, AcaoNaConversa.Encerrar).Motivo.Should().Be(MenuDoWhatsApp.SoComVoce);
        Item(menu, AcaoNaConversa.Encerrar).Perigo.Should().BeTrue();
    }

    [Fact]
    public void Conversa_minha_devolve_transfere_e_encerra_e_de_outra_pessoa_pede_a_transferencia()
    {
        var minha = MenuDoWhatsApp.DaConversa(Linha(agente: "ana"), "ana", Tudo);
        Acoes(minha).Should().Contain(AcaoNaConversa.Devolver).And.NotContain(AcaoNaConversa.Assumir);
        Item(minha, AcaoNaConversa.Transferir).Habilitado.Should().BeTrue();
        Item(minha, AcaoNaConversa.Encerrar).Habilitado.Should().BeTrue();

        var comPedido = MenuDoWhatsApp.DaConversa(Linha(agente: "ana", pedido: new("t1", "ana", "bruna", "", Agora)), "ana", Tudo);
        Item(comPedido, AcaoNaConversa.Transferir).Motivo.Should().Contain("aguardando o aceite");

        var daBruna = MenuDoWhatsApp.DaConversa(Linha(agente: "bruna"), "ana", Tudo);
        Item(daBruna, AcaoNaConversa.Assumir).Motivo.Should().Contain("peça a transferência");
    }

    [Fact]
    public void Encerrada_reabre_e_nao_oferece_transferir_nota_nem_encerrar()
    {
        var menu = MenuDoWhatsApp.DaConversa(Linha(encerrada: true), "ana", Tudo);
        Acoes(menu).Should().Contain(AcaoNaConversa.Reabrir)
            .And.NotContain([AcaoNaConversa.Assumir, AcaoNaConversa.Transferir, AcaoNaConversa.NotaInterna, AcaoNaConversa.Encerrar]);
    }

    [Fact]
    public void A_leitura_alterna_entre_lida_e_nao_lida_e_nao_pede_o_bit_de_escrever()
    {
        var semEscrever = Tudo with { PodeEscrever = false };
        Item(MenuDoWhatsApp.DaConversa(Linha(naoLidas: 2), "ana", semEscrever), AcaoNaConversa.MarcarLida).Habilitado.Should().BeTrue();
        Item(MenuDoWhatsApp.DaConversa(Linha(), "ana", semEscrever), AcaoNaConversa.MarcarNaoLida).Habilitado.Should().BeTrue();
        Item(MenuDoWhatsApp.DaConversa(Linha(recebeu: false), "ana", Tudo), AcaoNaConversa.MarcarNaoLida).Motivo
            .Should().Be("Ainda não há mensagem recebida");
        // O que grava pede o bit, com o motivo.
        Item(MenuDoWhatsApp.DaConversa(Linha(), "ana", semEscrever), AcaoNaConversa.Assumir).Motivo.Should().Contain("Seu acesso");
    }

    [Fact]
    public void Com_ficha_abre_a_ficha_sem_ficha_liga_e_sem_SemDor_nao_ha_resumo()
    {
        var comFicha = MenuDoWhatsApp.DaConversa(Linha(ficha: 7), "ana", Tudo with { SemDor = false });
        Acoes(comFicha).Should().Contain(AcaoNaConversa.AbrirFicha).And.NotContain([AcaoNaConversa.ConferirFicha, AcaoNaConversa.Resumir]);
        Item(MenuDoWhatsApp.DaConversa(Linha(ficha: 7), "ana", Tudo with { PodeVerFicha = false }), AcaoNaConversa.AbrirFicha).Habilitado
            .Should().BeFalse();
        Item(MenuDoWhatsApp.DaConversa(Linha(contato: "abc"), "ana", Tudo), AcaoNaConversa.CopiarTelefone).Motivo.Should().Be("Sem telefone");
    }

    // ------------------------------------------------------------ a mensagem

    private static ItemDaConversa Recebida(string texto = "Serve sim", MidiaDaMensagem? midia = null)
        => new(TipoNaLinhaDoTempo.Recebida, texto, "17:02", Id: "wamid.1", At: Agora, Midia: midia);

    private static ItemDaConversa Enviada(EstadoDaEntrega estado, string texto = "Fica combinado", string? modelo = null)
        => new(TipoNaLinhaDoTempo.Enviada, texto, "17:03", "Ana Paula", Tick: new(estado, estado.ToString(), "neutro"), Modelo: modelo,
            Id: "m2", At: Agora, Erro: estado == EstadoDaEntrega.NaoConfirmada ? "O WhatsApp não confirmou a entrega." : null);

    [Fact]
    public void Recebida_com_a_janela_aberta_responde_citando_e_copia()
    {
        var menu = MenuDoWhatsApp.DaMensagem(Recebida(), "", janelaLivre: true, sms: false, minha: true, fichaConferida: true, Tudo);
        menu.Select(i => i.Acao).Should().Equal(AcaoNaMensagem.Responder, AcaoNaMensagem.CopiarTexto, AcaoNaMensagem.CopiarComHora, AcaoNaMensagem.NotaSobre);
        menu.Should().OnlyContain(i => i.Habilitado);
    }

    [Fact]
    public void Citar_nao_cabe_no_SMS_nem_com_a_janela_fechada_e_a_selecao_aparece_so_quando_existe()
    {
        Item(MenuDoWhatsApp.DaMensagem(Recebida(), "", true, sms: true, true, true, Tudo), AcaoNaMensagem.Responder).Motivo.Should().Be("O SMS não cita mensagens");
        Item(MenuDoWhatsApp.DaMensagem(Recebida(), "", janelaLivre: false, false, true, true, Tudo), AcaoNaMensagem.Responder).Motivo.Should().Contain("Janela de 24 h fechada");
        MenuDoWhatsApp.DaMensagem(Recebida(), "Serve", true, false, true, true, Tudo).Select(i => i.Acao).Should().Contain(AcaoNaMensagem.CopiarSelecao);
    }

    [Fact]
    public void Enviada_que_nao_saiu_reenvia_e_a_entregue_nao()
    {
        var naoSaiu = MenuDoWhatsApp.DaMensagem(Enviada(EstadoDaEntrega.NaoConfirmada), "", true, false, minha: true, true, Tudo);
        Item(naoSaiu, AcaoNaMensagem.Reenviar).Habilitado.Should().BeTrue();
        naoSaiu.Select(i => i.Acao).Should().Contain([AcaoNaMensagem.UsarComoRascunho, AcaoNaMensagem.DetalhesDaEntrega]);

        MenuDoWhatsApp.DaMensagem(Enviada(EstadoDaEntrega.Entregue), "", true, false, true, true, Tudo).Select(i => i.Acao)
            .Should().NotContain(AcaoNaMensagem.Reenviar);
        // Modelo não se reenvia por aqui (escolhe-se em Modelos), nem com a janela fechada, nem a conversa de outra pessoa.
        MenuDoWhatsApp.MotivoParaNaoReenviar(Enviada(EstadoDaEntrega.Falhou, modelo: "semdor_lembrete"), true, true).Should().Contain("Modelos");
        MenuDoWhatsApp.MotivoParaNaoReenviar(Enviada(EstadoDaEntrega.Falhou), janelaLivre: false, true).Should().Contain("Janela");
        MenuDoWhatsApp.MotivoParaNaoReenviar(Enviada(EstadoDaEntrega.Falhou), true, minha: false).Should().Be(MenuDoWhatsApp.SoComVoce);
    }

    [Fact]
    public void Midia_recebida_salva_e_guarda_na_ficha_so_com_ficha_ligada_e_o_acesso()
    {
        var foto = new MidiaDaMensagem(TipoDeMidia.Imagem, "Foto", "foto.jpg", "c1", new AppMessage("wamid.1", "in", "", "IMAGE", null, Agora, "received", AttachmentId: "a1"));
        var menu = MenuDoWhatsApp.DaMensagem(Recebida("", foto), "", true, false, true, fichaConferida: false, Tudo);
        Item(menu, AcaoNaMensagem.SalvarArquivo).Habilitado.Should().BeTrue();
        Item(menu, AcaoNaMensagem.GuardarNaFicha).Motivo.Should().Be(GuardarNaFichaService.SemFicha);
        Item(menu, AcaoNaMensagem.CopiarTexto).Habilitado.Should().BeFalse("a foto sem legenda não tem texto");
        Item(MenuDoWhatsApp.DaMensagem(Recebida("", foto), "", true, false, true, true, Tudo with { PodeGuardarNaFicha = false }),
            AcaoNaMensagem.GuardarNaFicha).Motivo.Should().Be(GuardarNaFichaService.SemPermissao);
        // Áudio não se guarda na ficha (a regra do botão do balão): o item nem aparece.
        var audio = foto with { Tipo = TipoDeMidia.Audio, Rotulo = "Áudio" };
        MenuDoWhatsApp.DaMensagem(Recebida("", audio), "", true, false, true, true, Tudo).Select(i => i.Acao).Should().NotContain(AcaoNaMensagem.GuardarNaFicha);
    }

    [Fact]
    public void Copiar_com_hora_nota_e_detalhes_dizem_quem_quando_e_o_que_fazer()
    {
        MenuDoWhatsApp.ComDataEHora(Recebida(), "Roberto Lima", Agora).Should().Be("[25/10 10:00] Roberto: Serve sim");
        MenuDoWhatsApp.ComecoDaNota(Recebida()).Should().Be("Sobre a mensagem das 17:02 (\"Serve sim\"): ");
        MenuDoWhatsApp.DetalhesDaEntrega(Enviada(EstadoDaEntrega.NaoConfirmada)).Select(l => l.Rotulo).Should().Equal("Situação", "Enviada", "Por", "O que fazer");
    }

    [Fact]
    public void A_citacao_mostra_quem_escreveu_e_o_comeco_e_o_balao_a_carrega()
    {
        var original = new AppMessage("wamid.1", "in", "Boa tarde, preciso trocar a sessão da semana que vem, dá pra ser de manhã? Obrigado, até mais, um abraço",
            "TEXT", null, Agora.AddMinutes(-10), "received");
        var cit = ConversasNaTela.Citacao(original, "Roberto Lima");
        cit.Autor.Should().Be("Roberto");
        cit.Trecho.Length.Should().BeLessThanOrEqualTo(ConversasNaTela.LetrasDaCitacao);
        cit.Trecho.Should().EndWith("…");

        var resposta = new AppMessage("m2", "out", "Tenho às 9h", "TEXT", "ana", Agora, "seen", ReplyToId: "wamid.1");
        var conversa = new AppConversation("c1", "Roberto", "5522992403344", "whatsapp", "5522333300000", "Recepção", "ana", false, 3, Agora,
            Agora.AddMinutes(-10), null, "Roberto Lima", [original, resposta], [], [], [], null);
        var item = ConversasNaTela.LinhaDoTempo(conversa, 1, Agora).Single(i => i.Id == "m2");
        item.Citacao!.Id.Should().Be("wamid.1");
        ConversasNaTela.LinhaDoTempo(conversa with { Messages = [resposta] }, 0, Agora).Single(i => i.Id == "m2").Citacao
            .Should().BeNull("a citada não está na conversa");
    }
}
