using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public class AgendaServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;

    public AgendaServiceTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
    }

    private async Task<int> CriarPacienteAsync(Convenio convenio = Convenio.UnimedIntercambio)
    {
        var p = new Paciente { Nome = "Paciente", Convenio = convenio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Agendar_CriaAgendamentoNoDia()
    {
        var pacienteId = await CriarPacienteAsync();
        var dia = new DateTime(2026, 7, 20, 14, 0, 0);

        await _agenda.AgendarAsync(pacienteId, dia, ModalidadeAtendimento.AcupunturaComEletro, "primeira sessão");

        var doDia = await _agenda.DoDiaAsync(DateOnly.FromDateTime(dia));
        doDia.Should().ContainSingle().Which.Status.Should().Be(StatusAgendamento.Agendado);
    }

    [Fact]
    public async Task ConfirmarPresenca_GeraAtendimentoERetornoSugerido()
    {
        var pacienteId = await CriarPacienteAsync(Convenio.UnimedIntercambio);
        var dia = new DateTime(2026, 7, 20, 14, 0, 0);
        var ag = await _agenda.AgendarAsync(pacienteId, dia, ModalidadeAtendimento.AcupunturaComEletro, null);

        var resultado = await _agenda.ConfirmarPresencaAsync(ag.Id);

        // Gerou atendimento com os 2 códigos (acupuntura + eletro 2º).
        resultado.Atendimento.Codigos.Should().HaveCount(2);

        // O agendamento virou "Realizado" e ficou vinculado ao atendimento.
        var atualizado = await _db.Agendamentos.AsNoTracking().FirstAsync(a => a.Id == ag.Id);
        atualizado.Status.Should().Be(StatusAgendamento.Realizado);
        atualizado.AtendimentoId.Should().NotBeNull();

        // Criou um retorno sugerido (+24h) para obter o 2º código.
        var retorno = await _db.Agendamentos.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Origem == OrigemAgendamento.RetornoSugerido);
        retorno.Should().NotBeNull();
        retorno!.DataHora.Date.Should().Be(dia.Date.AddDays(1));
    }

    [Fact]
    public async Task ConfirmarPresenca_DuasVezes_Falha()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 7, 20, 9, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null);

        await _agenda.ConfirmarPresencaAsync(ag.Id);
        var acao = () => _agenda.ConfirmarPresencaAsync(ag.Id);
        await acao.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancelar_MudaStatus()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 7, 20, 9, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null);

        await _agenda.CancelarAsync(ag.Id);

        (await _db.Agendamentos.AsNoTracking().FirstAsync(a => a.Id == ag.Id))
            .Status.Should().Be(StatusAgendamento.Cancelado);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
