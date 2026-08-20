using System.Text.RegularExpressions;
using Clinica.Application.Assinatura;
using Clinica.Application.Servicos;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// ONDE o carimbo da assinatura é colado na folha (parcela 68, 8ª rodada).
///
/// A clínica mandou o print: o carimbo do prescritor saiu no ALTO da página, por cima do
/// título "PRESCRIÇÃO DE EXECUÇÃO INTERNA", enquanto o rodapé reservava o espaço dele e
/// ficava vazio. Saiu assim em TODO documento assinado até 20/08/2026.
///
/// A causa é uma inversão de eixo: o retângulo vai para o <c>/Rect</c> da anotação, e o
/// PDF tem a origem no canto INFERIOR esquerdo — foi medido, o PDFsharp grava o que
/// recebe, sem converter. A fórmula calculava a distância a partir do TOPO.
///
/// ⚠️ Estes testes olham o NÚMERO, e não o desenho, porque é o número que estava errado —
/// e olhar o desenho exigiria comparar imagem, que ninguém mantém. O invariante é o que a
/// geometria da folha promete: <b>o carimbo cabe dentro da faixa do rodapé</b>, medida do
/// pé da página.
/// </summary>
public class CarimboNoRodapeTests
{
    /// <summary>A4 em pontos — a régua contra a qual tudo aqui é medido.</summary>
    private const double AlturaPagina = 841.89;
    private const double LarguraPagina = 595.28;
    private const double Margem = 42.52;

    /// <summary>O rodapé da prescrição tem 116 pontos; o dos documentos clínicos, 104.</summary>
    private const double RodapePrescricao = 116;
    private const double RodapeDocumento = 104;

    [Fact]
    public void O_carimbo_da_prescricao_cabe_dentro_do_rodape()
        => DeveCaberNoRodape(PrescricaoInternaPdfService.AreaDaAssinatura(1), RodapePrescricao);

    [Fact]
    public void O_carimbo_do_documento_clinico_cabe_dentro_do_rodape()
        => DeveCaberNoRodape(DocumentosClinicosPdfService.AreaDaAssinatura(1), RodapeDocumento);

    /// <summary>
    /// O carimbo da 2ª assinatura fica AO LADO do primeiro, na mesma faixa, e os dois
    /// cabem na largura útil da folha — se um passasse da margem direita, ele sairia
    /// cortado no papel.
    /// </summary>
    [Fact]
    public void Os_dois_carimbos_ficam_lado_a_lado_e_cabem_na_folha()
    {
        var primeiro = PrescricaoInternaPdfService.AreaDaAssinatura(1);
        var segundo = PrescricaoInternaPdfService.AreaDaSegundaAssinatura(1);

        DeveCaberNoRodape(segundo, RodapePrescricao);

        segundo.Y.Should().Be(primeiro.Y, "os dois se leem lado a lado, na mesma linha");
        segundo.X.Should().BeGreaterThanOrEqualTo(primeiro.X + primeiro.Largura,
            "o segundo não pode cobrir o primeiro");
        (segundo.X + segundo.Largura).Should().BeLessThanOrEqualTo(LarguraPagina - Margem,
            "o carimbo que passa da margem direita sai cortado no papel");
    }

    /// <summary>
    /// A página pedida é sempre a ÚLTIMA — é onde está o fim do documento, e é a folha que
    /// alguém confere.
    /// </summary>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 2)]
    public void O_carimbo_vai_para_a_ultima_pagina(int total, int esperada)
        => PrescricaoInternaPdfService.AreaDaAssinatura(total).Pagina.Should().Be(esperada);

    /// <summary>
    /// A prova de que o teste pega o defeito real: a fórmula ANTIGA
    /// (<c>AlturaPagina - Margem - AlturaRodape + 2</c>) põe o carimbo no alto da página,
    /// e é exatamente isso que a asserção acima recusa.
    /// </summary>
    [Fact]
    public void A_formula_antiga_cairia_no_alto_da_pagina()
    {
        var yAntigo = AlturaPagina - Margem - RodapePrescricao + 2;

        yAntigo.Should().BeGreaterThan(AlturaPagina / 2,
            "medido do pé da página, o valor antigo fica na METADE DE CIMA — foi o que a "
            + "clínica viu por cima do título");

        var acao = () => DeveCaberNoRodape(
            PrescricaoInternaPdfService.AreaDaAssinatura(1) with { Y = yAntigo },
            RodapePrescricao);

        acao.Should().Throw<Exception>("senão este arquivo inteiro não provaria nada");
    }

    /// <summary>
    /// O invariante: o retângulo inteiro cai dentro da faixa que o rodapé ocupa, contada
    /// do PÉ da página — de <c>Margem</c> até <c>Margem + AlturaRodape</c>.
    /// </summary>
    private static void DeveCaberNoRodape(AreaAssinatura area, double alturaRodape)
    {
        area.Y.Should().BeGreaterThanOrEqualTo(Margem,
            "abaixo da margem o carimbo sai fora da área imprimível");

        (area.Y + area.Altura).Should().BeLessThanOrEqualTo(Margem + alturaRodape,
            "acima do rodapé o carimbo invade o conteúdo — foi o defeito de 20/08/2026");
    }
}
