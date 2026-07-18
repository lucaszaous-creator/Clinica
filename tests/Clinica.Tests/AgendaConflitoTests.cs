using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Tests;

public class AgendaConflitoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly AgendaService _agenda;

    public AgendaConflitoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(repo, new AtendimentoService(repo));
    }

    private async Task<int> CriarPacienteAsync(string nome)
    {
        var p = new Paciente { Nome = nome, Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task HorarioOcupado_AcusaConflito()
    {
        var pacienteId = await CriarPacienteAsync("Maria");
        var horario = new DateTime(2026, 7, 20, 8, 0, 0);
        await _agenda.AgendarAsync(pacienteId, horario, ModalidadeAtendimento.AcupunturaComEletro, null);

        var conflito = await _agenda.ConflitoAsync(horario);

        conflito.Should().NotBeNull();
        conflito!.PacienteId.Should().Be(pacienteId);
    }

    [Fact]
    public async Task HorarioLivre_OuApenasCancelado_NaoAcusaConflito()
    {
        var pacienteId = await CriarPacienteAsync("João");
        var horario = new DateTime(2026, 7, 20, 9, 0, 0);
        var ag = await _agenda.AgendarAsync(pacienteId, horario, ModalidadeAtendimento.AcupunturaComEletro, null);
        await _agenda.CancelarAsync(ag.Id);

        (await _agenda.ConflitoAsync(horario)).Should().BeNull();
        (await _agenda.ConflitoAsync(horario.AddHours(1))).Should().BeNull();
    }

    [Fact]
    public async Task NoPeriodo_TrazTodaASemana()
    {
        var pacienteId = await CriarPacienteAsync("Ana");
        await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 7, 20, 8, 0, 0), ModalidadeAtendimento.AcupunturaComEletro, null);
        await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 7, 24, 10, 0, 0), ModalidadeAtendimento.AcupunturaComEletro, null);

        var semana = await _agenda.NoPeriodoAsync(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 26));

        semana.Should().HaveCount(2);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
