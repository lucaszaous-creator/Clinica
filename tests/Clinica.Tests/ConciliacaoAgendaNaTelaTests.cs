using Clinica.Application.Formatacao;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que o formulário "Conferir atendimentos" (Blazor, out/2026) afirma e decide — migrado do
/// <c>ConciliacaoAgendaViewModel</c> WPF. A guarda que vale pela tela inteira é a do meio:
/// com sessão no dia, LANÇAR fica apagado (seria um segundo jogo de guias) e ENCERRAR acende.
/// </summary>
public class ConciliacaoAgendaNaTelaTests
{
    private static readonly DateTime Quando = new(2026, 9, 3, 14, 30, 0);

    private static HorarioParado Parado(params SessaoLancadaNoDia[] sessoes)
        => new(11, 4, "Severino da Silva", Quando, Importado: true, "Dra. Ana", DiasParado: 7, sessoes);

    private static SessaoLancadaNoDia Sessao(string numero, int guias = 1)
        => new(90 + numero.Length, numero, "Acupuntura", guias);

    [Fact]
    public void Lancar_so_sem_sessao_no_dia_e_encerrar_so_com_ela()
    {
        var sem = Parado();
        var com = Parado(Sessao("2026/0012"));

        ConciliacaoAgendaNaTela.PodeLancar(sem, resolvida: false).Should().BeTrue();
        ConciliacaoAgendaNaTela.PodeSubstituir(sem, resolvida: false).Should().BeFalse();
        ConciliacaoAgendaNaTela.PodeLancar(com, resolvida: false).Should().BeFalse("lançar criaria um segundo jogo de guias");
        ConciliacaoAgendaNaTela.PodeSubstituir(com, resolvida: false).Should().BeTrue();

        ConciliacaoAgendaNaTela.PodeLancar(sem, resolvida: true).Should().BeFalse();
        ConciliacaoAgendaNaTela.PodeSubstituir(com, resolvida: true).Should().BeFalse();
        ConciliacaoAgendaNaTela.PodeMarcarFalta(resolvida: true).Should().BeFalse();
        ConciliacaoAgendaNaTela.PodeMarcarFalta(resolvida: false).Should().BeTrue();
    }

    [Fact]
    public void As_barreiras_sao_os_bits_exatos_do_ViewModel_WPF()
    {
        ConciliacaoAgendaNaTela.BitFalta.Should().Be(Permissao.EditarAgenda);
        ConciliacaoAgendaNaTela.BitsLancarOuEncerrar.Should().Be(Permissao.EditarAgenda | Permissao.LancarAtendimento);
    }

    [Fact]
    public void A_linha_diz_quando_de_quem_e_ha_quantos_dias()
    {
        ConciliacaoAgendaNaTela.Cabecalho(Quando, "Severino").Should().Be("03/09/2026 14:30 · Severino");
        ConciliacaoAgendaNaTela.Origem(true).Should().Be("Importado do sistema anterior");
        ConciliacaoAgendaNaTela.Origem(false).Should().Be("Marcado aqui");
        ConciliacaoAgendaNaTela.Parado(1).Should().Be("parado há 1 dia");
        ConciliacaoAgendaNaTela.Parado(12).Should().Be("parado há 12 dias");
        ConciliacaoAgendaNaTela.Profissional(null).Should().Be("—");
        ConciliacaoAgendaNaTela.Profissional(" ").Should().Be("—");
        ConciliacaoAgendaNaTela.Profissional("Dra. Ana").Should().Be("Dra. Ana");
    }

    [Fact]
    public void O_resumo_conta_as_tres_perguntas()
    {
        var vazia = new ConciliacaoDaAgenda([], [], new DateOnly(2026, 5, 13), new DateOnly(2026, 9, 8));
        ConciliacaoAgendaNaTela.Resumo(vazia).Should().Be("Nada pendurado entre a agenda e os atendimentos.");
        ConciliacaoAgendaNaTela.Periodo(vazia).Should().Be("De 13/05/2026 a 08/09/2026.");

        var c = new ConciliacaoDaAgenda([Parado(), Parado(Sessao("1"))],
            [new HorarioOrfao(3, "Ana", Quando, null, 4)], vazia.Desde, vazia.Ate);
        ConciliacaoAgendaNaTela.Resumo(c).Should().Be(
            "2 horário(s) parado(s) — 1 com sessão já lançada no dia, 1 sem sessão nenhuma."
            + " E 1 horário(s) marcado(s) como realizados SEM atendimento.");
    }

    [Fact]
    public void Com_uma_sessao_so_ela_encerra_sem_perguntar()
    {
        var h = Parado(Sessao("2026/0012"));
        ConciliacaoAgendaNaTela.SessaoEscolhida(h, null)!.Numero.Should().Be("2026/0012");
    }

    [Fact]
    public void Com_varias_a_digitada_vale_sem_diferenca_de_espaco_nem_caixa()
    {
        var h = Parado(Sessao("2026/0012"), Sessao("A-7"));
        ConciliacaoAgendaNaTela.SessaoEscolhida(h, "  a-7 ")!.Numero.Should().Be("A-7");
        ConciliacaoAgendaNaTela.SessaoEscolhida(h, "999").Should().BeNull();
        ConciliacaoAgendaNaTela.SessaoEscolhida(h, null).Should().BeNull("cancelar a pergunta não escolhe nada");
        ConciliacaoAgendaNaTela.NumeroQueNaoExiste(h, "999")
            .Should().Be("\"999\" não é o número de nenhuma das sessões do dia (2026/0012 ou A-7).");
        ConciliacaoAgendaNaTela.PerguntaQualSessao(h).Should().Contain("• nº 2026/0012 · Acupuntura · 1 guia")
            .And.EndWith("Digite o número da sessão (2026/0012 ou A-7):");
    }

    [Fact]
    public void As_recusas_escritas_dizem_a_saida()
    {
        ConciliacaoAgendaNaTela.JaTemSessao(Parado(Sessao("1")))
            .Should().Be("Severino da Silva JÁ tem sessão lançada em 03/09/2026. Lançar de novo criaria um segundo jogo de guias para a mesma sessão.");
        ConciliacaoAgendaNaTela.SemSessao(Parado())
            .Should().Contain("falta ou lançar retroativo");
        ConciliacaoAgendaNaTela.ConvenioADefinirSemPonte("Fulano está com o convênio A DEFINIR.")
            .Should().EndWith("Escolha o convênio na ficha do paciente e volte para lançar.");
    }

    [Fact]
    public void O_desfecho_leva_os_avisos_do_servico_junto()
    {
        ConciliacaoAgendaNaTela.DesfechoFalta([]).Should().Be("Falta registrada.");
        ConciliacaoAgendaNaTela.DesfechoFalta(["A guia 123 já foi baixada no portal."])
            .Should().Be("Falta registrada. A guia 123 já foi baixada no portal.");

        var at = new Atendimento { Numero = "2026/0044" };
        at.Codigos.Add(new CodigoFaturamento());
        at.Codigos.Add(new CodigoFaturamento());
        ConciliacaoAgendaNaTela.DesfechoLancamento(at, []).Should().Be("Atendimento nº 2026/0044 lançado com 2 código(s).");
        ConciliacaoAgendaNaTela.DesfechoSubstituicao(Sessao("2026/0012"), ["Guia suspensa."])
            .Should().Be("Horário encerrado pela sessão nº 2026/0012. Guia suspensa.");
    }

    [Fact]
    public void As_perguntas_dizem_o_que_o_ato_faz_antes_do_clique()
    {
        var h = Parado(Sessao("2026/0012"));
        ConciliacaoAgendaNaTela.PerguntaFalta(h).Should().StartWith("Severino da Silva — horário de 03/09/2026 14:30.")
            .And.Contain("indicadores de falta");
        ConciliacaoAgendaNaTela.PerguntaLancar(h).Should().Contain("DIA DO HORÁRIO, não de hoje");
        ConciliacaoAgendaNaTela.PerguntaSubstituir(h, h.SessoesDoDia[0])
            .Should().Contain("nº 2026/0012 · Acupuntura · 1 guia").And.Contain("sem contar como cancelamento nem como falta");
    }
}
