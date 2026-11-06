using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela Blazor de Confirmações AFIRMA (out/2026). O Canal de SMS
/// saiu na fase 3 Recepção×CRM (quem manda SMS é só o CRM). Ela só desenha: cada frase,
/// contagem e recorte daqui é o que a recepcionista lê.
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
}
