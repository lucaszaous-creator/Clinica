using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A auditoria do atendimento (26/10): o que a tela afirma — rótulos e tons das notas, "x de y no prazo", a ordem da
/// fila, o formulário que parte do rascunho do @SemDor, o que falta para concluir e o corpo do POST — mora em
/// <see cref="AuditoriaNaTela"/>, puro. A nota que vale é a da gerente: o rascunho do @SemDor só preenche.
/// </summary>
public sealed class AuditoriaDoAtendimentoTests
{
    static readonly DateTimeOffset Agora = new(2026, 10, 26, 10, 0, 0, TimeSpan.FromHours(-3));
    static readonly DateOnly Hoje = new(2026, 10, 26);

    static readonly AppQualityCriterion[] Criterios =
    [
        new("courtesy", "Cordialidade e respeito", "g1"),
        new("understanding", "Entendimento da necessidade", "g2"),
        new("clarity", "Clareza da orientação", "g3"),
        new("resolution", "Encaminhamento e resolução", "g4"),
        new("followup", "Confirmação e continuidade", "g5")
    ];

    static AppQualityItem Item(string conversa, string estado, int horasAtras, string secretaria = "ana")
        => new(conversa, secretaria, "Ana", "Maria", Agora.AddHours(-horasAtras), "Sorteio da semana", estado, 70, null, false);

    static AppConversation Conversa(AppMessage[]? mensagens = null, AppNote[]? notas = null)
        => new("c1", "Maria", "5511999990000", "whatsapp", "s1", "Recepção", "ana", true, 3, Agora, Agora.AddHours(-2), 10, "Maria Souza",
            mensagens ?? [
                new AppMessage("m1", "in", "Oi, quero remarcar", "TEXT", null, Agora.AddHours(-3), "received"),
                new AppMessage("m2", "out", "Bom dia, Maria! Claro.", "TEXT", "ana", Agora.AddHours(-3).AddMinutes(4), "delivered"),
                new AppMessage("m3", "out", "Lembrete da sua sessão", "TEMPLATE", null, Agora.AddHours(-2), "delivered"),
                new AppMessage("m4", "out", "Segue o horário", "TEXT", "bia", Agora.AddHours(-1), "failed", "Número inválido")
            ],
            notas ?? [new AppNote("n1", "Paciente pediu manhã", "ana", Agora.AddHours(-3).AddMinutes(5))], [], [], Agora);

    static AppQualityReview Revisao(int versao, string revisor, string estado, string secretaria = "ana", int? nota = 1,
        string outcome = "inconclusive", string action = "", DateTimeOffset? due = null, decimal? score = null)
        => new(secretaria, versao, estado, revisor, Agora.AddHours(-versao), Criterios.Select(k =>
                new AppQualityAnswer(k.Id, nota, $"Motivo de {k.Id} bem explicado", ["m2"])).ToArray(),
            outcome, revisor == "semdor" ? "" : "Muito bem", action, due, score);

    static AppQualityDetail Detalhe(params AppQualityReview[] historico)
        => new(Conversa(), "impressao-1", Criterios, [new AppQualityFinding("media", "info", "Há anexos", "d", ["m1"])], ["ana", "bia"],
            historico.OrderByDescending(r => r.Version).ToArray());

    static readonly IReadOnlyDictionary<string, string> Nomes = new Dictionary<string, string> { ["ana"] = "Ana", ["bia"] = "Bia" };

    // ------------------------------------------------------------------ rótulos e tons

    [Theory]
    [InlineData(-1, "Não avaliável", "neutro")]
    [InlineData(0, "Não atendeu", "perigo")]
    [InlineData(1, "Em parte", "aviso")]
    [InlineData(2, "Atendeu", "sucesso")]
    [InlineData(null, "Sem nota", "neutro")]
    public void Rotulo_e_tom_de_cada_nota(int? nota, string rotulo, string tom)
    {
        Assert.Equal(rotulo, AuditoriaNaTela.RotuloDaNota(nota));
        Assert.Equal(tom, AuditoriaNaTela.TomDaNota(nota));
    }

    [Fact]
    public void As_quatro_opcoes_vem_na_ordem_da_tela()
        => Assert.Equal([-1, 0, 1, 2], AuditoriaNaTela.Opcoes.Select(o => o.Valor));

    [Theory]
    [InlineData(null, "neutro", "—")]
    [InlineData(100.0, "sucesso", "100")]
    [InlineData(80.0, "sucesso", "80")]
    [InlineData(79.5, "aviso", "79,5")]
    [InlineData(60.0, "aviso", "60")]
    [InlineData(59.0, "perigo", "59")]
    [InlineData(0.0, "perigo", "0")]
    public void Tom_e_texto_da_nota_final(double? nota, string tom, string texto)
    {
        decimal? n = nota is null ? null : (decimal)nota.Value;
        Assert.Equal(tom, AuditoriaNaTela.TomDaNotaFinal(n));
        Assert.Equal(texto, AuditoriaNaTela.NotaFinal(n));
    }

    [Fact]
    public void Nota_prevista_e_a_media_das_avaliaveis_vezes_50()
    {
        Assert.Null(AuditoriaNaTela.NotaPrevista([null, -1]));
        Assert.Equal(100m, AuditoriaNaTela.NotaPrevista([2, 2, -1, null]));
        Assert.Equal(50m, AuditoriaNaTela.NotaPrevista([2, 0]));
        Assert.Equal(67m, AuditoriaNaTela.NotaPrevista([2, 1, 1])); // 1,333 × 50 = 66,7
    }

    // ------------------------------------------------------------------ os números da secretária

    [Theory]
    [InlineData(12, 15, "12 de 15 no prazo", "sucesso")]
    [InlineData(7, 10, "7 de 10 no prazo", "aviso")]
    [InlineData(2, 10, "2 de 10 no prazo", "perigo")]
    [InlineData(0, 0, "nenhuma resposta medida", "neutro")]
    public void No_prazo_diz_x_de_y(int noPrazo, int medidas, string texto, string tom)
    {
        Assert.Equal(texto, AuditoriaNaTela.NoPrazo(noPrazo, medidas));
        Assert.Equal(tom, AuditoriaNaTela.TomDoPrazo(noPrazo, medidas));
    }

    [Theory]
    [InlineData(null, "—")]
    [InlineData(0.4, "menos de 1 min")]
    [InlineData(8.4, "8 min")]
    [InlineData(60.0, "1 h")]
    [InlineData(80.0, "1 h 20 min")]
    public void Primeira_resposta_em_minutos_uteis(double? minutos, string texto)
        => Assert.Equal(texto, AuditoriaNaTela.PrimeiraResposta(minutos));

    [Fact]
    public void Cartao_da_secretaria_escreve_os_numeros()
    {
        var c = AuditoriaNaTela.Cartao(new AppQualityAgent("ana", "Ana", 30, 6.6, 9, 10, 2, 20, 3, 1, 1, 82m, 2));
        Assert.Equal("7 min", c.PrimeiraResposta);
        Assert.Equal("9 de 10 no prazo", c.NoPrazo);
        Assert.Equal("sucesso", c.TomDoPrazo);
        Assert.Equal("82", c.Nota);
        Assert.Equal("1 avaliação", c.Avaliacoes);
        Assert.Equal("nenhuma avaliação", AuditoriaNaTela.Cartao(new AppQualityAgent("b", "B", 0, null, 0, 0, 0, 0, 0, 0, 0, null, 0)).Avaliacoes);
    }

    // ------------------------------------------------------------------ a fila

    [Fact]
    public void Fila_poe_para_conferir_primeiro_e_depois_a_mais_recente()
    {
        var fila = AuditoriaNaTela.OrdenarFila([
            Item("a", "avaliada", 1), Item("b", "para-conferir", 30), Item("c", "para-conferir", 2), Item("d", "avaliada", 10)
        ]);
        Assert.Equal(["c", "b", "a", "d"], fila.Select(i => i.ConversationId));
    }

    [Fact]
    public void Filtro_da_fila_por_secretaria()
    {
        var fila = new[] { Item("a", "avaliada", 1, "ana"), Item("b", "avaliada", 2, "bia") };
        Assert.Equal(2, AuditoriaNaTela.FiltrarFila(fila, null).Count);
        Assert.Equal(["b"], AuditoriaNaTela.FiltrarFila(fila, "bia").Select(i => i.ConversationId));
    }

    [Theory]
    [InlineData("Reclamação ou pedido de esclarecimento", "perigo")]
    [InlineData("Encerrada com mensagem sem resposta", "aviso")]
    [InlineData("Espera longa pela primeira resposta", "aviso")]
    [InlineData("Sorteio da semana", "neutro")]
    public void Tom_do_motivo(string motivo, string tom) => Assert.Equal(tom, AuditoriaNaTela.TomDoMotivo(motivo));

    [Fact]
    public void Estado_da_fila_nunca_aparece_cru()
    {
        Assert.Equal("Para conferir", AuditoriaNaTela.RotuloDoEstado("para-conferir"));
        Assert.Equal("Avaliada", AuditoriaNaTela.RotuloDoEstado("avaliada"));
    }

    [Fact]
    public void Nomes_juntam_a_equipe_o_painel_e_a_fila()
    {
        var painel = new AppQualityPanel(Agora, Agora, [new AppQualityAgent("ana", "Ana P.", 0, null, 0, 0, 0, 0, 0, 0, 0, null, 0)], true);
        var nomes = AuditoriaNaTela.Nomes([new AppAgent("ger", "Gerente")], painel, [Item("x", "avaliada", 1, "cris") with { SubjectName = "Cris" }]);
        Assert.Equal("Gerente", nomes["ger"]);
        Assert.Equal("Ana P.", nomes["ana"]);
        Assert.Equal("Cris", nomes["cris"]);
    }

    // ------------------------------------------------------------------ a conversa

    [Fact]
    public void Conversa_marca_a_avaliada_a_automatica_a_falha_e_a_nota_interna()
    {
        var d = AuditoriaNaTela.Detalhe(Detalhe(), Item("c1", "para-conferir", 1), Nomes, Agora);

        Assert.Equal(["m1", "m2", "n1", "m3", "m4"], d.Linhas.Select(l => l.Id));
        var recebida = d.Linhas[0];
        Assert.Equal("recebida", recebida.Tipo);
        Assert.Equal("Maria Souza", recebida.Autor);
        Assert.False(recebida.DaAvaliada);

        var dela = d.Linhas[1];
        Assert.True(dela.DaAvaliada);
        Assert.Equal("Ana", dela.Autor);

        var nota = d.Linhas[2];
        Assert.True(nota.Nota);
        Assert.False(nota.DaAvaliada);
        Assert.Null(d.Mensagem("n1")); // nota interna não é mensagem ao paciente nem evidência

        Assert.Equal("automatica", d.Linhas[3].Tipo);
        Assert.Equal("[Modelo] Lembrete da sua sessão", d.Linhas[3].Texto);

        var falhou = d.Linhas[4];
        Assert.False(falhou.DaAvaliada);
        Assert.Equal("Bia", falhou.Autor);
        Assert.Equal("Não enviada: Número inválido", falhou.Situacao);

        Assert.Equal(["Bia"], d.OutrasPessoas);
        Assert.Equal("07:00", recebida.Hora);
        Assert.StartsWith("Hoje", recebida.Dia);
    }

    [Fact]
    public void Texto_da_midia_vem_com_o_tipo_entre_colchetes()
    {
        static AppMessage M(string tipo, string texto = "", string? arquivo = null, bool sticker = false)
            => new("x", "in", texto, tipo, null, Agora, "received", FileName: arquivo, Sticker: sticker);
        Assert.Equal("[Foto] olha o exame", AuditoriaNaTela.TextoDa(M("IMAGE", "olha o exame")));
        Assert.Equal("[Figurinha]", AuditoriaNaTela.TextoDa(M("IMAGE", sticker: true)));
        Assert.Equal("[Áudio]", AuditoriaNaTela.TextoDa(M("AUDIO")));
        Assert.Equal("[Documento: guia.pdf]", AuditoriaNaTela.TextoDa(M("DOCUMENT", arquivo: "guia.pdf")));
    }

    [Fact]
    public void Historico_so_da_secretaria_avaliada_e_o_mais_novo_primeiro()
    {
        var d = AuditoriaNaTela.Detalhe(Detalhe(Revisao(1, "semdor", "draft"), Revisao(2, "ger", "final", score: 90m), Revisao(1, "semdor", "draft", "bia")),
            Item("c1", "avaliada", 1), Nomes, Agora);
        Assert.Equal([2, 1], d.Historico.Select(v => v.Versao));
        Assert.Equal("Concluída", d.Historico[0].Estado);
        Assert.Equal("90", d.Historico[0].Nota);
        Assert.Equal("sucesso", d.Historico[0].Tom);
        Assert.True(d.Historico[1].DoSemDor);
        Assert.Equal("@SemDor", d.Historico[1].Quem);
        Assert.Equal("50", d.Historico[1].Nota); // rascunho: a nota que as notas dele dariam
    }

    [Fact]
    public void Evidencia_e_trecho_cabem_numa_linha()
    {
        Assert.Equal("abc", AuditoriaNaTela.Trecho("  abc  ", 10));
        Assert.Equal("abcd…", AuditoriaNaTela.Trecho("abcdefghij", 5));
        var m = new MensagemNaAuditoria("m2", "enviada", "Ana", "Bom dia,\nMaria!", "07:04", "Hoje", true, null);
        Assert.Equal("07:04 · Ana: Bom dia, Maria!", AuditoriaNaTela.Evidencia(m));
    }

    // ------------------------------------------------------------------ o formulário

    [Fact]
    public void Sem_avaliacao_o_formulario_nasce_em_branco_na_versao_zero()
    {
        var f = AuditoriaNaTela.Formulario(Detalhe(), "ana");
        Assert.Equal(0, f.Versao);
        Assert.Equal("impressao-1", f.Impressao);
        Assert.Equal(Criterios.Select(k => k.Id), f.Criterios.Select(r => r.Criterio.Id));
        Assert.All(f.Criterios, r => { Assert.Null(r.Nota); Assert.Empty(r.Evidencias); Assert.Null(r.SugestaoDoSemDor); });
        Assert.False(f.PartiuDoSemDor);
    }

    [Fact]
    public void Ultima_versao_do_SemDor_preenche_e_marca_a_sugestao_mas_nao_escolhe_o_resultado()
    {
        var f = AuditoriaNaTela.Formulario(Detalhe(Revisao(1, "semdor", "draft", nota: 2), Revisao(3, "ger", "final", "bia")), "ana");

        Assert.True(f.PartiuDoSemDor);
        Assert.Equal(1, f.Versao);
        Assert.All(f.Criterios, r =>
        {
            Assert.Equal(2, r.Nota);
            Assert.Equal(["m2"], r.Evidencias);
            Assert.True(r.ComoOSemDorSugeriu);
        });
        Assert.Equal("", f.Resultado); // o "inconclusive" do rascunho é só o padrão do CRM

        f.Criterios[0].Nota = 1; // a gerente corrigiu
        Assert.False(f.Criterios[0].ComoOSemDorSugeriu);
        Assert.Equal(2, f.Criterios[0].SugestaoDoSemDor!.Score);
    }

    [Fact]
    public void Ultima_versao_da_gerente_preenche_tudo_dela()
    {
        var prazo = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(-3));
        var f = AuditoriaNaTela.Formulario(Detalhe(Revisao(1, "semdor", "draft"),
            Revisao(2, "ger", "final", outcome: "followup", action: "Ligar para a paciente", due: prazo, score: 50m)), "ana");

        Assert.False(f.PartiuDoSemDor);
        Assert.True(f.JaConcluida);
        Assert.Equal(2, f.Versao);
        Assert.Equal("followup", f.Resultado);
        Assert.Equal("Muito bem", f.Devolutiva);
        Assert.Equal("Ligar para a paciente", f.PlanoDeAcao);
        Assert.Equal(DateOnly.FromDateTime(prazo.LocalDateTime), f.Prazo);
        Assert.NotNull(f.Criterios[0].SugestaoDoSemDor); // a sugestão do @SemDor continua à vista
    }

    [Fact]
    public void Evidencia_alterna_e_nota_interna_nao_entra()
    {
        var f = AuditoriaNaTela.Formulario(Detalhe(), "ana");
        var r = f.Criterios[0];
        var m = new MensagemNaAuditoria("m2", "enviada", "Ana", "oi", "07:04", "Hoje", true, null);
        Assert.True(AuditoriaNaTela.AlternarEvidencia(r, m));
        Assert.Equal(["m2"], r.Evidencias);
        Assert.True(AuditoriaNaTela.AlternarEvidencia(r, m));
        Assert.Empty(r.Evidencias);
        Assert.False(AuditoriaNaTela.AlternarEvidencia(r, m with { Id = "n1", Tipo = "nota" }));
        Assert.Empty(r.Evidencias);
    }

    static FormularioDaAuditoria Completo()
    {
        var f = AuditoriaNaTela.Formulario(Detalhe(), "ana");
        foreach (var r in f.Criterios) { r.Nota = 2; r.Justificativa = "Respondeu com clareza e educação."; r.Evidencias.Add("m2"); }
        f.Resultado = "resolved";
        return f;
    }

    [Fact]
    public void Concluir_pede_as_cinco_notas_justificadas_com_evidencia_e_o_resultado()
    {
        Assert.Null(AuditoriaNaTela.Recusa(Completo(), final: true, Hoje));

        var semNota = Completo(); semNota.Criterios[1].Nota = null;
        Assert.Contains("Entendimento da necessidade", AuditoriaNaTela.Recusa(semNota, true, Hoje));

        var curta = Completo(); curta.Criterios[2].Justificativa = "ok";
        Assert.Contains("Justifique", AuditoriaNaTela.Recusa(curta, true, Hoje));

        var semProva = Completo(); semProva.Criterios[3].Evidencias.Clear();
        Assert.Contains("evidência", AuditoriaNaTela.Recusa(semProva, true, Hoje));

        var naoAvaliavel = Completo(); naoAvaliavel.Criterios[4].Nota = -1; naoAvaliavel.Criterios[4].Evidencias.Clear();
        Assert.Null(AuditoriaNaTela.Recusa(naoAvaliavel, true, Hoje)); // não avaliável dispensa a mensagem

        var semResultado = Completo(); semResultado.Resultado = "";
        Assert.Contains("resultado", AuditoriaNaTela.Recusa(semResultado, true, Hoje));

        var retorno = Completo(); retorno.Resultado = "followup";
        Assert.Contains("próximo passo", AuditoriaNaTela.Recusa(retorno, true, Hoje));
    }

    [Fact]
    public void Rascunho_aceita_o_incompleto_mas_nao_o_plano_sem_prazo_nem_prazo_vencido()
    {
        var f = AuditoriaNaTela.Formulario(Detalhe(), "ana");
        Assert.Null(AuditoriaNaTela.Recusa(f, final: false, Hoje));

        f.PlanoDeAcao = "Conversar com a Ana";
        Assert.Equal("Dê o prazo do plano de ação.", AuditoriaNaTela.Recusa(f, false, Hoje));
        f.Prazo = Hoje.AddDays(-1);
        Assert.Contains("já passou", AuditoriaNaTela.Recusa(f, false, Hoje));
        f.Prazo = Hoje;
        Assert.Null(AuditoriaNaTela.Recusa(f, false, Hoje));

        f.PlanoDeAcao = "  ";
        Assert.Contains("tire o prazo", AuditoriaNaTela.Recusa(f, false, Hoje));
    }

    [Fact]
    public void Montar_manda_os_cinco_criterios_a_versao_a_impressao_e_o_prazo_ao_meio_dia()
    {
        var f = Completo();
        f.Versao = 4;
        f.Devolutiva = "  Parabéns  ";
        f.PlanoDeAcao = " Retornar à paciente ";
        f.Prazo = new DateOnly(2026, 10, 30);
        var s = AuditoriaNaTela.Montar(f, final: true);

        Assert.Equal("c1", s.ConversationId);
        Assert.Equal("ana", s.SubjectId);
        Assert.Equal(4, s.Version);
        Assert.Equal("impressao-1", s.Fingerprint);
        Assert.Equal("final", s.State);
        Assert.Equal(5, s.Answers.Length);
        Assert.Equal(Criterios.Select(k => k.Id), s.Answers.Select(a => a.Criterion));
        Assert.All(s.Answers, a => Assert.Equal(["m2"], a.MessageIds));
        Assert.Equal("resolved", s.Outcome);
        Assert.Equal("Parabéns", s.Feedback);
        Assert.Equal("Retornar à paciente", s.Action);
        Assert.Equal(new DateTime(2026, 10, 30, 12, 0, 0), s.DueAt!.Value.DateTime);
        Assert.False(s.ActionDone);
    }

    [Fact]
    public void Rascunho_sem_resultado_vai_como_inconclusivo_e_sem_plano_nao_leva_prazo()
    {
        var f = AuditoriaNaTela.Formulario(Detalhe(), "ana");
        f.Prazo = Hoje;
        var s = AuditoriaNaTela.Montar(f, final: false);
        Assert.Equal("draft", s.State);
        Assert.Equal("inconclusive", s.Outcome);
        Assert.Null(s.DueAt);
        Assert.Equal(5, s.Answers.Length);
        Assert.All(s.Answers, a => Assert.Null(a.Score));
    }

    [Fact]
    public void Depois_do_409_o_formulario_fica_e_so_troca_a_impressao_e_a_versao()
    {
        var f = Completo();
        f.Devolutiva = "escrito pela gerente";
        var novo = Detalhe(Revisao(5, "outra", "draft")) with { Fingerprint = "impressao-2" };
        AuditoriaNaTela.Rebasear(f, novo);
        Assert.Equal("impressao-2", f.Impressao);
        Assert.Equal(5, f.Versao);
        Assert.Equal("escrito pela gerente", f.Devolutiva);
        Assert.All(f.Criterios, r => Assert.Equal(2, r.Nota));
    }

    // ------------------------------------------------------------------ as falhas

    [Fact]
    public void O_403_e_sem_permissao_e_o_409_diz_o_que_a_tela_fez()
    {
        var proibido = new RecusaDoCrm("Só a gestão.", 403);
        Assert.True(AuditoriaNaTela.SemPermissao(proibido));
        Assert.False(AuditoriaNaTela.SemPermissao(new RecusaDoCrm("x", 400)));
        Assert.Contains("administrador ou supervisor", AuditoriaNaTela.DescricaoSemPermissao(proibido));

        var conflito = AuditoriaNaTela.Conflito(new RecusaDoCrm("A conversa mudou durante a avaliação.", 409));
        Assert.StartsWith("A conversa mudou durante a avaliação.", conflito);
        Assert.Contains("continua aqui", conflito);

        Assert.Contains("Sem ligação", AuditoriaNaTela.FalhaDeAbertura(new CrmIndisponivel("caiu")));
    }

    [Fact]
    public async Task Salvar_recusa_o_incompleto_antes_de_ir_ao_CRM()
    {
        var servico = new AuditoriaDoAtendimentoService(new PreviaBlazor.CrmDeMentira(null));
        var f = AuditoriaNaTela.Formulario(Detalhe(), "ana");
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => servico.SalvarAsync(f, final: true));
        Assert.Contains("Dê a nota", erro.Message);
    }
}
