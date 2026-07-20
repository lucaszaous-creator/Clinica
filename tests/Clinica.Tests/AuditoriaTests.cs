using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

/// <summary>
/// A trilha de auditoria responde "quem fez o quê e quando" — cada ação que altera o
/// faturamento (baixa, estorno, glosa, lote) precisa deixar um evento gravado.
/// </summary>
public class AuditoriaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public AuditoriaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    private async Task<CodigoFaturamento> CriarCodigoAsync()
    {
        var p = new Paciente { Nome = "Paciente", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var r = await new AtendimentoService(_repo).LancarAsync(
            p.Id, new DateOnly(2026, 7, 10), ModalidadeAtendimento.AcupunturaComEletro);
        return r.Atendimento.Codigos.First();
    }

    [Fact]
    public async Task DarBaixa_GravaEventoComOperadorEGuia()
    {
        var codigo = await CriarCodigoAsync();

        await new FaturamentoService(_repo).DarBaixaAsync(
            codigo.Id, new DateOnly(2026, 7, 11), "G-123", "maria", null);

        var eventos = await _repo.EventosAuditoriaAsync();
        eventos.Should().ContainSingle(e => e.Acao == "BaixaGuia");
        var evento = eventos.Single(e => e.Acao == "BaixaGuia");
        evento.Operador.Should().Be("maria");
        evento.CodigoId.Should().Be(codigo.Id);
        evento.Detalhe.Should().Contain("G-123");
    }

    [Fact]
    public async Task EstornarBaixa_GravaEventoComGuiaAnterior()
    {
        var codigo = await CriarCodigoAsync();
        var faturamento = new FaturamentoService(_repo);
        await faturamento.DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), "G-123", "maria", null);

        await faturamento.EstornarBaixaAsync(codigo.Id, "baixa por engano", "joana");

        var eventos = await _repo.EventosAuditoriaAsync();
        var estorno = eventos.Should().ContainSingle(e => e.Acao == "EstornoBaixa").Subject;
        estorno.Operador.Should().Be("joana");
        estorno.Detalhe.Should().Contain("baixa por engano").And.Contain("G-123");
    }

    [Fact]
    public async Task Glosa_GravaEventosDoCicloCompleto()
    {
        var codigo = await CriarCodigoAsync();
        await new FaturamentoService(_repo).DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), "G-9", "maria", null);

        var glosas = new GlosaService(_repo);
        await glosas.RegistrarAsync(codigo.Id, new DateOnly(2026, 7, 20), "carteirinha inválida", "1801", "maria");
        await glosas.ReapresentarAsync(codigo.Id, new DateOnly(2026, 7, 22), "maria");
        await glosas.MarcarRecuperadaAsync(codigo.Id, "maria");

        var acoes = (await _repo.EventosAuditoriaAsync()).Select(e => e.Acao).ToList();
        acoes.Should().Contain("Glosa").And.Contain("GlosaReapresentada").And.Contain("GlosaRecuperada");
    }

    [Fact]
    public async Task OperadorNaoInformado_FicaComInterrogacao()
    {
        var codigo = await CriarCodigoAsync();

        await new FaturamentoService(_repo).DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), null, null, null);

        (await _repo.EventosAuditoriaAsync()).Single(e => e.Acao == "BaixaGuia").Operador.Should().Be("?");
    }

    [Fact]
    public async Task EventosVemDoMaisRecenteAoMaisAntigo_ELimitados()
    {
        var codigo = await CriarCodigoAsync();
        var faturamento = new FaturamentoService(_repo);
        await faturamento.DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), "G-1", "a", null);
        await faturamento.EstornarBaixaAsync(codigo.Id, "engano", "b");
        await faturamento.DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 12), "G-2", "c", null);

        var todos = await _repo.EventosAuditoriaAsync();
        todos.Should().HaveCount(3);
        todos.First().Detalhe.Should().Contain("G-2"); // o mais recente primeiro

        (await _repo.EventosAuditoriaAsync(limite: 2)).Should().HaveCount(2);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
