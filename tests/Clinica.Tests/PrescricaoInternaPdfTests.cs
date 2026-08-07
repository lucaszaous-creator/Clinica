using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
/// O ciclo INTEIRO da parcela 42, do rascunho ao PDF assinado e conferido.
///
/// Os testes de regra (<see cref="PrescricaoInternaTests"/>) provam o comportamento com
/// uma assinatura de mentira; os de criptografia (<see cref="AssinaturaDigitalTests"/>)
/// provam a assinatura sobre um PDF qualquer. Nenhum dos dois prova o que só aqui se vê:
/// que a folha que a clínica IMPRIME é a mesma que foi assinada, e que o registro de
/// execução carrega o hash da prescrição.
///
/// É o mesmo argumento do <c>CircuitoCompletoTests</c>: cada trecho pode passar com o
/// circuito partido. Se o PDF fosse regenerado na reimpressão em vez de guardado, nenhum
/// teste de unidade falharia — e a assinatura simplesmente não conferiria mais, porque
/// ela cobre BYTES, não conteúdo.
/// </summary>
public class PrescricaoInternaPdfTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly PrescricaoInternaService _prescricoes;
    private readonly ChecagemPrescricaoService _checagens;
    private readonly PrescricaoInternaPdfService _pdfs;
    private readonly AssinaturaDigitalService _assinador = new(exigirCadeiaConfiavel: false);
    private readonly AssinaturaDePrescricaoService _orquestra;

    private static readonly IdentificacaoExecutante Tecnica =
        new(null, "Joana Técnica", "COREN-SP 999999");

    public PrescricaoInternaPdfTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        var conferencia = new PrescricaoService(_repo);
        _prescricoes = new PrescricaoInternaService(_repo, conferencia);
        _checagens = new ChecagemPrescricaoService(_repo, () => DateTime.Today.AddHours(12));
        _pdfs = new PrescricaoInternaPdfService(_repo, conferencia);
        _orquestra = new AssinaturaDePrescricaoService(
            _repo, _prescricoes, _pdfs, _assinador, new ParametrosService(_repo));
    }

    [Fact]
    public async Task Folha_da_prescricao_sai_assinada_e_confere()
    {
        var prescricao = await AssinarPrescricaoAsync();

        var carregada = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;
        var assinatura = carregada.AssinaturaDoPrescritor!;
        var arquivo = (await _repo.ObterArquivoAssinadoAsync(assinatura.ArquivoId!.Value))!;

        var conferencia = _assinador.Conferir(arquivo.Conteudo);

        conferencia.Conferida.Should().BeTrue();
        conferencia.Integra.Should().BeTrue();
        assinatura.Tipo.Should().Be(TipoAssinatura.IcpBrasil);
        assinatura.CpfAssinante.Should().Be("12345678909");
    }

    /// <summary>
    /// O hash gravado é o do arquivo GUARDADO. Se um dia alguém trocar o armazenamento por
    /// "regenerar o PDF na hora de reimprimir", este teste cai — e tem de cair, porque a
    /// assinatura cobre bytes e dois PDFs "iguais" gerados em momentos diferentes não são
    /// os mesmos bytes.
    /// </summary>
    [Fact]
    public async Task Hash_gravado_bate_com_os_bytes_guardados()
    {
        var prescricao = await AssinarPrescricaoAsync();

        var carregada = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;
        var assinatura = carregada.AssinaturaDoPrescritor!;
        var arquivo = (await _repo.ObterArquivoAssinadoAsync(assinatura.ArquivoId!.Value))!;

        AssinaturaDigitalService.Hash(arquivo.Conteudo).Should().Be(assinatura.HashConteudo);
    }

    [Fact]
    public async Task Adulterar_o_arquivo_guardado_e_pego_na_conferencia()
    {
        var prescricao = await AssinarPrescricaoAsync();
        var carregada = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;
        var arquivo = (await _repo.ObterArquivoAssinadoAsync(
            carregada.AssinaturaDoPrescritor!.ArquivoId!.Value))!;

        var adulterado = (byte[])arquivo.Conteudo.Clone();
        adulterado[adulterado.Length / 3] ^= 0xFF;

        _assinador.Conferir(adulterado).Integra.Should().BeFalse();
    }

    /// <summary>
    /// O registro de execução NÃO é assinado eletronicamente — quem assina a execução é a
    /// enfermeira, na via impressa. O que o sistema guarda é o registro do que foi feito.
    ///
    /// Este teste é o guarda da decisão: se um dia voltar um segundo certificado aqui, ele
    /// cai.
    /// </summary>
    [Fact]
    public async Task Registro_de_execucao_sai_sem_assinatura_eletronica()
    {
        var prescricao = await AssinarPrescricaoAsync();
        var carregada = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;

        foreach (var item in carregada.Itens)
            await _checagens.ChecarAsync(
                item.Id, SituacaoChecagem.Realizado, new TimeOnly(9, 30), Tecnica);

        await _checagens.EncerrarAsync(prescricao.Id, Tecnica);

        var folha = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.RegistroExecucao);

        folha.Pdf.Should().NotBeEmpty();
        folha.Assinatura.Should().BeNull();
        folha.Conferencia.Should().BeNull();

        var final = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;
        final.Situacao.Should().Be(SituacaoPrescricao.Encerrada);
        // Uma assinatura eletrônica na folha inteira, e é a de quem prescreveu.
        final.Assinaturas.Should().ContainSingle()
            .Which.Papel.Should().Be(PapelAssinatura.Prescritor);
    }

    /// <summary>
    /// O registro de execução é montado NA HORA, e não congelado: ele muda a cada item
    /// checado, e devolver bytes guardados faria a reimpressão mostrar um estado que passou.
    /// </summary>
    [Fact]
    public async Task Registro_de_execucao_acompanha_o_que_ja_foi_checado()
    {
        var prescricao = await AssinarPrescricaoAsync();
        var carregada = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;

        var antes = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.RegistroExecucao);

        await _checagens.ChecarAsync(
            carregada.Itens[0].Id, SituacaoChecagem.NaoRealizado, new TimeOnly(9, 30),
            Tecnica, justificativa: "Paciente recusou.");

        var depois = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.RegistroExecucao);

        depois.Pdf.Should().NotEqual(antes.Pdf);
    }

    /// <summary>
    /// A folha imprime as alergias no topo, e é a MESMA regra da tela: alergia resolvida
    /// continua alertando. Duplicar a regra aqui faria papel e tela discordarem sobre o
    /// mesmo paciente.
    /// </summary>
    [Fact]
    public async Task Alergia_do_prontuario_entra_na_folha()
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        _db.ProblemasPaciente.Add(new ProblemaPaciente
        {
            PacienteId = pacienteId,
            Natureza = NaturezaProblema.Alergia,
            Descricao = "Dipirona",
            // Resolvida CONTINUA alertando: é a regra do PrescricaoService, e a folha usa
            // ele em vez de consultar a tabela justamente para papel e tela nunca
            // discordarem sobre o mesmo paciente.
            Situacao = SituacaoProblema.Resolvido
        });
        await _db.SaveChangesAsync();

        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);
        await _prescricoes.SalvarRascunhoAsync(prescricao.Id, "Hidratação", null,
            [new ItemPrescricaoInterna { Descricao = "Soro fisiológico 0,9%", Dose = "500 mL" }]);

        var carregada = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;
        var alergias = await _pdfs.AlergiasParaAFolhaAsync(carregada);

        alergias.Should().ContainSingle().Which.Should().Contain("Dipirona");

        // E a folha desenha sem estourar.
        (await _pdfs.GerarPrescricaoAsync(prescricao.Id)).Should().NotBeEmpty();
    }

    /// <summary>
    /// O rodapé nunca promete mais do que a via garante — e o caso sem carimbo do tempo é
    /// o que mais tenta a mentira, porque a data está lá, bonita, vinda do relógio de quem
    /// assinou.
    ///
    /// A frase é testada direto, e não procurada no PDF: o QuestPDF embute fontes com
    /// subconjunto CID, então o "texto" dentro do arquivo são IDs de glifo. Procurar a
    /// string ali daria um teste que passa por acidente ou falha por acidente, que é pior
    /// que não ter teste.
    /// </summary>
    [Fact]
    public void Rodape_declara_o_nivel_da_assinatura_sem_prometer_a_mais()
    {
        PrescricaoInternaPdfService.FraseDoNivel(null)
            .Should().Contain("Sem assinatura eletr");

        var semCarimbo = new AssinaturaDocumento
        {
            Tipo = TipoAssinatura.IcpBrasil,
            NomeAssinante = "Dra. Ana Souza",
            CertificadoTitular = "ANA SOUZA"
        };
        PrescricaoInternaPdfService.FraseDoNivel(semCarimbo)
            .Should().Contain("ICP-Brasil")
            .And.Contain("sem carimbo do tempo");

        var comCarimbo = new AssinaturaDocumento
        {
            Tipo = TipoAssinatura.IcpBrasil,
            NomeAssinante = "Dra. Ana Souza",
            CarimboTempoEm = new DateTime(2026, 8, 5, 14, 32, 0),
            CarimboTempoAutoridade = "http://act.exemplo.br"
        };
        PrescricaoInternaPdfService.FraseDoNivel(comCarimbo)
            .Should().Contain("carimbo do tempo 05/08/2026 14:32");
    }

    [Fact]
    public async Task As_duas_folhas_desenham_sem_estourar()
    {
        var prescricao = await AssinarPrescricaoAsync();
        var carregada = (await _repo.ObterPrescricaoInternaAsync(prescricao.Id))!;

        await _checagens.ChecarAsync(
            carregada.Itens[0].Id, SituacaoChecagem.NaoRealizado, new TimeOnly(9, 30),
            Tecnica, justificativa: "Paciente recusou.");

        (await _pdfs.GerarPrescricaoAsync(prescricao.Id)).Should().NotBeEmpty();
        (await _pdfs.GerarRegistroExecucaoAsync(prescricao.Id)).Should().NotBeEmpty();
    }


    // ==================== O certificado é DA PESSOA ====================

    /// <summary>
    /// A regra que faz a assinatura qualificada valer alguma coisa. Sem ela o sistema
    /// provaria que ALGUÉM com um token assinou — e o e-CPF da recepcionista assinaria a
    /// prescrição da médica sem um único alerta.
    /// </summary>
    [Fact]
    public async Task Certificado_de_OUTRA_pessoa_e_recusado()
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        var prescricao = await RascunhoAsync(pacienteId, profissionalId);

        var deOutraPessoa = ECpfDeTeste("Carla Recepção", "98765432100");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orquestra.AssinarPrescricaoAsync(prescricao.Id, deOutraPessoa));

        erro.Message.Should().Contain("Cada um assina com o próprio certificado");
    }

    [Fact]
    public async Task Certificado_sem_CPF_dentro_e_recusado()
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        var prescricao = await RascunhoAsync(pacienteId, profissionalId);

        var semCpf = SemECpfDeTeste("Dra. Ana Souza");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orquestra.AssinarPrescricaoAsync(prescricao.Id, semCpf));

        erro.Message.Should().Contain("não é um e-CPF");
    }

    /// <summary>
    /// Profissional sem CPF cadastrado também é recusado, e isso é decisão: aceitar em
    /// silêncio faria a conferência existir só para quem já tinha o campo preenchido, e o
    /// buraco ficaria exatamente em quem esqueceu.
    /// </summary>
    [Fact]
    public async Task Profissional_sem_CPF_cadastrado_e_recusado_dizendo_onde_cadastrar()
    {
        var paciente = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio };
        var profissional = new Profissional { Nome = "Dra. Ana Souza", Cpf = null };
        _db.Pacientes.Add(paciente);
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();

        var prescricao = await RascunhoAsync(paciente.Id, profissional.Id);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orquestra.AssinarPrescricaoAsync(
                prescricao.Id, ECpfDeTeste("Dra. Ana Souza", "12345678909")));

        erro.Message.Should().Contain("Equipe");
    }

    [Fact]
    public async Task Certificado_certo_assina_pelo_caminho_completo()
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        var prescricao = await RascunhoAsync(pacienteId, profissionalId);

        var resultado = await _orquestra.AssinarPrescricaoAsync(
            prescricao.Id, ECpfDeTeste("Dra. Ana Souza", "12345678909"));

        resultado.Prescricao.Situacao.Should().Be(SituacaoPrescricao.Assinada);

        var folha = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.Prescricao);
        folha.Conferencia!.Integra.Should().BeTrue();
        folha.Assinatura!.CpfAssinante.Should().Be("12345678909");
    }

    /// <summary>
    /// A reimpressão devolve os BYTES GUARDADOS, não um PDF novo. Se um dia isso mudar, a
    /// segunda via sai com a assinatura inválida — e este teste é o que avisa.
    /// </summary>
    [Fact]
    public async Task Segunda_via_devolve_os_bytes_guardados()
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        var prescricao = await RascunhoAsync(pacienteId, profissionalId);
        await _orquestra.AssinarPrescricaoAsync(
            prescricao.Id, ECpfDeTeste("Dra. Ana Souza", "12345678909"));

        var primeira = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.Prescricao);
        var segunda = await _orquestra.FolhaAsync(prescricao.Id, FolhaPrescricao.Prescricao);

        segunda.Pdf.Should().Equal(primeira.Pdf);
        segunda.Conferencia!.Integra.Should().BeTrue();
    }

    private Task<PrescricaoInterna> RascunhoAsync(int pacienteId, int profissionalId)
        => CriarRascunhoAsync(pacienteId, profissionalId);

    private async Task<PrescricaoInterna> CriarRascunhoAsync(int pacienteId, int profissionalId)
    {
        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);
        await _prescricoes.SalvarRascunhoAsync(prescricao.Id, "Crise álgica", null,
            [new ItemPrescricaoInterna { Descricao = "Soro fisiológico 0,9%", Dose = "500 mL" }]);
        return prescricao;
    }

    private static CertificadoAssinatura SemECpfDeTeste(string nome)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            $"CN={nome}, OU=Teste, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature, true));

        var certificado = pedido.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));

        return CertificadoIcpBrasil.Ler(new X509Certificate2(
            certificado.Export(X509ContentType.Pfx, "t"), "t", X509KeyStorageFlags.Exportable));
    }

    // ==================== Apoio ====================

    private async Task<PrescricaoInterna> AssinarPrescricaoAsync()
    {
        var (pacienteId, profissionalId) = await BaseAsync();
        var prescricao = await _prescricoes.CriarAsync(pacienteId, profissionalId);

        await _prescricoes.SalvarRascunhoAsync(prescricao.Id, "Crise álgica", "Monitorar PA",
        [
            new ItemPrescricaoInterna
            {
                Descricao = "Soro fisiológico 0,9%", Dose = "500 mL",
                Via = ViaAdministracao.Endovenosa, TempoInfusao = "60 min"
            }
        ]);

        var pdf = await _pdfs.GerarPrescricaoAsync(prescricao.Id);
        var certificado = ECpfDeTeste("Dra. Ana Souza", "12345678909");

        var assinado = await _assinador.AssinarAsync(pdf, certificado, new PedidoAssinatura(
            Motivo: "Prescrição de execução interna",
            NomeExibido: "Dra. Ana Souza",
            RegistroConselho: "CRM-SP 123456",
            Area: PrescricaoInternaPdfService.AreaDaAssinatura(ContarPaginas(pdf))));

        var arquivo = new ArquivoAssinado
        {
            Conteudo = assinado.Pdf,
            NomeArquivo = $"{prescricao.Numero}.pdf"
        };
        _db.ArquivosAssinados.Add(arquivo);
        await _db.SaveChangesAsync();

        await _prescricoes.AssinarAsync(prescricao.Id, new AssinaturaDocumento
        {
            Tipo = TipoAssinatura.IcpBrasil,
            HashConteudo = assinado.Hash,
            NomeAssinante = "Dra. Ana Souza",
            RegistroConselho = "CRM-SP 123456",
            CpfAssinante = certificado.Cpf,
            CertificadoTitular = certificado.Titular,
            CertificadoEmissor = certificado.Emissor,
            CertificadoSerie = certificado.NumeroSerie,
            CertificadoValidoDe = certificado.ValidoDe,
            CertificadoValidoAte = certificado.ValidoAte,
            CarimboTempoEm = assinado.CarimboTempoEm,
            CarimboTempoAutoridade = assinado.CarimboTempoAutoridade,
            ArquivoId = arquivo.Id
        });

        return prescricao;
    }

    private async Task<(int PacienteId, int ProfissionalId)> BaseAsync()
    {
        var paciente = new Paciente { Nome = "Maria de Teste", Convenio = Convenio.UnimedIntercambio };
        var profissional = new Profissional
        {
            Nome = "Dra. Ana Souza", RegistroConselho = "CRM-SP 123456", Cpf = "12345678909"
        };
        _db.Pacientes.Add(paciente);
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();
        return (paciente.Id, profissional.Id);
    }

    private static int ContarPaginas(byte[] pdf)
    {
        using var fluxo = new MemoryStream(pdf, writable: false);
        using var documento = PdfSharp.Pdf.IO.PdfReader.Open(
            fluxo, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        return documento.PageCount;
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private static CertificadoAssinatura ECpfDeTeste(string nome, string cpf)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest(
            $"CN={nome}, OU=Teste, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        pedido.CertificateExtensions.Add(ExtensaoECpf(cpf));

        var certificado = pedido.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));

        return CertificadoIcpBrasil.Ler(new X509Certificate2(
            certificado.Export(X509ContentType.Pfx, "t"), "t", X509KeyStorageFlags.Exportable));
    }

    private static X509Extension ExtensaoECpf(string cpf)
    {
        var conteudo = System.Text.Encoding.ASCII.GetBytes(
            "14031978" + cpf + "12345678901" + "123456".PadLeft(15, '0') + "SSPSP ");

        var escritor = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        using (escritor.PushSequence())
        using (escritor.PushSequence(new System.Formats.Asn1.Asn1Tag(
                   System.Formats.Asn1.TagClass.ContextSpecific, 0)))
        {
            escritor.WriteObjectIdentifier(CertificadoIcpBrasil.OidPessoaFisica);
            using (escritor.PushSequence(new System.Formats.Asn1.Asn1Tag(
                       System.Formats.Asn1.TagClass.ContextSpecific, 0)))
                escritor.WriteOctetString(conteudo);
        }

        return new X509Extension("2.5.29.17", escritor.Encode(), critical: false);
    }
}
