using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// As regras da coleta do termo do paciente que subiram do shell WPF (<c>ColetaDeTermo</c>, as
/// janelas de escolha de termo e de sessão e o <c>AssinaturaPacienteViewModel</c>) para
/// <see cref="ColetaDeTermoNaTela"/> na conversão para Blazor (out/2026). As quatro portas da
/// coleta passam por elas; o que se fixa aqui é que a decisão continua a mesma de antes.
/// </summary>
public sealed class ColetaDeTermoNaTelaTests
{
    private static SessaoParaTermo Sessao(int id, bool hoje) =>
        new(id, new DateTime(2026, 10, 20, 9, 0, 0).AddDays(hoje ? 0 : id), "BSV", "Dra. Ana", hoje);

    [Fact]
    public void Documento_ja_emitido_nao_pergunta_sessao()
        => ColetaDeTermoNaTela.Decidir(7, null, [Sessao(1, true), Sessao(2, false)])
            .Should().Be(DecisaoDaSessaoDoTermo.SemSessao);

    [Fact]
    public void Quem_chamou_ja_sabe_a_sessao_nada_e_perguntado()
    {
        var d = ColetaDeTermoNaTela.Decidir(null, 42, [Sessao(1, true), Sessao(2, false)]);
        d.Perguntar.Should().BeFalse();
        d.Sessao.Should().Be(42);
    }

    [Fact]
    public void Uma_sessao_hoje_e_nenhuma_a_frente_amarra_sozinho_e_diz_a_qual()
    {
        var s = Sessao(5, true);
        var d = ColetaDeTermoNaTela.Decidir(null, null, [s]);
        d.Perguntar.Should().BeFalse();
        d.Sessao.Should().Be(5);
        d.Rotulo.Should().Be(ColetaDeTermoNaTela.Descrever(s));
    }

    [Fact]
    public void Duas_sessoes_ou_sessao_a_frente_pergunta()
    {
        ColetaDeTermoNaTela.Decidir(null, null, [Sessao(1, true), Sessao(2, false)]).Perguntar.Should().BeTrue();
        ColetaDeTermoNaTela.Decidir(null, null, [Sessao(3, false)]).Perguntar.Should().BeTrue();
        ColetaDeTermoNaTela.Decidir(null, null, []).Should().Be(DecisaoDaSessaoDoTermo.SemSessao);
    }

    [Fact]
    public void Avulso_e_opcao_de_primeira_classe_e_so_a_unica_de_hoje_vem_marcada()
    {
        var opcoes = ColetaDeTermoNaTela.Opcoes([Sessao(1, true), Sessao(2, false)]);
        opcoes[^1].Should().Be(OpcaoSessaoTermo.Avulso);
        ColetaDeTermoNaTela.PreSelecionada(opcoes)!.AgendamentoId.Should().Be(1);

        var duasHoje = ColetaDeTermoNaTela.Opcoes([Sessao(1, true), Sessao(2, true)]);
        ColetaDeTermoNaTela.PreSelecionada(duasHoje).Should().BeNull("marcar a primeira seria decidir pela pessoa");
    }

    [Fact]
    public void Alergia_resolvida_continua_alertando()
    {
        ColetaDeTermoNaTela.FormatarAlergia(new ProblemaPaciente { Descricao = "Dipirona" }).Should().Be("Dipirona");
        ColetaDeTermoNaTela.FormatarAlergia(new ProblemaPaciente { Descricao = "Látex", Situacao = SituacaoProblema.Resolvido })
            .Should().Contain("o alerta continua");
    }

    [Fact]
    public void Confirmar_exige_permissao_traco_e_documento_conferido()
    {
        ColetaDeTermoNaTela.PodeConfirmar(true, true, "RG 123", false).Should().BeTrue();
        ColetaDeTermoNaTela.PodeConfirmar(false, true, "RG 123", false).Should().BeFalse();
        ColetaDeTermoNaTela.PodeConfirmar(true, false, "RG 123", false).Should().BeFalse();
        ColetaDeTermoNaTela.PodeConfirmar(true, true, "  ", false).Should().BeFalse();
        ColetaDeTermoNaTela.PodeConfirmar(true, true, "RG 123", true).Should().BeFalse();
    }

    [Fact]
    public void Whatsapp_poe_o_DDI_e_recusa_telefone_invalido()
    {
        ColetaDeTermoNaTela.EnderecoDoWhatsapp("(11) 98765-4321", "Olá")
            .Should().Be("https://wa.me/5511987654321?text=Ol%C3%A1");
        ColetaDeTermoNaTela.EnderecoDoWhatsapp("5511987654321", "x").Should().StartWith("https://wa.me/5511987654321");
        ColetaDeTermoNaTela.EnderecoDoWhatsapp("1234", "x").Should().BeNull();
        ColetaDeTermoNaTela.EnderecoDoWhatsapp(null, "x").Should().BeNull();
    }

    [Fact]
    public void Declaracao_nasce_sem_resposta()
    {
        var d = new DeclaracaoItem(1, "Estou em jejum", null);
        d.Resposta.Should().BeNull("pré-marcar seria fabricar a resposta");
        d.MarcarNao();
        d.EhNao.Should().BeTrue();
        d.MarcarSim();
        d.EhSim.Should().BeTrue();
    }
}
