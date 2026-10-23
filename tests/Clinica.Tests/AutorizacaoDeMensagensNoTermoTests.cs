using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Prontuario;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A AUTORIZAÇÃO DE MENSAGENS NO TERMO DO BSV (out/2026, fase 3 da unificação Recepção×CRM).
///
/// O recall do CRM usa um modelo de MARKETING da Meta, e a Meta exige que o paciente tenha
/// aceitado receber mensagens. A direção decidiu colher esse aceite como uma declaração
/// opcional do termo do BSV — e o recall voltou a exigir o consentimento
/// <see cref="FinalidadeConsentimento.ComunicacaoEMarketing"/> vigente.
///
/// O que estes testes fixam:
/// 1. só o termo EXIGIDO para o BSV ganha a pergunta, e só para quem nunca respondeu;
/// 2. "Sim" vira o consentimento que o portão consulta, com o número do termo;
/// 3. "Não" não acende alerta, grava a recusa (a pergunta não volta) e nunca revoga;
/// 4. o "Marcar todas como Sim" do balcão não alcança esta declaração.
/// </summary>
public class AutorizacaoDeMensagensNoTermoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoClinicoService _documentos;
    private readonly AssinaturaDoPacienteService _assinaturas;
    private readonly ConsentimentoService _consentimentos;
    private readonly TermoProcedimentoService _termos;

    public AutorizacaoDeMensagensNoTermoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _consentimentos = new ConsentimentoService(_repo);
        _documentos = new DocumentoClinicoService(_repo, new ProntuarioService(_repo), _consentimentos);
        _assinaturas = new AssinaturaDoPacienteService(_repo);
        _termos = new TermoProcedimentoService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<int> PacienteAsync()
    {
        var p = new Paciente { Nome = "Maria Silva", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    /// <summary>O TCLE do BSV, gravado e exigido para as duas modalidades, com a clínica nomeada.</summary>
    private async Task<ModeloDocumento> TcleExigidoAsync()
    {
        await new ParametrosService(_repo).SalvarPrestadorAsync(new DadosPrestador { NomeFantasia = "SemDor" });
        var modelo = ModelosTermoBsv.Consentimento();
        _db.ModelosDocumento.Add(modelo);
        await _db.SaveChangesAsync();
        foreach (var modalidade in ModelosTermoBsv.ModalidadesDoBsv)
            await _termos.ExigirAsync(modalidade, modelo.Id);
        return modelo;
    }

    private Task<DocumentoClinico> AssinarAsync(DocumentoClinico termo, string autorizacao)
        => _assinaturas.ColherAsync(termo.Id, new byte[512], 600, 220,
            termo.Itens.ToDictionary(i => i.Ordem,
                i => (string?)(i.Codigo == RespostaDeclaracao.CodigoAutorizacaoMensagens ? autorizacao : "Sim")),
            "CPF 123.456.789-00", "ana.recepcao");

    [Fact]
    public async Task Termo_exigido_para_o_BSV_pergunta_no_fim_nomeando_a_clinica()
    {
        var paciente = await PacienteAsync();
        var modelo = await TcleExigidoAsync();

        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        var ultima = termo.Itens.OrderBy(i => i.Ordem).Last();
        ultima.Codigo.Should().Be(RespostaDeclaracao.CodigoAutorizacaoMensagens);
        ultima.Descricao.Should().Contain("Autorizo a Clínica SemDor");
        ultima.Detalhe.Should().Contain("PARAR");
        ultima.Descricao.Length.Should().BeLessThanOrEqualTo(300, "é o teto da coluna no Postgres");
        ultima.Quantidade.Should().BeNull("a resposta é do paciente — pré-marcar seria fabricar o aceite");
        termo.Itens.Should().HaveCount(modelo.Itens.Count + 1,
            "o texto que o advogado da clínica aprovou fica intacto; a pergunta é acrescentada");
    }

    [Fact]
    public async Task Termo_que_nao_e_do_BSV_nao_pergunta()
    {
        var paciente = await PacienteAsync();
        var modelo = ModelosTermoBsv.Consentimento();
        _db.ModelosDocumento.Add(modelo);
        await _db.SaveChangesAsync();
        await _termos.ExigirAsync(ModalidadeAtendimento.AcupunturaSimples, modelo.Id);

        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        termo.Itens.Should().NotContain(i => i.Codigo == RespostaDeclaracao.CodigoAutorizacaoMensagens);
    }

    [Fact]
    public async Task Sim_vira_o_consentimento_que_o_recall_consulta_com_o_numero_do_termo()
    {
        var paciente = await PacienteAsync();
        var modelo = await TcleExigidoAsync();
        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        await AssinarAsync(termo, "Sim");

        (await _consentimentos.VigenteAsync(paciente, FinalidadeConsentimento.ComunicacaoEMarketing))
            .Should().BeTrue();
        (await _repo.PacientesComConsentimentoVigenteAsync(FinalidadeConsentimento.ComunicacaoEMarketing, [paciente]))
            .Should().Contain(paciente, "é esta leitura que a ponte do recall faz");
        var linha = (await _consentimentos.HistoricoAsync(paciente)).Single();
        linha.VersaoTermo.Should().Be(termo.Numero, "a evidência aponta para o termo assinado");
        (await _consentimentos.VigenteAsync(paciente, FinalidadeConsentimento.UsoDeImagem))
            .Should().BeFalse("o termo do procedimento não decide outra finalidade");

        var proximo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);
        proximo.Itens.Should().NotContain(i => i.Codigo == RespostaDeclaracao.CodigoAutorizacaoMensagens,
            "quem já autorizou não responde de novo a cada sessão");
    }

    [Fact]
    public async Task Nao_nao_acende_alerta_grava_a_recusa_e_a_pergunta_nao_volta()
    {
        var paciente = await PacienteAsync();
        var modelo = await TcleExigidoAsync();
        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        var assinado = await AssinarAsync(termo, "Não");

        assinado.Itens.Should().NotContain(i => RespostaDeclaracao.RequerAtencao(i),
            "recusar mensagens é escolha, não sinal clínico");
        (await _consentimentos.VigenteAsync(paciente, FinalidadeConsentimento.ComunicacaoEMarketing))
            .Should().BeFalse();
        (await _consentimentos.HistoricoAsync(paciente)).Single().Concedido.Should().BeFalse();

        var proximo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);
        proximo.Itens.Should().NotContain(i => i.Codigo == RespostaDeclaracao.CodigoAutorizacaoMensagens,
            "quem recusou não é perguntado a cada termo; mudar de ideia é pela aba LGPD");
    }

    [Fact]
    public async Task Nao_no_termo_do_BSV_nunca_revoga_uma_autorizacao_vigente()
    {
        var paciente = await PacienteAsync();
        var modelo = await TcleExigidoAsync();
        var termo = await _documentos.EmitirTermoProcedimentoAsync(paciente, modelo.Id);

        // A autorização chegou por outro caminho (o termo LGPD) entre a emissão e a assinatura.
        await _consentimentos.RegistrarAsync(paciente, FinalidadeConsentimento.ComunicacaoEMarketing, true);
        await AssinarAsync(termo, "Não");

        (await _consentimentos.VigenteAsync(paciente, FinalidadeConsentimento.ComunicacaoEMarketing))
            .Should().BeTrue("revogar é do termo LGPD e do PARAR do CRM, não do termo do procedimento");
    }

    [Fact]
    public void Marcar_todas_como_Sim_nao_alcanca_a_autorizacao()
    {
        var comum = new DeclaracaoItem(1, "Li o termo", null);
        var autorizacao = new DeclaracaoItem(2, "Autorizo…", null, RespostaDeclaracao.CodigoAutorizacaoMensagens);

        comum.AutorizacaoDeMensagens.Should().BeFalse();
        autorizacao.AutorizacaoDeMensagens.Should().BeTrue(
            "é a marca que tira a declaração do \"Marcar todas como Sim\" e a põe em destaque");
    }
}
