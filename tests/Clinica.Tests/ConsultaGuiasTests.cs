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
        await new FaturamentoService(_repo).DarBaixaAsync(rMaria.Atendimento.Codigos.First().Id, new DateOnly(2026, 7, 11), "G-777", "sec", null);
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

        var porGuia = await _repo.ConsultarCodigosAsync(new FiltroConsultaGuias { NumeroGuia = "G-777" });
        porGuia.Should().ContainSingle().Which.NumeroGuiaReal.Should().Be("G-777");
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

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
