using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a tela de Campanhas do Gerente AFIRMA (Blazor desde out/2026, etapa 6) — antes no
/// <c>CampanhasViewModel</c> do WPF: filtros, janela, linha, mensagem, nota e frase da rodada.
/// </summary>
public class CampanhasNaTelaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 18);

    private static ContatoCampanha Contato(TipoContato tipo, StatusContato status = StatusContato.Pendente,
        string? telefone = "(21) 98123-4501", int? nota = null, DateOnly? referencia = null)
        => new()
        {
            Id = 7, Tipo = tipo, Status = status, Nota = nota, Referencia = referencia ?? Hoje,
            Paciente = new Paciente { Nome = "Maria da Silva", Telefone = telefone }
        };

    [Fact]
    public void A_janela_padrao_olha_para_frente_e_as_outras_para_tras_sempre_com_sete_dias_adiante()
    {
        CampanhasNaTela.Intervalo(CampanhasNaTela.JanelaSemanaQueVem, Hoje)
            .Should().Be((Hoje.AddDays(-7), Hoje.AddDays(7)));
        CampanhasNaTela.Intervalo(CampanhasNaTela.JanelaUltimos30Dias, Hoje)
            .Should().Be((Hoje.AddDays(-30), Hoje.AddDays(7)));
        CampanhasNaTela.Intervalo(CampanhasNaTela.JanelaUltimos3Meses, Hoje)
            .Should().Be((Hoje.AddDays(-90), Hoje.AddDays(7)));
        CampanhasNaTela.RotularIntervalo((Hoje.AddDays(-7), Hoje.AddDays(7))).Should().Be("11/10 a 25/10");
    }

    [Fact]
    public void Os_filtros_traduzem_o_rotulo_e_Todos_e_sem_filtro()
    {
        CampanhasNaTela.TipoFiltro("Todos").Should().BeNull();
        CampanhasNaTela.TipoFiltro("Confirmação").Should().Be(TipoContato.ConfirmacaoSessao);
        CampanhasNaTela.TipoFiltro("NPS").Should().Be(TipoContato.Nps);
        CampanhasNaTela.TipoFiltro("Recall").Should().Be(TipoContato.Recall);
        CampanhasNaTela.StatusFiltro("A enviar").Should().Be(StatusContato.Pendente);
        CampanhasNaTela.StatusFiltro("Todos").Should().BeNull();
        CampanhasNaTela.StatusFiltro("Enviados").Should().Be(StatusContato.Enviado);
        CampanhasNaTela.StatusFiltro("Respondidos").Should().Be(StatusContato.Respondido);
    }

    [Fact]
    public void A_linha_formata_sem_enum_cru_e_desliga_o_que_nao_cabe()
    {
        var aberta = CampanhasNaTela.Linha(Contato(TipoContato.Nps), Hoje);
        aberta.Campanha.Should().Be("Pesquisa de satisfação (NPS)");
        aberta.Situacao.Should().Be("A enviar");
        aberta.Nota.Should().Be("—");
        aberta.EhNps.Should().BeTrue();
        aberta.PodeEnviar.Should().BeTrue();
        aberta.Referencia.Should().Be("18/10/2026");

        var respondida = CampanhasNaTela.Linha(Contato(TipoContato.Nps, StatusContato.Respondido, nota: 9), Hoje);
        respondida.Nota.Should().Be("9 · Promotor");
        respondida.PodeAgir.Should().BeFalse("respondido não volta ao fluxo");
        respondida.PodeEnviar.Should().BeFalse();

        var semFone = CampanhasNaTela.Linha(Contato(TipoContato.Recall, telefone: null), Hoje);
        semFone.Telefone.Should().Be("sem telefone");
        semFone.PodeAgir.Should().BeTrue("a linha continua na fila");
        semFone.PodeEnviar.Should().BeFalse("sem telefone não há WhatsApp a abrir");
        semFone.EhNps.Should().BeFalse("só o NPS registra nota");
    }

    [Fact]
    public void A_mensagem_de_cada_tipo_vem_da_definicao_unica()
    {
        var recall = CampanhasNaTela.Linha(Contato(TipoContato.Recall, referencia: Hoje.AddDays(-70)), Hoje);
        recall.DiasSemVir.Should().Be(70);
        CampanhasNaTela.Mensagem(recall, Hoje).Should().Be(MensagensDeContato.Recall("Maria da Silva", 70));

        var nps = CampanhasNaTela.Linha(Contato(TipoContato.Nps), Hoje);
        CampanhasNaTela.Mensagem(nps, Hoje).Should().StartWith("Olá, Maria! De 0 a 10");

        var conf = CampanhasNaTela.Linha(Contato(TipoContato.ConfirmacaoSessao), Hoje) with
        {
            QuandoCru = Hoje.AddDays(1).ToDateTime(new TimeOnly(14, 0))
        };
        CampanhasNaTela.Mensagem(conf, Hoje).Should().Contain("amanhã às 14:00");
    }

    [Fact]
    public void A_rodada_conta_quem_ficou_de_fora_e_por_que()
    {
        CampanhasNaTela.ResultadoDaRodada(new ResultadoCampanha(TipoContato.Nps, 4, 1, 2, 3))
            .Should().Be("Pesquisa de satisfação (NPS): 4 gerado(s) · 1 já existia(m) · 2 sem consentimento de comunicação (LGPD) · 3 sem telefone no cadastro.");
        CampanhasNaTela.ResultadoDaRodada(new ResultadoCampanha(TipoContato.Nps, 0, 0, 0, 0))
            .Should().Be("Pesquisa de satisfação (NPS): 0 gerado(s).");
    }

    [Theory]
    [InlineData("9", 9, null)]
    [InlineData("  10 adorei o atendimento ", 10, "adorei o atendimento")]
    [InlineData("0", 0, null)]
    public void A_nota_e_o_primeiro_termo_e_o_resto_e_o_comentario(string texto, int nota, string? comentario)
    {
        var lida = CampanhasNaTela.LerNota(texto);
        lida.Erro.Should().BeNull();
        lida.Nota.Should().Be(nota);
        lida.Comentario.Should().Be(comentario);
    }

    [Theory]
    [InlineData("11")]
    [InlineData("-1")]
    [InlineData("nove")]
    [InlineData("")]
    public void Nota_fora_de_0_a_10_e_recusada_com_a_frase_da_tela(string texto)
        => CampanhasNaTela.LerNota(texto).Erro.Should().Be("A nota precisa ser um número de 0 a 10.");

    [Fact]
    public void O_vazio_diz_qual_recorte_esta_vazio()
    {
        CampanhasNaTela.Vazio("Todos", "Todos").Titulo.Should().Be("Nenhuma mensagem nesta janela");
        var (titulo, descricao) = CampanhasNaTela.Vazio("NPS", "A enviar");
        titulo.Should().Be("Nada nesta fila");
        descricao.Should().Contain("NPS").And.Contain("\"A enviar\"");
    }
}
