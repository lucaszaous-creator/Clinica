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

public class ConsultaGuiasTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public ConsultaGuiasTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    private async Task SemearAsync()
    {
        var maria = new Paciente { Nome = "Maria Silva", Documento = "52998224725", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        var joao = new Paciente { Nome = "João Souza", Convenio = Convenio.Amil, Sexo = Sexo.Masculino };
        _db.AddRange(maria, joao);
        await _db.SaveChangesAsync();

        var atServ = new AtendimentoService(_repo);
        var rMaria = await atServ.LancarAsync(maria.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);
        await atServ.LancarAsync(joao.Id, new DateOnly(2026, 7, 12), ModalidadeAtendimento.AcupunturaSimples);

        // Baixa em uma guia da Maria (para filtrar por status Baixado).
        await new FaturamentoService(_repo).DarBaixaAsync(rMaria.Atendimento.Codigos.First().Id, new DateOnly(2026, 7, 11), "90777", "sec", null);
    }

    [Fact]
    public async Task Filtra_PorPaciente()
    {
        await SemearAsync();
        var r = await _repo.ConsultarCodigosAsync(new FiltroConsultaGuias { TermoPaciente = "maria" });
        r.Should().OnlyContain(c => c.Atendimento!.Paciente!.Nome == "Maria Silva");
        r.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Filtra_PorStatusBaixado_ENumeroGuia()
    {
        await SemearAsync();
        var baixados = await _repo.ConsultarCodigosAsync(new FiltroConsultaGuias { Status = FiltroStatusGuia.Baixado });
        baixados.Should().OnlyContain(c => c.Baixado);

        var porGuia = await _repo.ConsultarCodigosAsync(new FiltroConsultaGuias { NumeroGuia = "90777" });
        porGuia.Should().ContainSingle().Which.NumeroGuiaReal.Should().Be("90777");
    }

    [Fact]
    public async Task Filtra_PorConvenioEPeriodo()
    {
        await SemearAsync();
        var r = await _repo.ConsultarCodigosAsync(new FiltroConsultaGuias
        {
            Convenio = Convenio.Amil,
            Inicio = new DateOnly(2026, 7, 12),
            Fim = new DateOnly(2026, 7, 12)
        });
        r.Should().OnlyContain(c => c.Atendimento!.Paciente!.Convenio == Convenio.Amil);
    }

    /// <summary>
    /// Filtro por MODALIDADE (parcela 45) — "o que vem sendo feito". A Maria fez
    /// acupuntura com eletro e o João, acupuntura simples.
    /// </summary>
    [Fact]
    public async Task Filtra_PorModalidade()
    {
        await SemearAsync();

        var r = await _repo.ConsultarCodigosAsync(new FiltroConsultaGuias
        {
            Modalidade = ModalidadeAtendimento.AcupunturaComEletro
        });

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(c =>
            c.Atendimento!.Modalidade == ModalidadeAtendimento.AcupunturaComEletro);
        r.Should().OnlyContain(c => c.Atendimento!.Paciente!.Nome == "Maria Silva");
    }

    /// <summary>
    /// Filtro por ESPECIALIDADE. A especialidade da GUIA vem primeiro; a do atendimento é o
    /// caminho de baixo — sem ele, guia de um atendimento com especialidade declarada
    /// ficaria de fora do filtro da própria especialidade.
    /// </summary>
    [Fact]
    public async Task Filtra_PorEspecialidade()
    {
        await SemearAsync();

        var ana = new Paciente { Nome = "Ana Lima", Convenio = Convenio.Amil, Sexo = Sexo.Feminino };
        _db.Add(ana);
        await _db.SaveChangesAsync();

        await new AtendimentoService(_repo).LancarAsync(
            ana.Id, new DateOnly(2026, 7, 15), ModalidadeAtendimento.Consulta,
            especialidadeConsulta: Especialidade.Psiquiatria);

        var r = await _repo.ConsultarCodigosAsync(new FiltroConsultaGuias
        {
            Especialidade = Especialidade.Psiquiatria
        });

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(c => c.Atendimento!.Paciente!.Nome == "Ana Lima");
    }

    /// <summary>
    /// Os dois filtros novos se combinam com os antigos: a pergunta da direção é sempre
    /// "quantas consultas de psiquiatria da Amil em julho", não uma dimensão de cada vez.
    /// </summary>
    [Fact]
    public async Task Filtros_novos_combinam_com_convenio_e_periodo()
    {
        await SemearAsync();

        var r = await _repo.ConsultarCodigosAsync(new FiltroConsultaGuias
        {
            Convenio = Convenio.Amil,
            Modalidade = ModalidadeAtendimento.AcupunturaComEletro
        });

        // O João é da Amil, mas fez acupuntura SIMPLES: o cruzamento não devolve nada, e
        // é isso que separa um filtro que soma de um que intersecta.
        r.Should().BeEmpty();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
