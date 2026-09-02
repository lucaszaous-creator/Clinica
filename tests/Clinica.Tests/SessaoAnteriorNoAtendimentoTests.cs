using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O QUE O PROFISSIONAL VÊ DA SESSÃO PASSADA AO ABRIR O ATENDIMENTO (parcela 77).
///
/// As três últimas sessões ficam ABERTAS ao lado do formulário desde que a tela nasceu — e
/// o resumo delas tinha QUATRO campos enquanto a sessão passou a ter doze. Ficava cego para
/// a hipótese, o plano, o encaminhamento e o <b>retorno sugerido</b>, que é literalmente a
/// resposta para "por que este paciente está aqui hoje": o profissional escrevia "voltar em
/// 7 dias para reavaliar a EVA", o paciente voltava, e a tela não dizia.
///
/// ⚠️ A composição mora na APPLICATION e não na ViewModel de propósito: decisão que vive em
/// projeto WPF não é alcançada pelo <c>dotnet test</c> — é a lição da grade da semana
/// (parcela 69). Estes testes existem porque ela mudou de casa.
/// </summary>
public class SessaoAnteriorNoAtendimentoTests
{
    private static readonly DateOnly Dia = new(2026, 8, 20);

    private static Evolucao Sessao() => new() { Id = 7, Data = Dia };

    /// <summary>⚠️ A asserção que carrega a suíte: é o campo que faltava.</summary>
    [Fact]
    public void O_retorno_sugerido_aparece_com_a_data_e_o_motivo()
    {
        var e = Sessao();
        e.RetornoSugeridoEm = new DateOnly(2026, 8, 27);
        e.RetornoSugeridoNota = "reavaliar a EVA";

        ResumoSessaoAnterior.De(e).Retorno
            .Should().Contain("27/08/2026").And.Contain("reavaliar a EVA");
    }

    [Fact]
    public void Retorno_sem_motivo_ainda_diz_a_data()
    {
        var e = Sessao();
        e.RetornoSugeridoEm = new DateOnly(2026, 8, 27);

        ResumoSessaoAnterior.De(e).Retorno.Should().Contain("27/08/2026");
    }

    /// <summary>
    /// Linha vazia gasta o mesmo espaço de uma que informa, e a coluna tem ~350 px com três
    /// sessões. O que não foi escrito não vira "—": some.
    /// </summary>
    [Fact]
    public void O_que_nao_foi_escrito_NAO_ocupa_linha()
    {
        var r = ResumoSessaoAnterior.De(Sessao());

        r.Retorno.Should().BeEmpty();
        r.Queixa.Should().BeEmpty();
        r.Historia.Should().BeEmpty();
        r.ExameFisico.Should().BeEmpty();
        r.Hipotese.Should().BeEmpty();
        r.Conduta.Should().BeEmpty();
        r.Evolucao.Should().BeEmpty();
        r.Orientacoes.Should().BeEmpty();
        r.Plano.Should().BeEmpty();
        r.Encaminhamento.Should().BeEmpty();
    }

    /// <summary>
    /// Os três que o HISTÓRICO de sessões precisava e o resumo não tinha: história da
    /// doença, exame físico e orientações. O histórico lê esta MESMA composição — sem estas
    /// linhas, o texto inteiro da consulta não era legível em lugar nenhum, com o comentário
    /// do painel jurando que "o texto inteiro continua no Histórico".
    /// </summary>
    /// <summary>
    /// O selo da EVA na lista de sessões anteriores é pintado pela VARIAÇÃO: verde quando
    /// a dor caiu, vermelho quando subiu, neutro sem par. Cor errada aqui diria ao médico
    /// que a sessão passada ajudou quando piorou.
    /// </summary>
    [Theory]
    [InlineData(8, 3, true, false)]
    [InlineData(3, 8, false, true)]
    [InlineData(5, 5, false, false)]
    [InlineData(null, 5, false, false)]
    public void O_selo_da_EVA_segue_a_variacao(int? antes, int? depois, bool melhorou, bool piorou)
    {
        var e = Sessao();
        e.EvaAntes = antes;
        e.EvaDepois = depois;

        var r = ResumoSessaoAnterior.De(e);

        r.Melhorou.Should().Be(melhorou);
        r.Piorou.Should().Be(piorou);
    }

    /// <summary>A frase da régua é UMA para o balcão e o consultório.</summary>
    [Theory]
    [InlineData(8, 3, "Aliviou 5 ponto(s) nesta sessão.")]
    [InlineData(3, 8, "Piorou 5 ponto(s) nesta sessão.")]
    [InlineData(4, 4, "Sem mudança nesta sessão.")]
    [InlineData(null, 4, "Meça antes e depois para saber se aliviou.")]
    [InlineData(4, null, "Meça antes e depois para saber se aliviou.")]
    public void A_regua_escreve_a_variacao(int? antes, int? depois, string esperado)
        => VariacaoDaDor.Descrever(antes, depois).Should().Be(esperado);

    [Fact]
    public void A_historia_o_exame_fisico_e_as_orientacoes_entram_rotulados()
    {
        var e = Sessao();
        e.HistoriaDoencaAtual = "Dor há 3 semanas, pior ao sentar.";
        e.ExameFisico = "Lasègue positivo à direita.";
        e.Orientacoes = "Calor local, evitar carga.";

        var r = ResumoSessaoAnterior.De(e);

        r.Historia.Should().StartWith("História da doença atual: ").And.Contain("3 semanas");
        r.ExameFisico.Should().StartWith("Exame físico: ").And.Contain("Lasègue");
        r.Orientacoes.Should().StartWith("Orientações: ").And.Contain("Calor local");
    }

    [Theory]
    [InlineData("lombalgia mecânica", "M54.5", "Hipótese: lombalgia mecânica (M54.5)")]
    [InlineData("lombalgia mecânica", null, "Hipótese: lombalgia mecânica")]
    [InlineData(null, "M54.5", "CID: M54.5")]
    [InlineData(null, null, "")]
    public void A_hipotese_leva_o_CID_entre_parenteses(string? hipotese, string? cid, string esperado)
    {
        var e = Sessao();
        e.HipoteseDiagnostica = hipotese;
        e.CidSessao = cid;

        ResumoSessaoAnterior.De(e).Hipotese.Should().Be(esperado);
    }

    /// <summary>
    /// A EVOLUÇÃO é o texto mais escrito do sistema, e estava no modelo do painel sem
    /// aparecer na tela — dado calculado sem leitor, no lugar onde ele mais importa.
    /// </summary>
    [Fact]
    public void A_evolucao_e_a_conduta_entram_rotuladas()
    {
        var e = Sessao();
        e.Conduta = "Agulhamento lombar, 20 min.";
        e.TextoEvolucao = "Refere melhora da dor ao final da sessão.";
        e.PlanoTerapeutico = "10 sessões, reavaliar na 5ª";
        e.Encaminhamento = "psiquiatria daqui";

        var r = ResumoSessaoAnterior.De(e);

        r.Conduta.Should().StartWith("Conduta: ").And.Contain("Agulhamento");
        r.Evolucao.Should().StartWith("Evolução: ").And.Contain("melhora da dor");
        r.Plano.Should().StartWith("Plano: ").And.Contain("reavaliar na 5ª");
        r.Encaminhamento.Should().StartWith("Encaminhado: ").And.Contain("psiquiatria");
    }

    [Fact]
    public void A_EVA_so_aparece_como_par()
    {
        var comPar = Sessao();
        comPar.EvaAntes = 7;
        comPar.EvaDepois = 4;

        ResumoSessaoAnterior.De(comPar).Eva.Should().Be("EVA 7 → 4");

        var solta = Sessao();
        solta.EvaAntes = 7;

        ResumoSessaoAnterior.De(solta).Eva.Should().Be("EVA não medida",
            "uma medida solta não diz se o tratamento funcionou — a regra da parcela 2");
    }
}
