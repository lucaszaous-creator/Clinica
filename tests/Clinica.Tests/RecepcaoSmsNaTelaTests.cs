using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Application.Sms;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que as telas Blazor de Confirmações, Retornos a marcar e Canal de SMS AFIRMAM (out/2026).
/// As três só desenham: cada frase, contagem e recorte daqui é o que a recepcionista lê.
/// </summary>
public class RecepcaoSmsNaTelaTests
{
    private static readonly DateOnly Amanha = new(2026, 10, 17);

    // ==================== Confirmações ====================

    private static ContatoCampanha Contato(int id, StatusContato status, CanalContato canal, DateTime? enviado,
        string? comentario = null, string? telefone = "(21) 98123-4567", int hora = 9, string? email = null)
        => new()
        {
            Id = id, PacienteId = id, Status = status, Canal = canal, EnviadoEm = enviado, Comentario = comentario,
            EnviadoPor = enviado is null ? null : "Ana",
            Paciente = new Paciente { Id = id, Nome = $"Paciente {id} Silva", Telefone = telefone, Email = email },
            AgendamentoId = 100 + id,
            Agendamento = new Agendamento { Id = 100 + id, DataHora = Amanha.ToDateTime(new TimeOnly(hora, 0)), ModalidadePrevista = ModalidadeAtendimento.AcupunturaSimples }
        };

    [Fact]
    public void Resposta_pelo_link_do_SMS_aparece_escrita_e_o_nao_vou_pede_para_ligar()
    {
        var confirmou = ConfirmacoesNaTela.Linha(Contato(1, StatusContato.Respondido, CanalContato.Sms, DateTime.Now,
            RodadaDeConfirmacao.RespostaConfirmouPeloLink), Amanha);
        var naoVai = ConfirmacoesNaTela.Linha(Contato(2, StatusContato.Respondido, CanalContato.Sms, DateTime.Now,
            RodadaDeConfirmacao.RespostaNaoVaiPeloLink), Amanha);

        confirmou.Estado.Should().Be(SituacaoConfirmacao.Confirmou);
        confirmou.RespostaTexto.Should().Be("Confirmou pelo SMS");
        confirmou.RespondeuPeloLink.Should().BeTrue();
        confirmou.Destaque.Should().BeNull();
        confirmou.Aviso.Should().Be("SMS enviado");

        naoVai.Estado.Should().Be(SituacaoConfirmacao.PediuRemarcar);
        naoVai.RespostaTexto.Should().Be("Avisou pelo SMS que não vai");
        naoVai.Destaque.Should().Contain("ligue").And.Contain("SMS");
    }

    [Fact]
    public void Pedido_de_remarcar_no_balcao_tambem_pede_para_ligar_mas_sem_falar_de_SMS()
    {
        var l = ConfirmacoesNaTela.Linha(Contato(3, StatusContato.Respondido, CanalContato.WhatsApp, DateTime.Now,
            RodadaDeConfirmacao.RespostaRemarcar), Amanha);
        l.Estado.Should().Be(SituacaoConfirmacao.PediuRemarcar);
        l.RespondeuPeloLink.Should().BeFalse();
        l.Destaque.Should().Contain("ligue").And.NotContain("SMS");
        l.RespostaTexto.Should().Be("Pediu para remarcar");
    }

    [Fact]
    public void Dispensado_mostra_o_motivo_e_nao_avisado_diz_que_nao_foi()
    {
        ConfirmacoesNaTela.Linha(Contato(4, StatusContato.Dispensado, CanalContato.WhatsApp, null, "confirmou no balcão"), Amanha)
            .Detalhe.Should().Be("dispensado — confirmou no balcão");
        var nao = ConfirmacoesNaTela.Linha(Contato(5, StatusContato.Pendente, CanalContato.WhatsApp, null), Amanha);
        nao.Aviso.Should().Be("Não avisado");
        nao.Detalhe.Should().Be("ainda não avisado");
        nao.RotuloEnviar.Should().Be("Avisar");
    }

    [Fact]
    public void O_placar_sai_da_rodada_inteira_e_conta_o_SMS()
    {
        var linhas = ConfirmacoesNaTela.Montar(new[]
        {
            Contato(1, StatusContato.Respondido, CanalContato.Sms, DateTime.Now, RodadaDeConfirmacao.RespostaConfirmouPeloLink, hora: 8),
            Contato(2, StatusContato.Respondido, CanalContato.Sms, DateTime.Now, RodadaDeConfirmacao.RespostaNaoVaiPeloLink, hora: 9),
            Contato(3, StatusContato.Enviado, CanalContato.Sms, DateTime.Now, hora: 10),
            Contato(4, StatusContato.Pendente, CanalContato.WhatsApp, null, hora: 11),
            Contato(5, StatusContato.Dispensado, CanalContato.WhatsApp, null, "x", hora: 12)
        }, Amanha);

        var p = ConfirmacoesNaTela.Placar(linhas);
        p.Confirmaram.Should().Be(1);
        p.PediramRemarcar.Should().Be(1);
        p.SemResposta.Should().Be(1);
        p.NaoAvisados.Should().Be(1);
        p.Total.Should().Be(5);
        p.Percentual.Should().Be(75); // 3 avisados de 4 (dispensado fora da base)
        p.PorSms.Should().Be(3);
        p.RespostasPeloLink.Should().Be(2);

        ConfirmacoesNaTela.Recortar(linhas, SituacaoConfirmacao.PediuRemarcar).Should().ContainSingle().Which.ContatoId.Should().Be(2);
        ConfirmacoesNaTela.RecorteTexto(null, 5).Should().Be("Mostrando: todas as sessões (5)");
        ConfirmacoesNaTela.Resumo(linhas).Should().Contain("3 avisada(s) por SMS");
    }

    [Fact]
    public void Avisar_os_que_faltam_pula_quem_nao_tem_telefone_e_diz_por_que()
    {
        var linhas = ConfirmacoesNaTela.Montar(new[]
        {
            Contato(1, StatusContato.Pendente, CanalContato.WhatsApp, null, telefone: null, hora: 8),
            Contato(2, StatusContato.Pendente, CanalContato.WhatsApp, null, hora: 9)
        }, Amanha);
        ConfirmacoesNaTela.Proximo(linhas)!.ContatoId.Should().Be(2);

        var semTelefone = ConfirmacoesNaTela.Montar(new[] { Contato(1, StatusContato.Pendente, CanalContato.WhatsApp, null, telefone: null) }, Amanha);
        ConfirmacoesNaTela.Proximo(semTelefone).Should().BeNull();
        ConfirmacoesNaTela.SemProximo(1).Should().Contain("sem telefone");
        ConfirmacoesNaTela.SemProximo(0).Should().Contain("já foram avisadas");
    }

    [Fact]
    public void Gerar_distingue_nada_novo_de_nada_marcado()
    {
        ConfirmacoesNaTela.ResultadoDaGeracao(2, 1).Should().Be("2 sessão(ões) para avisar. 1 já estavam na rodada.");
        ConfirmacoesNaTela.ResultadoDaGeracao(0, 4).Should().StartWith("Nada novo");
        ConfirmacoesNaTela.ResultadoDaGeracao(0, 0).Should().Be("Nenhuma sessão marcada para este dia.");
    }

    // ==================== Retornos a marcar ====================

    private static RetornoAMarcar Retorno(int id, int atraso, DateOnly? marcado = null, string? nota = "rever") =>
        new(id, $"P{id}", "(21) 98123-4567", Amanha.AddDays(-10), Amanha.AddDays(-atraso), nota, 1, "Dra. Helena", atraso)
        { MarcadoPara = marcado };

    [Fact]
    public void Todos_e_a_fila_de_trabalho_e_o_ja_marcado_so_aparece_no_chip_dele()
    {
        var todas = new[] { Retorno(1, 5), Retorno(2, -3), Retorno(3, -20), Retorno(4, -2, Amanha) };
        RetornosAMarcarNaTela.Recortar(todas, null).Select(r => r.PacienteId).Should().Equal(1, 2, 3);
        RetornosAMarcarNaTela.Recortar(todas, FaixaRetorno.JaMarcado).Should().ContainSingle().Which.PacienteId.Should().Be(4);

        var c = RetornosAMarcarNaTela.Contar(todas);
        c.Should().Be(new ContagemRetornos(3, 1, 1, 1, 1));
        RetornosAMarcarNaTela.Resumo(todas).Should().Be("3 retorno(s) a marcar · 1 com a data sugerida já passada");
        RetornosAMarcarNaTela.Resumo([]).Should().StartWith("Ninguém");
    }

    [Fact]
    public void Marcar_o_atrasado_abre_em_hoje_e_a_nota_some_da_linha_para_quem_nao_le_prontuario()
    {
        var hoje = Amanha;
        RetornosAMarcarNaTela.DiaParaMarcar(Retorno(1, 5), hoje).Should().Be(hoje);
        RetornosAMarcarNaTela.DiaParaMarcar(Retorno(2, -3), hoje).Should().Be(hoje.AddDays(3));

        var linhas = new[] { Retorno(1, 1, nota: "ver exames") };
        RetornosAMarcarNaTela.SemNotaSeNaoPode(linhas, podeVerNota: false).Single().Nota.Should().BeNull();
        RetornosAMarcarNaTela.SemNotaSeNaoPode(linhas, podeVerNota: true).Single().Nota.Should().Be("ver exames");
    }

    // ==================== Canal de SMS — histórico ====================

    [Fact]
    public void Segmento_cobrado_e_so_o_que_saiu()
    {
        var ms = new[]
        {
            new MensagemSms { Situacao = SituacaoSms.Enviada, Segmentos = 2, Finalidade = FinalidadeSms.Campanha },
            new MensagemSms { Situacao = SituacaoSms.Enviada, Segmentos = 1, Finalidade = FinalidadeSms.ConfirmacaoSessao },
            new MensagemSms { Situacao = SituacaoSms.Falhou, Segmentos = 3, Tentativas = 3, Finalidade = FinalidadeSms.ConfirmacaoSessao },
            new MensagemSms { Situacao = SituacaoSms.Enviando, Finalidade = FinalidadeSms.Teste }
        };
        var r = HistoricoSmsNaTela.Resumir(ms);
        r.Should().Be(new ResumoDoHistoricoSms(2, 1, 1, 3, 2, 1, 1));
        HistoricoSmsNaTela.Situacao(ms[2]).Should().Be("Falhou");
        HistoricoSmsNaTela.Situacao(new MensagemSms { Situacao = SituacaoSms.Falhou, Tentativas = 1 }).Should().Contain("tenta de novo");
        HistoricoSmsNaTela.Filtrar(ms, null, soFalhas: true).Should().ContainSingle();
        HistoricoSmsNaTela.Filtrar(ms, FinalidadeSms.ConfirmacaoSessao, false).Should().HaveCount(2);
        HistoricoSmsNaTela.Celular("+5521981234567").Should().Be("(21) 98123-4567");
    }

    [Fact]
    public void O_vazio_do_historico_distingue_filtro_canal_ligado_e_desligado()
    {
        HistoricoSmsNaTela.Vazio(true, true).Titulo.Should().Contain("filtro");
        HistoricoSmsNaTela.Vazio(false, false).Titulo.Should().Contain("desligado");
        HistoricoSmsNaTela.Vazio(false, true).Titulo.Should().Be("Nenhum SMS neste período");
    }

    // ==================== Canal de SMS — texto e público ====================

    [Fact]
    public void A_medida_do_texto_inclui_o_rodape_e_avisa_o_acento_tirado()
    {
        TextoDaCampanhaSms.Medir("  ").Vazio.Should().BeTrue();

        var curto = TextoDaCampanhaSms.Medir("Promoção de novembro");
        curto.Segmentos.Should().Be(1);
        curto.AcentosRemovidos.Should().BeTrue();
        curto.Caracteres.Should().Be(LembreteSmsService.TextoDaCampanha("Promoção de novembro").Length);
        curto.Descricao.Should().Contain("1 SMS por pessoa");

        var longo = TextoDaCampanhaSms.Medir(new string('a', 500));
        longo.PassouDoTeto.Should().BeTrue();
        longo.Descricao.Should().Contain("encurte");
    }

    private static FatoTendencia Fato(int paciente, DateOnly dia, ModalidadeAtendimento m, string? esp = null)
        => new(dia, 1m, paciente, m, null, esp, 1, "Dra.", null, Convenio.UnimedPadrao);

    [Fact]
    public void Publico_por_convenio_e_por_quem_fez_a_modalidade_no_periodo()
    {
        var fichas = new[]
        {
            new PacienteParaContato(1, "Ana", "(21) 98123-0001", null, Convenio.UnimedPadrao),
            new PacienteParaContato(2, "Bia", "(21) 98123-0002", null, Convenio.Amil),
            new PacienteParaContato(3, "Caio", "(21) 98123-0003", null, Convenio.Amil),
        };
        var inicio = new DateOnly(2026, 9, 1);
        var fim = new DateOnly(2026, 9, 30);
        var fatos = new[]
        {
            Fato(1, new DateOnly(2026, 9, 10), ModalidadeAtendimento.AcupunturaSimples),
            Fato(2, new DateOnly(2026, 9, 12), ModalidadeAtendimento.Consulta, "Psiquiatria"),
            Fato(3, new DateOnly(2026, 8, 12), ModalidadeAtendimento.AcupunturaSimples) // fora do período
        };

        var todos = PublicoCampanhaSms.Montar(fichas, fatos, new FiltroPublicoSms(inicio, fim));
        todos.PacienteIds.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        todos.Descricao.Should().Be("3 pacientes — todos os cadastrados");
        todos.Convenios.First().Chave.Should().Be(nameof(Convenio.Amil));
        todos.Convenios.First().Pacientes.Should().Be(2);
        // As opções saem do PERÍODO: a acupuntura de agosto não conta o paciente 3.
        todos.Modalidades.Single(o => o.Chave == nameof(ModalidadeAtendimento.AcupunturaSimples)).Pacientes.Should().Be(1);
        todos.Especialidades.Should().ContainSingle(o => o.Chave == "Psiquiatria");

        PublicoCampanhaSms.Montar(fichas, fatos, new FiltroPublicoSms(inicio, fim, ConvenioCodigo: "Amil"))
            .PacienteIds.Should().BeEquivalentTo(new[] { 2, 3 });

        var acupuntura = PublicoCampanhaSms.Montar(fichas, fatos,
            new FiltroPublicoSms(inicio, fim, ModalidadeCodigo: nameof(ModalidadeAtendimento.AcupunturaSimples)));
        acupuntura.PacienteIds.Should().Equal(1);
        acupuntura.Descricao.Should().StartWith("1 paciente — atendidos em").And.Contain("01/09/2026 a 30/09/2026");

        PublicoCampanhaSms.Montar(fichas, fatos,
                new FiltroPublicoSms(inicio, fim, ConvenioCodigo: "Amil", EspecialidadeCodigo: "Psiquiatria"))
            .PacienteIds.Should().Equal(2);
    }

    [Fact]
    public void Resultado_da_campanha_conta_quem_ficou_de_fora()
    {
        ResultadoDaCampanhaSms.Descrever(new ResultadoCampanhaSms(10, 1, 0, 4))
            .Should().Be("10 SMS enviados · 1 falha(s) — veja o erro na aba Lembretes · 4 ficaram de fora (sem consentimento ou sem celular).");
    }

    // ==================== Canal de SMS — configuração ====================

    private static CamposSms Campos(ProvedorSms p, string? token, string? remetente = "CLINICA") =>
        new(p, null, null, token, remetente, null, null, 24, true);

    [Fact]
    public void Token_vazio_no_salvar_mantem_o_gravado_e_a_tela_nunca_recebe_o_token()
    {
        ConfiguracaoSmsNaTela.ParaSalvar(Campos(ProvedorSms.Zenvia, "  "), "segredo").Token.Should().Be("segredo");
        ConfiguracaoSmsNaTela.ParaSalvar(Campos(ProvedorSms.Zenvia, " novo "), "segredo").Token.Should().Be("novo");
        ConfiguracaoSmsNaTela.ParaTela(Campos(ProvedorSms.Zenvia, "segredo")).Token.Should().BeNull();
    }

    [Fact]
    public void O_estado_separa_ligado_desligado_e_meio_configurado()
    {
        ConfiguracaoSmsNaTela.Estado(Campos(ProvedorSms.Zenvia, "tok")).Tom.Should().Be("sucesso");
        ConfiguracaoSmsNaTela.Estado(Campos(ProvedorSms.Desligado, null)).Texto.Should().StartWith("Desligado");
        var meio = ConfiguracaoSmsNaTela.Estado(Campos(ProvedorSms.Zenvia, null));
        meio.Tom.Should().Be("aviso");
        meio.Texto.Should().Contain("token");
    }
}
