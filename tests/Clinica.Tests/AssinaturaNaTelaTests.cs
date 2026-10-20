using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Clinica.Application.Abstracoes;
using Clinica.Application.Assinatura;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A SEQUÊNCIA dos caminhos de assinatura que subiram dos ViewModels WPF para
/// <c>Application/Modelos/AssinaturaNaTela.cs</c> (out/2026 — autorização da direção de
/// 19/10/2026, SÓ A CAMADA DE TELA). O motor é o de sempre e continua congelado; o que estes
/// testes fixam é que a tela nova chama os MESMOS serviços, com os MESMOS argumentos, na MESMA
/// ordem:
///  • desistir no seletor (o <c>escolher</c> devolve nulo) não chama serviço nenhum;
///  • a confirmação da alergia vem ANTES do certificado — e recusá-la não abre o seletor;
///  • a continuidade sem A1 libera sem pedir certificado, com o usuário da SESSÃO;
///  • o usuário e o operador que a tela passa são os que ficam gravados.
///
/// Serviços REAIS sobre SQLite, com um <c>DbContext</c> novo por escopo — como em produção,
/// onde cada passo do fluxo abre o seu escopo. O certificado é autoassinado (e-CPF de teste):
/// o que se prova é a ordem, não a ICP-Brasil. Ver <c>docs/safeid-telas-blazor-out2026.md</c>.
/// </summary>
public sealed class AssinaturaNaTelaTests : IDisposable
{
    private const string CpfMedica = "12345678909";

    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<ClinicaDbContext> _opcoes;
    private readonly ServiceProvider _provedor;
    private readonly IServiceScopeFactory _escopos;

    public AssinaturaNaTelaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        using (var db = new ClinicaDbContext(_opcoes)) db.Database.EnsureCreated();

        var s = new ServiceCollection();
        s.AddScoped(_ => new ClinicaDbContext(_opcoes));
        s.AddScoped<IClinicaRepositorio>(sp => new ClinicaRepositorio(sp.GetRequiredService<ClinicaDbContext>()));
        s.AddScoped(sp => new PrescricaoService(sp.GetRequiredService<IClinicaRepositorio>()));
        s.AddScoped(sp => new PrescricaoInternaService(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<PrescricaoService>()));
        s.AddScoped(sp => new PrescricaoInternaPdfService(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<PrescricaoService>()));
        s.AddScoped(sp => new ParametrosService(sp.GetRequiredService<IClinicaRepositorio>()));
        s.AddSingleton(new AssinaturaDigitalService(exigirCadeiaConfiavel: false));
        s.AddScoped(sp => new AssinaturaDePrescricaoService(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<PrescricaoInternaService>(),
            sp.GetRequiredService<PrescricaoInternaPdfService>(), sp.GetRequiredService<AssinaturaDigitalService>(),
            sp.GetRequiredService<ParametrosService>()));
        s.AddScoped(sp => new DocumentosClinicosPdfService(sp.GetRequiredService<IClinicaRepositorio>()));
        s.AddScoped(sp => new AssinaturaDeDocumentoClinicoService(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<DocumentosClinicosPdfService>(),
            sp.GetRequiredService<AssinaturaDigitalService>(), sp.GetRequiredService<ParametrosService>()));
        s.AddScoped(sp => new DocumentoClinicoService(
            sp.GetRequiredService<IClinicaRepositorio>(),
            new ProntuarioService(sp.GetRequiredService<IClinicaRepositorio>()),
            new ConsentimentoService(sp.GetRequiredService<IClinicaRepositorio>())));
        _provedor = s.BuildServiceProvider();
        _escopos = _provedor.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
        _provedor.Dispose();
        _conn.Dispose();
    }

    // ==================== O editor da prescrição ====================

    [Fact]
    public async Task Desistir_no_seletor_nao_assina_nem_libera_nada()
    {
        var c = await CenarioAsync(alergia: false);
        var passos = new List<string>();

        var desfecho = await AssinaturaDaPrescricaoNaTela.LiberarDoEditorAsync(
            _escopos, c.PrescricaoId, continuidadeAtiva: false, "assunto",
            (_, _) => { passos.Add("confirmar"); return Task.FromResult(true); },
            _ => { passos.Add("escolher"); return Task.FromResult<CertificadoAssinatura?>(null); },
            c.UsuarioId, c.UsuarioId, "ana");

        desfecho.Should().Be(DesfechoDaLiberacao.Desistiu);
        passos.Should().Equal("escolher");
        var p = await PrescricaoAsync(c.PrescricaoId);
        p.Situacao.Should().Be(SituacaoPrescricao.Rascunho);
        p.AssinaturaDoPrescritor.Should().BeNull();
    }

    [Fact]
    public async Task Com_alergia_a_confirmacao_vem_ANTES_do_certificado_e_a_assinatura_leva_usuario_e_operador()
    {
        var c = await CenarioAsync(alergia: true);
        var passos = new List<string>();

        var desfecho = await AssinaturaDaPrescricaoNaTela.LiberarDoEditorAsync(
            _escopos, c.PrescricaoId, continuidadeAtiva: false,
            AssinaturaDaPrescricaoNaTela.AssuntoDoEditor("PRE 1", "Maria"),
            (titulo, msg) =>
            {
                passos.Add("confirmar");
                titulo.Should().Be(AssinaturaDaPrescricaoNaTela.TituloAlergiaDoEditor);
                msg.Should().Contain("Dipirona");
                return Task.FromResult(true);
            },
            assunto =>
            {
                passos.Add("escolher");
                assunto.Should().Be("Prescrição PRE 1 — Maria");
                return Task.FromResult<CertificadoAssinatura?>(ECpf("Dra. Ana Souza", CpfMedica));
            },
            c.UsuarioId, usuarioIdDaAssinatura: c.UsuarioId, operador: "ana.login");

        desfecho.Should().Be(DesfechoDaLiberacao.Assinada);
        passos.Should().Equal("confirmar", "escolher");

        var p = await PrescricaoAsync(c.PrescricaoId);
        p.AssinaturaDoPrescritor.Should().NotBeNull();
        p.AssinaturaDoPrescritor!.UsuarioId.Should().Be(c.UsuarioId);
        await using var db = new ClinicaDbContext(_opcoes);
        (await db.Auditoria.AnyAsync(e => e.Acao == "PrescricaoAssinada" && e.Operador == "ana.login"))
            .Should().BeTrue("o operador que a tela passa é o que vai para a trilha");
    }

    [Fact]
    public async Task Recusar_a_alergia_nao_abre_o_seletor_nem_assina()
    {
        var c = await CenarioAsync(alergia: true);
        var passos = new List<string>();

        var desfecho = await AssinaturaDaPrescricaoNaTela.LiberarDoEditorAsync(
            _escopos, c.PrescricaoId, continuidadeAtiva: false, "assunto",
            (_, _) => { passos.Add("confirmar"); return Task.FromResult(false); },
            _ => { passos.Add("escolher"); return Task.FromResult<CertificadoAssinatura?>(null); },
            c.UsuarioId, c.UsuarioId, "ana");

        desfecho.Should().Be(DesfechoDaLiberacao.CanceladaNaAlergia);
        passos.Should().Equal("confirmar");
        (await PrescricaoAsync(c.PrescricaoId)).AssinaturaDoPrescritor.Should().BeNull();
    }

    [Fact]
    public async Task Sem_alergia_nao_pergunta_nada_antes_do_seletor()
    {
        var c = await CenarioAsync(alergia: false);
        var passos = new List<string>();

        await AssinaturaDaPrescricaoNaTela.LiberarDoEditorAsync(
            _escopos, c.PrescricaoId, continuidadeAtiva: false, "assunto",
            (_, _) => { passos.Add("confirmar"); return Task.FromResult(true); },
            _ => { passos.Add("escolher"); return Task.FromResult<CertificadoAssinatura?>(ECpf("Dra. Ana Souza", CpfMedica)); },
            c.UsuarioId, c.UsuarioId, "ana");

        passos.Should().Equal("escolher");
    }

    [Fact]
    public async Task Continuidade_libera_sem_pedir_certificado_com_o_usuario_da_sessao()
    {
        var c = await CenarioAsync(alergia: false);
        using (var escopo = _escopos.CreateScope())
        {
            var repo = escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>();
            await repo.SalvarConfiguracaoAsync(ContinuidadeSemAssinatura.Configuracao, "true");
            await repo.SalvarAsync();
        }
        var passos = new List<string>();

        var desfecho = await AssinaturaDaPrescricaoNaTela.LiberarDoEditorAsync(
            _escopos, c.PrescricaoId, continuidadeAtiva: true, "assunto",
            (_, _) => { passos.Add("confirmar"); return Task.FromResult(true); },
            _ => { passos.Add("escolher"); return Task.FromResult<CertificadoAssinatura?>(null); },
            usuarioIdDaSessao: c.UsuarioId, usuarioIdDaAssinatura: null, operador: "ana");

        desfecho.Should().Be(DesfechoDaLiberacao.LiberadaSemAssinatura);
        passos.Should().BeEmpty();
        var p = await PrescricaoAsync(c.PrescricaoId);
        p.LiberadaSemAssinaturaEm.Should().NotBeNull();
        p.AssinaturaDoPrescritor.Should().BeNull();
    }

    // ==================== O documento clínico ====================

    [Fact]
    public async Task Documento_desistir_no_seletor_nao_chama_o_servico()
    {
        var c = await CenarioAsync(alergia: false);
        var docId = await DocumentoAsync(c);

        var r = await AssinaturaDeDocumentoNaTela.AssinarAsync(
            _escopos, docId, "assunto", _ => Task.FromResult<CertificadoAssinatura?>(null), c.UsuarioId, "ana");

        r.Should().BeNull();
        await using var db = new ClinicaDbContext(_opcoes);
        (await db.DocumentosClinicos.SingleAsync(d => d.Id == docId)).AssinadoEm.Should().BeNull();
    }

    [Fact]
    public async Task Documento_assinado_grava_o_usuario_que_a_tela_passou()
    {
        var c = await CenarioAsync(alergia: false);
        var docId = await DocumentoAsync(c);
        string? assuntoVisto = null;

        var r = await AssinaturaDeDocumentoNaTela.AssinarAsync(
            _escopos, docId, AssinaturaDeDocumentoNaTela.Assunto("Receita", "2026/0001"),
            a => { assuntoVisto = a; return Task.FromResult<CertificadoAssinatura?>(ECpf("Dra. Ana Souza", CpfMedica)); },
            c.UsuarioId, "ana.login");

        r.Should().NotBeNull();
        assuntoVisto.Should().Be("Assinar receita 2026/0001");
        await using var db = new ClinicaDbContext(_opcoes);
        (await db.DocumentosClinicos.SingleAsync(d => d.Id == docId)).AssinadoPorUsuarioId.Should().Be(c.UsuarioId);
    }

    // ==================== As frases e as recusas de antes do certificado ====================

    [Fact]
    public void As_recusas_do_documento_sao_as_do_WPF()
    {
        AssinaturaDeDocumentoNaTela.Recusa(TipoDocumentoClinico.Receita, "2026/0001", false, false)
            .Should().Be("Este tipo de documento é emitido para impressão, sem assinatura digital.");
        AssinaturaDeDocumentoNaTela.Recusa(TipoDocumentoClinico.TermoProcedimento, "2026/0002", cancelado: true, assinado: false)
            .Should().Be("O documento 2026/0002 está cancelado e não pode ser assinado.");
        AssinaturaDeDocumentoNaTela.Recusa(TipoDocumentoClinico.TermoProcedimento, "2026/0003", cancelado: false, assinado: true)
            .Should().Be("O documento 2026/0003 já foi assinado digitalmente.");
        AssinaturaDeDocumentoNaTela.Recusa(TipoDocumentoClinico.TermoProcedimento, "2026/0004", false, false)
            .Should().BeNull();
    }

    [Fact]
    public void O_arquivo_do_A1_e_conferido_antes_de_abrir()
    {
        CarregamentoDoA1.RecusaDoArquivo(null).Should().Be("Escolha o arquivo do certificado A1.");
        CarregamentoDoA1.RecusaDoArquivo([]).Should().Be("Escolha um arquivo A1 de até 128 KB.");
        CarregamentoDoA1.RecusaDoArquivo(new byte[CertificadoA1.LimiteBytes + 1]).Should().Be("Escolha um arquivo A1 de até 128 KB.");
        CarregamentoDoA1.RecusaDoArquivo(new byte[100]).Should().BeNull();
    }

    [Fact]
    public async Task Sem_servicos_o_A1_e_recusado_pedindo_o_login()
    {
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CarregamentoDoA1.CarregarAsync(null, new byte[10], "x"));
        erro.Message.Should().Be("Entre com seu usuário profissional para carregar o A1.");
        CarregamentoDoA1.FraseDaFalha(erro).Should().Be(erro.Message);
        CarregamentoDoA1.FraseDaFalha(new CryptographicException("x"))
            .Should().Be("Não foi possível carregar o A1. Confira o arquivo e tente novamente.");
    }

    [Fact]
    public void A_mensagem_da_segunda_assinatura_e_a_do_WPF()
    {
        AssinaturaDaPrescricaoNaTela.MensagemDaExecucao(true, new(false, true, true)).Frase
            .Should().Be("Execução assinada. Aguarda a validação do médico responsável.");
        var semArquivo = AssinaturaDaPrescricaoNaTela.MensagemDaExecucao(false, new(false, false, false));
        semArquivo.EhErro.Should().BeTrue();
        AssinaturaDaPrescricaoNaTela.MensagemDaExecucao(false, new(false, true, true)).Frase
            .Should().StartWith("Prescrição arquivada com as duas assinaturas, horários");
    }

    // ==================== Apoio ====================

    private sealed record Cenario(int PacienteId, int ProfissionalId, int UsuarioId, int PrescricaoId);

    private async Task<Cenario> CenarioAsync(bool alergia)
    {
        await using var db = new ClinicaDbContext(_opcoes);
        var paciente = new Paciente { Nome = "Maria de Teste", Convenio = Convenio.UnimedIntercambio, Endereco = "Rua A, 1" };
        var medica = new Profissional { Nome = "Dra. Ana Souza", RegistroConselho = "CRM-SP 123456", Cpf = CpfMedica };
        db.Pacientes.Add(paciente);
        db.Profissionais.Add(medica);
        await db.SaveChangesAsync();
        var usuario = new UsuarioSistema
        {
            Nome = "Dra. Ana Souza", Login = "ana", Perfil = PerfilAcesso.Profissional, ProfissionalId = medica.Id
        };
        db.Usuarios.Add(usuario);
        if (alergia)
            db.Add(new ProblemaPaciente { PacienteId = paciente.Id, Natureza = NaturezaProblema.Alergia, Descricao = "Dipirona" });
        await db.SaveChangesAsync();

        using var escopo = _escopos.CreateScope();
        var prescricoes = escopo.ServiceProvider.GetRequiredService<PrescricaoInternaService>();
        var p = await prescricoes.CriarAsync(paciente.Id, medica.Id);
        await prescricoes.SalvarRascunhoAsync(p.Id, "Crise álgica", null,
            [new ItemPrescricaoInterna { Descricao = "Dipirona 1 g", Dose = "1 ampola" }]);
        return new Cenario(paciente.Id, medica.Id, usuario.Id, p.Id);
    }

    private async Task<int> DocumentoAsync(Cenario c)
    {
        using var escopo = _escopos.CreateScope();
        var doc = await escopo.ServiceProvider.GetRequiredService<DocumentoClinicoService>().EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = c.PacienteId,
            ProfissionalId = c.ProfissionalId,
            Data = DateOnly.FromDateTime(DateTime.Today),
            Itens = [new ItemDocumento { Descricao = "Dipirona 500mg", Detalhe = "1 comprimido a cada 8 horas por 5 dias" }]
        }, "ana");
        return doc.Id;
    }

    private async Task<PrescricaoInterna> PrescricaoAsync(int id)
    {
        using var escopo = _escopos.CreateScope();
        return (await escopo.ServiceProvider.GetRequiredService<IClinicaRepositorio>().ObterPrescricaoInternaAsync(id))!;
    }

    private static CertificadoAssinatura ECpf(string nome, string cpf)
    {
        using var rsa = RSA.Create(2048);
        var pedido = new CertificateRequest($"CN={nome}, OU=Teste, C=BR", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, true));
        pedido.CertificateExtensions.Add(ExtensaoECpf(cpf));
        var certificado = pedido.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
        return CertificadoIcpBrasil.Ler(new X509Certificate2(
            certificado.Export(X509ContentType.Pfx, "t"), "t", X509KeyStorageFlags.Exportable));
    }

    private static X509Extension ExtensaoECpf(string cpf)
    {
        var conteudo = System.Text.Encoding.ASCII.GetBytes(
            "14031978" + cpf + "12345678901" + "123456".PadLeft(15, '0') + "SSPSP ");
        var escritor = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        using (escritor.PushSequence())
        using (escritor.PushSequence(new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0)))
        {
            escritor.WriteObjectIdentifier(CertificadoIcpBrasil.OidPessoaFisica);
            using (escritor.PushSequence(new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0)))
                escritor.WriteOctetString(conteudo);
        }
        return new X509Extension("2.5.29.17", escritor.Encode(), false);
    }
}
