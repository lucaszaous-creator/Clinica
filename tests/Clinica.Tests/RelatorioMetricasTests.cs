using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>Métricas novas do relatório: taxa de glosa e tempo médio de baixa.</summary>
public class RelatorioMetricasTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public RelatorioMetricasTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    [Fact]
    public async Task Gerar_CalculaTaxaDeGlosaETempoMedioDeBaixa()
    {
        var p = new Paciente { Nome = "P", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();

        var atendimentos = new AtendimentoService(_repo);
        var faturamento = new FaturamentoService(_repo);

        // Guia 1: baixada em +1 dia, depois glosada.
        var r1 = await atendimentos.LancarAsync(p.Id, new DateOnly(2026, 7, 6), ModalidadeAtendimento.AcupunturaSimples);
        var c1 = r1.Atendimento.Codigos.First();
        await faturamento.DarBaixaAsync(c1.Id, new DateOnly(2026, 7, 7), "G-1", "sec", null);
        c1.RegistrarGlosa(new DateOnly(2026, 7, 15), "m", "1201", 30);
        await _db.SaveChangesAsync();

        // Guia 2: baixada em +3 dias, sem glosa.
        var r2 = await atendimentos.LancarAsync(p.Id, new DateOnly(2026, 7, 13), ModalidadeAtendimento.AcupunturaSimples);
        var c2 = r2.Atendimento.Codigos.First();
        await faturamento.DarBaixaAsync(c2.Id, new DateOnly(2026, 7, 16), "G-2", "sec", null);

        var rel = await new RelatorioService(_repo).GerarAsync(
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), new DateOnly(2026, 7, 19));

        rel.Resumo.Baixados.Should().Be(2);
        rel.Resumo.Glosadas.Should().Be(1);
        rel.Resumo.TaxaGlosa.Should().Be(50);
        rel.Resumo.TempoMedioBaixaDias.Should().Be(2); // média de 1 e 3 dias

        var porConvenio = rel.PorConvenio.Single(c => c.Convenio == Convenio.UnimedIntercambio);
        porConvenio.Glosadas.Should().Be(1);
        porConvenio.TaxaGlosa.Should().Be(50);
        porConvenio.TempoMedioBaixaDias.Should().Be(2);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
