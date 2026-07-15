using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public class NumeroAtendimentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public NumeroAtendimentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    [Fact]
    public async Task LancarAtendimento_GeraNumeroComAnoEId()
    {
        var p = new Paciente { Nome = "P", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        var r = await new AtendimentoService(_repo).LancarAsync(
            p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);

        r.Atendimento.Numero.Should().NotBeNullOrWhiteSpace();
        r.Atendimento.Numero.Should().StartWith("2026-");
        r.Atendimento.Numero.Should().Be($"2026-{r.Atendimento.Id:D6}");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
