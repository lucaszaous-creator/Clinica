using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Clinica.Application.Assinatura;
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
