using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Clinica.Application.Assinatura;
using FluentAssertions;
using PdfSharp.Pdf.Signatures;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace Clinica.Tests;

/// <summary>Produz e confere PDFs com CMS em DER e BER, incluindo folga no Contents.
/// O assinador sintético cobre a leitura do RangedStream, que exige posicionamento
/// explícito para não calcular o hash de conteúdo vazio.</summary>
public class AssinaturaCmsFimAFimTests
{
    private sealed class AssinadorCmsDeTeste(
        X509Certificate2 certificado, int reservado, bool comprimentoIndefinido = false)
        : IDigitalSigner
    {
        public string CertificateName => "CMS de teste";
        public Task<int> GetSignatureSizeAsync() => Task.FromResult(reservado);

        public async Task<byte[]> GetSignatureAsync(Stream conteudoCoberto)
        {
            conteudoCoberto.Position = 0;

            var cobertos = await LerTudoAsync(conteudoCoberto);

            var cms = new SignedCms(new ContentInfo(cobertos), detached: true);
            cms.ComputeSignature(new CmsSigner(certificado));

            return comprimentoIndefinido ? EmBerIndefinido(cms.Encode()) : cms.Encode();
        }
    }

    [Theory]
    [InlineData(2048, false)]
    [InlineData(32768, false)]
    [InlineData(32768, true)]
    public async Task Documento_assinado_com_CMS_CONFERE(int reservado, bool indefinido)
    {
        using var cert = CertificadoComChave();
        var servico = new AssinaturaDigitalService(exigirCadeiaConfiavel: false);

        var assinado = await servico.AssinarAsync(
            PdfDeTeste(),
            CertificadoIcpBrasil.Ler(cert), Pedido(), new AssinadorCmsDeTeste(cert, reservado, indefinido));

        var conferencia = servico.Conferir(assinado.Pdf);

        conferencia.Conferida.Should().BeTrue(conferencia.Frase);
        conferencia.Integra.Should().BeTrue(conferencia.Frase);
    }

    [Fact]
    public async Task Ler_o_conteudo_coberto_SEM_posicionar_devolve_vazio()
    {
        using var cert = CertificadoComChave();
        var espiao = new EspiaoDeStream(cert);

        await new AssinaturaDigitalService(exigirCadeiaConfiavel: false).AssinarAsync(
            PdfDeTeste(), CertificadoIcpBrasil.Ler(cert), Pedido(), espiao);

        espiao.HashSemPosicionar.Should().Equal(
            SHA256.HashData([]),
            "é o SHA-256 da cadeia vazia — o stream não lê nada antes de Position = 0");

        espiao.BytesAposPosicionar.Should().BeGreaterThan(0);
    }

    private sealed class EspiaoDeStream(X509Certificate2 certificado) : IDigitalSigner
    {
        public string CertificateName => "espião";
        public Task<int> GetSignatureSizeAsync() => Task.FromResult(4096);

        public byte[] HashSemPosicionar { get; private set; } = [];
        public long BytesAposPosicionar { get; private set; }

        public async Task<byte[]> GetSignatureAsync(Stream conteudoCoberto)
        {
            HashSemPosicionar = await SHA256.HashDataAsync(conteudoCoberto);

            conteudoCoberto.Position = 0;
            var cobertos = await LerTudoAsync(conteudoCoberto);
            BytesAposPosicionar = cobertos.Length;

            var cms = new SignedCms(new ContentInfo(cobertos), detached: true);
            cms.ComputeSignature(new CmsSigner(certificado));
            return cms.Encode();
        }
    }

    // ---- Apoio ----

    /// <summary>
    /// Reescreve o SEQUENCE de fora na forma INDEFINIDA, permitida no CMS em BER. O miolo
    /// não muda — muda o cabeçalho e o <c>00 00</c> que marca o fim.
    /// </summary>
    private static byte[] EmBerIndefinido(byte[] der)
    {
        AsnDecoder.ReadEncodedValue(
            der, AsnEncodingRules.DER, out var inicio, out var tamanho, out _);

        return [0x30, 0x80, .. der.AsSpan(inicio, tamanho).ToArray(), 0x00, 0x00];
    }

    /// <summary>
    /// Laço à mão: <c>CopyTo</c> consulta <c>CanSeek</c>, e no RangedStream do PDFsharp essa
    /// propriedade LANÇA (<c>NotImplementedException</c>) em vez de devolver false.
    /// </summary>
    private static async Task<byte[]> LerTudoAsync(Stream fonte)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[8192];
        int lidos;
        while ((lidos = await fonte.ReadAsync(buffer)) > 0)
            memoria.Write(buffer, 0, lidos);
        return memoria.ToArray();
    }

    private static X509Certificate2 CertificadoComChave()
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            "CN=Dra. Ana Souza, OU=Teste, C=BR",
            rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return pedido.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
    }

    private static PedidoAssinatura Pedido() => new(
        Motivo: "Receita 2026/0010",
        NomeExibido: "Dra. Ana Souza",
        RegistroConselho: "CRM-SP 123456",
        Area: new AreaAssinatura(Pagina: 0, X: 40, Y: 640, Largura: 240, Altura: 46));

    private static byte[] PdfDeTeste()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(d => d.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Text("Dipirona 500mg — 1 comprimido de 8 em 8 horas por 3 dias");
        })).GeneratePdf();
    }
}
