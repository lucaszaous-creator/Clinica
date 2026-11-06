using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela de WhatsApp afirma sobre o @SemDor (24/10/2026, <see cref="SemDorNaTela"/>): desligado sai inteiro,
/// a etiqueta só vale enquanto o paciente espera, a sugestão vai para o campo (e perde para quem continuou
/// escrevendo), a revisão guarda o texto dela para o ↶, e a falha mostra a frase do CRM.
/// </summary>
public sealed class SemDorNaTelaTests
{
    private static readonly AppSemDorState Ligada = new(true, true, DateTimeOffset.UnixEpoch, null, "");
    private static readonly AppSemDorTag Remarcar = new("remarcar", "Remarcar", false);

    private static AppConversationSummary Linha(AppSemDorTag? tag, bool fechada = false, string direcao = "in")
        => new("c1", "Maria", "5522998765432", "whatsapp", "5522333300000", "Recepção", null, fechada, 1, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch, null, "", 1, "Posso remarcar?", false, false, null, null, direcao, "received", tag);

    private static AppConversation Conversa(bool fechada, params string[] direcoes)
        => new("c1", "Maria", "5522998765432", "whatsapp", "5522333300000", "Recepção", "ana", fechada, 2, DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch, null, "",
            direcoes.Select((d, i) => new AppMessage("m" + i, d, "texto", "TEXT", d == "out" ? "ana" : null, DateTimeOffset.UnixEpoch.AddMinutes(i), "received")).ToArray(),
            [], [], [], null);

    [Fact]
    public void Desligado_ou_sem_estado_nao_mostra_nada_do_SemDor()
    {
        Assert.False(SemDorNaTela.Ligado(null));
        Assert.False(SemDorNaTela.Ligado(new AppSemDorState(false, false, null, null, "")));
        Assert.True(SemDorNaTela.Ligado(Ligada));
        // Ligada e falhando continua com os botões: a frase aparece no clique, e ela escreve normalmente.
        Assert.True(SemDorNaTela.Ligado(new AppSemDorState(true, false, null, DateTimeOffset.UnixEpoch, "Fale com a gestão.")));

        Assert.Null(SemDorNaTela.Etiqueta(null, Linha(Remarcar)));
        Assert.Null(SemDorNaTela.Etiqueta(new AppSemDorState(false, false, null, null, ""), Linha(Remarcar)));
    }

    [Fact]
    public void Etiqueta_so_vale_enquanto_o_paciente_espera_e_a_atencao_e_vermelha()
    {
        Assert.Equal(new EtiquetaDoSemDor("Remarcar", false, SemDorNaTela.DicaDoAssunto), SemDorNaTela.Etiqueta(Ligada, Linha(Remarcar)));
        var urgente = SemDorNaTela.Etiqueta(Ligada, Linha(new AppSemDorTag("urgencia", " Urgência clínica ", true)))!;
        Assert.Equal(("Urgência clínica", true, SemDorNaTela.DicaDaAtencao), (urgente.Texto, urgente.Atencao, urgente.Dica));

        Assert.Null(SemDorNaTela.Etiqueta(Ligada, Linha(null)));
        Assert.Null(SemDorNaTela.Etiqueta(Ligada, Linha(Remarcar, fechada: true)));
        Assert.Null(SemDorNaTela.Etiqueta(Ligada, Linha(Remarcar, direcao: "out")));
        Assert.Null(SemDorNaTela.Etiqueta(Ligada, Linha(new AppSemDorTag("outro", "  ", false))));
    }

    [Fact]
    public void Etiqueta_sai_quando_a_tela_responde_ou_encerra_e_fica_com_a_nota()
    {
        Assert.Null(SemDorNaTela.EtiquetaDepoisDaEscrita(Remarcar, Conversa(false, "in", "out")));
        Assert.Null(SemDorNaTela.EtiquetaDepoisDaEscrita(Remarcar, Conversa(true, "in")));
        // A nota interna não muda as mensagens: o paciente continua esperando.
        Assert.Same(Remarcar, SemDorNaTela.EtiquetaDepoisDaEscrita(Remarcar, Conversa(false, "out", "in")));
    }

    [Fact]
    public void Campo_vazio_sugere_e_com_texto_revisa_e_o_nome_do_botao_acompanha()
    {
        Assert.Equal(PedidoAoSemDor.Sugerir, SemDorNaTela.Pedido("  \n "));
        Assert.Null(SemDorNaTela.RascunhoDoPedido(" "));
        Assert.Equal(PedidoAoSemDor.Revisar, SemDorNaTela.Pedido("oi"));
        Assert.Equal("oi maria", SemDorNaTela.RascunhoDoPedido("  oi maria \n"));

        Assert.Equal("Sugerir resposta com o @SemDor", SemDorNaTela.NomeDoBotao("", escrevendo: false));
        Assert.Equal("Melhorar o texto com o @SemDor", SemDorNaTela.NomeDoBotao("oi", escrevendo: false));
        Assert.Equal("O @SemDor está escrevendo…", SemDorNaTela.NomeDoBotao("oi", escrevendo: true));
        Assert.Contains("Você revisa antes de enviar", SemDorNaTela.DicaDoBotao(null, false));
        Assert.Contains("sem mudar o sentido", SemDorNaTela.DicaDoBotao("oi", false));
    }

    [Fact]
    public void Sugestao_entra_no_campo_vazio_sem_desfazer()
    {
        var r = SemDorNaTela.Aplicar("", "", false, new AppSemDorSuggestion("  Olá, Maria! Vou verificar na agenda.  ", "sugestao"));
        Assert.Equal((true, "Olá, Maria! Vou verificar na agenda.", (string?)null), (r.Aplicada, r.Texto, r.Anterior));
        Assert.Contains("Revise antes de enviar", r.Aviso);
    }

    [Fact]
    public void Revisao_troca_o_texto_e_guarda_o_dela_para_o_desfazer()
    {
        var r = SemDorNaTela.Aplicar("pode vir quinta", "pode vir quinta\n", false, new AppSemDorSuggestion("Pode vir na quinta?", "revisao"));
        Assert.Equal((true, "Pode vir na quinta?", "pode vir quinta\n"), (r.Aplicada, r.Texto, r.Anterior));
        Assert.Contains("Confira antes de enviar", r.Aviso);
    }

    [Fact]
    public void Quem_continuou_escrevendo_vence_a_sugestao_e_texto_vazio_nao_apaga_o_campo()
    {
        var continuou = SemDorNaTela.Aplicar("pode vir", "pode vir quinta às 14h", false, new AppSemDorSuggestion("Pode vir?", "revisao"));
        Assert.Equal((false, "pode vir quinta às 14h", (string?)null), (continuou.Aplicada, continuou.Texto, continuou.Anterior));
        Assert.Contains("continuou escrevendo", continuou.Aviso);

        var vazia = SemDorNaTela.Aplicar("oi", "oi", false, new AppSemDorSuggestion("  ", "revisao"));
        Assert.Equal((false, "oi"), (vazia.Aplicada, vazia.Texto));
        Assert.Equal(SemDorNaTela.SemResposta, vazia.Aviso);
    }

    [Fact]
    public void Sugestao_nao_entra_na_nota_interna()
    {
        var r = SemDorNaTela.Aplicar("", "", true, new AppSemDorSuggestion("Olá, Maria!", "sugestao"));
        Assert.Equal((false, ""), (r.Aplicada, r.Texto));
        Assert.Contains("nota interna", r.Aviso);
    }

    [Fact]
    public void Desfazer_so_enquanto_o_texto_do_SemDor_esta_intacto()
    {
        Assert.True(SemDorNaTela.PodeDesfazer("Pode vir na quinta?", "Pode vir na quinta? "));
        Assert.False(SemDorNaTela.PodeDesfazer("Pode vir na quinta?", "Pode vir na quinta? Às 14h."));
        Assert.False(SemDorNaTela.PodeDesfazer("Pode vir na quinta?", ""));
        Assert.False(SemDorNaTela.PodeDesfazer(null, "qualquer"));
    }

    [Fact]
    public void Falha_mostra_a_frase_do_CRM_e_so_cai_na_padrao_sem_ela()
    {
        Assert.Equal("O @SemDor está desligado neste CRM.", SemDorNaTela.Falha(new RecusaDoCrm("O @SemDor está desligado neste CRM.", 409)));
        Assert.Equal("O @SemDor não conseguiu resumir agora. Tente de novo em instantes.",
            SemDorNaTela.Falha(new CrmIndisponivel("O @SemDor não conseguiu resumir agora. Tente de novo em instantes.")));
        Assert.Equal(SemDorNaTela.SemResposta, SemDorNaTela.Falha(new InvalidOperationException("detalhe interno")));
    }

    [Fact]
    public void Resumo_tem_tres_linhas_rotuladas_e_vazio_vira_travessao()
    {
        Assert.Equal([("O paciente quer", "Remarcar a sessão de hoje"), ("Já foi feito", "—"), ("Falta", "Confirmar o novo horário")],
            SemDorNaTela.LinhasDoResumo(new AppSemDorSummary(" Remarcar a sessão de hoje ", "", "Confirmar o novo horário")));
    }

    [Fact]
    public void Aviso_da_gestao_so_quando_ligada_parou_e_o_motivo_veio()
    {
        Assert.Equal("O @SemDor não está respondendo: conta sem crédito.",
            SemDorNaTela.AvisoDaGestao(new AppSemDorState(true, false, null, DateTimeOffset.UnixEpoch, "conta sem crédito.")));
        Assert.Null(SemDorNaTela.AvisoDaGestao(new AppSemDorState(true, false, null, DateTimeOffset.UnixEpoch, SemDorNaTela.MotivoSoDaGestao)));
        Assert.Null(SemDorNaTela.AvisoDaGestao(new AppSemDorState(true, true, null, null, "")));
        Assert.Null(SemDorNaTela.AvisoDaGestao(new AppSemDorState(false, false, null, null, "desligada")));
        Assert.Null(SemDorNaTela.AvisoDaGestao(null));
    }
}
