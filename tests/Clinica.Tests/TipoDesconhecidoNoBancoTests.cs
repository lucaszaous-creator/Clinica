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
/// O app VELHO lendo o que o app NOVO gravou.
///
/// Por que isto é um teste e não uma precaução
/// -------------------------------------------
/// Os cinco apps se auto-atualizam por Velopack, <b>um canal por app</b>, e dividem UM banco.
/// A janela em que um já atualizou e o outro não é o desenho, não o acidente.
///
/// Em 14/08/2026 a clínica perdeu a tela de Prescrições inteira: um <c>DocumentoClinico</c>
/// gravado como <c>TermoProcedimento</c> (parcela 66) chegou a um build que não tinha esse
/// valor no enum, e o <c>HasConversion&lt;string&gt;()</c> do EF não falha a LINHA — falha a
/// CONSULTA. No lugar da lista veio "Cannot convert string value 'TermoProcedimento' from the
/// database to any value in the mapped 'TipoDocumentoClinico' enum", que não diz a ninguém que
/// o que falta é atualizar o programa.
///
/// Os testes abaixo escrevem no banco, por SQL, um nome de tipo que o enum <b>desta</b> versão
/// não tem — que é exatamente o que a versão seguinte vai fazer com o tipo que ela inventar.
/// </summary>
public class TipoDesconhecidoNoBancoTests : IDisposable
{
    /// <summary>
    /// O nome que a versão de amanhã vai gravar. Ele NÃO existe em
    /// <see cref="TipoDocumentoClinico"/> de propósito — e não pode passar a existir, senão o
    /// teste deixa de exercitar o caso e passa por outro motivo.
    /// </summary>
    private const string TipoDoFuturo = "LaudoDeAltaHospitalar";

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly DocumentoClinicoService _documentos;
    private readonly DocumentosClinicosPdfService _pdfs;

    public TipoDesconhecidoNoBancoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(opcoes);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        var prontuario = new ProntuarioService(_repo);
        _documentos = new DocumentoClinicoService(
            _repo, prontuario, new ConsentimentoService(_repo));
        _pdfs = new DocumentosClinicosPdfService(_repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// O teste que reproduz o defeito da clínica: a lista do paciente tem uma receita comum e
    /// um documento do futuro, e ela precisa ABRIR — com as duas linhas.
    /// </summary>
    [Fact]
    public async Task Documento_de_versao_mais_nova_nao_derruba_a_lista_do_paciente()
    {
        var (pacienteId, profissionalId) = await CadastroAsync();
        await EmitirReceitaAsync(pacienteId, profissionalId);
        var doFuturo = await EmitirReceitaAsync(pacienteId, profissionalId);
        await GravarTipoCruAsync(doFuturo.Id, TipoDoFuturo);

        var lista = await _repo.DocumentosDoPacienteAsync(pacienteId);

        lista.Should().HaveCount(2, "a linha desconhecida aparece — não some nem derruba a tela");
        lista.Should().ContainSingle(d => d.Tipo == TipoDocumentoClinico.Receita);
        lista.Should().ContainSingle(d => d.Tipo == TipoDocumentoClinico.Desconhecido);
    }

    /// <summary>
    /// Cair no primeiro valor do enum (o <c>default</c>) seria pior do que estourar: um termo
    /// de procedimento apareceria como "Receita", e isso é mentir sobre um registro de
    /// prontuário. O rótulo diz o que HÁ e o que FAZER.
    /// </summary>
    [Fact]
    public async Task O_tipo_desconhecido_nao_se_disfarca_de_outro_e_manda_atualizar()
    {
        var (pacienteId, profissionalId) = await CadastroAsync();
        var doFuturo = await EmitirReceitaAsync(pacienteId, profissionalId);
        await GravarTipoCruAsync(doFuturo.Id, TipoDoFuturo);

        var lido = await _repo.ObterDocumentoAsync(doFuturo.Id);

        lido!.Tipo.Should().NotBe(default(TipoDocumentoClinico));
        lido.Tipo.Should().Be(TipoDocumentoClinico.Desconhecido);
        TipoDocumentoInfo.Rotular(lido.Tipo).Should().Contain("atualize o sistema");
    }

    /// <summary>
    /// A metade que torna a tolerância segura. Sem ela, o app velho leria "não sei o que é
    /// isto" e no primeiro Salvar gravaria o sentinela POR CIMA do tipo verdadeiro — apagando
    /// em silêncio o que a versão nova registrou. Registro clínico não se apaga, e apagar por
    /// conversão é a forma mais discreta de fazê-lo.
    /// </summary>
    [Fact]
    public async Task Gravar_o_tipo_desconhecido_e_RECUSADO_para_nao_apagar_o_verdadeiro()
    {
        var (pacienteId, profissionalId) = await CadastroAsync();
        var doFuturo = await EmitirReceitaAsync(pacienteId, profissionalId);
        await GravarTipoCruAsync(doFuturo.Id, TipoDoFuturo);

        var lido = await _repo.ObterDocumentoAsync(doFuturo.Id);
        lido!.Observacoes = "qualquer edição que force a gravação da linha";
        _db.Entry(lido).Property(d => d.Tipo).IsModified = true;

        // Pelo repositório, que é por onde toda escrita do sistema passa: a frase que chega à
        // tela tem de dizer o que fazer, e não o "An error occurred…" genérico do EF.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.SalvarAsync());

        erro.Message.Should().Contain("Atualize o sistema");
        erro.Message.Should().NotContain("An error occurred");

        // E o banco continua com o nome VERDADEIRO — que é o ponto de tudo isto.
        _db.ChangeTracker.Clear();
        (await TipoCruAsync(doFuturo.Id)).Should().Be(TipoDoFuturo);
    }

    /// <summary>
    /// A folha SAIRIA — cabeçalho, número e linha de assinatura, e sem o miolo, que este
    /// build não sabe montar. É a garantia aparente que o serviço de PDF recusa desde a
    /// parcela 3, e por isso a recusa é explícita em vez de uma página pela metade.
    /// </summary>
    [Fact]
    public async Task Imprimir_documento_de_tipo_desconhecido_e_recusado_em_vez_de_sair_pela_metade()
    {
        var (pacienteId, profissionalId) = await CadastroAsync();
        var doFuturo = await EmitirReceitaAsync(pacienteId, profissionalId);
        await GravarTipoCruAsync(doFuturo.Id, TipoDoFuturo);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _pdfs.GerarAsync(doFuturo.Id));

        erro.Message.Should().Contain(doFuturo.Numero);
        erro.Message.Should().Contain("Atualize o sistema");
    }

    /// <summary>
    /// O sentinela não é um papel: ele não pode aparecer no seletor de emissão nem na lista
    /// que o teste de cobertura de PDF percorre.
    /// </summary>
    [Fact]
    public void O_sentinela_nao_entra_na_lista_dos_tipos_que_a_clinica_emite()
    {
        TipoDocumentoInfo.Todos.Should().NotContain(TipoDocumentoClinico.Desconhecido);
        TipoDocumentoInfo.Todos.Should().HaveCount(
            Enum.GetValues<TipoDocumentoClinico>().Length - 1,
            "todo tipo REAL entra em Todos; só o sentinela fica de fora");
    }

    // ---- Apoio ----

    private async Task<(int Paciente, int Profissional)> CadastroAsync()
    {
        var paciente = new Paciente
        {
            Nome = "Maria",
            Convenio = Convenio.UnimedIntercambio,
            Sexo = Sexo.Feminino,
            DataNascimento = new DateOnly(1980, 5, 20)
        };
        var profissional = new Profissional { Nome = "Dra. Ana", RegistroConselho = "CRM-SP 1" };

        _db.Pacientes.Add(paciente);
        _db.Profissionais.Add(profissional);
        await _db.SaveChangesAsync();

        return (paciente.Id, profissional.Id);
    }

    private Task<DocumentoClinico> EmitirReceitaAsync(int pacienteId, int profissionalId)
        => _documentos.EmitirAsync(new DocumentoClinico
        {
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            Data = new DateOnly(2026, 8, 12),
            Itens = [new ItemDocumento { Descricao = "Dipirona 500mg", Detalhe = "8/8h" }]
        }, "secretaria");

    /// <summary>
    /// Faz o papel da versão de amanhã: grava na coluna um nome de tipo por SQL, sem passar
    /// pelo conversor — que é a única forma de o valor chegar lá.
    /// </summary>
    private async Task GravarTipoCruAsync(int documentoId, string tipo)
    {
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE DocumentosClinicos SET Tipo = {0} WHERE Id = {1}", tipo, documentoId);
        _db.ChangeTracker.Clear();
    }

    private async Task<string?> TipoCruAsync(int documentoId)
    {
        await using var comando = _conn.CreateCommand();
        comando.CommandText = "SELECT Tipo FROM DocumentosClinicos WHERE Id = " + documentoId;
        return (string?)await comando.ExecuteScalarAsync();
    }
}
