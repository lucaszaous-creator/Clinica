using Clinica.Application.Servicos;
using PreviaBlazor;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A notificação do Windows e o número no ícone (24/11/2026, F1b): o vigia acha a mensagem de paciente que CHEGOU desde a
/// leitura anterior (a primeira semeia, cada mensagem conta uma vez) e o texto diz QUEM escreveu, nunca o que escreveu.
/// </summary>
public sealed class NotificacaoDoWhatsAppTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 24, 13, 0, 0, TimeSpan.Zero);
    private static MensagemQueChegou M(string id, string nome, int min = 0) => new(id, nome, Agora.AddMinutes(min));

    [Fact]
    public void Uma_conversa_diz_o_nome_e_o_clique_abre_a_conversa()
    {
        var aviso = NotificacaoDoWhatsApp.Aviso([M("c1", "Maria Fictícia")])!;
        Assert.Equal(("WhatsApp da clínica", "Maria Fictícia mandou mensagem.", "c1"), (aviso.Titulo, aviso.Texto, aviso.ConversaId));
    }

    [Fact]
    public void Varias_conversas_dizem_quantas_e_de_quem_e_o_clique_abre_a_lista()
    {
        Assert.Null(NotificacaoDoWhatsApp.Aviso([]));
        Assert.Equal("2 conversas com mensagem nova: Ana e Bia.", NotificacaoDoWhatsApp.Aviso([M("a", "Ana"), M("b", "Bia")])!.Texto);
        Assert.Equal("3 conversas com mensagem nova: Ana, Bia e Caio.", NotificacaoDoWhatsApp.Aviso([M("a", "Ana"), M("b", "Bia"), M("c", "Caio")])!.Texto);
        var cinco = NotificacaoDoWhatsApp.Aviso([M("a", "Ana"), M("b", "Bia"), M("c", "Caio"), M("d", "Davi"), M("e", "Eva")])!;
        Assert.Equal("5 conversas com mensagem nova: Ana, Bia e mais 3.", cinco.Texto);
        Assert.Null(cinco.ConversaId);
        // A mesma conversa duas vezes é UMA conversa.
        Assert.Equal("c1", NotificacaoDoWhatsApp.Aviso([M("c1", "Maria"), M("c1", "Maria", 1)])!.ConversaId);
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(1, "1", "1 conversa não lida no WhatsApp")]
    [InlineData(9, "9", "9 conversas não lidas no WhatsApp")]
    [InlineData(10, "9+", "10 conversas não lidas no WhatsApp")]
    public void O_numero_no_icone_some_com_zero_e_nao_passa_de_9(int naoLidas, string? numero, string? dica)
    {
        Assert.Equal(numero, NotificacaoDoWhatsApp.NoIcone(naoLidas));
        Assert.Equal(dica, NotificacaoDoWhatsApp.DicaDoIcone(naoLidas));
    }

    [Fact]
    public async Task O_vigia_semeia_e_depois_avisa_cada_mensagem_que_chega_uma_vez()
    {
        var crm = new CrmDeMentira(null);
        var vigia = new VigiaDoWhatsApp(crm);
        var agora = DateTimeOffset.Now;

        Assert.Empty((await vigia.LerAsync(7, agora)).Chegaram); // abrir o app não é rajada
        Assert.Empty((await vigia.LerAsync(7, agora.AddSeconds(15))).Chegaram); // nada chegou

        crm.ChegouDoPaciente(CrmDeMentira.Ids.Carlos, "Vou sim, obrigado", DateTimeOffset.UtcNow.AddSeconds(1));
        var chegou = await vigia.LerAsync(7, agora.AddSeconds(30));
        Assert.Equal([CrmDeMentira.Ids.Carlos], chegou.Chegaram.Select(m => m.ConversaId));

        // A mesma mensagem não avisa de novo; a próxima, sim.
        Assert.Empty((await vigia.LerAsync(7, agora.AddSeconds(45))).Chegaram);
        crm.ChegouDoPaciente(CrmDeMentira.Ids.Carlos, "Ah, e posso levar acompanhante?", DateTimeOffset.UtcNow.AddSeconds(2));
        Assert.Single((await vigia.LerAsync(7, agora.AddSeconds(60))).Chegaram);
    }
}
