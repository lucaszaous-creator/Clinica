using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

public class FaturamentoServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly FaturamentoService _faturamento;

    public FaturamentoServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _faturamento = new FaturamentoService(_repo);
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
    public async Task DarBaixa_RegistraDataGuiaEUsuario()
    {
        var codigo = await CriarCodigoAsync();

        await _faturamento.DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), "G-123", "secretaria", "ok");

        var salvo = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id);
        salvo.Baixado.Should().BeTrue();
        salvo.Status.Should().Be(StatusCodigo.Baixado);
        salvo.DataBaixa.Should().Be(new DateOnly(2026, 7, 11));
        salvo.NumeroGuiaReal.Should().Be("G-123");
        salvo.UsuarioBaixa.Should().Be("secretaria");
    }

    [Fact]
    public async Task EstornarBaixa_ReabreAPendencia()
    {
        var codigo = await CriarCodigoAsync();
        await _faturamento.DarBaixaAsync(codigo.Id, new DateOnly(2026, 7, 11), "G-123", "secretaria", null);

        await _faturamento.EstornarBaixaAsync(codigo.Id, "baixado por engano", "gerente");

        var salvo = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id);
        salvo.Baixado.Should().BeFalse();
        salvo.Status.Should().Be(StatusCodigo.Aberto);
        salvo.DataBaixa.Should().BeNull();
        salvo.NumeroGuiaReal.Should().BeNull();
        salvo.UsuarioBaixa.Should().BeNull();
    }

    [Fact]
    public async Task EstornarBaixa_DeCodigoAberto_NaoFazNada()
    {
        var codigo = await CriarCodigoAsync();

        await _faturamento.EstornarBaixaAsync(codigo.Id, "sem efeito", "gerente");

        var salvo = await _db.Codigos.AsNoTracking().FirstAsync(c => c.Id == codigo.Id);
        salvo.Baixado.Should().BeFalse();
        salvo.Status.Should().Be(StatusCodigo.Aberto);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
