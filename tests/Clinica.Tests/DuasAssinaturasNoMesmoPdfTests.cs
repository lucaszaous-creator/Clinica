using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Clinica.Application.Assinatura;
using FluentAssertions;
using PdfSharp.Pdf.Signatures;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// DUAS assinaturas no MESMO PDF — a prescritora e a enfermagem (parcela 68).
///
/// A premissa antiga ("duas assinaturas no mesmo PDF não existem") foi MEDIDA antes de ser
/// derrubada, e ela estava certa pela metade: o PDFsharp de fato reescreve o arquivo ao
/// salvar, então assinar por cima do assinado quebra a primeira. O que estava errado era a
/// conclusão — a limitação é da biblioteca, não do formato. O PDF prevê exatamente este
/// caso, e o mecanismo é a atualização incremental.
///
/// ⚠️ <b>Estes testes existem porque cada tentativa de assinatura no SafeID é COBRADA.</b>
/// A conferência tem de fechar aqui, de graça, antes de a clínica gastar uma. E o teste
/// central não é "o nosso Conferir aprovou": é que o arquivo continue íntegro para quem
/// não conhece o nosso código — a lição da 7ª rodada da parcela 67, em que a nossa
/// conferência aprovava um BER que o Adobe podia recusar.
/// </summary>
public class DuasAssinaturasNoMesmoPdfTests
{
    /// <summary>
    /// O invariante que torna tudo o resto possível: a revisão da enfermagem é ANEXADA, e
    /// os bytes que a médica assinou não mudam nem um.
    /// </summary>
    [Fact]
    public async Task A_segunda_assinatura_nao_toca_nos_bytes_da_primeira()
    {
        var (uma, duas) = await AssinarDuasVezesAsync();

        duas.Length.Should().BeGreaterThan(uma.Length);
        duas.AsSpan(0, uma.Length).SequenceEqual(uma)
            .Should().BeTrue("a assinatura da médica cobre os bytes 0..N do arquivo dela — "
                + "mudar um byte ali a invalida, e foi por isso que salvar por cima nunca "
                + "funcionou");
    }

    /// <summary>
    /// As DUAS fecham. É a asserção que o produto inteiro depende: a folha prova quem
    /// mandou e quem executou.
    /// </summary>
    [Fact]
    public async Task As_duas_assinaturas_conferem()
    {
        var servico = new AssinaturaDigitalService(exigirCadeiaConfiavel: false);
        var (_, duas) = await AssinarDuasVezesAsync();

        var conferencias = servico.ConferirTodas(duas);

        conferencias.Should().HaveCount(2);
        conferencias.Should().OnlyContain(c => c.Conferida && c.Integra);

        conferencias[0].Titular.Should().Contain("Ana Souza");
        conferencias[1].Titular.Should().Contain("Rita Lima");

        // E o veredito único do documento também é "íntegro".
        servico.Conferir(duas).Integra.Should().BeTrue();

        Despejar("duas-assinaturas.pdf", duas);
    }

    /// <summary>
    /// ⚠️ O teste que impede o pior desfecho: um documento cuja SEGUNDA assinatura não
    /// fecha não pode ser exibido como íntegro só porque a primeira fecha. Antes desta
    /// parcela o <c>Conferir</c> lia o primeiro <c>/ByteRange</c> e parava ali.
    /// </summary>
    [Fact]
    public async Task Documento_vale_pela_PIOR_das_assinaturas()
    {
        var servico = new AssinaturaDigitalService(exigirCadeiaConfiavel: false);
        var (uma, duas) = await AssinarDuasVezesAsync();

        // Adultera um byte do texto que só a SEGUNDA assinatura cobre (depois do fim da
        // revisão da médica). A dela continua fechando; a da enfermeira, não.
        var adulterado = (byte[])duas.Clone();
        var alvo = Encoding.Latin1.GetString(adulterado).IndexOf(
            "Rita Lima", uma.Length, StringComparison.Ordinal);
        alvo.Should().BeGreaterThan(0, "o carimbo da enfermeira está na revisão nova");
        adulterado[alvo] = (byte)'X';

        var conferencias = servico.ConferirTodas(adulterado);

        conferencias[0].Integra.Should().BeTrue("os bytes da médica não foram tocados");
        conferencias[1].Integra.Should().BeFalse("a revisão da enfermeira mudou");

        servico.Conferir(adulterado).Integra
            .Should().BeFalse("o documento vale pela pior das assinaturas");
    }

    /// <summary>
    /// O arquivo tem DUAS revisões e DUAS entradas de assinatura — é o que faz qualquer
    /// leitor de PDF listar os dois signatários no painel de assinaturas.
    /// </summary>
    [Fact]
    public async Task O_arquivo_tem_duas_revisoes_encadeadas()
    {
        var (_, duas) = await AssinarDuasVezesAsync();
        var texto = Encoding.Latin1.GetString(duas);

        Regex.Matches(texto, "/ByteRange").Should().HaveCount(2);
        Regex.Matches(texto, "startxref").Should().HaveCount(2);
        texto.Should().Contain("/Prev ", "sem /Prev o leitor não enxerga a revisão anterior");
        Regex.Matches(texto, @"/Type/Sig\b").Should().HaveCount(2);
    }

    /// <summary>
    /// Assinar um PDF que não tem a PRIMEIRA assinatura recusa, e a frase diz o quê. É a
    /// regra da casa: um meio-parser que "dá um jeito" produziria um arquivo que a nossa
    /// conferência aprova e o Adobe recusa.
    /// </summary>
    [Fact]
    public async Task Recusa_anexar_assinatura_a_PDF_nao_assinado()
    {
        var enfermeira = Certificado("Enf. Rita Lima");

        var acao = async () => await RevisaoIncrementalPdf.AnexarAssinaturaAsync(
            PdfDeTeste(), Pedido("enfermeira", 580), enfermeira,
            Assinador(enfermeira), "Signature2");

        (await acao.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*AcroForm*");
    }

    /// <summary>
    /// A folha de infusão real tem MAIS DE UMA PÁGINA, e as duas assinaturas caem na
    /// página que o pedido escolhe — inclusive quando não é a primeira. Um percurso da
    /// árvore de páginas que só acerta o caso de uma página passaria neste projeto sem
    /// ninguém notar até a folha comprida chegar à sala.
    /// </summary>
    [Fact]
    public async Task Funciona_em_documento_de_varias_paginas()
    {
        var servico = new AssinaturaDigitalService(exigirCadeiaConfiavel: false);
        var medica = Certificado("Dra. Ana Souza");
        var enfermeira = Certificado("Enf. Rita Lima");

        var uma = await servico.AssinarAsync(
            PdfDeTeste(paginas: 3), medica,
            Pedido("médica", 640) with { Area = new AreaAssinatura(2, 40, 640, 240, 46) });

        var duas = await RevisaoIncrementalPdf.AnexarAssinaturaAsync(
            uma.Pdf,
            Pedido("enfermeira", 580) with { Area = new AreaAssinatura(2, 40, 580, 240, 46) },
            enfermeira, Assinador(enfermeira), "Signature2");

        duas.AsSpan(0, uma.Pdf.Length).SequenceEqual(uma.Pdf).Should().BeTrue();

        var conferencias = servico.ConferirTodas(duas);
        conferencias.Should().HaveCount(2);
        conferencias.Should().OnlyContain(c => c.Conferida && c.Integra);

        Despejar("varias-paginas.pdf", duas);
    }

    // ---- Apoio ----

    /// <summary>
    /// Grava o arquivo quando <c>CLINICA_DUMP_PDF</c> aponta uma pasta, para conferir com
    /// um validador de FORA (o pyhanko, o Adobe, o do ITI).
    ///
    /// Não é conforto: cada tentativa de assinatura no SafeID é COBRADA, e a lição da 7ª
    /// rodada da parcela 67 é que a nossa conferência pode aprovar o que o mundo lá fora
    /// recusa. Conferir de graça, antes, é o que evita a próxima rodada paga.
    /// </summary>
    private static void Despejar(string nome, byte[] pdf)
    {
        var pasta = Environment.GetEnvironmentVariable("CLINICA_DUMP_PDF");
        if (!string.IsNullOrWhiteSpace(pasta) && Directory.Exists(pasta))
            File.WriteAllBytes(Path.Combine(pasta, nome), pdf);
    }

    private static async Task<(byte[] Uma, byte[] Duas)> AssinarDuasVezesAsync()
    {
        var servico = new AssinaturaDigitalService(exigirCadeiaConfiavel: false);
        var medica = Certificado("Dra. Ana Souza");
        var enfermeira = Certificado("Enf. Rita Lima");

        var uma = await servico.AssinarAsync(PdfDeTeste(), medica, Pedido("médica", 640));

        var duas = await RevisaoIncrementalPdf.AnexarAssinaturaAsync(
            uma.Pdf, Pedido("enfermeira", 580), enfermeira,
            Assinador(enfermeira), "Signature2");

        return (uma.Pdf, duas);
    }

    private static IDigitalSigner Assinador(CertificadoAssinatura certificado)
        => new PdfSharpDefaultSigner(
            certificado.Certificado, PdfMessageDigestType.SHA256, null);

    private static PedidoAssinatura Pedido(string quem, double y) => new(
        Motivo: $"Assinatura da {quem}",
        NomeExibido: quem,
        RegistroConselho: "CRM/COREN 000",
        Area: new AreaAssinatura(0, 40, y, 240, 46));

    private static byte[] PdfDeTeste(int paginas = 1)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(c =>
        {
            for (var i = 1; i <= paginas; i++)
            {
                var numero = i;
                c.Page(p =>
                {
                    p.Size(PageSizes.A4);
                    p.Margin(2, Unit.Centimetre);
                    p.Content().Text($"PRESCRIÇÃO DE EXECUÇÃO INTERNA — folha {numero}");
                });
            }
        }).GeneratePdf();
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
