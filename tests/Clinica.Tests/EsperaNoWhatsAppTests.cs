using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using PreviaBlazor;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O PACIENTE QUE ESPERA VIRA AVISO FORA DA ABA (25/10, R8) E O APP CONTA COMO CONECTADO (D12): a regra
/// <see cref="ConversasNaTela.PedemResposta"/> (quem espera, de quem é, há quanto tempo) e o
/// <see cref="VigiaDoWhatsApp"/> sobre o MESMO CRM de mentira da prévia — a primeira leitura semeia, a
/// espera nova é anunciada UMA vez, o pulso vai e PARA (depois de parar, nada mais chega ao CRM).
/// </summary>
public sealed class EsperaNoWhatsAppTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 23, 13, 12, 0, TimeSpan.Zero);

    private static AppConversationSummary Linha(string id, DateTimeOffset ultimaDoPaciente, string direcao = "in", string? agente = null,
        bool encerrada = false, bool menu = false, string nome = "")
        => new(id, nome, "5522999990000", "whatsapp", "5522333300000", "Recepção", agente, encerrada, 1, ultimaDoPaciente, ultimaDoPaciente,
            null, "", 1, "oi", menu, false, null, null, PreviewDirection: direcao);

    [Fact]
    public void Espera_quem_escreveu_por_ultimo_ha_cinco_minutos_ou_mais_sem_resposta_e_e_novo_ou_meu()
    {
        var conversas = new[]
        {
            Linha("ha-6-min", Agora.AddMinutes(-6), nome: "Maria"),
            Linha("ha-4-min", Agora.AddMinutes(-4)),
            Linha("ja-respondida", Agora.AddMinutes(-30), direcao: "out"),
            Linha("minha", Agora.AddMinutes(-40), agente: "ana", nome: "João"),
            Linha("da-colega", Agora.AddMinutes(-50), agente: "bruna"),
            Linha("encerrada", Agora.AddMinutes(-50), encerrada: true),
            Linha("no-menu", Agora.AddMinutes(-50), menu: true),
            Linha("nunca-escreveu", default),
        };

        var esperando = ConversasNaTela.PedemResposta(conversas, Agora, ConversasNaTela.MinutosDeEspera, meuAgente: "ana");

        // Quem espera há mais tempo vem primeiro; a de 4 min ainda não, a da colega é dela.
        Assert.Equal(["minha", "ha-6-min"], esperando.Select(e => e.Id));
        Assert.Equal((40, "João"), (esperando[0].Minutos, esperando[0].Nome));
        Assert.Equal($"wa-espera:minha:{Agora.AddMinutes(-40).UtcTicks}", esperando[0].Chave);
        // Sem a sessão do CRM (meuAgente nulo), só as sem responsável.
        Assert.Equal(["ha-6-min"], ConversasNaTela.PedemResposta(conversas, Agora).Select(e => e.Id));
        // A borda: exatamente 5 minutos já pede.
        Assert.Single(ConversasNaTela.PedemResposta([Linha("borda", Agora.AddMinutes(-5))], Agora));
    }

    [Fact]
    public void A_mensagem_nova_depois_da_resposta_e_outra_espera_com_outra_chave()
    {
        var primeira = ConversasNaTela.PedemResposta([Linha("c", Agora.AddMinutes(-20))], Agora).Single();
        var segunda = ConversasNaTela.PedemResposta([Linha("c", Agora.AddMinutes(-8))], Agora).Single();
        Assert.NotEqual(primeira.Chave, segunda.Chave);
    }

    [Theory]
    [InlineData(0, "agora")]
    [InlineData(7, "há 7 min")]
    [InlineData(60, "há 1 h")]
    [InlineData(135, "há 2 h 15 min")]
    [InlineData(1500, "há 1 dia")]
    [InlineData(4400, "há 3 dias")]
    public void Ha_quanto_diz_a_espera_como_a_recepcao_le(int minutos, string esperado)
        => Assert.Equal(esperado, ConversasNaTela.HaQuanto(minutos));

    [Fact]
    public void O_numero_do_menu_some_sem_espera_e_nao_cresce_acima_de_99()
    {
        Assert.Null(ConversasNaTela.ContadorDoMenu(0));
        Assert.Equal("3", ConversasNaTela.ContadorDoMenu(3));
        Assert.Equal("99+", ConversasNaTela.ContadorDoMenu(120));
        Assert.Equal("1 espera resposta", ConversasNaTela.ContadorDoSino(1));
        Assert.Equal("2 esperam resposta", ConversasNaTela.ContadorDoSino(2));
        Assert.Equal("Maria espera resposta no WhatsApp há 6 min",
            ConversasNaTela.FraseDaEspera(new ConversaQueEspera("c", "Maria", Agora, 6, "k")));
    }

    // ------------------------------------------------------------------ o vigia, sobre o CRM de mentira da prévia

    [Fact]
    public async Task O_vigia_semeia_na_primeira_leitura_e_anuncia_so_a_espera_nova_uma_vez()
    {
        var crm = new CrmDeMentira(null);
        var vigia = new VigiaDoWhatsApp(crm);
        var agora = DateTimeOffset.Now;

        // Na semente, o "novo contato" e a Terezinha (sem responsável) esperam desde cedo; a Maria é da Ana e respondeu há pouco.
        var primeira = await vigia.LerAsync(7, agora);
        Assert.True(primeira.Ligado);
        Assert.Contains(primeira.Esperando, e => e.Id == CrmDeMentira.Ids.NovoContato);
        Assert.Contains(primeira.Esperando, e => e.Id == CrmDeMentira.Ids.Terezinha);
        Assert.DoesNotContain(primeira.Esperando, e => e.Id == CrmDeMentira.Ids.Lucia); // é da Bruna
        Assert.Empty(primeira.Novas); // abrir o app não é uma rajada de avisos
        // O número do menu é o MESMO da aba "Conversas" da tela: as abertas com mensagem não lida.
        var conversas = await crm.ListarConversasAsync(7);
        Assert.Equal(ConversasNaTela.Contagens(conversas, (await crm.SessaoAsync(7)).AgentId).NaoLidas, primeira.NaoLidas);
        Assert.True(primeira.NaoLidas > 0);

        // O Carlos (da Ana) escreve; dez minutos depois, a espera dele é nova — anunciada UMA vez.
        crm.ChegouDoPaciente(CrmDeMentira.Ids.Carlos, "Vou sim, obrigado", DateTimeOffset.UtcNow);
        var depois = await vigia.LerAsync(7, agora.AddMinutes(10));
        var nova = Assert.Single(depois.Novas);
        Assert.Equal(CrmDeMentira.Ids.Carlos, nova.Id);
        Assert.Contains(depois.Esperando, e => e.Id == CrmDeMentira.Ids.Carlos);

        var deNovo = await vigia.LerAsync(7, agora.AddMinutes(11));
        Assert.Empty(deNovo.Novas);
        Assert.Contains(deNovo.Esperando, e => e.Id == CrmDeMentira.Ids.Carlos);
    }

    [Fact]
    public async Task O_pulso_vai_ao_CRM_e_para_de_vez_quando_o_vigia_para()
    {
        var crm = new CrmDeMentira(null);
        var vigia = new VigiaDoWhatsApp(crm);

        Assert.True(await vigia.PulsarAsync(7));
        Assert.Equal(1, crm.Pulsos);

        vigia.Parar();
        Assert.False(await vigia.PulsarAsync(7));
        Assert.Equal(LeituraDaEspera.Desligada, await vigia.LerAsync(7, DateTimeOffset.Now));
        Assert.Equal(1, crm.Pulsos);
        // Ninguém logado (0) nunca pulsa.
        Assert.False(await new VigiaDoWhatsApp(crm).PulsarAsync(0));
        Assert.True(VigiaDoWhatsApp.Pulso < TimeSpan.FromSeconds(120 / 2), "uma batida perdida não pode derrubar a presença de 120 s");
    }

    [Fact]
    public async Task Sem_endereco_do_CRM_o_vigia_fica_desligado_em_silencio()
    {
        var crm = new CrmDeMentira(null) { Modo = CrmDeMentira.ModoDoCrm.NaoConfigurado };
        var vigia = new VigiaDoWhatsApp(crm);

        var leitura = await vigia.LerAsync(7, DateTimeOffset.Now);

        Assert.False(leitura.Ligado);
        Assert.Empty(leitura.Esperando);
        Assert.False(await vigia.PulsarAsync(7));
    }

    [Fact]
    public async Task CRM_fora_do_ar_sobe_para_quem_chama_registrar_e_manter_a_lista()
    {
        var crm = new CrmDeMentira(null);
        var vigia = new VigiaDoWhatsApp(crm);
        await vigia.LerAsync(7, DateTimeOffset.Now);
        crm.Modo = CrmDeMentira.ModoDoCrm.ForaDoAr;

        await Assert.ThrowsAsync<Clinica.Application.Abstracoes.CrmIndisponivel>(() => vigia.LerAsync(7, DateTimeOffset.Now.AddMinutes(1)));
        await Assert.ThrowsAsync<Clinica.Application.Abstracoes.CrmIndisponivel>(() => vigia.PulsarAsync(7));
    }
}
