using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public class GlosaServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public GlosaServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    private async Task<CodigoFaturamento> CriarCodigoBaixadoAsync()
    {
        var p = new Paciente { Nome = "P", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var atendimentos = new AtendimentoService(_repo);
        var r = await atendimentos.LancarAsync(p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);
        var codigo = r.Atendimento.Codigos.First();
        await new FaturamentoService(_repo).DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), "G-1", "sec", null);
        return codigo;
    }

    [Fact]
    public async Task FluxoGlosa_Registrar_Reapresentar_Recuperar()
    {
        var codigo = await CriarCodigoBaixadoAsync();
        var glosas = new GlosaService(_repo);

        await glosas.RegistrarAsync(codigo.Id, new DateOnly(2026, 7, 20), "código inconsistente");
        (await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id)).Glosa.Should().Be(StatusGlosa.Glosada);

        (await glosas.ListarAsync(somenteEmAberto: true)).Should().ContainSingle(c => c.Id == codigo.Id);

        await glosas.ReapresentarAsync(codigo.Id, new DateOnly(2026, 7, 22));
        (await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id)).Glosa.Should().Be(StatusGlosa.Reapresentada);

        await glosas.MarcarRecuperadaAsync(codigo.Id);
        (await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id)).Glosa.Should().Be(StatusGlosa.Recuperada);

        // Recuperada não aparece mais entre as glosas em aberto.
        (await glosas.ListarAsync(somenteEmAberto: true)).Should().NotContain(c => c.Id == codigo.Id);
    }

    [Fact]
    public async Task NaoPodeGlosarGuiaNaoFaturada()
    {
        var p = new Paciente { Nome = "P", Convenio = Convenio.Amil, Sexo = Sexo.Masculino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaSimples);
        var codigo = r.Atendimento.Codigos.First(); // não baixado

        var acao = () => new GlosaService(_repo).RegistrarAsync(codigo.Id, new DateOnly(2026, 7, 20), "x");
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
