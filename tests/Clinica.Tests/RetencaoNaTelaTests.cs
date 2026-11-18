using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Xunit;
using static Clinica.Tests.WhatsAppDeTeste;

namespace Clinica.Tests;

/// <summary>
/// A retenção dentro da conversa do WhatsApp: o selo, a etiqueta, o convite e o "Como foi?" do caso,
/// só para quem trata, e nenhum caminho de envio para quem não autorizou. (A LISTA da retenção, com os
/// grupos, saiu da aba WhatsApp em nov/2026: é a Fila 360, testada em Fila360NaTelaTests.)
/// </summary>
public sealed class RetencaoNaTelaTests
{
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

    [Fact]
    public void So_quem_trata_esta_em_tratamento_e_quem_so_consultou_nao_recebe_convite()
    {
        Assert.False(RetencaoNaTela.PodeConvidar(SoConsulta));
        Assert.False(RetencaoNaTela.EmTratamento(SoConsulta));
        Assert.True(RetencaoNaTela.EmTratamento(Felipe));
        Assert.True(RetencaoNaTela.EmTratamento(ModalidadeAtendimento.AcupunturaComEletro));
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
        Assert.Equal(new SeloDoWhatsApp("Pode convidar", "sucesso"), Selo(Sebastiao));
        Assert.Equal(new SeloDoWhatsApp("Pediu para não receber", "aviso"), Selo(Helena));
        Assert.Equal(new SeloDoWhatsApp("Indicado ao BSV", "info"), Selo(Felipe));
        Assert.Equal(new SeloDoWhatsApp("Respondeu", "sucesso"), Selo(Alzira));
        Assert.Equal(new SeloDoWhatsApp("Aguardando · 30/10", "info"), Selo(Fernanda));
        Assert.Equal(new SeloDoWhatsApp("Voltou em 23/10", "sucesso"), Selo(Ines));
        Assert.Equal(new SeloDoWhatsApp("Encerrado · Mudou de cidade", "neutro"), Selo(Geraldo));
        Assert.Equal(new SeloDoWhatsApp("Sem telefone", "aviso"), Selo(Sebastiao with { Telefone = "123" }));
    }

    [Fact]
    public void A_coluna_so_mostra_a_situacao_que_ela_ainda_nao_diz()
    {
        SeloDoWhatsApp? Coluna(LinhaAcompanhamento l) => RetencaoNaTela.SeloNaColuna(l, Respostas.TryGetValue(l.PacienteId, out var u) ? u : null, Agora);
        // Antes do contato: telefone e autorização já estão na coluna ("Mensagens"), e o Novo BSV no título.
        Assert.Null(Coluna(Sebastiao));
        Assert.Null(Coluna(Helena));
        Assert.Null(Coluna(Felipe));
        Assert.Null(Coluna(Sebastiao with { Telefone = "123" }));
        // Depois: o que aconteceu com o caso é a informação nova.
        Assert.Equal(new SeloDoWhatsApp("Respondeu", "sucesso"), Coluna(Alzira));
        Assert.Equal(new SeloDoWhatsApp("Aguardando · 30/10", "info"), Coluna(Fernanda));
        Assert.Equal(new SeloDoWhatsApp("Voltou em 23/10", "sucesso"), Coluna(Ines));
    }

    [Fact]
    public void Os_dias_do_caso()
    {
        Assert.Equal("32 dias", RetencaoNaTela.Dias(Sebastiao));
        Assert.Equal("indicado há 3 dias", RetencaoNaTela.Dias(Felipe));
    }

    [Fact]
    public void A_etiqueta_da_faixa_diz_com_quem_o_caso_esta()
    {
        // Quem atende pela recepção vê com quem está o caso; quem cuida dele lê "com você".
        Assert.Equal("Retenção · BSV · 32 dias · com Ana", RetencaoNaTela.Etiqueta(Sebastiao, usuarioId: 99));
        Assert.Equal("Retenção · BSV · 32 dias · com você", RetencaoNaTela.Etiqueta(Sebastiao, usuarioId: 7));
        Assert.Equal("Retenção · BSV · 32 dias", RetencaoNaTela.Etiqueta(Sebastiao with { ResponsavelId = null }, usuarioId: 7));
        Assert.Equal("Retenção · BSV · indicado há 3 dias · com Ana", RetencaoNaTela.Etiqueta(Felipe, usuarioId: 99));
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
        // Neutros (item 22): nenhum parece a resposta já escolhida; só o que encerra o caso tem o contorno de perigo.
        Assert.DoesNotContain(botoes, b => b.Primario);
        Assert.Equal(DesfechoContato.NaoQuerVoltar, botoes.Single(b => b.Perigo).Desfecho);
    }

    [Fact]
    public void O_como_foi_da_coluna_so_aparece_depois_de_um_contato_e_quando_o_aviso_nao_o_mostra()
    {
        // Sem convite nem ligação (só a abertura do ciclo), não há o que perguntar.
        Assert.False(RetencaoNaTela.ComoFoiNaColuna(Sebastiao, null));
        var convidado = Sebastiao with { Tentativas = 1, UltimoContato = new DateTime(2026, 10, 23, 15, 0, 0) };
        Assert.True(RetencaoNaTela.ComoFoiNaColuna(convidado, null));
        // O aviso "respondeu ao convite" já traz os cinco: a coluna não repete.
        var aviso = new AcaoPedida(TipoDaAcaoPedida.RetencaoRespondeu, "sucesso", "", "", RetencaoNaTela.BotoesDoComoFoi());
        Assert.False(RetencaoNaTela.ComoFoiNaColuna(convidado, aviso));
        Assert.False(RetencaoNaTela.ComoFoiNaColuna(convidado with { Pendente = false }, null));
    }
}
