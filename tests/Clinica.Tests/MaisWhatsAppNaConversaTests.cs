using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// "O CRM está cru" (nov/2026, pedido do dono): o tempo no fuso da clínica e o separador de dia do WhatsApp, a figurinha e o GIF,
/// a mídia antiga sem balão vazio e o histórico que carrega aos poucos sem perder o dia. Dados fictícios.
/// </summary>
public sealed class MaisWhatsAppNaConversaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 24, 10, 0, 0, TimeSpan.FromHours(-3)); // terça

    [Fact]
    public void O_separador_de_dia_e_o_do_WhatsApp()
    {
        var hoje = new DateOnly(2026, 11, 24);
        ConversasNaTela.DiaDoWhatsApp(hoje, hoje).Should().Be("Hoje");
        ConversasNaTela.DiaDoWhatsApp(hoje.AddDays(-1), hoje).Should().Be("Ontem");
        ConversasNaTela.DiaDoWhatsApp(hoje.AddDays(-6), hoje).Should().Be("Quarta-feira");
        ConversasNaTela.DiaDoWhatsApp(hoje.AddDays(-7), hoje).Should().Be("17/11/2026");
    }

    [Fact]
    public void A_hora_e_a_da_clinica_mesmo_que_o_CRM_mande_em_UTC()
    {
        // 13:05 UTC = 10:05 em São Paulo (sem horário de verão desde 2019).
        var emUtc = new DateTimeOffset(2026, 11, 24, 13, 5, 0, TimeSpan.Zero);
        ConversasNaTela.NaClinica(emUtc).ToString("HH:mm").Should().Be("10:05");
        var c = new AppConversation("c1", "Ana", "5522990000000", "whatsapp", "x", "Recepção", null, false, 1, Agora, Agora, null, "",
            [new AppMessage("m1", "in", "Oi", "TEXT", null, emUtc, "received")], [], [], [], null);
        ConversasNaTela.LinhaDoTempo(c, 1, Agora).Single(i => i.Id == "m1").Hora.Should().Be("10:05");
        ConversasNaTela.LinhaDoTempo(c, 1, Agora).First().Texto.Should().Be("Hoje");
    }

    [Fact]
    public void Figurinha_e_GIF_tem_nome_proprio_na_lista_e_no_balao()
    {
        MidiaDaConversa.PreviaDaLista("STICKER").Should().Be("Figurinha");
        MidiaDaConversa.PreviaDaLista("GIF").Should().Be("GIF");

        var figurinha = MidiaDaConversa.Da("c1", new AppMessage("m1", "in", "", "STICKER", null, Agora, "received"))!;
        figurinha.Figurinha.Should().BeTrue();
        figurinha.Rotulo.Should().Be("Figurinha");

        var gif = MidiaDaConversa.Da("c1", new AppMessage("m2", "in", "", "VIDEO", null, Agora, "received", FileName: "danca.gif"))!;
        gif.Gif.Should().BeTrue();
        gif.Rotulo.Should().Be("GIF");
        gif.CarregaSozinha.Should().BeTrue("o GIF toca sozinho, como no WhatsApp");
        MidiaDaConversa.Da("c1", new AppMessage("m3", "in", "", "VIDEO", null, Agora, "received"))!.Gif.Should().BeFalse();
    }

    [Fact]
    public void A_midia_antiga_que_o_CRM_nao_tem_vira_nota_e_nao_balao_vazio()
    {
        var foto = MidiaDaConversa.Da("c1", new AppMessage("m1", "in", "", "IMAGE", null, Agora, "received"))!;
        MidiaDaConversa.Falha(foto, new RecusaDoCrm("do histórico", 410)).Should().Be("Foto não disponível (anterior à integração)");
        MidiaDaConversa.Falha(foto, new RecusaDoCrm("não chegou", 404)).Should().Contain("não entregou", "o 404 é a recente que não chegou");
        MidiaDaConversa.Falha(foto, new CrmIndisponivel("fora do ar")).Should().Contain("Sem ligação");
    }

    [Fact]
    public void O_historico_vem_aos_poucos_sem_perder_o_dia_e_a_citacao_sabe_ate_onde_carregar()
    {
        var linha = new List<ItemDaConversa> { new(TipoNaLinhaDoTempo.Dia, "Ontem") };
        for (var k = 0; k < 10; k++) linha.Add(new(TipoNaLinhaDoTempo.Recebida, $"m{k}", Id: $"m{k}"));

        var (itens, escondidos) = ConversasNaTela.UltimosDaLinha(linha, 4);
        itens.Select(i => i.Texto).Should().Equal("Ontem", "m6", "m7", "m8", "m9");
        escondidos.Should().Be(7);
        ConversasNaTela.UltimosDaLinha(linha, 50).Escondidos.Should().Be(0);

        ConversasNaTela.ItensAte(linha, "m1", 4).Should().Be(9);
        ConversasNaTela.ItensAte(linha, "m8", 4).Should().BeNull("já está na tela");
    }

    [Fact]
    public void Mensagem_nota_e_evento_em_horarios_cruzados_saem_na_ordem_da_hora_real()
    {
        // Pedido do dono: "ordem cronológica certinha". Uma linha só, intercalada pela hora — não blocos separados.
        var c = new AppConversation("c1", "Ana", "5522990000000", "whatsapp", "x", "Recepção", null, false, 1, Agora, Agora, null, "",
            [new AppMessage("m1", "in", "Primeira", "TEXT", null, Agora.AddMinutes(-30), "received"),
             new AppMessage("m2", "out", "Terceira", "TEXT", "bia", Agora.AddMinutes(-10), "seen")],
            [new AppNote("n1", "Segunda (nota)", "bia", Agora.AddMinutes(-20))],
            [new AppAudit(Agora.AddMinutes(-5), "bia", "Transferiu de Recepção para Acupuntura")], [], null);
        ConversasNaTela.LinhaDoTempo(c, 1, Agora).Where(i => i.Tipo != TipoNaLinhaDoTempo.Dia).Select(i => i.Tipo).Should()
            .Equal(TipoNaLinhaDoTempo.Recebida, TipoNaLinhaDoTempo.Nota, TipoNaLinhaDoTempo.Enviada, TipoNaLinhaDoTempo.Evento);
    }
}
