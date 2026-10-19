using Clinica.Application.Modelos;
using Clinica.Domain.Avaliacoes;
using Clinica.Domain.Medidas;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O que a seção Acompanhamento da tela do paciente afirma (out/2026: subido das ViewModels WPF
/// EvolucaoDor, Medidas e Avaliações na migração para Blazor).
/// </summary>
public class AcompanhamentoDoPacienteNaTelaTests
{
    private static PontoEva P(int dia, int antes, int depois) => new(new DateOnly(2026, 10, dia), antes, depois);

    [Fact]
    public void Sem_par_EVA_nao_ha_curva_e_o_resumo_diz_por_que()
    {
        var t = EvolucaoDaDorNaTela.Montar(new EvolucaoDaDor([], 4));

        t.TemCurva.Should().BeFalse();
        t.DorInicial.Should().Be("—");
        t.Resumo.Should().Contain("4 sessão(ões) registradas e nenhuma com o par EVA");
    }

    [Fact]
    public void Nenhuma_sessao_diz_que_nada_foi_registrado()
        => EvolucaoDaDorNaTela.Montar(new EvolucaoDaDor([], 0)).Resumo
            .Should().Be("Nenhuma sessão registrada no prontuário ainda.");

    [Fact]
    public void Com_menos_de_tres_sessoes_nao_se_fala_em_tendencia()
    {
        var t = EvolucaoDaDorNaTela.Montar(new EvolucaoDaDor([P(1, 8, 4), P(8, 7, 3)], 2));

        t.TemCurva.Should().BeTrue();
        t.Tendencia.Should().StartWith("São necessárias ao menos 3 sessões");
        t.TendenciaPreocupa.Should().BeFalse();
        t.DorInicial.Should().Be("8/10");
        t.DorAtual.Should().Be("3/10");
        t.GanhoAcumulado.Should().Be("−5 pontos");
    }

    [Fact]
    public void Dor_de_chegada_parada_preocupa()
    {
        var t = EvolucaoDaDorNaTela.Montar(new EvolucaoDaDor([P(1, 7, 3), P(8, 7, 3), P(15, 7, 2)], 3));

        t.TendenciaPreocupa.Should().BeTrue();
        t.Tendencia.Should().Contain("não mudou");
    }

    [Fact]
    public void Dor_de_chegada_caindo_nao_preocupa_e_a_linha_mostra_o_alivio()
    {
        var t = EvolucaoDaDorNaTela.Montar(new EvolucaoDaDor([P(1, 8, 4), P(8, 6, 3), P(15, 5, 5)], 3));

        t.TendenciaPreocupa.Should().BeFalse();
        t.Linhas[0].Variacao.Should().Be("−4");
        t.Linhas[0].Aliviou.Should().BeTrue();
        t.Linhas[2].Variacao.Should().Be("sem mudança");
        t.CurvaAntes.Should().HaveCount(3);
    }

    [Fact]
    public void Medida_sem_colheita_diz_que_nao_ha()
    {
        var l = MedidasNaTela.Ler(new SerieMedida(CatalogoMedidas.Peso, "Peso", "kg", []));

        l.TemSerie.Should().BeFalse();
        l.Frase.Should().Be("Nenhuma colheita de Peso para este paciente ainda.");
    }

    [Fact]
    public void Uma_colheita_e_linha_de_base_e_nao_variacao_zero()
    {
        var l = MedidasNaTela.Ler(new SerieMedida(CatalogoMedidas.Peso, "Peso", "kg",
            [new PontoMedida(new DateOnly(2026, 10, 1), 80m, null, null, GravidadeFaixa.Normal)]));

        l.Variacao.Should().Be("—");
        l.Frase.Should().Contain("linha de base");
    }

    [Fact]
    public void A_curva_de_duas_procedencias_diz_isso_e_a_enfermagem_e_avisada()
    {
        var l = MedidasNaTela.Ler(new SerieMedida("pa", "Pressão", "mmHg",
        [
            new PontoMedida(new DateOnly(2026, 10, 1), 130m, 85m, null, GravidadeFaixa.Normal, Procedencia: "consultório"),
            new PontoMedida(new DateOnly(2026, 10, 1), 120m, 80m, null, GravidadeFaixa.Normal, Procedencia: "enfermagem",
                Hora: new TimeOnly(14, 20))
        ]));

        l.Frase.Should().Contain("duas procedências");
        l.HistoricoSoDoConsultorio.Should().Contain("1 aferição(ões) da ENFERMAGEM");
        l.Curva[1].Rotulo.Should().Be("01/10 14:20");
    }

    [Theory]
    [InlineData("72,5", 72.5)]
    [InlineData("72.5", 72.5)]
    [InlineData(" 80 ", 80)]
    public void O_numero_digitado_aceita_virgula_e_ponto(string texto, double esperado)
        => MedidasNaTela.LerNumero(texto).Should().Be((decimal)esperado);

    [Fact]
    public void Numero_invalido_ou_vazio_nao_vira_zero()
    {
        MedidasNaTela.LerNumero("").Should().BeNull();
        MedidasNaTela.LerNumero("abc").Should().BeNull();
    }

    [Fact]
    public void A_soma_parcial_diz_quantas_faltam()
    {
        AvaliacoesNaTela.Parcial(9, []).Should().Be("9 item(ns) a responder.");
        AvaliacoesNaTela.Parcial(9, [1, 2]).Should().Be("2 de 9 respondidas — faltam 7. Soma parcial: 3.");
        AvaliacoesNaTela.Parcial(2, [1, 2]).Should().Be("Escala completa. Soma: 3.");
    }

    [Fact]
    public void Escala_nunca_aplicada_e_linha_de_base()
    {
        var l = AvaliacoesNaTela.Ler(new EvolucaoDoEscore("phq9", "PHQ-9", "pontos", 27, true, []), "PHQ-9");

        l.TemCurva.Should().BeFalse();
        l.Frase.Should().StartWith("PHQ-9 ainda não foi aplicado");
    }

    [Fact]
    public void Todas_as_escalas_oferece_o_registro_inteiro()
        => AvaliacoesNaTela.Oferecidos(true, null).Should().BeEquivalentTo(RegistroInstrumentos.Todos);
}
