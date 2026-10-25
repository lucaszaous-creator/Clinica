using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;
using static Clinica.Tests.WhatsAppDeTeste;

namespace Clinica.Tests;

/// <summary>
/// O modo Retenção da tela do WhatsApp (PR 2): a fila do Acompanhamento nos grupos do mockup,
/// com os dias do Gerente, só quem trata, e nenhum caminho de envio para quem não autorizou.
/// </summary>
public sealed class RetencaoNaTelaTests
{
    private static readonly int[] LimitesDoMockup = [15, 20, 30];

    private static readonly LinhaAcompanhamento Sebastiao = Caso(1, 1, "Sebastião Ramos", telefone: "(22) 99555-1234", dias: 32);
    private static readonly LinhaAcompanhamento Helena = Caso(2, 2, "Helena Marques", modalidade: ModalidadeAtendimento.AcupunturaSimples, dias: 30, consentimento: false);
    private static readonly LinhaAcompanhamento Marcos = Caso(3, 3, "Marcos Vinícius Teles", modalidade: ModalidadeAtendimento.BsvComAcupuntura, dias: 21);
    private static readonly LinhaAcompanhamento Rita = Caso(4, 4, "Rita de Cássia Lopes", modalidade: ModalidadeAtendimento.AcupunturaSimples, dias: 15);
    private static readonly LinhaAcompanhamento Curto = Caso(5, 5, "Breno Dias", modalidade: ModalidadeAtendimento.AcupunturaComEletro, dias: 10);
    private static readonly LinhaAcompanhamento SoConsulta = Caso(6, 6, "Quem só consultou", modalidade: ModalidadeAtendimento.Consulta, dias: 40);
    private static readonly LinhaAcompanhamento Felipe = Caso(7, 7, "Felipe Arantes", tipo: TipoAcompanhamento.NovoBsv, dias: 3, referencia: new DateTime(2026, 10, 21));
    private static readonly LinhaAcompanhamento Alzira = Caso(8, 8, "Alzira Pinto", dias: 16, tentativas: 1,
        ultimoContato: new DateTime(2026, 10, 22, 15, 12, 0), proximo: new DateOnly(2026, 10, 29));
    private static readonly LinhaAcompanhamento Fernanda = Caso(9, 9, "Fernanda Nogueira", modalidade: ModalidadeAtendimento.AcupunturaSimples, dias: 20,
        tentativas: 1, ultimoContato: new DateTime(2026, 10, 23, 15, 10, 0), proximo: new DateOnly(2026, 10, 30));
    private static readonly LinhaAcompanhamento Ines = Caso(10, 10, "Inês Barreto", pendente: false, voltou: new DateOnly(2026, 10, 23),
        modalidade: ModalidadeAtendimento.AcupunturaSimples, dias: 23);
    private static readonly LinhaAcompanhamento Geraldo = Caso(11, 11, "Geraldo Nunes", pendente: false, motivoEncerramento: "Mudou para Campos",
        motivo: "Mudou de cidade", ultimoContato: new DateTime(2026, 10, 19, 11, 0, 0), dias: 39);
    private static readonly LinhaAcompanhamento Antigo = Caso(12, 12, "Caso Antigo", pendente: false, voltou: new DateOnly(2026, 10, 10));

    private static readonly LinhaAcompanhamento[] Fila =
        [Helena, Sebastiao, Marcos, Rita, Curto, SoConsulta, Felipe, Alzira, Fernanda, Ines, Geraldo, Antigo];

    private static readonly IReadOnlyDictionary<int, DateTimeOffset> Respostas = new Dictionary<int, DateTimeOffset>
    {
        [8] = Em(23, 17, 40), // Alzira respondeu depois do convite
        [9] = Em(4, 10, 20), // Fernanda escreveu muito antes do convite
    };

    private static IReadOnlyList<(string Chave, string Titulo, string Tom, string Pacientes)> Grupos(FiltroDaRetencao filtro,
        IEnumerable<int>? limites = null, string? busca = null, IEnumerable<LinhaAcompanhamento>? fila = null)
        => RetencaoNaTela.Agrupar(fila ?? Fila, filtro, limites ?? LimitesDoMockup, Respostas, Agora, busca)
            .Select(g => (g.Chave, g.Titulo, g.Tom, string.Join(", ", g.Linhas.Select(l => l.Paciente)))).ToList();

    [Fact]
    public void Para_hoje_quem_respondeu_vem_primeiro_depois_30_20_15_dias_e_os_novos_no_BSV()
    {
        Assert.Equal(
        [
            ("respondeu", "Responderam · decidir", "sucesso", "Alzira Pinto"),
            ("dias-30", "Sem vir há 30 dias", "perigo", "Sebastião Ramos, Helena Marques"),
            ("dias-20", "Sem vir há 20 dias", "aviso", "Marcos Vinícius Teles"),
            ("dias-15", "Sem vir há 15 dias", "neutro", "Rita de Cássia Lopes"),
            ("dias-abaixo", "Sem vir há menos de 15 dias", "neutro", "Breno Dias"),
            ("novo-bsv", "Novos no BSV · sem a 1ª sessão", "info", "Felipe Arantes"),
        ], Grupos(FiltroDaRetencao.ParaHoje));
    }

    [Fact]
    public void So_entra_quem_trata_e_quem_so_consultou_nao_aparece_em_filtro_nenhum()
    {
        foreach (var filtro in Enum.GetValues<FiltroDaRetencao>())
            Assert.DoesNotContain(Grupos(filtro), g => g.Pacientes.Contains("Quem só consultou"));
        Assert.False(RetencaoNaTela.EmTratamento(SoConsulta));
        Assert.True(RetencaoNaTela.EmTratamento(Felipe));
        Assert.True(RetencaoNaTela.EmTratamento(ModalidadeAtendimento.AcupunturaComEletro));
    }

    [Fact]
    public void Aguardando_e_fechados_da_semana_sao_um_grupo_cada()
    {
        Assert.Equal([("aguardando", "Aguardando", "neutro", "Fernanda Nogueira")], Grupos(FiltroDaRetencao.Aguardando));
        // Semana de segunda (19/10) a hoje: o caso que voltou em 10/10 já não é "desta semana".
        Assert.Equal([("fechado", "Fechados esta semana", "neutro", "Inês Barreto, Geraldo Nunes")], Grupos(FiltroDaRetencao.Fechados));
        Assert.Equal(new ContagemDaRetencao(7, 1, 2), RetencaoNaTela.Contagens(Fila, Respostas, Agora));
        Assert.Equal(["Para hoje", "Aguardando", "Fechados"], Enum.GetValues<FiltroDaRetencao>().Select(RetencaoNaTela.Rotulo));
    }

    [Fact]
    public void Os_dias_sao_os_do_Gerente_e_nenhum_numero_mora_aqui()
    {
        // Hoje o Gerente guarda UM número ("Recall: dias sem vir", padrão 60): a lista sai com ele.
        var comSessenta = Grupos(FiltroDaRetencao.ParaHoje, [60]);
        Assert.Equal(["respondeu", "dias-abaixo", "novo-bsv"], comSessenta.Select(g => g.Chave));
        Assert.Equal("Sem vir há menos de 60 dias", comSessenta[1].Titulo);
        Assert.Equal(5, comSessenta[1].Pacientes.Split(", ").Length);

        // Repetido, fora de ordem ou zero: o mesmo que os três limites limpos.
        Assert.Equal(Grupos(FiltroDaRetencao.ParaHoje), Grupos(FiltroDaRetencao.ParaHoje, [20, 15, 30, 20, 0]));

        var semLimite = Grupos(FiltroDaRetencao.ParaHoje, []);
        Assert.Equal(("recall", "Sem vir"), (semLimite[1].Chave, semLimite[1].Titulo));
    }

    [Fact]
    public void Dentro_do_grupo_o_contato_atrasado_vem_antes_de_quem_esta_ha_mais_tempo()
    {
        var fila = new[] { Sebastiao, Helena with { Atrasado = true } };
        Assert.Equal("Helena Marques, Sebastião Ramos", Grupos(FiltroDaRetencao.ParaHoje, fila: fila).Single().Pacientes);
    }

    [Fact]
    public void A_busca_acha_pelo_nome_sem_acento_e_pelo_telefone()
    {
        Assert.Equal([("dias-30", "Sem vir há 30 dias", "perigo", "Sebastião Ramos")], Grupos(FiltroDaRetencao.ParaHoje, busca: "sebastiao"));
        Assert.Single(Grupos(FiltroDaRetencao.ParaHoje, busca: "99555 1234"));
        Assert.Empty(Grupos(FiltroDaRetencao.ParaHoje, busca: "ninguém"));
        Assert.Equal(("Ninguém com esse nome", "Confira o nome."), RetencaoNaTela.Vazio(FiltroDaRetencao.ParaHoje, true));
        Assert.Equal("Ninguém para hoje", RetencaoNaTela.Vazio(FiltroDaRetencao.ParaHoje, false).Titulo);
    }

    [Fact]
    public void Respondeu_compara_a_mensagem_do_CRM_em_UTC_com_o_contato_na_hora_da_clinica()
    {
        var caso = Caso(tentativas: 1, ultimoContato: new DateTime(2026, 10, 23, 17, 0, 0));
        Assert.True(RetencaoNaTela.Respondeu(caso, new DateTimeOffset(2026, 10, 23, 20, 30, 0, TimeSpan.Zero), Agora)); // 17:30
        Assert.False(RetencaoNaTela.Respondeu(caso, new DateTimeOffset(2026, 10, 23, 19, 30, 0, TimeSpan.Zero), Agora)); // 16:30
        Assert.False(RetencaoNaTela.Respondeu(caso, null, Agora));
        Assert.False(RetencaoNaTela.Respondeu(caso, default(DateTimeOffset), Agora));
        Assert.False(RetencaoNaTela.Respondeu(caso with { Pendente = false }, Em(24, 9, 0), Agora));
        Assert.False(RetencaoNaTela.Respondeu(Caso(), Em(24, 9, 0), Agora)); // sem contato registrado, não é resposta a nada
    }

    [Fact]
    public void O_selo_da_linha()
    {
        SeloDoWhatsApp Selo(LinhaAcompanhamento l) => RetencaoNaTela.Selo(l, Respostas.TryGetValue(l.PacienteId, out var u) ? u : null, Agora);
        Assert.Equal(new SeloDoWhatsApp("Autorizou", "sucesso"), Selo(Sebastiao));
        Assert.Equal(new SeloDoWhatsApp("Sem autorização", "aviso"), Selo(Helena));
        Assert.Equal(new SeloDoWhatsApp("Indicado ao BSV", "info"), Selo(Felipe));
        Assert.Equal(new SeloDoWhatsApp("Respondeu", "sucesso"), Selo(Alzira));
        Assert.Equal(new SeloDoWhatsApp("Aguardando · 30/10", "info"), Selo(Fernanda));
        Assert.Equal(new SeloDoWhatsApp("Voltou em 23/10", "sucesso"), Selo(Ines));
        Assert.Equal(new SeloDoWhatsApp("Encerrado · Mudou de cidade", "neutro"), Selo(Geraldo));
        Assert.Equal(new SeloDoWhatsApp("Sem telefone", "aviso"), Selo(Sebastiao with { Telefone = "123" }));
    }

    [Fact]
    public void O_detalhe_e_os_dias_da_linha()
    {
        Assert.Equal("BSV · última 22/09 · 6 sessões", RetencaoNaTela.Detalhe(Sebastiao));
        Assert.Equal("BSV e acupuntura · última 22/09 · 1 sessão · pacote com saldo",
            RetencaoNaTela.Detalhe(Marcos with { TotalSessoes = 1, PacoteComSaldo = true }));
        Assert.Equal("BSV · indicado em 21/10 por Dr. Marcos", RetencaoNaTela.Detalhe(Felipe));
        Assert.Equal("32 dias", RetencaoNaTela.Dias(Sebastiao));
        Assert.Equal("indicado há 3 dias", RetencaoNaTela.Dias(Felipe));
    }

    [Fact]
    public void A_abertura_da_conversa_sem_mensagens_e_um_modelo_para_quem_autorizou()
    {
        var convite = RetencaoNaTela.Abertura(Sebastiao, null, Agora)!;
        Assert.Equal("Primeira mensagem desta conversa: um modelo aprovado", convite.Titulo);
        Assert.Equal("Sebastião ainda não escreveu, então a Meta só aceita modelo. Depois da resposta, o campo livre abre.", convite.Texto);
        Assert.Equal(ModeloDoWhatsApp.RetornoTratamento, convite.ModeloSugerido);
        Assert.Equal([BotaoDaAcao.PrepararConvite, BotaoDaAcao.NotaInterna], convite.Botoes.Select(b => b.Acao));

        var novo = RetencaoNaTela.Abertura(Felipe, Conversa(paciente: 7), Agora)!;
        Assert.Equal(ModeloDoWhatsApp.PrimeiroContato, novo.ModeloSugerido);
        Assert.Equal(BotaoDaAcao.PrepararMensagem, novo.Botoes[0].Acao);

        // Já tem mensagem, está aguardando ou está fechado: vale o bloco normal da janela.
        Assert.Null(RetencaoNaTela.Abertura(Sebastiao, Conversa([Recebida("m1", Em(1, 9, 0))], paciente: 1), Agora));
        Assert.Null(RetencaoNaTela.Abertura(Fernanda, null, Agora));
        Assert.Null(RetencaoNaTela.Abertura(Ines, null, Agora));
        Assert.Null(RetencaoNaTela.Abertura(SoConsulta, null, Agora));

        var semTelefone = RetencaoNaTela.Abertura(Sebastiao with { Telefone = null }, null, Agora)!;
        Assert.Equal(("Sem telefone no cadastro", (string?)null), (semTelefone.Titulo, semTelefone.ModeloSugerido));
        Assert.Equal([BotaoDaAcao.AtualizarTelefone], semTelefone.Botoes.Select(b => b.Acao));
    }

    [Fact]
    public void Sem_consentimento_nenhum_caminho_oferece_envio()
    {
        BotaoDaAcao[] envio = [BotaoDaAcao.PrepararConvite, BotaoDaAcao.PrepararMensagem];
        var semAutorizacao = new[]
        {
            Helena,
            Helena with { ProximoContato = new DateOnly(2026, 10, 30), Tentativas = 1, UltimoContato = new DateTime(2026, 10, 22, 9, 0, 0) },
            Felipe with { Consentimento = false },
            Helena with { Atrasado = true },
        };
        foreach (var caso in semAutorizacao)
        {
            Assert.False(RetencaoNaTela.PodeConvidar(caso));
            Assert.Null(RetencaoNaTela.ModeloDoConvite(caso));
            var abertura = RetencaoNaTela.Abertura(caso, null, Agora)!;
            Assert.Equal("Sem autorização de mensagens", abertura.Titulo);
            Assert.Null(abertura.ModeloSugerido);
            Assert.Equal([BotaoDaAcao.JaLiguei, BotaoDaAcao.NotaInterna], abertura.Botoes.Select(b => b.Acao));
            foreach (var conversa in new[] { null, Conversa(paciente: caso.PacienteId), Conversa([Recebida("m1", Em(2, 9, 0))], paciente: caso.PacienteId) })
            {
                var acao = ConversasNaTela.PedeAcao(conversa, [], 1, caso, Agora);
                Assert.True(acao is null || !acao.Botoes.Any(b => envio.Contains(b.Acao)), $"{caso.Paciente}: {acao?.Titulo}");
            }
        }
    }

    [Fact]
    public void O_convite_so_cabe_em_caso_aberto_de_quem_trata_com_telefone_e_autorizacao()
    {
        Assert.Equal(ModeloDoWhatsApp.RetornoTratamento, RetencaoNaTela.ModeloDoConvite(Sebastiao));
        Assert.Equal(ModeloDoWhatsApp.PrimeiroContato, RetencaoNaTela.ModeloDoConvite(Felipe));
        Assert.Null(RetencaoNaTela.ModeloDoConvite(Ines));
        Assert.Null(RetencaoNaTela.ModeloDoConvite(SoConsulta));
        Assert.Null(RetencaoNaTela.ModeloDoConvite(Sebastiao with { Telefone = " " }));
    }

    [Fact]
    public void Os_candidatos_do_convite_poem_o_modelo_aprovado_na_Meta_antes_do_nome_do_mockup()
    {
        Assert.Equal([ModeloDoWhatsApp.RecallReal, ModeloDoWhatsApp.RetornoTratamento], RetencaoNaTela.CandidatosDoConvite(Sebastiao));
        Assert.Equal([ModeloDoWhatsApp.IniciarAtendimentoReal, ModeloDoWhatsApp.PrimeiroContato], RetencaoNaTela.CandidatosDoConvite(Felipe));
        // A mesma regra do ModeloDoConvite: quem não pode ser convidado não ganha candidato nenhum.
        Assert.Empty(RetencaoNaTela.CandidatosDoConvite(Ines));
        Assert.Empty(RetencaoNaTela.CandidatosDoConvite(SoConsulta));
        Assert.Empty(RetencaoNaTela.CandidatosDoConvite(Sebastiao with { Telefone = " " }));
    }

    [Fact]
    public void Os_cinco_como_foi_usam_os_rotulos_do_Acompanhamento()
    {
        var botoes = RetencaoNaTela.BotoesDoComoFoi();
        Assert.Equal(Enum.GetValues<DesfechoContato>().Length, botoes.Count);
        Assert.All(botoes, b => Assert.Equal(AcompanhamentoNaTela.Rotulo(b.Desfecho!.Value), b.Rotulo));
        Assert.Equal(DesfechoContato.QuerMarcar, botoes.Single(b => b.Primario).Desfecho);
        Assert.Equal(DesfechoContato.NaoQuerVoltar, botoes.Single(b => b.Perigo).Desfecho);
    }

    [Fact]
    public void O_rodape_diz_quem_entra_e_que_nao_ha_disparo_em_massa()
    {
        Assert.Contains("BSV, acupuntura ou os dois", RetencaoNaTela.QuemApareceAqui);
        Assert.Contains("não há disparo em massa", RetencaoNaTela.QuemApareceAqui);
    }
}
