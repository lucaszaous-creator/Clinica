using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Clinica.Application.Assinatura;
using FluentAssertions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Signatures;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O carimbo da assinatura APARECE — na tela e na impressora (parcela 68, 9ª rodada).
///
/// A clínica assinou as duas vias e reclamou que "as duas assinaturas digitais não saíram
/// na folha". Reproduzido: eram DOIS defeitos somados, e cada um sozinho já apagava o
/// bloco do prescritor.
///
/// 1. <b>A aparência do widget era recortada.</b> O PDFsharp entrega ao desenhador o
///    retângulo NA PÁGINA, e o <c>XGraphics</c> desenha dentro de um form XObject de BBox
///    <c>[0 0 largura altura]</c> — coordenada local. Desenhar em <c>area.X</c>/<c>area.Y</c>
///    punha o traço inteiro fora da BBox, e o form recorta pela BBox. Nada era desenhado,
///    sem uma linha de erro.
/// 2. <b>Anotação sem o bit Print não é impressa.</b> O PDFsharp não escreve o <c>/F</c> do
///    widget, e o padrão é 0 — então, mesmo com a coordenada certa, o carimbo apareceria no
///    leitor e sumiria na folha que a enfermagem leva para a sala.
///
/// A saída foi tirar o bloco visível da ANOTAÇÃO e pô-lo no CONTEÚDO DA PÁGINA, que não
/// tem nenhuma das duas armadilhas — e ainda é coberto pela assinatura, porque entra antes
/// dela. Só a segunda assinatura continua sendo anotação, porque a página já está assinada
/// e não se toca; lá o <c>/F 4</c> é escrito à mão.
///
/// ⚠️ Estes testes olham o CONTEÚDO gerado, e não o desenho, pela mesma razão do
/// <see cref="CarimboNoRodapeTests"/>: comparar imagem ninguém mantém. E cada um deles
/// falha no código de antes — teste de carimbo visível que não reprova o carimbo invisível
/// não prova nada.
/// </summary>
public class CarimboVisivelTests
{
    private const string Frase = "ASSINADO DIGITALMENTE";

    /// <summary>
    /// O bloco entra no fluxo de conteúdo da página — é o que faz o leitor mostrar e a
    /// impressora imprimir, sem depender de flag de anotação nenhuma.
    /// </summary>
    [Fact]
    public async Task O_carimbo_do_prescritor_entra_no_conteudo_da_pagina()
    {
        var assinado = await AssinarAsync();

        ConteudoDaPagina(assinado, 0).Should().Contain(Frase,
            "o carimbo desenhado como aparência de anotação era recortado pela BBox e "
            + "nunca chegou à folha");
        ConteudoDaPagina(assinado, 0).Should().Contain("Dra. Ana Souza",
            "o emissor do certificado é o que só a assinatura digital oferece");
    }

    /// <summary>
    /// O campo da assinatura fica INVISÍVEL (retângulo de área zero). Se ele voltasse a ter
    /// o mesmo retângulo do bloco, o leitor desenharia o carimbo duas vezes sobre si mesmo
    /// — o texto sai mais grosso e a moldura, mais escura.
    /// </summary>
    [Fact]
    public async Task O_campo_da_primeira_assinatura_nao_desenha_nada()
    {
        var assinado = await AssinarAsync();

        using var entrada = new MemoryStream(assinado, writable: false);
        var doc = PdfReader.Open(entrada, PdfDocumentOpenMode.Modify);

        var retangulo = Campos(doc)
            .Single(d => d.Elements.GetName("/FT") == "/Sig")
            .Elements.GetRectangle("/Rect");

        (retangulo.Width * retangulo.Height).Should().Be(0);
    }

    /// <summary>
    /// A SEGUNDA assinatura não pode entrar no conteúdo — a página já está assinada. Ela é
    /// anotação, e por isso precisa do bit <b>Print</b> escrito à mão: sem ele o carimbo da
    /// enfermagem aparece na tela e some no papel.
    /// </summary>
    [Fact]
    public async Task O_carimbo_da_enfermagem_traz_o_bit_de_imprimir()
    {
        const int bitImprimir = 4;
        var duas = await AssinarDuasVezesAsync();

        using var entrada = new MemoryStream(duas, writable: false);
        var doc = PdfReader.Open(entrada, PdfDocumentOpenMode.Modify);

        var anotacoes = Campos(doc);

        anotacoes.Should().HaveCount(2, "o campo do prescritor continua lá, só que invisível");

        var daEnfermagem = anotacoes.Single(d => d.Elements.GetString("/T") == "Signature2");
        (daEnfermagem.Elements.GetInteger("/F") & bitImprimir).Should().Be(bitImprimir,
            "anotação sem o bit Print é desenhada na tela e NÃO é impressa");
    }

    /// <summary>
    /// O documento com as duas assinaturas mostra os DOIS nomes — um no conteúdo (a
    /// médica) e outro na anotação da revisão nova (a enfermeira). É a folha que prova quem
    /// mandou e quem executou, e foi exatamente ela que chegou à clínica com um nome só.
    /// </summary>
    [Fact]
    public async Task A_folha_das_duas_vias_mostra_os_dois_nomes()
    {
        var duas = await AssinarDuasVezesAsync();

        ConteudoDaPagina(duas, 0).Should().Contain("Dra. Ana Souza");

        // A revisão anexada não passa pelo leitor do PDFsharp como conteúdo de página: ela
        // é uma aparência de anotação, escrita sem compressão.
        Encoding.Latin1.GetString(duas).Should().Contain("Enf. Rita Lima");
    }

    /// <summary>
    /// ⚠️ A régua da 2ª assinatura tem de bater com a de FORA. A aparência dela é escrita à
    /// mão em Helvetica, e o resolvedor de fontes do projeto só conhece a Segoe WP — que é
    /// ~9% mais estreita —, então medir com o PDFsharp deixaria o texto estourar justamente
    /// no caso apertado. A tabela é das métricas base-14; este número (215,319 pontos) foi
    /// conferido contra o que o poppler mede no arquivo GERADO. Um dígito errado na tabela
    /// só apareceria como texto por cima do carimbo vizinho, na clínica.
    /// </summary>
    [Fact]
    public void A_regua_da_helvetica_bate_com_a_medicao_de_fora()
        => RevisaoIncrementalPdf.LarguraNaHelvetica(
                "20/08/2026 16:23 · emissor: Autoridade Certificadora do SERPRO Final v5", 6.5)
            .Should().BeApproximately(215.319, 0.01);

    /// <summary>
    /// O carimbo do prescritor CORTA o que não cabe.
    ///
    /// <c>DrawString</c> com <c>TopLeft</c> não quebra linha nem corta: o que sobra sai por
    /// cima do vizinho. Enquanto o bloco era invisível isso não aparecia. Medido no pior
    /// caso realista, sobravam 8 pontos na RECEITA — o papel que vai à farmácia, com o QR
    /// logo ao lado —, e o nome do profissional e o emissor do certificado não têm tamanho
    /// máximo nenhum.
    /// </summary>
    [Fact]
    public async Task Texto_que_nao_cabe_no_carimbo_sai_cortado()
    {
        const string nomeEnorme = "Dra. Maria Aparecida Gonçalves de Oliveira Santos "
            + "Albuquerque Vasconcelos do Nascimento Figueiredo";

        var servico = new AssinaturaDigitalService(exigirCadeiaConfiavel: false);
        var assinado = (await servico.AssinarAsync(
            PdfDeTeste(), Certificado("Dra. Ana Souza"),
            Pedido("médica", 640) with { NomeExibido = nomeEnorme })).Pdf;

        var conteudo = ConteudoDaPagina(assinado, 0);

        conteudo.Should().NotContain(nomeEnorme,
            "sem corte o nome sairia por cima do que estiver ao lado do carimbo");
        conteudo.Should().Contain("Dra. Maria Aparecida",
            "o começo continua legível — cortar não é apagar");
        conteudo.Should().Contain("\u0085",
            "as reticências (0x85 em WinAnsi) são o que diz ao leitor que há mais texto");
    }

    /// <summary>
    /// O carimbo da enfermagem corta pela MESMA regra — e ali o vizinho fica a 10 pontos.
    /// </summary>
    [Fact]
    public async Task O_carimbo_da_enfermagem_tambem_corta()
    {
        const string emissorEnorme =
            "Autoridade Certificadora do Sistema de Justica Federal de Sao Paulo v10";

        var enfermeira = Certificado(emissorEnorme);
        var duas = await RevisaoIncrementalPdf.AnexarAssinaturaAsync(
            await AssinarAsync(), Pedido("enfermeira", 580), enfermeira,
            new PdfSharpDefaultSigner(enfermeira.Certificado, PdfMessageDigestType.SHA256, null),
            "Signature2");

        var texto = Encoding.Latin1.GetString(duas);

        texto.Should().NotContain($"emissor: {emissorEnorme}");
        texto.Should().Contain("\u0085", "o corte marca com reticências");
    }

    // ---- Apoio ----

    /// <summary>Os dicionários das anotações da primeira página.</summary>
    private static List<PdfDictionary> Campos(PdfDocument doc)
    {
        var anotacoes = doc.Pages[0].Annotations;
        return Enumerable.Range(0, anotacoes.Count)
            .Select(i => (PdfDictionary)anotacoes[i])
            .ToList();
    }

    /// <summary>
    /// O texto do fluxo de conteúdo da página, já descomprimido — é onde o carimbo TEM de
    /// estar.
    /// </summary>
    private static string ConteudoDaPagina(byte[] pdf, int pagina)
    {
        using var entrada = new MemoryStream(pdf, writable: false);
        var doc = PdfReader.Open(entrada, PdfDocumentOpenMode.Modify);

        var texto = new StringBuilder();
        foreach (var conteudo in doc.Pages[pagina].Contents)
            texto.Append(Encoding.Latin1.GetString(conteudo.Stream.UnfilteredValue));

        return texto.ToString();
    }

    private static async Task<byte[]> AssinarAsync()
    {
        var servico = new AssinaturaDigitalService(exigirCadeiaConfiavel: false);
        var resultado = await servico.AssinarAsync(
            PdfDeTeste(), Certificado("Dra. Ana Souza"), Pedido("médica", 640));
        return resultado.Pdf;
    }

    private static async Task<byte[]> AssinarDuasVezesAsync()
    {
        var enfermeira = Certificado("Enf. Rita Lima");
        return await RevisaoIncrementalPdf.AnexarAssinaturaAsync(
            await AssinarAsync(), Pedido("enfermeira", 580), enfermeira,
            new PdfSharpDefaultSigner(enfermeira.Certificado, PdfMessageDigestType.SHA256, null),
            "Signature2");
    }

    private static PedidoAssinatura Pedido(string quem, double y) => new(
        Motivo: $"Assinatura da {quem}",
        NomeExibido: quem,
        RegistroConselho: "CRM/COREN 000",
        Area: new AreaAssinatura(0, 40, y, 240, 46));

    private static byte[] PdfDeTeste()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(c => c.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Text("PRESCRIÇÃO DE EXECUÇÃO INTERNA");
        })).GeneratePdf();
    }

    private static CertificadoAssinatura Certificado(string nome)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            $"CN={nome}, OU=Teste, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = pedido.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
        var comChave = new X509Certificate2(
            cert.Export(X509ContentType.Pfx, "teste"), "teste", X509KeyStorageFlags.Exportable);
        return CertificadoIcpBrasil.Ler(comChave);
    }
}
