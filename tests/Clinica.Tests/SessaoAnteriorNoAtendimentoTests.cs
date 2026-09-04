using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O QUE O PROFISSIONAL VÊ DA SESSÃO PASSADA AO ABRIR O ATENDIMENTO (parcela 77).
///
/// O resumo da sessão anterior tinha QUATRO campos enquanto a sessão passou a ter doze.
/// Ficava cego para a hipótese, o plano, o encaminhamento e o <b>retorno sugerido</b>, que é
/// literalmente a resposta para "por que este paciente está aqui hoje": o profissional
/// escrevia "voltar em 7 dias para reavaliar a EVA", o paciente voltava, e a tela não dizia.
///
/// ⚠️ A composição mora na APPLICATION e não na ViewModel de propósito: decisão que vive em
/// projeto WPF não é alcançada pelo <c>dotnet test</c> — é a lição da grade da semana
/// (parcela 69). Estes testes existem porque ela mudou de casa.
///
/// ⚠️ Na rodada da ABA (set/2026) o rótulo deixou de ser um prefixo dentro da frase e virou
/// um par: <c>Rotulo</c> e <c>Valor</c> separados. A aba desenha uma coluna de rótulos
/// alinhada, e é isso que faz seis campos se lerem como um registro em vez de seis
/// fragmentos de texto — a reprovação do cliente.
/// </summary>
public class SessaoAnteriorNoAtendimentoTests
{
    private static readonly DateOnly Dia = new(2026, 8, 20);

    private static Evolucao Sessao() => new() { Id = 7, Data = Dia };

    private static string? Valor(ResumoSessaoAnterior r, string rotulo)
        => r.Campos.FirstOrDefault(c => c.Rotulo == rotulo)?.Valor;

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
    /// Linha vazia gasta o mesmo espaço de uma que informa — e numa sessão da folha única o
    /// normal é haver UM campo escrito, não seis. O que não foi escrito não vira "—": não
    /// existe.
    /// </summary>
    [Fact]
    public void O_que_nao_foi_escrito_NAO_ocupa_linha()
    {
        var r = ResumoSessaoAnterior.De(Sessao());

        r.Retorno.Should().BeEmpty();
        r.Campos.Should().BeEmpty();
    }

    [Theory]
    [InlineData("lombalgia mecânica", "M54.5", "Hipótese", "lombalgia mecânica (M54.5)")]
    [InlineData("lombalgia mecânica", null, "Hipótese", "lombalgia mecânica")]
    [InlineData(null, "M54.5", "CID", "M54.5")]
    public void A_hipotese_leva_o_CID_entre_parenteses(
        string? hipotese, string? cid, string rotulo, string valor)
    {
        var e = Sessao();
        e.HipoteseDiagnostica = hipotese;
        e.CidSessao = cid;

        var r = ResumoSessaoAnterior.De(e);

        r.Campos.Should().ContainSingle()
            .Which.Should().Be(new CampoDaSessaoAnterior(rotulo, valor));
    }

    [Fact]
    public void Sem_hipotese_e_sem_CID_nao_ha_linha_de_hipotese()
    {
        ResumoSessaoAnterior.De(Sessao()).Campos
            .Should().NotContain(c => c.Rotulo == "Hipótese" || c.Rotulo == "CID");
    }

    /// <summary>
    /// A EVOLUÇÃO é o texto mais escrito do sistema, e estava no modelo do painel sem
    /// aparecer na tela — dado calculado sem leitor, no lugar onde ele mais importa.
    ///
    /// ⚠️ A ORDEM é a de quem lê um prontuário, e é a mesma que o relatório do convênio
    /// imprime: queixa → hipótese → conduta → evolução → plano → encaminhamento.
    /// </summary>
    [Fact]
    public void Os_campos_escritos_saem_rotulados_e_na_ordem_da_leitura()
    {
        var e = Sessao();
        e.QueixaPrincipal = "dor lombar";
        e.HipoteseDiagnostica = "lombalgia mecânica";
        e.Conduta = "Agulhamento lombar, 20 min.";
        e.TextoEvolucao = "Refere melhora da dor ao final da sessão.";
        e.PlanoTerapeutico = "10 sessões, reavaliar na 5ª";
        e.Encaminhamento = "psiquiatria daqui";

        var r = ResumoSessaoAnterior.De(e);

        r.Campos.Select(c => c.Rotulo).Should().Equal(
            "Queixa", "Hipótese", "Conduta", "Evolução", "Plano", "Encaminhado");

        Valor(r, "Conduta").Should().Contain("Agulhamento");
        Valor(r, "Evolução").Should().Contain("melhora da dor");
        Valor(r, "Plano").Should().Contain("reavaliar na 5ª");
        Valor(r, "Encaminhado").Should().Contain("psiquiatria");
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

        ResumoSessaoAnterior.De(comPar).EvaMedida.Should().BeTrue();
        ResumoSessaoAnterior.De(solta).EvaMedida.Should().BeFalse();
    }

    /// <summary>
    /// ⚠️ A LINHA QUIETA DA FOLHA (set/2026) — é o que sobrou da coluna aberta que virou
    /// aba. Reler a sessão passada passou a custar um clique, e o que não pode custar
    /// clique nenhum é a razão de o paciente estar ali hoje.
    /// </summary>
    [Fact]
    public void A_linha_de_contexto_diz_a_data_a_EVA_e_o_retorno()
    {
        var e = Sessao();
        e.EvaAntes = 8;
        e.EvaDepois = 3;
        e.RetornoSugeridoEm = new DateOnly(2026, 8, 27);

        var linha = ResumoSessaoAnterior.ContextoDaUltima(ResumoSessaoAnterior.De(e));

        linha.Should().Contain("20/08/2026").And.Contain("EVA 8 → 3").And.Contain("27/08/2026");
    }

    [Fact]
    public void Sem_par_de_EVA_a_linha_de_contexto_NAO_diz_que_ela_faltou()
    {
        var linha = ResumoSessaoAnterior.ContextoDaUltima(ResumoSessaoAnterior.De(Sessao()));

        linha.Should().Contain("20/08/2026");
        linha.Should().NotContain("EVA",
            "numa linha quieta de contexto, 'EVA não medida' é ruído sobre o que a sessão " +
            "passada deixou de fazer — e o que a linha responde é o que ela deixou dito");
    }

    [Fact]
    public void Sem_sessao_anterior_a_linha_de_contexto_e_VAZIA()
    {
        ResumoSessaoAnterior.ContextoDaUltima(null).Should().BeEmpty(
            "a primeira consulta não tem o que dizer, e um 'Última sessão em —' seria uma " +
            "afirmação sobre uma sessão que não houve");
    }
}
