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

    // ===== Remarcação =====
    // Remarcar preserva o agendamento em vez de cancelar e recriar: um cancelamento que
    // nunca aconteceu falseia o histórico da recepção.

    [Fact]
    public async Task Remarcar_MoveOHorarioMantendoOMesmoAgendamento()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 7, 20, 14, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, "encaixe");

        await _agenda.RemarcarAsync(ag.Id, new DateTime(2026, 7, 20, 15, 0, 0), "encaixe");

        var todos = await _db.Agendamentos.AsNoTracking().ToListAsync();
        todos.Should().HaveCount(1, "remarcar não pode criar um segundo registro");
        todos[0].Id.Should().Be(ag.Id);
        todos[0].DataHora.Should().Be(new DateTime(2026, 7, 20, 15, 0, 0));
        todos[0].Status.Should().Be(StatusAgendamento.Agendado);
        todos[0].Observacoes.Should().Be("encaixe");
    }

    [Fact]
    public async Task Remarcar_TrazDeVoltaUmHorarioCancelado()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 7, 20, 9, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null);
        await _agenda.CancelarAsync(ag.Id);

        await _agenda.RemarcarAsync(ag.Id, new DateTime(2026, 7, 22, 9, 0, 0), null);

        (await _db.Agendamentos.AsNoTracking().FirstAsync(a => a.Id == ag.Id))
            .Status.Should().Be(StatusAgendamento.Agendado);
    }

    [Fact]
    public async Task Remarcar_RecusaHorarioQueJaVirouAtendimento()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 7, 20, 9, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null);
        await _agenda.ConfirmarPresencaAsync(ag.Id);

        var acao = () => _agenda.RemarcarAsync(ag.Id, new DateTime(2026, 7, 21, 9, 0, 0), null);

        await acao.Should().ThrowAsync<InvalidOperationException>(
            "o atendimento e seus códigos já existem; o caminho é estornar antes");
    }

    [Fact]
    public async Task Conflito_NaoAcusaOProprioAgendamentoAoRemarcar()
    {
        var pacienteId = await CriarPacienteAsync();
        var hora = new DateTime(2026, 7, 20, 9, 0, 0);
        var ag = await _agenda.AgendarAsync(pacienteId, hora, ModalidadeAtendimento.AcupunturaSimples, null);

        var conflitoComEle = await _agenda.ConflitoAsync(hora);
        var conflitoIgnorandoEle = await _agenda.ConflitoAsync(hora, ignorarAgendamentoId: ag.Id);

        conflitoComEle.Should().NotBeNull();
        conflitoIgnorandoEle.Should().BeNull("mudar só a modalidade não pode acusar choque consigo mesmo");
    }

    [Fact]
    public async Task Cancelar_DeixaRastroNaAuditoria()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 7, 20, 9, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null);

        await _agenda.CancelarAsync(ag.Id, "secretaria");

        var evento = await _db.Auditoria.AsNoTracking().SingleAsync();
        evento.Acao.Should().Be("AgendamentoCancelado");
        evento.Operador.Should().Be("secretaria");
        evento.PacienteId.Should().Be(pacienteId);
    }

    [Fact]
    public async Task Remarcar_DeixaRastroComOHorarioAnterior()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(pacienteId, new DateTime(2026, 7, 20, 14, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null);

        await _agenda.RemarcarAsync(ag.Id, new DateTime(2026, 7, 21, 16, 30, 0), null,
            operador: "secretaria");

        var evento = await _db.Auditoria.AsNoTracking().SingleAsync();
        evento.Acao.Should().Be("AgendamentoRemarcado");
        evento.Detalhe.Should().Contain("20/07/2026 14:00").And.Contain("21/07/2026 16:30");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
