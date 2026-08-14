using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Clinica.Application.Assinatura;
using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A assinatura ICP-Brasil da parcela 42.
///
/// Estes testes existem porque a promessa aqui é grande e barata de falsificar: é fácil
/// escrever um serviço que "assina" e produz um PDF com um bloco bonito escrito
/// "assinado digitalmente" que nenhum leitor valida. O que separa um do outro é
/// exatamente o que está coberto aqui — a assinatura CONFERE, e a alteração de um único
/// bit é PEGA.
///
/// O certificado é autoassinado e construído na memória (com a extensão do e-CPF montada
/// à mão), pela mesma razão de os testes de regra usarem repositório fake: o que se está
/// testando é a mecânica, não a ICP-Brasil. Por isso o serviço nasce aqui com
/// <c>exigirCadeiaConfiavel: false</c> — em produção ele é true, e é o que impede a
/// clínica de assinar com certificado que abriria como inválido.
/// </summary>
public class AssinaturaDigitalTests
{
    private static AssinaturaDigitalService Servico() => new(exigirCadeiaConfiavel: false);

    [Fact]
    public async Task Assinatura_confere_no_arquivo_intacto()
    {
        var pdf = PdfDeTeste("Dipirona 1g + SF 0,9% 100 mL — EV — 30 min");
        var certificado = CertificadoDeTeste("Dra. Ana Souza", "12345678909");

        var resultado = await Servico().AssinarAsync(pdf, certificado, Pedido());
        var conferencia = Servico().Conferir(resultado.Pdf);

        Assert.True(conferencia.Conferida);
        Assert.True(conferencia.Integra);
        Assert.Contains("Ana Souza", conferencia.Titular);
    }

    /// <summary>
    /// O teste que dá sentido a todos os outros: um bit trocado no miolo do arquivo
    /// derruba a assinatura. Sem isto, "assinado" seria decoração.
    /// </summary>
    [Fact]
    public async Task Um_bit_trocado_derruba_a_assinatura()
    {
        var pdf = PdfDeTeste("Dipirona 1g");
        var certificado = CertificadoDeTeste("Dra. Ana Souza", "12345678909");
        var assinado = (await Servico().AssinarAsync(pdf, certificado, Pedido())).Pdf;

        var adulterado = (byte[])assinado.Clone();
        adulterado[assinado.Length / 4] ^= 0xFF;

        var conferencia = Servico().Conferir(adulterado);

        // Conferida = true e Integra = false: o sistema OLHOU e concluiu que mudou. É
        // diferente de não ter conseguido olhar, e a tela escreve coisas diferentes.
        Assert.True(conferencia.Conferida);
        Assert.False(conferencia.Integra);
        Assert.Contains("ALTERADO", conferencia.Frase);
    }

    // ---- O recorte do PKCS#7 dentro do /Contents ----
    //
    // O espaço é reservado ANTES de assinar e completado com zeros à direita. A primeira
    // versão cortava com TrimEnd('0') — caracteres '0', não bytes zero —, e por isso comia
    // os zeros FINAIS da própria assinatura. Um CMS terminado em 0x00 voltava um byte mais
    // curto e o SignedCms.Decode respondia "ASN1 corrupted data", indistinguível de arquivo
    // adulterado. O último byte de um CMS é o último byte da assinatura RSA, ou seja é
    // sorteado: dava uma folha a cada 256 — raro para cair em teste, frequente para
    // acontecer na clínica (14/08/2026, na primeira assinatura em nuvem).

    /// <summary>
    /// O caso que quebrou. <c>30 03 02 01 00</c> é um DER legítimo (SEQUENCE { INTEGER 0 })
    /// que termina em <c>0x00</c> — exatamente a forma que o corte por zeros destruía.
    /// </summary>
    [Fact]
    public void O_recorte_preserva_os_zeros_do_FIM_da_assinatura()
    {
        byte[] der = [0x30, 0x03, 0x02, 0x01, 0x00];
        var comFolga = der.Concat(new byte[500]).ToArray();

        AssinaturaDigitalService.RecortarAsn1(comFolga).Should().Equal(der);
    }

    [Fact]
    public void O_recorte_usa_o_comprimento_do_DER_e_nao_o_enchimento()
    {
        // Forma longa (0x82 = dois bytes de comprimento): 4 de conteúdo, e o conteúdo TEM
        // zeros no meio e no fim. Cortar por enchimento devolveria três bytes a menos.
        byte[] der = [0x30, 0x82, 0x00, 0x04, 0xAB, 0x00, 0xCD, 0x00];
        var comFolga = der.Concat(new byte[2000]).ToArray();

        AssinaturaDigitalService.RecortarAsn1(comFolga).Should().Equal(der);
    }

    [Theory]
    [InlineData(new byte[] { 0x30 })]                    // curto demais para ter comprimento
    [InlineData(new byte[] { 0x30, 0x0A, 0x01 })]        // diz 10 bytes e só tem 1
    [InlineData(new byte[] { 0x30, 0x80, 0x01 })]        // indefinida e SEM o 00 00 do fim
    public void Estrutura_ilegivel_devolve_nulo_em_vez_de_bytes_pela_metade(byte[] entrada)
    {
        AssinaturaDigitalService.RecortarAsn1(entrada).Should().BeNull();
    }

    /// <summary>
    /// ⚠️ O caso que a clínica levou em 14/08/2026: o SafeID devolve o CMS em <b>BER com
    /// comprimento INDEFINIDO</b> (<c>30 80 … 00 00</c>) — legal em CMS (RFC 5652) e recusado
    /// pela primeira versão do recorte, que lia o cabeçalho à mão e tratava o <c>0x80</c>
    /// como erro. Os bytes estavam perfeitos; quem não sabia lê-los era o recorte.
    /// </summary>
    [Fact]
    public void CMS_em_BER_com_comprimento_INDEFINIDO_e_aceito()
    {
        // SEQUENCE indefinida { INTEGER 0 } — o mesmo formato do cabeçalho que veio do PSC.
        byte[] indefinido = [0x30, 0x80, 0x02, 0x01, 0x00, 0x00, 0x00];
        var comFolga = indefinido.Concat(new byte[500]).ToArray();

        AssinaturaDigitalService.RecortarAsn1(comFolga).Should().Equal(indefinido);
    }

    /// <summary>
    /// A prova de que o recorte serve para o que ele existe: um CMS destacado de verdade,
    /// com o enchimento do <c>/Contents</c> em volta, volta decodificável.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]   // BER indefinido — o que o SafeID devolve
    public void Um_CMS_de_verdade_com_enchimento_volta_inteiro(bool indefinido)
    {
        var conteudo = "os bytes cobertos pelo ByteRange"u8.ToArray();

        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            "CN=Dra. Ana Souza", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificado = pedido.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));

        var assinado = new SignedCms(new ContentInfo(conteudo), detached: true);
        assinado.ComputeSignature(new CmsSigner(certificado));
        var pkcs7 = indefinido ? ParaComprimentoIndefinido(assinado.Encode()) : assinado.Encode();

        var recortado = AssinaturaDigitalService.RecortarAsn1(
            pkcs7.Concat(new byte[32 * 1024 - pkcs7.Length]).ToArray());

        recortado.Should().Equal(pkcs7);

        var relido = new SignedCms(new ContentInfo(conteudo), detached: true);
        relido.Decode(recortado!);
        relido.CheckSignature(verifySignatureOnly: true);   // não lança = fechou
    }

    /// <summary>
    /// Reescreve o SEQUENCE de fora na forma INDEFINIDA (<c>30 80 … 00 00</c>), que é como o
    /// SafeID entrega. O miolo continua igual — muda só o cabeçalho e o marcador de fim.
    /// </summary>
    private static byte[] ParaComprimentoIndefinido(byte[] der)
    {
        AsnDecoder.ReadEncodedValue(
            der, AsnEncodingRules.DER, out var inicioConteudo, out var tamanho, out _);

        return [0x30, 0x80,
                .. der.AsSpan(inicioConteudo, tamanho).ToArray(),
                0x00, 0x00];
    }

    [Fact]
    public void Pdf_sem_assinatura_nao_e_conferido_e_nao_e_dado_por_integro()
    {
        var conferencia = Servico().Conferir(PdfDeTeste("folha em branco"));

        Assert.False(conferencia.Conferida);
        Assert.False(conferencia.Integra);
        Assert.Contains("não foi possível conferir", conferencia.Frase, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// O CPF sai de DENTRO do certificado. É o que permite recusar alguém assinar a
    /// prescrição com o token de outra pessoa — sem isso, "assinatura qualificada"
    /// provaria só que alguém com um token assinou.
    /// </summary>
    [Fact]
    public void Cpf_do_titular_sai_da_extensao_icp_brasil()
    {
        var certificado = CertificadoDeTeste("Dra. Ana Souza", "12345678909");

        Assert.Equal("12345678909", certificado.Cpf);
        Assert.True(certificado.EhECpf);
    }

    [Fact]
    public void Certificado_sem_extensao_icp_brasil_fica_sem_cpf()
    {
        var certificado = CertificadoDeTeste("Fulano", cpf: null);

        Assert.Null(certificado.Cpf);
        Assert.False(certificado.EhECpf);
    }

    [Fact]
    public async Task Certificado_vencido_e_recusado_antes_de_produzir_arquivo()
    {
        var vencido = CertificadoDeTeste(
            "Dra. Ana Souza", "12345678909",
            de: DateTimeOffset.Now.AddYears(-3), ate: DateTimeOffset.Now.AddDays(-1));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servico().AssinarAsync(PdfDeTeste("x"), vencido, Pedido()));

        Assert.Contains("validade", erro.Message);
    }

    [Fact]
    public async Task Sem_carimbadora_configurada_nao_se_inventa_carimbo_do_tempo()
    {
        var certificado = CertificadoDeTeste("Dra. Ana Souza", "12345678909");

        var resultado = await Servico().AssinarAsync(PdfDeTeste("x"), certificado, Pedido());

        // Nulo, e não DateTime.Now: sem ACT a data é declarada pelo relógio de quem
        // assinou, e gravá-la como carimbo do tempo seria inventar a prova.
        Assert.Null(resultado.CarimboTempoEm);
        Assert.Null(resultado.CarimboTempoAutoridade);
    }

    [Fact]
    public async Task Hash_gravado_e_o_do_arquivo_assinado()
    {
        var certificado = CertificadoDeTeste("Dra. Ana Souza", "12345678909");

        var resultado = await Servico().AssinarAsync(PdfDeTeste("x"), certificado, Pedido());

        Assert.Equal(AssinaturaDigitalService.Hash(resultado.Pdf), resultado.Hash);
        Assert.Equal(64, resultado.Hash.Length);   // SHA-256 em hexa
    }

    // ---- Apoio ----

    private static PedidoAssinatura Pedido() => new(
        Motivo: "Prescrição de execução interna",
        NomeExibido: "Dra. Ana Souza",
        RegistroConselho: "CRM-SP 123456",
        Area: new AreaAssinatura(Pagina: 0, X: 40, Y: 640, Largura: 240, Altura: 46));

    private static byte[] PdfDeTeste(string texto)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(c => c.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(2, Unit.Centimetre);
            p.Content().Column(col =>
            {
                col.Item().Text("PRESCRIÇÃO DE EXECUÇÃO INTERNA").FontSize(14).Bold();
                col.Item().Text(texto);
            });
        })).GeneratePdf();
    }

    /// <summary>
    /// Um e-CPF de mentira: autoassinado, com a extensão <c>2.16.76.1.3.1</c> montada no
    /// leiaute da norma (DDMMAAAA + CPF + NIS + RG + órgão, tudo colado).
    /// </summary>
    private static CertificadoAssinatura CertificadoDeTeste(
        string nome, string? cpf,
        DateTimeOffset? de = null, DateTimeOffset? ate = null)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            $"CN={nome}, OU=Teste, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));

        if (cpf is not null)
            pedido.CertificateExtensions.Add(ExtensaoECpf(cpf));

        var certificado = pedido.CreateSelfSigned(
            de ?? DateTimeOffset.Now.AddDays(-1),
            ate ?? DateTimeOffset.Now.AddYears(1));

        // Reimportar do PFX é o que traz a chave privada num formato que o assinador usa.
        var comChave = new X509Certificate2(
            certificado.Export(X509ContentType.Pfx, "teste"), "teste",
            X509KeyStorageFlags.Exportable);

        return CertificadoIcpBrasil.Ler(comChave);
    }

    private static X509Extension ExtensaoECpf(string cpf)
    {
        var conteudo = Encoding.ASCII.GetBytes(
            "14031978" + cpf + "12345678901" + "123456".PadLeft(15, '0') + "SSPSP ");

        var escritor = new AsnWriter(AsnEncodingRules.DER);
        using (escritor.PushSequence())                                        // GeneralNames
        using (escritor.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0))) // otherName
        {
            escritor.WriteObjectIdentifier(CertificadoIcpBrasil.OidPessoaFisica);
            using (escritor.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                escritor.WriteOctetString(conteudo);
        }

        return new X509Extension("2.5.29.17", escritor.Encode(), critical: false);
    }
}
