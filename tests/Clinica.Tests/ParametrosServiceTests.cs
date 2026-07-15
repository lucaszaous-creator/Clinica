using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public class ParametrosServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;

    public ParametrosServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
    }

    [Fact]
    public async Task ObterAsync_UsaDefaultsQuandoNaoHaOverride()
    {
        var snap = await _parametros.ObterAsync();
        snap.ValidadeConsultaDias(Convenio.Amil).Should().Be(30);
        snap.ValidadeConsultaDias(Convenio.UnimedIntercambio).Should().Be(22);
        snap.DiasSegundoCodigo(Convenio.UnimedIntercambio).Should().Be(1);
    }

    [Fact]
    public async Task Override_MudaSegundoCodigoNoLancamento()
    {
        await _parametros.SalvarAsync(new[]
        {
            new ParametroConvenio { Convenio = Convenio.UnimedIntercambio, ValidadeConsultaDias = 25, DiasSegundoCodigo = 3 }
        });

        var p = new Paciente { Nome = "P", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        var service = new AtendimentoService(_repo, null, _parametros);
        var r = await service.LancarAsync(p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);

        var segundo = r.Atendimento.Codigos.Single(c => c.Ordem == OrdemCodigo.Segundo);
        segundo.DataPrevistaFaturamento.Should().Be(new DateOnly(2026, 7, 13)); // +3 dias

        var snap = await _parametros.ObterAsync();
        snap.ValidadeConsultaDias(Convenio.UnimedIntercambio).Should().Be(25);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
