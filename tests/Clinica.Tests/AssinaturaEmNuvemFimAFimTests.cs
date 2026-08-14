using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Clinica.Application.Assinatura;
using Clinica.Application.Assinatura.SafeID;
using FluentAssertions;
using PdfSharp.Pdf.Signatures;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A assinatura em NUVEM de ponta a ponta — o caminho que só existia em pedaços.
///
/// Por que este arquivo precisou existir
/// -------------------------------------
/// Havia teste para a montagem da requisição ao PSC, para a leitura da resposta, para o
/// recorte do PKCS#7 e para a conferência com certificado LOCAL. Não havia um só que
/// fizesse o circuito inteiro <b>com o assinador de nuvem</b>: assinar um PDF de verdade
/// pedindo a assinatura a um "PSC" e depois CONFERIR o arquivo produzido.
///
/// Foi nesse vão que morava o pior defeito da integração. O <c>RangedStream</c> que o
/// PDFsharp entrega ao assinador chega <b>sem posição</b> — <c>Position</c> nem getter
/// utilizável tem (lança <c>NullReferenceException</c>) — e ler antes de posicionar devolve
/// <b>zero bytes</b>, calado. O que subia para o PSC era o SHA-256 de NADA
/// (<c>e3b0c442…b7852b855</c>), a mesma constante em toda folha da clínica. O PSC assinava
/// esse hash corretamente e devolvia um PKCS#7 impecável: build verde, teste verde, e um
/// documento cuja assinatura não cobre coisa alguma.
///
/// É a regra do projeto aplicada ao lugar onde ela mais custa: <b>garantia aparente é pior
/// que ausência de garantia</b>.
/// </summary>
public class AssinaturaEmNuvemFimAFimTests
{
    /// <summary>
    /// Faz o papel do PSC: assina de verdade o conteúdo coberto e devolve um CMS destacado,
    /// como o SafeID faz com <c>signature_format: CMS</c>.
    /// </summary>
    private sealed class PscDeMentira(X509Certificate2 certificado, int reservado)
        : IDigitalSigner
    {
        public string CertificateName => "PSC de mentira";
        public Task<int> GetSignatureSizeAsync() => Task.FromResult(reservado);

        public async Task<byte[]> GetSignatureAsync(Stream conteudoCoberto)
        {
            // O MESMO cuidado do AssinadorSafeID: sem posicionar, o stream lê vazio.
            conteudoCoberto.Position = 0;

            var cobertos = await LerTudoAsync(conteudoCoberto);

            var cms = new SignedCms(new ContentInfo(cobertos), detached: true);
            cms.ComputeSignature(new CmsSigner(certificado));
            return cms.Encode();
        }
    }

    /// <summary>
    /// O teste que faltava. Roda também com os 32 KB do SafeID, que é o tamanho reservado
    /// em produção — o enchimento grande é o que exercita o recorte do DER.
    /// </summary>
    [Theory]
    [InlineData(2048)]
    [InlineData(AssinadorSafeID.TamanhoReservado)]
    public async Task Documento_assinado_em_nuvem_CONFERE(int reservado)
    {
        using var cert = CertificadoComChave();
        var servico = new AssinaturaDigitalService(exigirCadeiaConfiavel: false);

        var assinado = await servico.AssinarAsync(
            PdfDeTeste(),
            CertificadoIcpBrasil.Ler(cert) with
            {
                AssinadorRemoto = new PscDeMentira(cert, reservado)
            },
            Pedido());

        var conferencia = servico.Conferir(assinado.Pdf);

        conferencia.Conferida.Should().BeTrue(conferencia.Frase);
        conferencia.Integra.Should().BeTrue(conferencia.Frase);
    }

    /// <summary>
    /// O defeito exato, fixado: um assinador que NÃO posiciona o stream recebe zero bytes.
    /// Se um dia o PDFsharp passar a entregar o stream já posicionado, este teste passa a
    /// falhar — e aí a linha `Position = 0` do AssinadorSafeID pode sair com segurança.
    /// Enquanto ele falhar, ela é obrigatória.
    /// </summary>
    [Fact]
    public async Task Ler_o_conteudo_coberto_SEM_posicionar_devolve_vazio()
    {
        using var cert = CertificadoComChave();
        var espiao = new EspiaoDeStream(cert);

        await new AssinaturaDigitalService(exigirCadeiaConfiavel: false).AssinarAsync(
            PdfDeTeste(), CertificadoIcpBrasil.Ler(cert) with { AssinadorRemoto = espiao },
            Pedido());

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
