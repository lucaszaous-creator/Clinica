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
        r.Hipotese.Should().BeEmpty();
        r.Conduta.Should().BeEmpty();
        r.Evolucao.Should().BeEmpty();
        r.Plano.Should().BeEmpty();
        r.Encaminhamento.Should().BeEmpty();
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
