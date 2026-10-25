using System.Text.RegularExpressions;
using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;

namespace Clinica.Tests;

/// <summary>Os dados de teste do WhatsApp nativo: o relógio do mockup (sábado, 24/10/2026, 10:12 em Brasília — o mockup diz "sexta", mas 24/10/2026 é sábado).</summary>
internal static class WhatsAppDeTeste
{
    public static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);
    public static readonly DateTimeOffset Agora = new(2026, 10, 24, 10, 12, 0, Brasilia);
    public static readonly DateOnly Hoje = new(2026, 10, 24);

    public static DateTimeOffset Em(int dia, int hora, int minuto) => new(2026, 10, dia, hora, minuto, 0, Brasilia);

    public static AppConversationSummary Resumo(string id = "c1", string nome = "Maria das Graças Silva", string contato = "5522998765432",
        string canal = "whatsapp", string? agente = null, bool encerrada = false, DateTimeOffset? atualizada = null,
        DateTimeOffset? ultimaRecebida = null, int? paciente = null, string nomeDaFicha = "", int naoLidas = 0,
        bool noMenu = false, bool falhaDeEntrega = false, AppResolution? resultado = null, string direcao = "in", string status = "")
        => new(id, nome, contato, canal, "5522333300000", "Recepção", agente, encerrada, 1, atualizada ?? Agora,
            ultimaRecebida ?? Em(24, 9, 58), paciente, nomeDaFicha, naoLidas, "…", noMenu, falhaDeEntrega, resultado,
            encerrada ? Agora : null, direcao, status);

    public static AppConversation Conversa(AppMessage[]? mensagens = null, AppNote[]? notas = null, AppAudit[]? trilha = null,
        int? paciente = 101, string nomeDaFicha = "Maria das Graças Silva", bool encerrada = false, string canal = "whatsapp",
        DateTimeOffset? ultimaRecebida = null)
        => new("c1", "Maria das Graças Silva", "5522998765432", canal, "5522333300000", "Recepção", "ana", encerrada, 3, Agora,
            ultimaRecebida ?? (mensagens ?? []).Where(m => m.Direction == "in").Select(m => m.At).DefaultIfEmpty().Max(),
            paciente, nomeDaFicha, mensagens ?? [], notas ?? [], trilha ?? [], [], encerrada ? Agora : null);

    public static AppMessage Recebida(string id, DateTimeOffset at, string texto = "Oi")
        => new(id, "in", texto, "TEXT", null, at, "received");

    public static AppMessage Enviada(string id, DateTimeOffset at, string? agente = "ana", string status = "delivered", string texto = "Olá")
        => new(id, "out", texto, "TEXT", agente, at, status);

    public static LinhaAcompanhamento Caso(int id = 1, int paciente = 101, string nome = "Sebastião Ramos", string? telefone = "(22) 99120-4477",
        TipoAcompanhamento tipo = TipoAcompanhamento.Recall, ModalidadeAtendimento modalidade = ModalidadeAtendimento.BsvApenas,
        int dias = 32, bool pendente = true, bool consentimento = true, DateOnly? proximo = null, int tentativas = 0,
        DateTime? ultimoContato = null, DateOnly? voltou = null, DateTime? referencia = null, int totalSessoes = 6,
        string? motivoEncerramento = null, string? motivo = null, bool atrasado = false, string profissional = "Dr. Marcos")
        => new(id, paciente, nome, telefone, "Unimed", tipo, modalidade, profissional, 3, 7, "Ana Paula",
            referencia ?? new DateTime(2026, 9, 22), dias, "A contatar", pendente, atrasado, proximo ?? Hoje,
            EtapaAcompanhamento.AContatar, tentativas, ultimoContato, null, consentimento, false, null, false, false, Guid.NewGuid(),
            motivoEncerramento, motivo, totalSessoes, voltou);

    public static readonly IReadOnlyDictionary<string, string> Agentes = new Dictionary<string, string>
    {
        ["ana"] = "Ana Paula", ["bruna"] = "Bruna"
    };
}

/// <summary>
/// O que a tela "WhatsApp da clínica" afirma (PR 2 do WhatsApp nativo): a janela de 24 h, os
/// ticks, o selo de cada linha (com o MESMO aviso do sino), a ficha, o que pede ação, a linha
/// do tempo e a lista.
/// </summary>
public sealed class ConversasNaTelaTests
{
    private static readonly DateTimeOffset Agora = WhatsAppDeTeste.Agora;

    // ------------------------------------------------------------ janela de 24 h

    [Fact]
    public void A_janela_fica_aberta_ate_23h59_e_fecha_na_borda_das_24h()
    {
        var aberta = ConversasNaTela.Janela(Agora - TimeSpan.FromHours(23) - TimeSpan.FromMinutes(59), Agora, "whatsapp");
        Assert.Equal(SituacaoDaJanela.Aberta, aberta.Estado);
        Assert.True(aberta.PodeEscreverLivre);
        Assert.Equal(Agora + TimeSpan.FromMinutes(1), aberta.Ate);
        Assert.Equal("Janela aberta até hoje às 10:13", aberta.Texto);

        var borda = ConversasNaTela.Janela(Agora - TimeSpan.FromHours(24), Agora, "whatsapp");
        Assert.Equal(SituacaoDaJanela.Fechada, borda.Estado); // o CRM recusa com now - LastIncoming >= 24h

        var fechada = ConversasNaTela.Janela(Agora - TimeSpan.FromHours(24) - TimeSpan.FromMinutes(1), Agora, "whatsapp");
        Assert.Equal(SituacaoDaJanela.Fechada, fechada.Estado);
        Assert.False(fechada.PodeEscreverLivre);
        Assert.Equal(Agora - TimeSpan.FromMinutes(1), fechada.Desde);
        Assert.Equal("Janela fechada desde hoje às 10:11", fechada.Texto);
    }

    [Fact]
    public void A_janela_le_o_carimbo_do_CRM_em_UTC_na_hora_da_clinica()
    {
        // 23/10 13:09 UTC = 10:09 em Brasília: aberta até amanhã às 10:09 (a frase do mockup).
        var j = ConversasNaTela.Janela(new DateTimeOffset(2026, 10, 24, 13, 9, 0, TimeSpan.Zero), Agora, "whatsapp");
        Assert.Equal("Janela aberta até amanhã às 10:09", j.Texto);
        Assert.Equal(WhatsAppDeTeste.Brasilia, j.Ate!.Value.Offset);

        var antiga = ConversasNaTela.Janela(WhatsAppDeTeste.Em(21, 11, 5), Agora, "whatsapp");
        Assert.Equal("Janela fechada desde qui 22/10 às 11:05", antiga.Texto);
        Assert.Equal("Janela fechada desde ontem às 11:05",
            ConversasNaTela.Janela(WhatsAppDeTeste.Em(22, 11, 5), Agora, "whatsapp").Texto);
    }

    [Fact]
    public void Quem_nunca_escreveu_tem_a_janela_fechada_desde_sempre_e_SMS_nao_tem_janela()
    {
        foreach (var nunca in new DateTimeOffset?[] { null, default(DateTimeOffset), DateTimeOffset.MinValue })
        {
            var j = ConversasNaTela.Janela(nunca, Agora, "whatsapp");
            Assert.Equal(SituacaoDaJanela.NuncaEscreveu, j.Estado);
            Assert.False(j.PodeEscreverLivre);
            Assert.Equal("Janela fechada: a pessoa ainda não escreveu", j.Texto);
        }
        var sms = ConversasNaTela.Janela(WhatsAppDeTeste.Em(1, 8, 0), Agora, "SMS");
        Assert.Equal(SituacaoDaJanela.SemJanela, sms.Estado);
        Assert.True(sms.PodeEscreverLivre);
        Assert.Equal("SMS · sem janela de 24 h", sms.Texto);
        // Canal desconhecido segue a regra mais estrita (a do WhatsApp).
        Assert.False(ConversasNaTela.PodeEscreverLivre(WhatsAppDeTeste.Em(1, 8, 0), Agora, "telegrama"));
        Assert.True(ConversasNaTela.PodeEscreverLivre(WhatsAppDeTeste.Em(24, 9, 0), Agora, "whatsapp"));
    }

    [Fact]
    public void O_campo_fechado_diz_desde_quando_e_some_quando_a_janela_esta_aberta()
    {
        var fechada = ConversasNaTela.Janela(WhatsAppDeTeste.Em(21, 11, 5), Agora, "whatsapp");
        var bloco = ConversasNaTela.CampoFechado(fechada, "Carlos Eduardo Mota", Agora);
        Assert.Equal("Janela de 24 h fechada desde qui 22/10 às 11:05", bloco!.Value.Titulo);
        Assert.Equal("Fora dela a Meta só aceita modelo aprovado. Quando Carlos responder, o campo livre volta.", bloco.Value.Texto);

        var nunca = ConversasNaTela.CampoFechado(ConversasNaTela.Janela(null, Agora, "whatsapp"), "(22) 99712-0045", Agora);
        Assert.Equal("A pessoa ainda não escreveu, então a Meta só aceita modelo. Depois da resposta, o campo livre abre.", nunca!.Value.Texto);

        Assert.Null(ConversasNaTela.CampoFechado(ConversasNaTela.Janela(Agora.AddHours(-1), Agora, "whatsapp"), "Maria", Agora));
        Assert.Null(ConversasNaTela.CampoFechado(ConversasNaTela.Janela(null, Agora, "sms"), "Maria", Agora));
    }

    // ------------------------------------------------------------ ticks

    [Theory]
    [InlineData("sending", EstadoDaEntrega.Enviando, "Enviando", "neutro")]
    [InlineData("accepted", EstadoDaEntrega.Enviada, "Enviada", "neutro")]
    [InlineData("simulated", EstadoDaEntrega.Enviada, "Enviada", "neutro")]
    [InlineData("delivered", EstadoDaEntrega.Entregue, "Entregue", "neutro")]
    [InlineData("seen", EstadoDaEntrega.Lida, "Lida", "info")]
    [InlineData("SEEN", EstadoDaEntrega.Lida, "Lida", "info")]
    [InlineData("failed", EstadoDaEntrega.Falhou, "Falhou", "perigo")]
    [InlineData("uncertain", EstadoDaEntrega.NaoConfirmada, "Envio não confirmado: confira antes de reenviar", "aviso")]
    [InlineData("algo-novo", EstadoDaEntrega.NaoConfirmada, "Envio não confirmado: confira antes de reenviar", "aviso")]
    public void O_tick_traduz_cada_status_que_o_CRM_grava(string status, EstadoDaEntrega estado, string texto, string tom)
    {
        var tick = ConversasNaTela.Tick(status);
        Assert.Equal(new TickDaMensagem(estado, texto, tom), tick);
    }

    [Fact]
    public void Recebida_nao_tem_tick_e_a_previa_so_tem_tick_quando_a_ultima_foi_da_clinica()
    {
        Assert.Null(ConversasNaTela.Tick("received"));
        Assert.Null(ConversasNaTela.Tick(null));
        Assert.Null(ConversasNaTela.Tick(""));
        Assert.Null(ConversasNaTela.TickDaPrevia(WhatsAppDeTeste.Resumo(direcao: "in", status: "received")));
        Assert.Equal(EstadoDaEntrega.Lida, ConversasNaTela.TickDaPrevia(WhatsAppDeTeste.Resumo(direcao: "out", status: "seen"))!.Estado);
        Assert.Null(ConversasNaTela.TickDaPrevia(WhatsAppDeTeste.Resumo(direcao: "out", status: ""))); // CRM antigo: sem afirmar nada
    }

    // ------------------------------------------------------------ selo, com o MESMO aviso do sino

    private static readonly DateTime AgoraLocal = Agora.DateTime;

    /// <summary>Os contatos de confirmação do dia, passados pela MESMA decisão do sino.</summary>
    private static IReadOnlyList<AvisoDeConfirmacao> AvisosDoSino() => AvisosDeConfirmacao.Decidir(
    [
        new(1, "Maria das Graças Silva", AgoraLocal.Date.AddHours(14), "Dra. Helena", StatusContato.Respondido, true,
            AgoraLocal.Date.AddHours(6), AgoraLocal.Date.AddHours(9).AddMinutes(58), RodadaDeConfirmacao.RespostaRemarcar),
        new(2, "Carlos Eduardo Mota", AgoraLocal.Date.AddHours(16), "Dr. Marcos", StatusContato.Enviado, true,
            AgoraLocal.Date.AddHours(6), null, null),
        new(3, "João Batista Ferreira", AgoraLocal.Date.AddHours(15), "Dra. Helena", StatusContato.Respondido, true,
            AgoraLocal.Date.AddHours(6), AgoraLocal.Date.AddHours(9).AddMinutes(40), RodadaDeConfirmacao.RespostaConfirmouPeloLink),
        new(4, "Pedro Henrique Souza", AgoraLocal.Date.AddDays(1).AddHours(9), "Dra. Helena", StatusContato.Respondido, true,
            AgoraLocal.Date.AddHours(6), AgoraLocal.Date.AddHours(8), RodadaDeConfirmacao.RespostaCancelar),
    ], AgoraLocal, AgoraLocal.Date);

    private static readonly IReadOnlyDictionary<int, int> PacienteDoAgendamento = new Dictionary<int, int>
    {
        [1] = 101, [2] = 102, [3] = 103, [4] = 104
    };

    private static SeloDoWhatsApp SeloDe(int paciente, IReadOnlyList<AvisoDeConfirmacao> avisos)
    {
        var resumo = WhatsAppDeTeste.Resumo(paciente: paciente, agente: "ana");
        return ConversasNaTela.Selo(resumo, ConversasNaTela.AvisosDaConversa(paciente, avisos, PacienteDoAgendamento), 1, null, Agora);
    }

    [Fact]
    public void O_mesmo_aviso_do_sino_da_o_selo_correspondente_na_linha()
    {
        var avisos = AvisosDoSino();
        Assert.Equal(TipoAvisoConfirmacao.NaoVem, avisos.Single(a => a.AgendamentoId == 1).Tipo);
        Assert.Equal(new SeloDoWhatsApp("Não vem — remarcar", "perigo"), SeloDe(101, avisos));
        Assert.Equal(TipoAvisoConfirmacao.SemResposta, avisos.Single(a => a.AgendamentoId == 2).Tipo);
        Assert.Equal(new SeloDoWhatsApp("Sem resposta · 16:00", "aviso"), SeloDe(102, avisos));
        Assert.Equal(TipoAvisoConfirmacao.Confirmou, avisos.Single(a => a.AgendamentoId == 3).Tipo);
        Assert.Equal(new SeloDoWhatsApp("Confirmou · 15:00", "sucesso"), SeloDe(103, avisos));
        Assert.Equal(TipoAvisoConfirmacao.PediuCancelar, avisos.Single(a => a.AgendamentoId == 4).Tipo);
        Assert.Equal(new SeloDoWhatsApp("Pediu para cancelar", "perigo"), SeloDe(104, avisos));
    }

    [Fact]
    public void Todo_tipo_de_aviso_do_sino_tem_um_selo_proprio()
    {
        var textos = Enum.GetValues<TipoAvisoConfirmacao>().Select(tipo =>
        {
            var aviso = new AvisoDeConfirmacao(1, "Maria", AgoraLocal.Date.AddDays(1).AddHours(8).AddMinutes(30), null, tipo, AgoraLocal);
            return SeloDe(101, [aviso]).Texto;
        }).ToList();
        Assert.Equal(textos.Count, textos.Distinct().Count());
        Assert.Contains("Sem resposta · amanhã 08:30", textos);
        Assert.Contains("Confirmou · amanhã 08:30", textos);
        Assert.DoesNotContain("Em atendimento", textos);
    }

    [Fact]
    public void Aviso_so_vale_para_a_conversa_cuja_ficha_foi_conferida()
    {
        var avisos = AvisosDoSino();
        Assert.Empty(ConversasNaTela.AvisosDaConversa(null, avisos, PacienteDoAgendamento));
        Assert.Single(ConversasNaTela.AvisosDaConversa(101, avisos, PacienteDoAgendamento));
        Assert.Empty(ConversasNaTela.AvisosDaConversa(999, avisos, PacienteDoAgendamento));
        // Mesmo que a tela passe os avisos, conversa SEM ficha não ganha selo de confirmação.
        var semFicha = WhatsAppDeTeste.Resumo(paciente: null);
        Assert.Equal("Sem ficha", ConversasNaTela.Selo(semFicha, avisos, 0, null, Agora).Texto);
    }

    [Fact]
    public void O_selo_segue_a_ordem_do_que_pede_acao()
    {
        var resultados = new Dictionary<string, string> { ["scheduled"] = "Agendado" };
        var encerrada = WhatsAppDeTeste.Resumo(encerrada: true, paciente: 101,
            resultado: new AppResolution("scheduled", "Marcou o BSV", "bruna", Agora));
        Assert.Equal(new SeloDoWhatsApp("Encerrada · Agendado", "neutro"), ConversasNaTela.Selo(encerrada, AvisosDoSino(), 1, null, Agora, resultados));
        var semRotulo = encerrada with { Resolution = new AppResolution("chave-nova", "x", "bruna", Agora) };
        Assert.Equal("Encerrada", ConversasNaTela.Selo(semRotulo, [], 1, null, Agora, resultados).Texto);

        Assert.Equal(new SeloDoWhatsApp("Mensagem não entregue · conferir", "perigo"),
            ConversasNaTela.Selo(WhatsAppDeTeste.Resumo(paciente: 101, falhaDeEntrega: true), [], 1, null, Agora));
        Assert.Equal(new SeloDoWhatsApp("2 fichas", "info"), ConversasNaTela.Selo(WhatsAppDeTeste.Resumo(), [], 2, null, Agora));
        Assert.Equal(new SeloDoWhatsApp("Ficha a conferir", "info"), ConversasNaTela.Selo(WhatsAppDeTeste.Resumo(), [], 1, null, Agora));
        Assert.Equal(new SeloDoWhatsApp("Sem ficha", "neutro"), ConversasNaTela.Selo(WhatsAppDeTeste.Resumo(), [], 0, null, Agora));
        Assert.Equal("No menu de atendimento", ConversasNaTela.Selo(WhatsAppDeTeste.Resumo(paciente: 101, noMenu: true), [], 1, null, Agora).Texto);

        Assert.Equal(new SeloDoWhatsApp("Aguardando paciente", "neutro"),
            ConversasNaTela.Selo(WhatsAppDeTeste.Resumo(paciente: 101, agente: "ana", direcao: "out", status: "seen"), [], 1, null, Agora));
        Assert.Equal("Em atendimento", ConversasNaTela.Selo(WhatsAppDeTeste.Resumo(paciente: 101, agente: "ana"), [], 1, null, Agora).Texto);
        Assert.Equal("Nova · sem responsável", ConversasNaTela.Selo(WhatsAppDeTeste.Resumo(paciente: 101), [], 1, null, Agora).Texto);
    }

    [Fact]
    public void O_caso_do_acompanhamento_aparece_na_linha_e_o_respondeu_vence_a_confirmacao()
    {
        var caso = WhatsAppDeTeste.Caso(paciente: 101, dias: 20, modalidade: ModalidadeAtendimento.AcupunturaSimples,
            tentativas: 1, ultimoContato: new DateTime(2026, 10, 23, 15, 10, 0), proximo: new DateOnly(2026, 10, 30));
        var aguardando = WhatsAppDeTeste.Resumo(paciente: 101, agente: "bruna", ultimaRecebida: WhatsAppDeTeste.Em(4, 10, 20));
        Assert.Equal(new SeloDoWhatsApp("Acompanhamento · 20 dias", "info"), ConversasNaTela.Selo(aguardando, [], 1, caso, Agora));

        var respondeu = aguardando with { LastIncoming = new DateTimeOffset(2026, 10, 23, 20, 40, 0, TimeSpan.Zero) }; // 17:40 em Brasília
        Assert.Equal(new SeloDoWhatsApp("Respondeu ao convite", "sucesso"), ConversasNaTela.Selo(respondeu, [], 1, caso, Agora));

        // O caso de OUTRA ficha, o fechado e o de consulta não aparecem.
        Assert.Equal("Em atendimento", ConversasNaTela.Selo(aguardando, [], 1, caso with { PacienteId = 555 }, Agora).Texto);
        Assert.Equal("Em atendimento", ConversasNaTela.Selo(aguardando, [], 1, caso with { Pendente = false }, Agora).Texto);
        Assert.Equal("Em atendimento", ConversasNaTela.Selo(aguardando, [], 1, caso with { Modalidade = ModalidadeAtendimento.Consulta }, Agora).Texto);
    }

    [Fact]
    public void O_caso_do_paciente_prefere_o_aberto_de_quem_trata()
    {
        var aberto = WhatsAppDeTeste.Caso(id: 1, paciente: 101, dias: 30);
        var fechado = WhatsAppDeTeste.Caso(id: 2, paciente: 101, dias: 5, pendente: false);
        var consulta = WhatsAppDeTeste.Caso(id: 3, paciente: 101, dias: 90, modalidade: ModalidadeAtendimento.Consulta);
        Assert.Equal(1, ConversasNaTela.CasoDoPaciente(101, [fechado, consulta, aberto])!.Id);
        Assert.Equal(2, ConversasNaTela.CasoDoPaciente(101, [fechado, consulta])!.Id);
        Assert.Null(ConversasNaTela.CasoDoPaciente(null, [aberto]));
        Assert.Null(ConversasNaTela.CasoDoPaciente(202, [aberto]));
    }

    // ------------------------------------------------------------ ficha, nome e telefone

    [Fact]
    public void A_ficha_e_conferida_so_com_PatientId_e_o_telefone_nunca_decide_sozinho()
    {
        var conferida = ConversasNaTela.EstadoDaFicha(4512, "Maria das Graças Silva", 1);
        Assert.Equal(EstadoFicha.Conferida, conferida.Estado);
        Assert.False(conferida.PedeConferir);
        Assert.Equal("Ficha conferida", conferida.Texto);

        var nenhuma = ConversasNaTela.EstadoDaFicha(null, "", 0);
        Assert.Equal((EstadoFicha.Nenhuma, "Sem ficha — buscar"), (nenhuma.Estado, nenhuma.Texto));

        var uma = ConversasNaTela.EstadoDaFicha(null, "", 1);
        Assert.Equal((EstadoFicha.AConferir, 1, "Ficha a conferir"), (uma.Estado, uma.Candidatas, uma.Texto));
        Assert.True(uma.PedeConferir);

        var duas = ConversasNaTela.EstadoDaFicha(WhatsAppDeTeste.Conversa(paciente: null), 2);
        Assert.Equal((EstadoFicha.AConferir, 2, "2 fichas — conferir"), (duas.Estado, duas.Candidatas, duas.Texto));
    }

    [Fact]
    public void Telefone_do_CRM_e_do_cadastro_se_comparam_pelo_numero_com_DDI()
    {
        Assert.Equal("(22) 99876-5432", ConversasNaTela.TelefoneNaTela("5522998765432"));
        Assert.Equal("(22) 3333-0000", ConversasNaTela.TelefoneNaTela("552233330000"));
        Assert.True(ConversasNaTela.MesmoTelefone("5522998765432", "(22) 99876-5432"));
        Assert.True(ConversasNaTela.MesmoTelefone("552233330000", "22 3333-0000"));
        Assert.False(ConversasNaTela.MesmoTelefone("5522998765432", "(22) 99876-5431"));
        Assert.False(ConversasNaTela.MesmoTelefone("", ""));
        Assert.False(ConversasNaTela.MesmoTelefone("123", "123"));
    }

    [Fact]
    public void O_nome_da_conversa_e_o_da_ficha_senao_o_do_CRM_senao_o_telefone()
    {
        Assert.Equal("Pedro Henrique Souza", ConversasNaTela.NomeDaConversa("Terezinha", "Pedro Henrique Souza", "5522988112233"));
        Assert.Equal("Terezinha Souza", ConversasNaTela.NomeDaConversa("Terezinha Souza", "", "5522988112233"));
        Assert.Equal("(22) 99712-0045", ConversasNaTela.NomeDaConversa("5522997120045", "", "5522997120045"));
    }

    // ------------------------------------------------------------ o que pede ação

    private static readonly BotaoDaAcao[] BotoesDeEnvio = [BotaoDaAcao.PrepararConvite, BotaoDaAcao.PrepararMensagem];

    [Fact]
    public void Nao_vem_pede_Remarcar_ou_Manter_com_o_horario_do_aviso()
    {
        var avisos = ConversasNaTela.AvisosDaConversa(101, AvisosDoSino(), PacienteDoAgendamento);
        var acao = ConversasNaTela.PedeAcao(WhatsAppDeTeste.Conversa(), avisos, 1, null, Agora)!;
        Assert.Equal(TipoDaAcaoPedida.NaoVem, acao.Tipo);
        Assert.Equal("Maria não vem hoje 14:00 (Dra. Helena).", acao.Titulo);
        Assert.Equal([BotaoDaAcao.Remarcar, BotaoDaAcao.ManterHorario], acao.Botoes.Select(b => b.Acao));
        Assert.True(acao.Botoes[0].Primario);
        Assert.All(acao.Botoes, b => Assert.Equal(1, b.AgendamentoId));
        Assert.Equal("Manter o horário", acao.Botoes[1].Rotulo);
    }

    [Fact]
    public void Sem_resposta_e_pediu_cancelar_pedem_os_botoes_da_agenda()
    {
        var semResposta = ConversasNaTela.PedeAcao(WhatsAppDeTeste.Conversa(paciente: 102, nomeDaFicha: "Carlos Eduardo Mota"),
            ConversasNaTela.AvisosDaConversa(102, AvisosDoSino(), PacienteDoAgendamento), 1, null, Agora)!;
        Assert.Equal("Carlos não respondeu ao lembrete de hoje 16:00 (Dr. Marcos).", semResposta.Titulo);
        Assert.Equal([BotaoDaAcao.ConfirmouPorTelefone, BotaoDaAcao.NaoVem], semResposta.Botoes.Select(b => b.Acao));
        Assert.True(semResposta.Botoes[1].Perigo);

        var cancelar = ConversasNaTela.PedeAcao(WhatsAppDeTeste.Conversa(paciente: 104, nomeDaFicha: "Pedro Henrique Souza"),
            ConversasNaTela.AvisosDaConversa(104, AvisosDoSino(), PacienteDoAgendamento), 1, null, Agora)!;
        Assert.Equal(TipoDaAcaoPedida.PediuCancelar, cancelar.Tipo);
        Assert.Equal(BotaoDaAcao.AceitarCancelamento, cancelar.Botoes[0].Acao);

        // Confirmou é notícia, não pede nada.
        Assert.Null(ConversasNaTela.PedeAcao(WhatsAppDeTeste.Conversa(paciente: 103),
            ConversasNaTela.AvisosDaConversa(103, AvisosDoSino(), PacienteDoAgendamento), 1, null, Agora));
    }

    [Fact]
    public void Conversa_encerrada_nao_pede_nada()
    {
        var avisos = ConversasNaTela.AvisosDaConversa(101, AvisosDoSino(), PacienteDoAgendamento);
        Assert.Null(ConversasNaTela.PedeAcao(WhatsAppDeTeste.Conversa(encerrada: true), avisos, 1, WhatsAppDeTeste.Caso(), Agora));
    }

    [Fact]
    public void Sem_ficha_conferida_pede_conferir_ou_buscar_e_cadastrar()
    {
        var duas = ConversasNaTela.PedeAcao(WhatsAppDeTeste.Conversa(paciente: null, nomeDaFicha: ""), [], 2, null, Agora)!;
        Assert.Equal((TipoDaAcaoPedida.ConferirFicha, "Duas fichas têm este telefone."), (duas.Tipo, duas.Titulo));
        Assert.Equal([BotaoDaAcao.ConferirFicha], duas.Botoes.Select(b => b.Acao));

        var uma = ConversasNaTela.PedeAcao(WhatsAppDeTeste.Conversa(paciente: null, nomeDaFicha: ""), [], 1, null, Agora)!;
        Assert.Equal(TipoDaAcaoPedida.ConferirFicha, uma.Tipo);

        var nova = ConversasNaTela.PedeAcao(WhatsAppDeTeste.Conversa(paciente: null, nomeDaFicha: ""), [], 0, null, Agora)!;
        Assert.Equal(TipoDaAcaoPedida.NovoContato, nova.Tipo);
        Assert.Equal([BotaoDaAcao.BuscarFicha, BotaoDaAcao.CadastrarPaciente], nova.Botoes.Select(b => b.Acao));
    }

    [Fact]
    public void A_confirmacao_vence_o_caso_da_retencao()
    {
        var avisos = ConversasNaTela.AvisosDaConversa(101, AvisosDoSino(), PacienteDoAgendamento);
        var caso = WhatsAppDeTeste.Caso(paciente: 101, tentativas: 1, ultimoContato: new DateTime(2026, 10, 20, 9, 0, 0));
        var conversa = WhatsAppDeTeste.Conversa([WhatsAppDeTeste.Recebida("m1", WhatsAppDeTeste.Em(24, 9, 58))]);
        Assert.Equal(TipoDaAcaoPedida.NaoVem, ConversasNaTela.PedeAcao(conversa, avisos, 1, caso, Agora)!.Tipo);
        Assert.Equal(TipoDaAcaoPedida.RetencaoRespondeu, ConversasNaTela.PedeAcao(conversa, [], 1, caso, Agora)!.Tipo);
    }

    [Fact]
    public void Retencao_sem_mensagens_nao_pede_aqui_porque_o_bloco_de_abertura_responde()
    {
        var caso = WhatsAppDeTeste.Caso(paciente: 101);
        Assert.Null(ConversasNaTela.PedeAcao(null, [], 1, caso, Agora));
        Assert.Null(ConversasNaTela.PedeAcao(WhatsAppDeTeste.Conversa(), [], 1, caso, Agora));
        Assert.NotNull(RetencaoNaTela.Abertura(caso, null, Agora));
    }

    [Fact]
    public void Retencao_que_respondeu_pede_o_Como_foi_com_os_cinco_desfechos()
    {
        var caso = WhatsAppDeTeste.Caso(paciente: 101, nome: "Alzira Pinto", tentativas: 1,
            ultimoContato: new DateTime(2026, 10, 22, 15, 12, 0), proximo: new DateOnly(2026, 10, 29));
        var conversa = WhatsAppDeTeste.Conversa([WhatsAppDeTeste.Enviada("m1", WhatsAppDeTeste.Em(22, 15, 12)),
            WhatsAppDeTeste.Recebida("m2", WhatsAppDeTeste.Em(23, 17, 40))], nomeDaFicha: "Alzira Pinto");
        var acao = ConversasNaTela.PedeAcao(conversa, [], 1, caso, Agora)!;
        Assert.Equal("Alzira respondeu ao convite. Como foi?", acao.Titulo);
        Assert.Equal([DesfechoContato.QuerMarcar, DesfechoContato.PediuOutraData, DesfechoContato.AguardandoPlano,
            DesfechoContato.NaoRespondeu, DesfechoContato.NaoQuerVoltar], acao.Botoes.Select(b => b.Desfecho!.Value));
        Assert.Equal(["Quer marcar", "Pediu outra data", "Aguardando o plano", "Não respondeu", "Não quer voltar"], acao.Botoes.Select(b => b.Rotulo));
        Assert.True(acao.Botoes[^1].Perigo);
    }

    [Fact]
    public void Retencao_aguardando_e_convite_com_mensagens()
    {
        var aguardando = WhatsAppDeTeste.Caso(paciente: 101, nome: "Fernanda Nogueira", modalidade: ModalidadeAtendimento.AcupunturaSimples,
            dias: 20, tentativas: 1, ultimoContato: new DateTime(2026, 10, 23, 15, 10, 0), proximo: new DateOnly(2026, 10, 30));
        var conversa = WhatsAppDeTeste.Conversa([WhatsAppDeTeste.Recebida("m1", WhatsAppDeTeste.Em(4, 10, 20)),
            WhatsAppDeTeste.Enviada("m2", WhatsAppDeTeste.Em(23, 15, 10))], nomeDaFicha: "Fernanda Nogueira");
        var acao = ConversasNaTela.PedeAcao(conversa, [], 1, aguardando, Agora)!;
        Assert.Equal((TipoDaAcaoPedida.RetencaoAguardando, "Contato feito · aguardando Fernanda."), (acao.Tipo, acao.Titulo));
        Assert.Contains("em 30/10", acao.Frase);
        Assert.Equal([BotaoDaAcao.Desfecho, BotaoDaAcao.JaLiguei], acao.Botoes.Select(b => b.Acao));

        var convidar = ConversasNaTela.PedeAcao(conversa, [], 1, aguardando with { ProximoContato = WhatsAppDeTeste.Hoje }, Agora)!;
        Assert.Equal("Fernanda está há 20 dias sem vir (acupuntura) e autorizou mensagens.", convidar.Titulo);
        Assert.Equal(BotaoDaAcao.PrepararConvite, convidar.Botoes[0].Acao);

        var bsv = ConversasNaTela.PedeAcao(conversa, [], 1, aguardando with { ProximoContato = WhatsAppDeTeste.Hoje, Modalidade = ModalidadeAtendimento.BsvApenas, Dias = 32 }, Agora)!;
        Assert.Equal("Fernanda está há 32 dias sem vir (BSV) e autorizou mensagens.", bsv.Titulo);

        var novoBsv = ConversasNaTela.PedeAcao(conversa, [], 1, aguardando with
        {
            ProximoContato = WhatsAppDeTeste.Hoje, Tipo = TipoAcompanhamento.NovoBsv, ReferenciaEm = new DateTime(2026, 10, 21)
        }, Agora)!;
        Assert.Equal("Fernanda tem indicação de BSV desde 21/10 e ainda não marcou a 1ª sessão.", novoBsv.Titulo);
        Assert.Equal(BotaoDaAcao.PrepararMensagem, novoBsv.Botoes[0].Acao);
    }

    [Fact]
    public void Sem_autorizacao_o_aviso_da_conversa_nunca_oferece_envio()
    {
        var conversa = WhatsAppDeTeste.Conversa([WhatsAppDeTeste.Recebida("m1", WhatsAppDeTeste.Em(1, 10, 0))], nomeDaFicha: "Helena Marques");
        foreach (var proximo in new[] { WhatsAppDeTeste.Hoje, new DateOnly(2026, 10, 30) })
        {
            var caso = WhatsAppDeTeste.Caso(paciente: 101, nome: "Helena Marques", consentimento: false, proximo: proximo,
                modalidade: ModalidadeAtendimento.AcupunturaSimples, dias: 30);
            foreach (var c in new[] { conversa, null, WhatsAppDeTeste.Conversa(nomeDaFicha: "Helena Marques") })
            {
                var acao = ConversasNaTela.PedeAcao(c, [], 1, caso, Agora);
                Assert.True(acao is null || !acao.Botoes.Any(b => BotoesDeEnvio.Contains(b.Acao)));
            }
        }
        var paraHoje = ConversasNaTela.PedeAcao(conversa, [], 1,
            WhatsAppDeTeste.Caso(paciente: 101, nome: "Helena Marques", consentimento: false), Agora)!;
        Assert.Equal("Helena não autorizou mensagens: a ação é ligar.", paraHoje.Titulo);
        Assert.Equal([BotaoDaAcao.JaLiguei, BotaoDaAcao.ColherAutorizacao], paraHoje.Botoes.Select(b => b.Acao));
    }

    // ------------------------------------------------------------ a linha do tempo

    [Fact]
    public void A_linha_do_tempo_intercala_por_hora_com_dia_novas_e_seguida()
    {
        var conversa = WhatsAppDeTeste.Conversa(
            mensagens:
            [
                WhatsAppDeTeste.Recebida("m5", WhatsAppDeTeste.Em(24, 10, 9), "Terça às 14h fica ótimo"),
                WhatsAppDeTeste.Enviada("m1", WhatsAppDeTeste.Em(23, 16, 20), "bruna", "seen", "Boa tarde, Maria!"),
                WhatsAppDeTeste.Recebida("m2", WhatsAppDeTeste.Em(24, 9, 58), "Não vou conseguir ir hoje"),
                WhatsAppDeTeste.Recebida("m3", WhatsAppDeTeste.Em(24, 9, 58), "Surgiu um compromisso"),
                WhatsAppDeTeste.Enviada("m4", WhatsAppDeTeste.Em(24, 10, 5), "ana", "delivered", "Tenho terça ou quinta"),
            ],
            notas: [new AppNote("n1", "Ela prefere a tarde.", "ana", WhatsAppDeTeste.Em(24, 10, 2))],
            trilha:
            [
                new AppAudit(WhatsAppDeTeste.Em(24, 9, 58), "Sistema", "Reaberta após nova mensagem"),
                new AppAudit(WhatsAppDeTeste.Em(24, 10, 2), "Ana Paula", "Adicionou uma nota interna"),
            ]);

        var itens = ConversasNaTela.LinhaDoTempo(conversa, 2, Agora, WhatsAppDeTeste.Agentes);
        Assert.Equal(
        [
            (TipoNaLinhaDoTempo.Dia, "Ontem · sexta-feira, 23/10", false),
            (TipoNaLinhaDoTempo.Enviada, "Boa tarde, Maria!", false),
            (TipoNaLinhaDoTempo.Dia, "Hoje · sábado, 24/10", false),
            (TipoNaLinhaDoTempo.Recebida, "Não vou conseguir ir hoje", false),
            (TipoNaLinhaDoTempo.Recebida, "Surgiu um compromisso", true),
            (TipoNaLinhaDoTempo.Evento, "A conversa reabriu sozinha com a nova mensagem do paciente", false),
            (TipoNaLinhaDoTempo.Nota, "Ela prefere a tarde.", false),
            (TipoNaLinhaDoTempo.Enviada, "Tenho terça ou quinta", false),
            (TipoNaLinhaDoTempo.Novas, "1 nova mensagem", false),
            (TipoNaLinhaDoTempo.Recebida, "Terça às 14h fica ótimo", false),
        ], itens.Select(i => (i.Tipo, i.Texto, i.Seguida)));

        var primeira = itens[1];
        Assert.Equal(("Bruna", "16:20", EstadoDaEntrega.Lida), (primeira.Autor, primeira.Hora, primeira.Tick!.Estado));
        Assert.Equal("Ana Paula", itens.Single(i => i.Tipo == TipoNaLinhaDoTempo.Nota).Autor);
        Assert.Null(itens[3].Tick);
    }

    [Fact]
    public void A_divisoria_conta_as_nao_lidas_e_some_quando_tudo_foi_lido()
    {
        var conversa = WhatsAppDeTeste.Conversa(
        [
            WhatsAppDeTeste.Recebida("a", WhatsAppDeTeste.Em(24, 9, 0)),
            WhatsAppDeTeste.Recebida("b", WhatsAppDeTeste.Em(24, 9, 1)),
            WhatsAppDeTeste.Recebida("c", WhatsAppDeTeste.Em(24, 9, 2)),
        ]);
        var todas = ConversasNaTela.LinhaDoTempo(conversa, 0, Agora);
        Assert.Equal((TipoNaLinhaDoTempo.Novas, "3 novas mensagens"), (todas[1].Tipo, todas[1].Texto));
        Assert.Equal("a", todas[2].Id);
        Assert.False(todas[2].Seguida); // a divisória quebra o grupo
        Assert.DoesNotContain(ConversasNaTela.LinhaDoTempo(conversa, 3, Agora), i => i.Tipo == TipoNaLinhaDoTempo.Novas);
        Assert.Equal(1, ConversasNaTela.RecebidasJaLidas(conversa, 2));
        Assert.Equal(0, ConversasNaTela.RecebidasJaLidas(conversa, 9));
    }

    [Fact]
    public void Autor_desconhecido_nunca_aparece_cru_e_a_saida_automatica_diz_que_e_automatica()
    {
        var conversa = WhatsAppDeTeste.Conversa(
        [
            WhatsAppDeTeste.Enviada("a", WhatsAppDeTeste.Em(24, 6, 0), null, "seen", "Lembrete do dia") with
            {
                Template = new AppTemplateSelection("lembrete_sessao", "pt_BR", ["Maria"])
            },
            WhatsAppDeTeste.Enviada("b", WhatsAppDeTeste.Em(24, 7, 0), "f3a9c1", "failed") with { Error = "A Meta recusou o envio." },
            WhatsAppDeTeste.Enviada("c", WhatsAppDeTeste.Em(24, 7, 1), "automation", "accepted"),
        ]);
        var itens = ConversasNaTela.LinhaDoTempo(conversa, 0, Agora, WhatsAppDeTeste.Agentes).Where(i => i.Tipo == TipoNaLinhaDoTempo.Enviada).ToList();
        Assert.Equal(("Mensagem automática", "lembrete_sessao"), (itens[0].Autor, itens[0].Modelo));
        Assert.Equal(("Equipe", "Não entregue. Confira o número ou envie de novo."), (itens[1].Autor, itens[1].Erro));
        Assert.Equal("Mensagem automática", itens[2].Autor);
        Assert.Null(itens[2].Erro);
    }

    // ------------------------------------------------------------ a trilha do CRM

    [Theory]
    [InlineData("Ana Paula", "Assumiu o atendimento humano; respostas automáticas suspensas", "Ana Paula assumiu a conversa")]
    [InlineData("Ana Paula", "Assumiu ao escrever para o paciente", "Ana Paula assumiu a conversa ao responder")]
    [InlineData("Bruna", "Concluiu: Agendado", "Encerrada por Bruna · Agendado")]
    [InlineData("Bruna", "Encerrou o atendimento", "Encerrada por Bruna")]
    [InlineData("Ana Paula", "Transferiu de Recepção / Ana Paula para Financeiro / fila sem responsável",
        "Ana Paula transferiu de Recepção / Ana Paula para Financeiro / fila sem responsável")]
    [InlineData("Ana Paula", "Identificou o paciente: ficha Pedro Henrique Souza (nº 2231).", "Ana Paula ligou a ficha de Pedro Henrique Souza a esta conversa")]
    [InlineData("Bruna", "Cancelou na clínica o horário #77 (paciente viajou). Nenhuma mensagem enviada ao paciente.",
        "Bruna cancelou o horário na agenda da clínica (paciente viajou). Nenhuma mensagem foi ao paciente")]
    [InlineData("Agenda da clínica", "Clínica confirmou novo agendamento #12. Nenhuma mensagem enviada ao paciente.",
        "A agenda da clínica confirmou o novo horário. Nenhuma mensagem foi ao paciente")]
    [InlineData("Ana Paula", "Acompanhamento: Retomar depois · 2026-10-27T12:00:00.0000000+00:00",
        "Ana Paula marcou o acompanhamento: retomar depois · retomar ter 27/10 às 09:00")]
    [InlineData("Paciente", "Pediu para suspender marketing e convites de retorno (vale para WhatsApp e SMS)",
        "O paciente pediu PARAR: nenhum convite de retorno, nem pelo WhatsApp nem por SMS")]
    [InlineData("Paciente", "Paciente pediu remarcação; horário original mantido até a equipe combinar o novo",
        "Paciente pediu remarcação; horário original mantido até a equipe combinar o novo")]
    [InlineData("Agenda da clínica", "Remarcação do horário #44 registrada", "Remarcação do horário registrada")]
    [InlineData("Bruna", "Fez algo que a tabela não conhece #12", "Bruna: movimentação registrada no CRM")]
    [InlineData("system", "Algo do servidor 2026-10-24T13:00:00Z", "Movimentação registrada no CRM")]
    public void A_trilha_vira_frase_legivel(string ator, string acao, string esperado)
        => Assert.Equal(esperado, ConversasNaTela.Evento(new AppAudit(Agora, ator, acao), Agora)!.Texto);

    [Theory]
    [InlineData("Ana Paula", "Adicionou uma nota interna")]
    [InlineData("Ana Paula", "Etapa: Novo contato → Em conversa. Prioridade: Normal → Alta.")]
    [InlineData("Ana Paula", "Gerou sugestão de IA para revisão; nenhuma mensagem enviada")]
    [InlineData("Assistente virtual", "Encaminhou para atendimento humano: pediu humano")]
    [InlineData("system", "Iniciou coleta de respostas humanas para revisão")]
    [InlineData("Ana Paula", "Especialidade do atendimento: ACUP")]
    [InlineData("Ana Paula", "   ")]
    public void Funcao_que_saiu_e_o_que_ja_aparece_de_outro_jeito_somem_da_trilha(string ator, string acao)
        => Assert.Null(ConversasNaTela.Evento(new AppAudit(Agora, ator, acao), Agora));

    [Fact]
    public void Nenhuma_frase_da_trilha_mostra_numero_de_horario_ou_data_ISO()
    {
        string[] acoes =
        [
            "Confirmou paciente e solicitou remarcação do horário #31; aguardando a clínica",
            "Clínica confirmou remarcação do horário #31. Nenhuma mensagem enviada ao paciente.",
            "Cancelou na clínica o horário #31 (sem motivo). Nenhuma mensagem enviada ao paciente.",
            "Acompanhamento: Aguardando paciente · 2026-11-02T15:30:00.0000000+00:00",
            "Desligou a ficha Maria desta conversa.",
            "Regra Fora do expediente: encaminhou para Recepção / fila sem responsável",
            "Solicitou transferência para Bruna / Recepção; aguardando aceite",
            "Recusou a transferência: estou de saída",
        ];
        var re = new Regex(@"#\d|\d{4}-\d{2}-\d{2}T");
        foreach (var ator in new[] { "Ana Paula", "Paciente", "Agenda da clínica", "system" })
            foreach (var acao in acoes)
                if (ConversasNaTela.Evento(new AppAudit(Agora, ator, acao), Agora) is { } e)
                    Assert.False(re.IsMatch(e.Texto), $"{ator}: {acao} → {e.Texto}");
    }

    // ------------------------------------------------------------ a lista

    private static readonly AppConversationSummary[] Lista =
    [
        WhatsAppDeTeste.Resumo("a", "Maria das Graças Silva", "5522998765432", naoLidas: 1, atualizada: WhatsAppDeTeste.Em(24, 10, 9)),
        WhatsAppDeTeste.Resumo("b", "Antônio Carvalho", "5522993339090", agente: "ana", atualizada: WhatsAppDeTeste.Em(24, 9, 0)),
        WhatsAppDeTeste.Resumo("c", "Menu", "5522990000001", noMenu: true, atualizada: WhatsAppDeTeste.Em(24, 8, 0)),
        WhatsAppDeTeste.Resumo("d", "Encerrado", "5522990000002", agente: "bruna", encerrada: true, naoLidas: 4),
        WhatsAppDeTeste.Resumo("e", "Fernanda", "5522998774455", agente: "bruna", naoLidas: 2, atualizada: WhatsAppDeTeste.Em(24, 9, 30)),
        WhatsAppDeTeste.Resumo("f", "5522997120045", "5522997120045", nomeDaFicha: "Lúcia Andrade", atualizada: WhatsAppDeTeste.Em(24, 7, 0)),
    ];

    [Fact]
    public void Os_filtros_e_as_contagens_da_lista()
    {
        Assert.Equal(["f", "a"], ConversasNaTela.Filtrar(Lista, FiltroDasConversas.Novas, "ana").Select(c => c.Id)); // a fila: a mais antiga primeiro
        Assert.Equal(["b"], ConversasNaTela.Filtrar(Lista, FiltroDasConversas.Minhas, "ana").Select(c => c.Id));
        Assert.Empty(ConversasNaTela.Filtrar(Lista, FiltroDasConversas.Minhas, null));
        Assert.Equal(["a", "e", "b", "c", "f"], ConversasNaTela.Filtrar(Lista, FiltroDasConversas.Todas, "ana").Select(c => c.Id));
        Assert.Equal(["d"], ConversasNaTela.Filtrar(Lista, FiltroDasConversas.Encerradas, "ana").Select(c => c.Id));
        Assert.Equal(["a", "e"], ConversasNaTela.Filtrar(Lista, FiltroDasConversas.Todas, "ana", soNaoLidas: true).Select(c => c.Id));
        Assert.Equal(new ContagemDasConversas(2, 1, 5, 1, 2), ConversasNaTela.Contagens(Lista, "ana"));
        Assert.Equal(["Novas", "Minhas", "Todas", "Encerradas"], Enum.GetValues<FiltroDasConversas>().Select(ConversasNaTela.Rotulo));
    }

    [Fact]
    public void So_Novas_e_Minhas_levam_numero_e_acima_de_99_vira_99_mais()
    {
        var c = new ContagemDasConversas(3, 120, 250, 9, 4);
        Assert.Equal("3", ConversasNaTela.ContagemDoFiltro(FiltroDasConversas.Novas, c));
        Assert.Equal("99+", ConversasNaTela.ContagemDoFiltro(FiltroDasConversas.Minhas, c));
        Assert.Null(ConversasNaTela.ContagemDoFiltro(FiltroDasConversas.Todas, c));
        Assert.Null(ConversasNaTela.ContagemDoFiltro(FiltroDasConversas.Encerradas, c));
    }

    [Fact]
    public void A_busca_acha_pelo_nome_sem_acento_pela_ficha_e_pelos_digitos_do_telefone()
    {
        Assert.Equal(["b"], ConversasNaTela.Filtrar(Lista, FiltroDasConversas.Todas, "ana", "antonio").Select(c => c.Id));
        Assert.Equal(["f"], ConversasNaTela.Filtrar(Lista, FiltroDasConversas.Todas, "ana", "lucia").Select(c => c.Id));
        Assert.Equal(["a"], ConversasNaTela.Filtrar(Lista, FiltroDasConversas.Todas, "ana", "99876-5432").Select(c => c.Id));
        Assert.Empty(ConversasNaTela.Filtrar(Lista, FiltroDasConversas.Todas, "ana", "998")); // três dígitos não bastam
        Assert.Equal(("Nenhuma conversa com esse nome", "Confira o nome ou o número."), ConversasNaTela.Vazio(FiltroDasConversas.Novas, true));
        Assert.Equal("Nenhuma conversa nova", ConversasNaTela.Vazio(FiltroDasConversas.Novas, false).Titulo);
    }

    [Fact]
    public void A_ultima_recebida_por_paciente_so_conta_conversa_com_ficha_e_mensagem()
    {
        var mapa = ConversasNaTela.UltimaRecebidaPorPaciente(
        [
            WhatsAppDeTeste.Resumo("x", paciente: 101, ultimaRecebida: WhatsAppDeTeste.Em(20, 9, 0)),
            WhatsAppDeTeste.Resumo("y", paciente: 101, ultimaRecebida: WhatsAppDeTeste.Em(23, 9, 0)),
            WhatsAppDeTeste.Resumo("z", paciente: 102, ultimaRecebida: default(DateTimeOffset)),
            WhatsAppDeTeste.Resumo("w", paciente: null, ultimaRecebida: WhatsAppDeTeste.Em(24, 9, 0)),
        ]);
        Assert.Equal(WhatsAppDeTeste.Em(23, 9, 0), Assert.Single(mapa).Value);
    }
}
