using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Xunit;
using static Clinica.Tests.WhatsAppDeTeste;

namespace Clinica.Tests;

/// <summary>
/// O acabamento profissional da tela do WhatsApp (25/10/2026, docs/whatsapp-nativo-implementacao.md §15):
/// a linha do tempo limpa, a espera na fila, a ordem de "Novas" e as frases de falha sem o nome do sistema.
/// </summary>
public sealed class ConversasNaTelaAcabamentoTests
{
    private static IReadOnlyList<(TipoNaLinhaDoTempo Tipo, string Texto)> Itens(AppConversation c)
        => ConversasNaTela.LinhaDoTempo(c, int.MaxValue, Agora, Agentes).Select(i => (i.Tipo, i.Texto)).ToList();

    // ------------------------------------------------------------ a linha do tempo limpa

    [Fact]
    public void O_pendente_sai_quando_a_agenda_ja_registrou_o_fim()
    {
        // O João confirmou: a ponte grava "pendente" e, segundos depois, a agenda registra (semdor-crm, ClinicFlow.cs).
        var c = Conversa([Recebida("m1", Em(24, 9, 40), "Confirmado, obrigado!")], trilha:
        [
            new AppAudit(Em(24, 9, 40).AddSeconds(5), "Paciente", "Confirmação recebida; atualização da agenda pendente"),
            new AppAudit(Em(24, 9, 40).AddSeconds(30), "Agenda da clínica", "A agenda da clínica registrou a confirmação do horário"),
        ]);
        var eventos = Itens(c).Where(i => i.Tipo == TipoNaLinhaDoTempo.Evento).Select(i => i.Texto).ToList();
        Assert.Equal(["A agenda da clínica registrou a confirmação do horário"], eventos);

        // Sem a resposta da agenda, o pendente é a notícia: fica.
        var semAgenda = Conversa([Recebida("m1", Em(24, 9, 40))], trilha:
            [new AppAudit(Em(24, 9, 41), "Paciente", "Paciente pediu remarcação; horário original mantido até a equipe combinar o novo")]);
        Assert.Contains(Itens(semAgenda), i => i.Texto.StartsWith("Paciente pediu remarcação", StringComparison.Ordinal));
    }

    [Fact]
    public void Cada_resposta_da_agenda_resolve_um_pendente_so()
    {
        var c = Conversa([Recebida("m1", Em(24, 8, 0))], trilha:
        [
            new AppAudit(Em(24, 8, 1), "Paciente", "Cancelamento solicitado; atualização da agenda pendente"),
            new AppAudit(Em(24, 9, 0), "Paciente", "Confirmação recebida; atualização da agenda pendente"),
            new AppAudit(Em(24, 9, 1), "Agenda da clínica", "Confirmação registrada"),
        ]);
        var eventos = Itens(c).Where(i => i.Tipo == TipoNaLinhaDoTempo.Evento).Select(i => i.Texto).ToList();
        Assert.Equal(["Cancelamento solicitado; atualização da agenda pendente", "Confirmação registrada"], eventos);
    }

    [Fact]
    public void Assumiu_ao_responder_sai_quando_o_balao_ao_lado_e_da_mesma_pessoa()
    {
        // O CRM grava o "assumiu" no mesmo instante da resposta: o nome dela já está no balão.
        var c = Conversa(
            [Recebida("m1", Em(24, 9, 40)), Enviada("m2", Em(24, 9, 41), "ana", "seen", "Perfeito, João!")],
            trilha: [new AppAudit(Em(24, 9, 41), "Ana Paula", "Assumiu ao escrever para o paciente")]);
        Assert.DoesNotContain(Itens(c), i => i.Tipo == TipoNaLinhaDoTempo.Evento);

        // Antes do balão (o carimbo do CRM pode vir um instante antes) também sai.
        var antes = Conversa(
            [Enviada("m2", Em(24, 9, 41).AddSeconds(2), "ana", "seen", "Oi")],
            trilha: [new AppAudit(Em(24, 9, 41), "Ana Paula", "Reabriu ao escrever para o paciente")]);
        Assert.DoesNotContain(Itens(antes), i => i.Tipo == TipoNaLinhaDoTempo.Evento);

        // Balão de OUTRA pessoa ao lado: o evento é a notícia de quem pegou a conversa — fica.
        var outra = Conversa(
            [Enviada("m2", Em(24, 9, 41), "bruna", "seen", "Oi")],
            trilha: [new AppAudit(Em(24, 9, 41), "Ana Paula", "Assumiu ao escrever para o paciente")]);
        Assert.Contains(Itens(outra), i => i.Texto == "Ana Paula assumiu a conversa ao responder");
    }

    [Fact]
    public void Eventos_seguidos_no_mesmo_minuto_viram_uma_nota_com_o_tom_mais_forte()
    {
        var c = Conversa([Recebida("m1", Em(24, 9, 0))], trilha:
        [
            new AppAudit(Em(24, 10, 0), "Bruna", "Encerrou o atendimento"),
            new AppAudit(Em(24, 10, 0).AddSeconds(20), "Bruna", "Desligou a ficha Maria desta conversa."),
            new AppAudit(Em(24, 10, 5), "Sistema", "Reaberta após nova mensagem"),
        ]);
        var eventos = ConversasNaTela.LinhaDoTempo(c, 1, Agora, Agentes).Where(i => i.Tipo == TipoNaLinhaDoTempo.Evento).ToList();
        Assert.Equal(2, eventos.Count);
        Assert.Equal(("Encerrada por Bruna. Bruna desligou a ficha de Maria desta conversa", "aviso", "10:00"),
            (eventos[0].Texto, eventos[0].Tom, eventos[0].Hora));
        Assert.Equal("A conversa reabriu sozinha com a nova mensagem do paciente", eventos[1].Texto);
    }

    [Fact]
    public void O_caso_sem_conversa_nao_mostra_o_separador_de_dia_sozinho()
    {
        var dados = new DadosDaConversa(null, "5522991204477", "Sebastião Ramos", Agentes, "ana", 0, [], [], null, [], [],
            Caso(), [], new Dictionary<string, string>());
        Assert.Empty(WhatsAppDaRecepcaoService.Compor(dados, Agora).LinhaDoTempo);
    }

    [Fact]
    public void O_erro_de_entrega_e_frase_da_clinica_nunca_o_texto_do_provedor()
    {
        var falhou = ConversasNaTela.Tick("failed");
        Assert.Equal("Não entregue. Confira o número ou envie de novo.", ConversasNaTela.ErroDaEntrega(falhou, "(#131026) Receiver is incapable of receiving"));
        Assert.Equal("O WhatsApp não confirmou a entrega. Confira com a pessoa antes de reenviar.",
            ConversasNaTela.ErroDaEntrega(ConversasNaTela.Tick("uncertain"), "timeout"));
        Assert.Null(ConversasNaTela.ErroDaEntrega(falhou, " ")); // sem o CRM dizer por quê, o tick já diz
        Assert.Null(ConversasNaTela.ErroDaEntrega(ConversasNaTela.Tick("seen"), "qualquer"));
    }

    // ------------------------------------------------------------ a fila: quem espera

    [Fact]
    public void A_linha_diz_ha_quanto_tempo_a_pessoa_espera_a_clinica()
    {
        Assert.Equal(new EsperaNaLinha("espera há 12 min", "neutro", "há 12 min"),
            ConversasNaTela.Espera(Resumo(ultimaRecebida: Agora.AddMinutes(-12)), Agora));
        Assert.Equal(new EsperaNaLinha("espera há 2 h", "aviso", "há 2 h"),
            ConversasNaTela.Espera(Resumo(ultimaRecebida: Agora.AddMinutes(-179)), Agora));
        // Sábado de manhã até segunda: a janela já fechou, e a espera conta em dias.
        Assert.Equal(new EsperaNaLinha("espera há 2 dias", "aviso", "há 2 dias"),
            ConversasNaTela.Espera(Resumo(ultimaRecebida: Agora.AddDays(-2).AddHours(-1)), Agora));
        Assert.Null(ConversasNaTela.Espera(Resumo(ultimaRecebida: Agora.AddSeconds(-30)), Agora)); // acabou de chegar
    }

    [Fact]
    public void Respondeu_a_linha_deixa_de_esperar_na_hora_e_mostra_o_que_a_clinica_mandou()
    {
        var linha = Resumo(ultimaRecebida: Agora.AddMinutes(-8), naoLidas: 1);
        Assert.NotNull(ConversasNaTela.Espera(linha, Agora));
        var nova = Conversa([Recebida("m1", Agora.AddMinutes(-8), "Terça fica ótimo"),
                             Enviada("m2", Agora, "ana", "sending", "Combinado, Maria!")]);
        var depois = ConversasNaTela.LinhaDepoisDaEscrita(linha, nova);
        Assert.Equal(("Combinado, Maria!", "out", 0, "ana"), (depois.Preview, depois.PreviewDirection, depois.Unread, depois.AgentId));
        Assert.Null(ConversasNaTela.Espera(depois, Agora));
        Assert.Equal(EstadoDaEntrega.Enviando, ConversasNaTela.TickDaPrevia(depois)!.Estado);
        // A nota interna não é mensagem: a prévia continua a do paciente.
        var comNota = nova with { Messages = [Recebida("m1", Agora.AddMinutes(-8), "Terça fica ótimo")], Notes = [new AppNote("n1", "ligar à tarde", "ana", Agora)] };
        Assert.Equal(("Terça fica ótimo", "in"), (ConversasNaTela.LinhaDepoisDaEscrita(linha, comNota).Preview, ConversasNaTela.LinhaDepoisDaEscrita(linha, comNota).PreviewDirection));
    }

    [Fact]
    public void Faltando_menos_de_4_h_a_linha_diz_a_hora_em_que_a_janela_fecha()
    {
        // Escreveu ontem às 14:10: a janela fecha hoje às 14:10 — às 10:12 faltam menos de 4 h.
        var ontem = new DateTimeOffset(2026, 10, 23, 14, 10, 0, Brasilia);
        Assert.Equal(new EsperaNaLinha("janela fecha às 14:10", "aviso", "fecha às 14:10"), ConversasNaTela.Espera(Resumo(ultimaRecebida: ontem), Agora));
        // Com mais de 4 h de folga, a espera.
        var cedo = new DateTimeOffset(2026, 10, 23, 15, 0, 0, Brasilia);
        Assert.Equal("espera há 19 h", ConversasNaTela.Espera(Resumo(ultimaRecebida: cedo), Agora)!.Texto);
        // O SMS não tem janela: só a espera.
        Assert.Equal("espera há 20 h", ConversasNaTela.Espera(Resumo(canal: "sms", ultimaRecebida: ontem), Agora)!.Texto);
    }

    [Fact]
    public void Sem_espera_quando_a_clinica_falou_por_ultimo_encerrada_ou_no_menu()
    {
        var umaHora = Agora.AddHours(-1);
        Assert.Null(ConversasNaTela.Espera(Resumo(ultimaRecebida: umaHora, direcao: "out"), Agora));
        Assert.Null(ConversasNaTela.Espera(Resumo(ultimaRecebida: umaHora, encerrada: true), Agora));
        Assert.Null(ConversasNaTela.Espera(Resumo(ultimaRecebida: umaHora, noMenu: true), Agora));
        Assert.Null(ConversasNaTela.Espera(Resumo(ultimaRecebida: default(DateTimeOffset)), Agora)); // nunca escreveu
    }

    [Fact]
    public void Em_Novas_a_mais_antiga_vem_primeiro_e_o_que_pede_acao_na_frente()
    {
        var lista = new[]
        {
            Resumo("recente", atualizada: Em(24, 10, 9)),
            Resumo("sabado", atualizada: Em(17, 10, 0)),
            Resumo("ontem", atualizada: Em(23, 16, 0)),
        };
        Assert.Equal(["sabado", "ontem", "recente"], ConversasNaTela.Filtrar(lista, FiltroDasConversas.Novas, "ana").Select(c => c.Id));
        // As outras abas continuam a mais recente primeiro.
        Assert.Equal(["recente", "ontem", "sabado"], ConversasNaTela.Filtrar(lista, FiltroDasConversas.Todas, "ana").Select(c => c.Id));

        var selos = new Dictionary<string, SeloDoWhatsApp>
        {
            ["sabado"] = new("Nova", "neutro"), ["ontem"] = new("Não vem — remarcar", "perigo"), ["recente"] = new("Sem resposta · 16:00", "aviso")
        };
        Assert.Equal(["ontem", "recente", "sabado"],
            ConversasNaTela.OrdenarAsNovas(new[] { "sabado", "ontem", "recente" }, id => selos[id]));
    }

    // ------------------------------------------------------------ frases

    [Fact]
    public void O_centro_vazio_tem_uma_frase_por_modo_sem_detalhe_tecnico()
    {
        var (conversas, textoConversas) = ConversasNaTela.SemConversaAberta(retencao: false);
        var (retencao, textoRetencao) = ConversasNaTela.SemConversaAberta(retencao: true);
        Assert.Equal("Escolha uma conversa", conversas);
        Assert.Equal("Escolha um paciente para preparar o convite", retencao);
        foreach (var frase in new[] { textoConversas, textoRetencao })
            Assert.DoesNotContain("segundos", frase);
    }

    [Fact]
    public void Sem_ligacao_a_frase_nao_fala_do_sistema_e_diz_o_que_fazer()
    {
        var foraDoAr = new CrmIndisponivel("O CRM não respondeu. Tente de novo em instantes.");
        Assert.Equal("Sem ligação com o WhatsApp da clínica agora. Nada foi alterado; tente de novo em instantes.",
            ConversasNaTela.FalhaNaEscrita(foraDoAr, "Nada foi alterado; tente de novo em instantes."));
        var abertura = ConversasNaTela.FalhaDeAbertura(foraDoAr);
        Assert.Contains("atenda pelo celular da recepção", abertura);
        Assert.Equal("O WhatsApp da clínica não aceitou o seu acesso. Fale com a direção.",
            ConversasNaTela.SemLigacao(new CrmIndisponivel(CrmIndisponivel.AcessoRecusado)));
        // A falha do próprio app (o banco) não vira "sem ligação com o WhatsApp".
        Assert.StartsWith("Os dados da clínica não puderam ser lidos", ConversasNaTela.FalhaDeAbertura(new TimeoutException("db")));
        var faixa = ConversasNaTela.FaixaSemLigacao(new DateTimeOffset(2026, 10, 24, 13, 14, 0, TimeSpan.Zero), Agora);
        Assert.Equal("Sem ligação com o WhatsApp desde 10:14. O que você enviar agora pode não sair; tentando de novo…", faixa);
        foreach (var frase in new[] { abertura, faixa, ConversasNaTela.FalhaDaConversa(foraDoAr), ConversasNaTela.SemLigacao(foraDoAr) })
            Assert.DoesNotContain("CRM", frase);
    }

    [Fact]
    public void O_rodape_da_lista_diz_o_estado_da_ligacao()
    {
        Assert.Equal(("Conectado", false), ConversasNaTela.EstadoDaLigacao(false, null, Agora));
        Assert.Equal(("WhatsApp desligado", true), ConversasNaTela.EstadoDaLigacao(true, null, Agora));
        Assert.Equal(("Sem ligação desde 10:05", true), ConversasNaTela.EstadoDaLigacao(false, Agora.AddMinutes(-7), Agora));
    }
}
