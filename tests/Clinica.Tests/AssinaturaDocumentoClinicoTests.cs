using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Clinica.Application.Assinatura;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Receita, atestado, comparecimento e pedido de exame assinados com ICP-Brasil, e o que
/// a lei exige de cada um (parcela 43).
///
/// Por que estes testes são diferentes dos da parcela 42
/// -----------------------------------------------------
/// Aqueles provam a MECÂNICA (a assinatura confere, um bit trocado é pego). Estes provam
/// a CONFORMIDADE, que é o que a cliente pediu quando disse "dentro da lei": um arquivo
/// criptograficamente perfeito de uma receita sem endereço do paciente é um documento
/// impecável que a farmácia recusa — e, por parecer oficial, é pior do que um papel
/// obviamente incompleto, porque ninguém o confere antes de o paciente sair.
///
/// O certificado é autoassinado e montado na memória, como na parcela 42: o que se testa
/// é a regra, não a ICP-Brasil.
/// </summary>
public class AssinaturaDocumentoClinicoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoClinicoService _documentos;
    private readonly DocumentosClinicosPdfService _pdfs;
    private readonly AssinaturaDeDocumentoClinicoService _assinaturas;

    private const string CpfDaMedica = "12345678909";
    private static readonly DateOnly Dia = new(2026, 8, 12);

    public AssinaturaDocumentoClinicoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        _documentos = new DocumentoClinicoService(
            _repo, new ProntuarioService(_repo), new ConsentimentoService(_repo));
        _pdfs = new DocumentosClinicosPdfService(_repo);

        _assinaturas = new AssinaturaDeDocumentoClinicoService(
            _repo, _pdfs,
            new AssinaturaDigitalService(exigirCadeiaConfiavel: false),
            new ParametrosService(_repo));
    }

    // ==================== Conformidade: o que a lei exige do CONTEÚDO ====================

    [Fact]
    public async Task Receita_completa_esta_conforme()
    {
        var receita = await ReceitaAsync();

        ConformidadeDocumentoClinico.Conferir(receita).Should().BeEmpty();
        ConformidadeDocumentoClinico.Conforme(receita).Should().BeTrue();
    }

    /// <summary>
    /// A exigência que mais volta da farmácia — e que o sistema nem tinha onde guardar
    /// antes desta parcela.
    /// </summary>
    [Fact]
    public async Task Receita_sem_endereco_do_paciente_nao_esta_conforme()
    {
        var receita = await ReceitaAsync(endereco: null);

        var faltas = ConformidadeDocumentoClinico.Conferir(receita);

        faltas.Should().ContainSingle()
            .Which.Frase.Should().Contain("endereço residencial")
            .And.Contain("art. 35");
        ConformidadeDocumentoClinico.Conforme(receita).Should().BeFalse();
    }

    /// <summary>"Modo de usar" é literal no art. 35: sem posologia o farmacêutico adivinha a dose.</summary>
    [Fact]
    public async Task Receita_sem_posologia_nao_esta_conforme()
    {
        var receita = await ReceitaAsync(posologia: null);

        ConformidadeDocumentoClinico.Conferir(receita)
            .Should().ContainSingle()
            .Which.Frase.Should().Contain("modo de usar");
    }

    [Fact]
    public async Task Profissional_sem_registro_no_conselho_nao_esta_conforme()
    {
        var receita = await ReceitaAsync(registroConselho: null);

        ConformidadeDocumentoClinico.Conferir(receita)
            .Should().ContainSingle()
            .Which.Frase.Should().Contain("registro no conselho");
    }

    /// <summary>
    /// Art. 13 da Lei 14.063/2020: atestado em meio eletrônico só vale com assinatura
    /// QUALIFICADA. Em papel, assinado à caneta, vale como sempre valeu — por isso o
    /// aviso é recomendação, não impedimento, e ele SOME quando a folha vai ser assinada.
    /// </summary>
    [Fact]
    public async Task Atestado_em_papel_avisa_que_o_arquivo_exigiria_certificado()
    {
        var atestado = await AtestadoAsync();

        var noPapel = ConformidadeDocumentoClinico.Conferir(atestado);
        noPapel.Should().ContainSingle()
            .Which.Fundamento.Should().Be(ConformidadeDocumentoClinico.Art13);
        noPapel[0].Impeditiva.Should().BeFalse("em papel o atestado vale assinado à caneta");

        ConformidadeDocumentoClinico.Conferir(atestado, assinaturaEletronica: true)
            .Should().BeEmpty();
    }

    // ==================== A assinatura ====================

    [Fact]
    public async Task Receita_assinada_confere_e_guarda_o_arquivo()
    {
        var receita = await ReceitaAsync();

        var assinado = await _assinaturas.AssinarAsync(
            receita.Id, Certificado(), operador: "ana");

        assinado.Conferencia.Conferida.Should().BeTrue();
        assinado.Conferencia.Integra.Should().BeTrue();
        assinado.NomeArquivo.Should().EndWith("-assinado.pdf");

        var gravado = await _repo.ObterDocumentoAsync(receita.Id);
        gravado!.AssinadoEletronicamente.Should().BeTrue();
        gravado.AssinaturaQualificada.Should().BeTrue();
        gravado.AssinanteCpf.Should().Be(CpfDaMedica);
        gravado.AssinanteNome.Should().Be("Dra. Ana Souza");
        gravado.ArquivoAssinadoId.Should().NotBeNull();

        // Sem ACT contratada a data é DECLARADA, e a frase precisa dizer isso — prometer
        // precisão que a via não tem é o que este projeto se recusa a fazer.
        gravado.CarimboTempoEm.Should().BeNull();
        gravado.FraseAssinatura.Should().Contain("data declarada");
    }

    /// <summary>
    /// A regra que faz a segunda via valer: reimprimir devolve os BYTES GUARDADOS. Um PDF
    /// "igual" regerado agora tem outra faixa de bytes e abriria como inválido no leitor
    /// de quem confere.
    /// </summary>
    [Fact]
    public async Task Reimpressao_de_documento_assinado_devolve_os_bytes_guardados()
    {
        var receita = await ReceitaAsync();
        var assinado = await _assinaturas.AssinarAsync(receita.Id, Certificado());

        var reimpresso = await _pdfs.GerarAsync(receita.Id);

        reimpresso.Should().Equal(assinado.Pdf);
        (await _assinaturas.ConferirAsync(receita.Id)).Integra.Should().BeTrue();
    }

    /// <summary>
    /// A única recusa do serviço, e a razão dela: assinar sela os bytes. Corrigir o
    /// endereço depois exige cancelar e emitir outro documento — e o arquivo já estaria
    /// na mão do paciente, com toda a aparência de oficial.
    /// </summary>
    [Fact]
    public async Task Nao_assina_receita_que_a_farmacia_nao_poderia_aviar()
    {
        var receita = await ReceitaAsync(endereco: null);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _assinaturas.AssinarAsync(receita.Id, Certificado()));

        erro.Message.Should().Contain("endereço residencial");
        erro.Message.Should().Contain("art. 35");

        (await _repo.ObterDocumentoAsync(receita.Id))!
            .AssinadoEletronicamente.Should().BeFalse();
    }

    /// <summary>
    /// O e-CPF da recepcionista não assina a receita da médica. É esta conferência que
    /// separa "assinatura qualificada" de "alguém com algum token assinou".
    /// </summary>
    [Fact]
    public async Task Nao_assina_com_certificado_de_outra_pessoa()
    {
        var receita = await ReceitaAsync();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _assinaturas.AssinarAsync(
                receita.Id, Certificado("Joana Recepção", "98765432100")));

        erro.Message.Should().Contain("Cada um assina com o próprio certificado");
    }

    [Fact]
    public async Task Nao_assina_duas_vezes_o_mesmo_documento()
    {
        var receita = await ReceitaAsync();
        await _assinaturas.AssinarAsync(receita.Id, Certificado());

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _assinaturas.AssinarAsync(receita.Id, Certificado()));

        erro.Message.Should().Contain("já foi assinado");
    }

    [Fact]
    public async Task Nao_assina_documento_cancelado()
    {
        var receita = await ReceitaAsync();
        await _documentos.CancelarAsync(receita.Id, "erro de digitação", "ana");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _assinaturas.AssinarAsync(receita.Id, Certificado()));

        erro.Message.Should().Contain("cancelado");
    }

    /// <summary>
    /// Documento sem assinatura devolve o TERCEIRO estado, não "adulterado": "não
    /// consegui olhar" e "olhei e mudou" são respostas diferentes, e a tela escreve
    /// coisas diferentes para cada uma.
    /// </summary>
    [Fact]
    public async Task Documento_sem_assinatura_nao_e_conferido_nem_dado_por_adulterado()
    {
        var receita = await ReceitaAsync();

        var conferencia = await _assinaturas.ConferirAsync(receita.Id);

        conferencia.Conferida.Should().BeFalse();
        conferencia.Integra.Should().BeFalse();
        conferencia.Frase.Should().Contain("Não foi possível conferir");
    }

    [Fact]
    public async Task Assinatura_grava_auditoria_com_o_titular_do_certificado()
    {
        var receita = await ReceitaAsync();
        await _assinaturas.AssinarAsync(receita.Id, Certificado(), operador: "ana");

        var evento = await _db.Auditoria
            .SingleAsync(e => e.Acao == "DocumentoClinicoAssinado");

        evento.Operador.Should().Be("ana");
        evento.PacienteId.Should().Be(receita.PacienteId);
        evento.Detalhe.Should().Contain("Dra. Ana Souza");
        evento.Detalhe.Should().Contain("sem carimbo do tempo");
    }

    /// <summary>
    /// O atestado é o caso em que a assinatura não é melhoria, é o que faz o arquivo
    /// existir juridicamente (art. 13 da Lei 14.063/2020).
    /// </summary>
    [Fact]
    public async Task Atestado_assinado_fica_qualificado()
    {
        var atestado = await AtestadoAsync();

        await _assinaturas.AssinarAsync(atestado.Id, Certificado());

        var gravado = await _repo.ObterDocumentoAsync(atestado.Id);
        gravado!.AssinaturaQualificada.Should().BeTrue();
    }

    // ==================== O que chega ao balcão da farmácia ====================

    /// <summary>
    /// O endereço impresso é o VALIDAR, e este teste existe porque a constante já esteve
    /// ERRADA em produção: o ITI teve por três anos um validador separado para documentos
    /// de saúde e outro genérico, e os dois foram desativados em 06/03/2023 e unificados
    /// no <c>validar.iti.gov.br</c>. Orientação antiga de CRF ainda aponta para os
    /// endereços mortos — e o endereço morto foi impresso em receita.
    ///
    /// O teste não substitui abrir a URL no navegador (nenhum teste faz isso); ele trava
    /// a regressão depois que alguém conferiu.
    /// </summary>
    [Fact]
    public void O_endereco_impresso_e_o_validador_vigente_do_iti()
    {
        DocumentosClinicosPdfService.ValidadorOficial
            .Should().Be("validar.iti.gov.br");

        DocumentosClinicosPdfService.ValidadorFarmaceutico
            .Should().StartWith("https://validar.iti.gov.br");

        // Os dois endereços aposentados não podem voltar por descuido.
        DocumentosClinicosPdfService.ValidadorFarmaceutico
            .Should().NotContain("assinaturadigital.iti.gov.br")
            .And.NotContain("verificador.iti.gov.br");
    }

    /// <summary>
    /// O QR poupa a digitação do endereço no balcão. Ele é acessório: se a codificação
    /// falhar, o endereço continua escrito por extenso e o documento continua conferível
    /// — mas enquanto existir tem de sair um PNG de verdade.
    /// </summary>
    [Fact]
    public void O_qr_do_validador_sai_como_png()
    {
        var qr = DocumentosClinicosPdfService.QrDoValidador();

        qr.Should().NotBeNull();
        qr!.Take(4).Should().Equal(0x89, (byte)'P', (byte)'N', (byte)'G');
    }

    /// <summary>
    /// A folha assinada carrega o que a folha em papel não precisa: a faixa do carimbo
    /// digital, o QR e o passo a passo de quem vai conferir. Se um dia alguém "simplificar"
    /// o rodapé, o documento sai igual ao de papel — e aí o farmacêutico recebe um arquivo
    /// sem uma palavra sobre como verificá-lo, que é o caso em que ele recusa por
    /// precaução, com razão.
    /// </summary>
    [Fact]
    public async Task Folha_para_assinar_traz_mais_do_que_a_folha_de_papel()
    {
        var receita = await ReceitaAsync();
        var documento = await _repo.ObterDocumentoAsync(receita.Id);

        var papel = _pdfs.Gerar(documento!);
        var paraAssinar = _pdfs.Gerar(documento!, paraAssinaturaEletronica: true);

        paraAssinar.Length.Should().BeGreaterThan(papel.Length);
    }

    // ==================== Apoio ====================

    private async Task<DocumentoClinico> ReceitaAsync(
        string? endereco = "Rua das Acácias, 120 — Santo André/SP",
        string? posologia = "1 comprimido a cada 8 horas por 5 dias",
        string? registroConselho = "CRM-SP 123456")
    {
        var (pacienteId, profissionalId) = await CadastroAsync(endereco, registroConselho);

        return await _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = Dia,
            Itens = [new ItemDocumento { Descricao = "Dipirona 500mg", Detalhe = posologia }]
        }, "ana");
    }

    private async Task<DocumentoClinico> AtestadoAsync()
    {
        var (pacienteId, profissionalId) = await CadastroAsync(
            "Rua das Acácias, 120", "CRM-SP 123456");

        return await _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Atestado,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = Dia,
            DiasAfastamento = 2
        }, "ana");
    }

    private async Task<(int PacienteId, int ProfissionalId)> CadastroAsync(
        string? endereco, string? registroConselho)
    {
        var paciente = new Paciente
        {
            Nome = "Maria Silva",
            Endereco = endereco,
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            DataNascimento = new DateOnly(1980, 5, 20)
        };

        var profissional = new Profissional
        {
            Nome = "Dra. Ana Souza",
            RegistroConselho = registroConselho,
            Cpf = CpfDaMedica
        };

        _db.Pacientes.Add(paciente);
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();

        return (paciente.Id, profissional.Id);
    }

    private static CertificadoAssinatura Certificado(
        string nome = "Dra. Ana Souza", string cpf = CpfDaMedica)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            $"CN={nome}, OU=Teste, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));
        pedido.CertificateExtensions.Add(ExtensaoECpf(cpf));

        var certificado = pedido.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));

        var comChave = new X509Certificate2(
            certificado.Export(X509ContentType.Pfx, "teste"), "teste",
            X509KeyStorageFlags.Exportable);

        return CertificadoIcpBrasil.Ler(comChave);
    }

    /// <summary>O CPF do titular como o e-CPF ICP-Brasil o carrega: dentro do certificado.</summary>
    private static X509Extension ExtensaoECpf(string cpf)
    {
        var conteudo = Encoding.ASCII.GetBytes(
            "14031978" + cpf + "12345678901" + "123456".PadLeft(15, '0') + "SSPSP ");

        var escritor = new AsnWriter(AsnEncodingRules.DER);
        using (escritor.PushSequence())
        using (escritor.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
        {
            escritor.WriteObjectIdentifier(CertificadoIcpBrasil.OidPessoaFisica);
            using (escritor.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0)))
                escritor.WriteOctetString(conteudo);
        }

        return new X509Extension("2.5.29.17", escritor.Encode(), critical: false);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
