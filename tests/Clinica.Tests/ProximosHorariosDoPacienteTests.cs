using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// Os próximos horários do paciente na ficha (set/2026). Executa a consulta nova do
/// repositório — a tradução acontece em runtime, e método sem chamador em teste é código
/// que ninguém rodou.
/// </summary>
public class ProximosHorariosDoPacienteTests : IDisposable
{
    private static readonly DateTime Agora = new(2026, 9, 10, 8, 0, 0);

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly int _paciente;
    private readonly int _prof;

    public ProximosHorariosDoPacienteTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);

        var paciente = new Paciente { Nome = "Maria Silva" };
        var prof = new Profissional { Nome = "Ana Lima", NomeCurto = "Dra. Ana" };
        var sala = new Sala { Nome = "Sala 2" };
        _db.AddRange(paciente, prof, sala);
        _db.SaveChanges();
        _paciente = paciente.Id;
        _prof = prof.Id;

        void Horario(DateTime quando, StatusAgendamento status = StatusAgendamento.Agendado, int? pacienteId = null)
            => _db.Agendamentos.Add(new Agendamento
            {
                PacienteId = pacienteId ?? _paciente,
                ProfissionalId = _prof,
                SalaId = sala.Id,
                DataHora = quando,
                ModalidadePrevista = ModalidadeAtendimento.Consulta,
                ModalidadeCodigo = nameof(ModalidadeAtendimento.Consulta),
                Status = status
            });

        Horario(Agora.AddDays(-1));                                   // passado: fora
        Horario(Agora.AddHours(2));                                   // hoje mais tarde: entra
        Horario(Agora.AddDays(3), StatusAgendamento.Cancelado);       // cancelado: fora
        Horario(Agora.AddDays(1));                                    // amanhã: entra
        Horario(Agora.AddDays(7));                                    // semana que vem: entra
        var outro = new Paciente { Nome = "Outro" };
        _db.Pacientes.Add(outro);
        _db.SaveChanges();
        Horario(Agora.AddDays(2), pacienteId: outro.Id);              // de outro paciente: fora
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Traz_so_os_agendados_deste_paciente_a_partir_de_agora_em_ordem()
    {
        var horarios = await _repo.AgendamentosFuturosDoPacienteAsync(_paciente, Agora, limite: 5);

        horarios.Select(h => h.DataHora).Should().Equal(
            Agora.AddHours(2), Agora.AddDays(1), Agora.AddDays(7));
        horarios.Should().OnlyContain(h => h.Status == StatusAgendamento.Agendado);
    }

    [Fact]
    public async Task Respeita_o_teto_e_carrega_profissional_e_sala()
    {
        var horarios = await _repo.AgendamentosFuturosDoPacienteAsync(_paciente, Agora, limite: 2);

        horarios.Should().HaveCount(2);
        horarios[0].Profissional!.Rotulo.Should().Be("Dra. Ana");
        horarios[0].Sala!.Nome.Should().Be("Sala 2");
    }
}
