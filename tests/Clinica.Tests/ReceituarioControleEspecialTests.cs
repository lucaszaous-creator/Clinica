using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Clinica.Application.Assinatura;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// RECEITUÁRIO DE CONTROLE ESPECIAL (nov/2026). A receita 2026/0484 (claritromicina + amoxicilina) voltou da
/// farmácia: antimicrobiano e a lista C1 pedem o receituário em duas vias com o comprador identificado. Estes testes
/// fixam a regra (sugestão, comprador, tamanho, cidade/UF da clínica), o PDF (duas vias) e — o ponto do dono — que o
/// carimbo da assinatura cai DENTRO do campo "ASSINATURA MÉDICO(A)" nas DUAS vias, sem encostar na linha, no rótulo
/// nem no logo. Nenhuma assinatura real: certificado autoassinado em memória. Dados fictícios.
/// </summary>
public sealed class ReceituarioControleEspecialTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public ReceituarioControleEspecialTests()
    {
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    // ==================== a sugestão ====================

    [Theory]
    [InlineData("Claritromicina 500 mg + Amoxicilina 1 g, 12/12 h", new[] { "amoxicilina", "claritromicina" })]
    [InlineData("SERTRALINA 50mg 1x ao dia", new[] { "sertralina" })]
    [InlineData("Carbonato de lítio 300 mg", new[] { "litio", "carbonato de litio" })]
    public void Antimicrobiano_ou_C1_na_receita_sugere_o_controle_especial(string texto, string[] esperados)
    {
        ReceituarioControleEspecialRegras.TermosEncontrados(texto).Should().BeEquivalentTo(esperados);
        ReceituarioControleEspecialRegras.Sugere(texto).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Dipirona 1 g se dor")]
    [InlineData("xamoxicilinax — não é o termo, é parte de outra palavra")]
    public void Sem_termo_da_lista_nao_sugere(string? texto)
        => ReceituarioControleEspecialRegras.Sugere(texto).Should().BeFalse();

    [Fact]
    public void A_folha_sugere_so_na_receita_comum()
    {
        var r = new RascunhoDeDocumento(10, TipoDocumentoClinico.Receita) { Corpo = "Azitromicina 500 mg por 3 dias" };
        r.SugestaoControleEspecial.Should().Equal("azitromicina");
        ReceituarioControleEspecialRegras.FraseDaSugestao(r.SugestaoControleEspecial)
            .Should().Contain("Receituário de Controle Especial");

        r.TrocarTipo(TipoDocumentoClinico.ReceituarioControleEspecial).Should().BeTrue();
        r.SugestaoControleEspecial.Should().BeEmpty("no próprio receituário a sugestão não faz sentido");
        r.Corpo.Should().Be("Azitromicina 500 mg por 3 dias", "trocar o papel não apaga o que foi escrito");
    }

    // ==================== o comprador ====================

    [Fact]
    public void O_que_falta_do_comprador_vem_na_ordem_do_modelo()
    {
        var p = new Paciente { Nome = "Paciente Fictícia", Endereco = "Rua Fictícia, 1", Telefone = "22900000000", Uf = "XX" };
        ReceituarioControleEspecialRegras.FaltasDoComprador(p).Select(f => f.Chave)
            .Should().Equal("rg", "orgao", "cidade", "uf");

        p.Rg = "00.000.000-0"; p.RgOrgaoEmissor = "DETRAN-RJ"; p.Cidade = "Macaé"; p.Uf = "rj";
        ReceituarioControleEspecialRegras.FaltasDoComprador(p).Should().BeEmpty();
    }

    [Fact]
    public async Task Completar_o_comprador_grava_so_o_vazio_e_audita_os_nomes_dos_campos()
    {
        var paciente = new Paciente
        {
            Nome = "Paciente Fictícia", Endereco = "Rua do Balcão, 5", Telefone = "22900000000",
            Convenio = Convenio.UnimedIntercambio
        };
        _db.Add(paciente);
        await _db.SaveChangesAsync();

        await new PacienteService(_repo).CompletarCompradorAsync(paciente.Id,
            new PacienteService.DadosDoComprador(" 00.000.000-0 ", "detran-rj", "Endereço de janela antiga", "Macaé", "rj", null),
            "medica.teste");

        _db.ChangeTracker.Clear();
        var salvo = (await _repo.ObterPacienteAsync(paciente.Id))!;
        salvo.Rg.Should().Be("00.000.000-0");
        salvo.RgOrgaoEmissor.Should().Be("DETRAN-RJ");
        salvo.Cidade.Should().Be("Macaé");
        salvo.Uf.Should().Be("RJ");
        salvo.Endereco.Should().Be("Rua do Balcão, 5", "o que o balcão já preencheu não é sobrescrito");
        var evento = await _db.Set<EventoAuditoria>().SingleAsync(e => e.Acao == "DadosCompradorCompletadosNaEmissao");
        evento.Operador.Should().Be("medica.teste");
        evento.Detalhe.Should().Contain("RG").And.Contain("UF").And.NotContain("00.000.000-0",
            "a trilha diz QUAIS campos, nunca o documento da pessoa");
    }

    [Fact]
    public async Task Comprador_incompleto_ou_UF_invalida_nao_grava_nada()
    {
        var paciente = new Paciente { Nome = "Paciente Fictícia", Convenio = Convenio.UnimedIntercambio };
        _db.Add(paciente);
        await _db.SaveChangesAsync();
        var servico = new PacienteService(_repo);

        var semUf = () => servico.CompletarCompradorAsync(paciente.Id,
            new PacienteService.DadosDoComprador("1", "SSP", "Rua", "Cidade", "ZZ", "22900000000"), "medica.teste");
        await semUf.Should().ThrowAsync<InvalidOperationException>().WithMessage("*UF*");

        var faltando = () => servico.CompletarCompradorAsync(paciente.Id,
            new PacienteService.DadosDoComprador("1", null, "Rua", "Cidade", "RJ", "22900000000"), "medica.teste");
        await faltando.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Órgão emissor*");

        _db.ChangeTracker.Clear();
        (await _repo.ObterPacienteAsync(paciente.Id))!.Rg.Should().BeNull();
        (await _db.Set<EventoAuditoria>().CountAsync()).Should().Be(0);
    }

    // ==================== o emitente e o tamanho ====================

    [Fact]
    public void Cidade_e_UF_da_clinica_saem_do_fim_do_endereco_cadastrado()
    {
        var prestador = new DadosPrestador { Endereco = "RUA FICTÍCIA, 10, CENTRO,MACAÉ-RJ" };
        ReceituarioControleEspecialRegras.CidadeUfDaClinica(prestador).Should().Be(("Macaé", "RJ"));
        ReceituarioControleEspecialRegras.EnderecoSemCidade(prestador.Endereco).Should().Be("RUA FICTÍCIA, 10, CENTRO");

        prestador.Cidade = "Rio das Ostras"; prestador.Uf = "rj";
        ReceituarioControleEspecialRegras.CidadeUfDaClinica(prestador).Should().Be(("Rio das Ostras", "RJ"));
    }

    [Fact]
    public void Prescricao_que_nao_cabe_na_via_e_recusada_antes_de_numerar()
    {
        ReceituarioControleEspecialRegras.ErroDeTamanho(string.Join('\n', Enumerable.Repeat("Item fictício", 15)))
            .Should().BeNull();
        ReceituarioControleEspecialRegras.ErroDeTamanho(string.Join('\n', Enumerable.Repeat("Item fictício", 16)))
            .Should().Contain("Divida em dois");
        ReceituarioControleEspecialRegras.ErroDeTamanho(new string('x', 92 * 15 + 1)).Should().NotBeNull(
            "linha longa ocupa mais de uma linha da via");
    }

    [Fact]
    public void O_arquivo_tem_o_nome_que_a_farmacia_ve()
        => EmissaoDeDocumentoNaTela.NomeDoArquivo(TipoDocumentoClinico.ReceituarioControleEspecial, "2026/0485")
            .Should().Be("Receituario-Controle-Especial-2026-0485.pdf");

    // ==================== o PDF e o carimbo ====================

    [Fact]
    public void O_PDF_tem_as_duas_vias_em_A4()
    {
        var pdf = ReceituarioControleEspecialPdf.Gerar(Documento(), Prestador(), null, previa: false, "https://validar.iti.gov.br");
        using var doc = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        doc.PageCount.Should().Be(2);
        foreach (var p in doc.Pages)
        {
            p.Width.Point.Should().BeApproximately(595, 1);
            p.Height.Point.Should().BeApproximately(842, 1);
        }
    }

    /// <summary>
    /// O retângulo do carimbo, medido no sistema do PDF (origem no pé) e convertido para o do desenho (origem no
    /// topo): DENTRO do campo x 392–552 · y 182–226, abaixo do logo (124–182) e acima da linha (228) e do rótulo.
    /// </summary>
    [Fact]
    public void O_carimbo_cai_dentro_do_campo_da_assinatura_nas_duas_vias()
    {
        var area = DocumentosClinicosPdfService.AreaDaAssinatura(TipoDocumentoClinico.ReceituarioControleEspecial, 2);
        area.Should().BeEquivalentTo(ReceituarioControleEspecialPdf.AreaDaAssinatura());
        area.Pagina.Should().Be(0);
        area.OutrasPaginas.Should().Equal(1);
        area.AjustarFonte.Should().BeTrue();

        var topo = 842 - area.Y - area.Altura;
        var base_ = topo + area.Altura;
        area.X.Should().BeGreaterThanOrEqualTo(392);
        (area.X + area.Largura).Should().BeLessThanOrEqualTo(552);
        topo.Should().BeGreaterThanOrEqualTo(182, "acima disso é o logo (124–182)");
        base_.Should().BeLessThan(228, "a linha da assinatura fica em 228 e o rótulo, abaixo dela");
    }

    /// <summary>
    /// O texto do carimbo CABE no campo de 160 × 44 com o nome longo e o emissor ICP-Brasil mais comprido: a escala
    /// encolhe a letra, e as quatro linhas mais a margem ficam dentro da altura.
    /// </summary>
    [Theory]
    [InlineData("Dra. Fictícia de Teste", "AC Teste")]
    [InlineData("Dra. Maria Fictícia Bittencourt de Albuquerque Cavalcanti", "Autoridade Certificadora do SERPRO Final v5")]
    public void O_texto_do_carimbo_cabe_inteiro_no_campo(string nome, string emissorNoNome)
    {
        AssinaturaDigitalService.GarantirFonte();
        var certificado = Certificado(emissorNoNome);
        var pedido = new PedidoAssinatura("Receituário", nome, "CRM-RJ 000000", ReceituarioControleEspecialPdf.AreaDaAssinatura());
        var caixa = new XRect(392, 182, 160, 44);
        using var doc = new PdfDocument();
        var pagina = doc.AddPage();
        using var gfx = XGraphics.FromPdfPage(pagina);

        var escala = AssinaturaDigitalService.CarimboDeAssinatura.Escala(gfx, caixa, certificado, pedido);
        escala.Should().BeInRange(0.55, 1.0);
        var margem = 4.0 * Math.Max(escala, 0.75);
        (margem + (9 + 8 + 8 + 8) * escala).Should().BeLessThanOrEqualTo(caixa.Height,
            "as quatro linhas e a margem cabem na altura do campo");
        var (l1, _, _, _) = AssinaturaDigitalService.CarimboDeAssinatura.Linhas(certificado, pedido);
        gfx.MeasureString(l1, new XFont("SegoeWP", 7.5 * escala, XFontStyleEx.Bold)).Width
            .Should().BeLessThanOrEqualTo(caixa.Width - margem * 2, "o título não é cortado");
    }

    /// <summary>A assinatura (certificado de teste, sem PSC) desenha o carimbo nas DUAS vias.</summary>
    [Fact]
    public async Task Assinado_o_carimbo_aparece_na_via_da_farmacia_e_na_do_paciente()
    {
        var pdf = ReceituarioControleEspecialPdf.Gerar(Documento(), Prestador(), null, previa: false, "https://validar.iti.gov.br");
        var assinado = await new AssinaturaDigitalService(exigirCadeiaConfiavel: false).AssinarAsync(pdf,
            Certificado("AC Teste"), new PedidoAssinatura("Receituário de Controle Especial", "Dra. Fictícia de Teste",
                "CRM-RJ 000000", ReceituarioControleEspecialPdf.AreaDaAssinatura()));

        ConteudoDaPagina(pdf, 0).Should().NotContain("ASSINADO DIGITALMENTE");
        ConteudoDaPagina(assinado.Pdf, 0).Should().Contain("ASSINADO DIGITALMENTE").And.Contain("Dra. Fict");
        ConteudoDaPagina(assinado.Pdf, 1).Should().Contain("ASSINADO DIGITALMENTE").And.Contain("Dra. Fict");
    }

    // ==================== o registro do prescritor e os metadados do ITI ====================

    [Theory]
    [InlineData("06", "520115854", "33", null, "CRM", "520115854", "RJ")]
    [InlineData(null, null, null, "CRM-RJ 123456", "CRM", "123456", "RJ")]
    [InlineData(null, null, null, "CRM/SP 52-0115854-6", "CRM", "52-0115854-6", "SP")]
    [InlineData(null, null, null, "CRM 123456 RJ", "CRM", "123456", "RJ")]
    [InlineData("06", "777", null, "CRM-MG 123", "CRM", "777", "MG")]
    public void O_registro_sai_por_sigla_e_nunca_pelo_codigo_TISS(string? conselho, string? numero, string? uf,
        string? textoLivre, string esperadoConselho, string esperadoNumero, string esperadaUf)
    {
        var r = RegistroDoPrescritor.De(new Profissional
        {
            Nome = "Dra. Fictícia", ConselhoProfissional = conselho, NumeroConselho = numero, UfConselho = uf,
            RegistroConselho = textoLivre
        });
        r.Conselho.Should().Be(esperadoConselho);
        r.Numero.Should().Be(esperadoNumero);
        r.Uf.Should().Be(esperadaUf, "o papel e o metadado dizem RJ, nunca o código IBGE 33");
        r.EhMedico.Should().BeTrue();
    }

    [Fact]
    public void Receita_de_medico_leva_os_OIDs_do_guia_e_outros_papeis_nao()
    {
        var d = Documento();
        d.Profissional = new Profissional { Nome = "Dra. Fictícia", ConselhoProfissional = "06", NumeroConselho = "123456", UfConselho = "33" };
        MetadadosDePrescricaoIti.Entradas(d).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["2.16.76.1.12.1.1"] = "Prescrição de medicamento",
            ["2.16.76.1.4.2.2.1"] = "123456",
            ["2.16.76.1.4.2.2.2"] = "RJ"
        });

        d.Tipo = TipoDocumentoClinico.Receita;
        MetadadosDePrescricaoIti.Entradas(d).Should().ContainKey("2.16.76.1.12.1.1");

        d.Tipo = TipoDocumentoClinico.Atestado;
        MetadadosDePrescricaoIti.Entradas(d).Should().BeEmpty("o OID de prescrição é só da receita");

        d.Tipo = TipoDocumentoClinico.Receita;
        d.Profissional = new Profissional { Nome = "Fictícia", RegistroConselho = "CRO-RJ 999" };
        // Os OIDs 2.16.76.1.4.2.2.x são do CFM: dentista não recebe número de médico.
        MetadadosDePrescricaoIti.Entradas(d).Keys.Should().Equal(new[] { "2.16.76.1.12.1.1" });
    }

    /// <summary>Os metadados entram ANTES da assinatura e continuam no Info depois dela (certificado de teste).</summary>
    [Fact]
    public async Task Os_metadados_sobrevivem_a_assinatura()
    {
        var d = Documento();
        d.Profissional = new Profissional { Nome = "Dra. Fictícia de Teste", ConselhoProfissional = "06", NumeroConselho = "123456", UfConselho = "33" };
        var pdf = new DocumentosClinicosPdfService(_repo).Gerar(d, Prestador(), paraAssinaturaEletronica: true);
        var assinado = await new AssinaturaDigitalService(exigirCadeiaConfiavel: false).AssinarAsync(pdf,
            Certificado("AC Teste"), new PedidoAssinatura("Receituário", "Dra. Fictícia de Teste", "CRM-RJ 123456",
                ReceituarioControleEspecialPdf.AreaDaAssinatura()));

        foreach (var bytes in new[] { pdf, assinado.Pdf })
        {
            using var doc = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
            doc.Info.Elements.GetString("/2.16.76.1.12.1.1").Should().Be("Prescrição de medicamento");
            doc.Info.Elements.GetString("/2.16.76.1.4.2.2.1").Should().Be("123456");
            doc.Info.Elements.GetString("/2.16.76.1.4.2.2.2").Should().Be("RJ");
        }

        var previa = new DocumentosClinicosPdfService(_repo).Gerar(d, Prestador(), previa: true);
        using var semMetadados = PdfReader.Open(new MemoryStream(previa), PdfDocumentOpenMode.Import);
        semMetadados.Info.Elements.ContainsKey("/2.16.76.1.12.1.1").Should().BeFalse("a prévia não é prescrição");
    }

    private static DocumentoClinico Documento() => new()
    {
        Id = 1, Numero = "2026/9999", Tipo = TipoDocumentoClinico.ReceituarioControleEspecial, Data = new DateOnly(2026, 11, 25),
        Corpo = "Medicamento fictício 500 mg\nUso oral, 1 comprimido de 12/12 h por 7 dias",
        Paciente = new Paciente
        {
            Nome = "Paciente Fictícia de Teste", Rg = "00.000.000-0", RgOrgaoEmissor = "DETRAN-RJ",
            Endereco = "Rua Fictícia, 10, Centro", Cidade = "Macaé", Uf = "RJ", Telefone = "22900000000"
        },
        Profissional = new Profissional { Nome = "Dra. Fictícia de Teste", RegistroConselho = "CRM-RJ 000000" }
    };

    private static DadosPrestador Prestador() => new()
    {
        RazaoSocial = "Clínica Fictícia Ltda", Cnpj = "00000000000000", Endereco = "RUA FICTÍCIA, 10, CENTRO,MACAÉ-RJ",
        Telefone = "(22) 0000-0000"
    };

    private static string ConteudoDaPagina(byte[] pdf, int pagina)
    {
        using var entrada = new MemoryStream(pdf, writable: false);
        var doc = PdfReader.Open(entrada, PdfDocumentOpenMode.Modify);
        var texto = new StringBuilder();
        foreach (var conteudo in doc.Pages[pagina].Contents)
            texto.Append(Encoding.Latin1.GetString(conteudo.Stream.UnfilteredValue));
        return texto.ToString();
    }

    private static CertificadoAssinatura Certificado(string emissor)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest($"CN={emissor}, OU=Teste, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = pedido.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
        return CertificadoIcpBrasil.Ler(new X509Certificate2(
            cert.Export(X509ContentType.Pfx, "teste"), "teste", X509KeyStorageFlags.Exportable));
    }
}
