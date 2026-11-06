using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;
using static Clinica.Tests.WhatsAppDeTeste;

namespace Clinica.Tests;

/// <summary>
/// QUAL CONVERSA O PAINEL AO LADO ABRE (out/2026, fase 2 do WhatsApp nativo): a regra pura de
/// <see cref="ConversaAoLadoNaTela"/>. O telefone não é identidade — a conversa ligada a OUTRA ficha com o
/// mesmo número (a mãe que divide o celular com o filho) nunca aparece no painel deste paciente.
/// </summary>
public sealed class ConversaAoLadoNaTelaTests
{
    [Fact]
    public void As_conversas_ligadas_a_ficha_vem_primeiro_e_a_mais_recente_e_a_escolhida()
    {
        var antiga = Resumo("wa", paciente: 101, atualizada: Em(20, 9, 0));
        var nova = Resumo("sms", canal: "sms", paciente: 101, atualizada: Em(24, 9, 0));
        var doTelefone = Resumo("solta", atualizada: Em(24, 10, 0));

        var r = ConversaAoLadoNaTela.DoPaciente([antiga, doTelefone, nova], 101, "(22) 99876-5432");

        r.PeloTelefone.Should().BeFalse();
        r.Conversas.Select(c => c.Id).Should().Equal("sms", "wa");
        r.Escolhida!.Id.Should().Be("sms");
        r.TemEscolha.Should().BeTrue();
    }

    [Fact]
    public void Sem_conversa_ligada_valem_as_do_telefone_da_ficha_ainda_sem_ficha_com_o_aviso()
    {
        var sem = Resumo("solta", contato: "5522988112233");
        var deOutraFicha = Resumo("da-mae", contato: "5522988112233", paciente: 7, nomeDaFicha: "Terezinha Souza");
        var outroNumero = Resumo("outra", contato: "5522991337788");

        var r = ConversaAoLadoNaTela.DoPaciente([sem, deOutraFicha, outroNumero], 8, "22 98811-2233");

        r.PeloTelefone.Should().BeTrue("a conversa não está ligada à ficha: a recepção confere");
        r.Conversas.Select(c => c.Id).Should().Equal(["solta"], "a conversa ligada à ficha da mãe é de OUTRA pessoa");
        r.TemEscolha.Should().BeFalse();
    }

    [Fact]
    public void Paciente_sem_conversa_nenhuma_nao_tem_o_que_abrir()
    {
        var r = ConversaAoLadoNaTela.DoPaciente([Resumo("outra", contato: "5522991337788")], 101, "(22) 99876-5432");

        r.Escolhida.Should().BeNull();
        r.PeloTelefone.Should().BeFalse("sem conversa não há o que conferir");
    }

    [Fact]
    public void Pedido_sem_ficha_procura_so_pelo_telefone_e_telefone_invalido_nao_acha_nada()
    {
        var sem = Resumo("solta");
        ConversaAoLadoNaTela.DoPaciente([sem], null, "(22) 99876-5432").Escolhida!.Id.Should().Be("solta");
        ConversaAoLadoNaTela.DoPaciente([sem], null, "123").Escolhida.Should().BeNull();
        ConversaAoLadoNaTela.DoPaciente([sem], null, null).Escolhida.Should().BeNull();
    }

    [Fact]
    public void As_frases_do_painel()
    {
        ConversaAoLadoNaTela.Opcao(Resumo("c", canal: "sms", encerrada: true, atualizada: Em(24, 9, 5)), Agora)
            .Should().Be("SMS · (22) 99876-5432 · encerrada · hoje às 09:05");
        ConversaAoLadoNaTela.Opcao(Resumo("c", atualizada: Em(23, 17, 40)), Agora)
            .Should().Be("WhatsApp · (22) 99876-5432 · aberta · ontem às 17:40");
        ConversaAoLadoNaTela.SemConversa("Rosana Figueiredo Lemos").Titulo.Should().Be("Ainda não há conversa com Rosana");
        ConversaAoLadoNaTela.SemConversa("(22) 99876-5432").Titulo.Should().Be("Ainda não há conversa com este paciente");
        ConversaAoLadoNaTela.AvisoDoTelefone("Pedro Henrique Souza").Texto.Should().Contain("ficha de Pedro");
    }
}
