using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public class ZzProvaTemp : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ProntuarioService _servico;

    public ZzProvaTemp()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var o = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(o);
        _db.Database.EnsureCreated();
        _servico = new ProntuarioService(new ClinicaRepositorio(_db));
    }

    [Fact]
    public async Task Campos_da_parcela_73_sobrevivem_ao_salvar()
    {
        var p = new Paciente { Nome = "Marisa", Convenio = Clinica.Domain.Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p); await _db.SaveChangesAsync();

        var salva = await _servico.SalvarAsync(new Evolucao
        {
            PacienteId = p.Id,
            Data = new DateOnly(2026, 8, 23),
            QueixaPrincipal = "lombalgia",
            HistoriaDoencaAtual = "ha 3 meses",
            ExameFisico = "dor a palpacao",
            HipoteseDiagnostica = "lombalgia mecanica",
            CidSessao = "M54.5"
        }, "medica");

        var lida = await _db.Evolucoes.AsNoTracking().FirstAsync(e => e.Id == salva.Id);
        lida.HistoriaDoencaAtual.Should().Be("ha 3 meses");
        lida.ExameFisico.Should().Be("dor a palpacao");
        lida.HipoteseDiagnostica.Should().Be("lombalgia mecanica");
        lida.CidSessao.Should().Be("M54.5");
    }

    [Fact]
    public async Task So_exame_e_hipotese_e_recusado()
    {
        var p = new Paciente { Nome = "Marisa", Convenio = Clinica.Domain.Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p); await _db.SaveChangesAsync();

        var acao = () => _servico.SalvarAsync(new Evolucao
        {
            PacienteId = p.Id,
            Data = new DateOnly(2026, 8, 23),
            ExameFisico = "dor a palpacao",
            HipoteseDiagnostica = "lombalgia mecanica"
        }, "medica");

        await acao.Should().NotThrowAsync();
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
