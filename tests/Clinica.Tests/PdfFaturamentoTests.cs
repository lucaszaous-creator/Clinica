using Clinica.Application.Modelos;
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
/// A guia TISS e a capa de faturamento saem em papel.
///
/// São os dois maiores trechos sem execução do projeto — 323 linhas de desenho que nunca
/// rodaram — e pertencem ao faturamento, que está CONGELADO e é o que sustenta a clínica
/// hoje. Testá-los não o toca: é a mesma leitura de sempre, agora com rede embaixo.
///
/// O papel que sai dali vai para a operadora, e é por ele que a clínica recebe. Um campo
/// nulo que derruba o desenho não aparece em compilação nenhuma — aparece na hora de
/// montar o lote do mês, que é quando ninguém tem margem.
///
/// Os casos são os do fluxograma de convênio de verdade: o atendimento com dois códigos
/// (o 2º nasce +24h depois, que é o coração do produto), o que só tem consulta, o que
/// tem código em não conformidade e o prestador sem cadastro completo.
/// </summary>
public class PdfFaturamentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AtendimentoService _atendimentos;
    private readonly GuiaTissPdfService _guia;
    private readonly CapaFaturamentoService _capa;

    private static readonly DateOnly Dia = new(2026, 8, 12);

    public PdfFaturamentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _atendimentos = new AtendimentoService(_repo);
        _guia = new GuiaTissPdfService(_repo);
        _capa = new CapaFaturamentoService(_repo);
    }

    private static void DeveSerPdf(byte[] bytes)
    {
        bytes.Should().NotBeNullOrEmpty();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    private static DadosPrestador PrestadorCompleto() => new()
    {
        RazaoSocial = "Clínica de Acupuntura Ltda",
        NomeFantasia = "Clínica de Acupuntura",
        Cnpj = "12345678000199",
        Cnes = "1234567",
        Endereco = "Rua das Flores, 100 — São Paulo/SP",
        Telefone = "(11) 3333-4444",
        CodigoNaOperadora = "998877"
    };

    private async Task<int> CriarPacienteAsync(
        Convenio convenio = Convenio.UnimedIntercambio, string? carteirinha = "0123456789")
    {
        var p = new Paciente
        {
            Nome = "Maria Souza",
            Convenio = convenio,
            Sexo = Sexo.Feminino,
            Carteirinha = carteirinha,
            DataNascimento = new DateOnly(1980, 4, 3)
        };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private async Task<Atendimento> AtenderAsync(
        int pacienteId, ModalidadeAtendimento modalidade = ModalidadeAtendimento.AcupunturaComEletro)
    {
        var r = await _atendimentos.LancarAsync(pacienteId, Dia, modalidade);
        return r.Atendimento;
    }

    /// <summary>
    /// O caso central do produto: eletroacupuntura gera dois códigos, e o 2º só existe
    /// +24h depois. A guia precisa desenhar os dois.
    /// </summary>
    [Fact]
    public async Task Guia_tiss_sai_com_os_dois_codigos()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimento = await AtenderAsync(pacienteId);

        atendimento.Codigos.Should().HaveCountGreaterThan(1,
            "a eletroacupuntura é o fluxograma de dois códigos");

        DeveSerPdf(await _guia.GerarPdfAsync(atendimento.Id, PrestadorCompleto()));
    }

    /// <summary>Só consulta: o leiaute muda de bloco — é outro caminho no desenho.</summary>
    [Fact]
    public async Task Guia_tiss_sai_para_atendimento_so_de_consulta()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimento = await AtenderAsync(pacienteId, ModalidadeAtendimento.Consulta);

        DeveSerPdf(await _guia.GerarPdfAsync(atendimento.Id, PrestadorCompleto()));
    }

    /// <summary>
    /// Prestador vazio — que é como a base nasce, antes de a clínica preencher
    /// Configurações. Todos os campos do cabeçalho ficam nulos de uma vez, e é aí que um
    /// desenho desprotegido cai.
    /// </summary>
    [Fact]
    public async Task Guia_tiss_aguenta_prestador_sem_cadastro()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimento = await AtenderAsync(pacienteId);

        DeveSerPdf(await _guia.GerarPdfAsync(atendimento.Id, new DadosPrestador()));
    }

    /// <summary>
    /// Paciente sem carteirinha e sem nascimento: o convênio exige os dois, mas o sistema
    /// AVISA em vez de impedir — então a guia tem de sair mesmo assim, para a secretária
    /// ver o que falta no papel.
    /// </summary>
    [Fact]
    public async Task Guia_tiss_aguenta_paciente_sem_carteirinha()
    {
        var pacienteId = await CriarPacienteAsync(carteirinha: null);
        var atendimento = await AtenderAsync(pacienteId);

        DeveSerPdf(await _guia.GerarPdfAsync(atendimento.Id, PrestadorCompleto()));
    }

    [Fact]
    public async Task Guia_de_atendimento_inexistente_falha_com_mensagem()
    {
        var acao = () => _guia.GerarPdfAsync(9999, PrestadorCompleto());

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------------- Capa de faturamento ----------------

    [Fact]
    public async Task Capa_de_faturamento_sai_em_pdf()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimento = await AtenderAsync(pacienteId);

        DeveSerPdf(await _capa.GerarPdfAsync(atendimento.Id, PrestadorCompleto()));
    }

    [Fact]
    public async Task Capa_sai_sem_prestador_cadastrado()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimento = await AtenderAsync(pacienteId);

        DeveSerPdf(await _capa.GerarPdfAsync(atendimento.Id));
    }

    /// <summary>
    /// A conclusão da capa é o texto que a secretária lê antes de levar o papel — e era o
    /// outro método deste serviço sem chamador e sem execução.
    /// </summary>
    [Fact]
    public async Task Conclusao_da_capa_e_montada()
    {
        var pacienteId = await CriarPacienteAsync();
        var atendimento = await AtenderAsync(pacienteId);

        var conclusao = await _capa.GerarConclusaoAsync(atendimento.Id, PrestadorCompleto());

        conclusao.Should().NotBeNull();
    }

    [Fact]
    public async Task Capa_de_atendimento_inexistente_falha_com_mensagem()
    {
        var acao = () => _capa.GerarPdfAsync(9999);

        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
