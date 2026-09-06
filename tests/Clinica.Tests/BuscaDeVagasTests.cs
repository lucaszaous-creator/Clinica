using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A busca de vagas (set/2026): o cálculo puro e o serviço que executa as consultas.
/// </summary>
public class BuscaDeVagasTests : IDisposable
{
    // Segunda-feira, 14/09/2026, 8h.
    private static readonly DateTime Segunda8h = new(2026, 9, 14, 8, 0, 0);

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public BuscaDeVagasTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Profissional Ana(int? dias = null, TimeOnly? das = null, TimeOnly? ate = null)
        => new() { Id = 7, Nome = "Ana Lima", NomeCurto = "Dra. Ana", DiasDeAtendimento = dias, AtendeDas = das, AtendeAte = ate };

    private static Agendamento Marcado(DateTime quando, int minutos = 30, int profissionalId = 7,
        StatusAgendamento status = StatusAgendamento.Agendado)
        => new() { PacienteId = 1, ProfissionalId = profissionalId, DataHora = quando, DuracaoMinutos = minutos, Status = status };

    // ==================== O cálculo ====================

    [Fact]
    public void Sem_jornada_presume_a_grade_de_segunda_a_sabado_e_comeca_no_instante_pedido()
    {
        var vagas = BuscaDeVagas.Calcular(Segunda8h.AddHours(10).AddMinutes(45), 30, Ana(), [], [], quantidade: 3);

        // Já passou das 18h45: a primeira vaga é 19h00 (passo de 30 min, dentro de 7h–20h).
        vagas.Select(v => v.Inicio).Should().Equal(
            Segunda8h.AddHours(11), Segunda8h.AddHours(11).AddMinutes(30), Segunda8h.AddDays(1).AddHours(-1));
        BuscaDeVagas.JornadaPresumida(Ana()).Should().BeTrue();
    }

    [Fact]
    public void Domingo_nao_e_oferecido_sem_jornada_declarada()
    {
        var sabado20h = new DateTime(2026, 9, 19, 20, 0, 0);
        var vagas = BuscaDeVagas.Calcular(sabado20h, 30, Ana(), [], [], quantidade: 1);

        vagas.Single().Inicio.Should().Be(new DateTime(2026, 9, 21, 7, 0, 0)); // segunda
    }

    [Fact]
    public void A_jornada_declarada_manda_nos_dias_e_nas_horas()
    {
        var segQuaSex = Profissional.BitDe(DayOfWeek.Monday) | Profissional.BitDe(DayOfWeek.Wednesday) | Profissional.BitDe(DayOfWeek.Friday);
        var ana = Ana(segQuaSex, new TimeOnly(14, 0), new TimeOnly(16, 0));

        var vagas = BuscaDeVagas.Calcular(Segunda8h, 60, ana, [], [], quantidade: 4);

        // O passo é de 30 min (a grade da casa), então uma sessão de 60 min cabe às 14h00,
        // 14h30 e 15h00 — e a terça, fora da jornada, é PULADA: a quarta é a próxima.
        vagas.Select(v => v.Inicio).Should().Equal(
            Segunda8h.AddHours(6), Segunda8h.AddHours(6).AddMinutes(30), Segunda8h.AddHours(7),
            Segunda8h.AddDays(2).AddHours(6));
        vagas.Should().OnlyContain(v => v.Fim.TimeOfDay <= new TimeSpan(16, 0, 0));
    }

    [Fact]
    public void O_que_esta_marcado_e_o_que_esta_fechado_nao_e_vaga_e_cancelado_libera()
    {
        var ana = Ana(null, new TimeOnly(8, 0), new TimeOnly(10, 0));
        var ocupados = new List<Agendamento>
        {
            Marcado(Segunda8h, minutos: 60),                                   // 8h–9h ocupado
            Marcado(Segunda8h.AddHours(1), status: StatusAgendamento.Cancelado) // 9h cancelado: livre
        };
        var bloqueios = new List<BloqueioAgenda>
        {
            new() { ProfissionalId = 7, Inicio = Segunda8h.AddHours(1).AddMinutes(30), Fim = Segunda8h.AddHours(2), Motivo = "reunião" }
        };

        var vagas = BuscaDeVagas.Calcular(Segunda8h, 30, ana, ocupados, bloqueios, quantidade: 2);

        vagas.Select(v => v.Inicio).Should().Equal(
            Segunda8h.AddHours(1),            // 9h00: o cancelado não ocupa
            Segunda8h.AddDays(1));            // 9h30 é reunião; 10h não cabe; próximo dia
    }

    [Fact]
    public void Horario_de_outro_profissional_nao_conta_e_bloqueio_da_clinica_inteira_conta()
    {
        var ana = Ana(null, new TimeOnly(8, 0), new TimeOnly(9, 0));
        var ocupados = new List<Agendamento> { Marcado(Segunda8h, profissionalId: 99) };
        var bloqueios = new List<BloqueioAgenda>
        {
            new() { Inicio = Segunda8h.AddMinutes(30), Fim = Segunda8h.AddHours(1), Motivo = "feriado da clínica" }
        };

        var vagas = BuscaDeVagas.Calcular(Segunda8h, 30, ana, ocupados, bloqueios, quantidade: 2);

        vagas.Select(v => v.Inicio).Should().Equal(Segunda8h, Segunda8h.AddDays(1));
    }

    [Fact]
    public void Sem_vaga_em_dois_meses_devolve_vazio_e_o_criterio_diz_o_que_procurou()
    {
        var ana = Ana(Profissional.BitDe(DayOfWeek.Monday), new TimeOnly(8, 0), new TimeOnly(8, 30));
        // Toda segunda das 8h ocupada por 60 dias.
        var ocupados = Enumerable.Range(0, 10).Select(i => Marcado(Segunda8h.AddDays(7 * i))).ToList();

        var vagas = BuscaDeVagas.Calcular(Segunda8h, 30, ana, ocupados, [], quantidade: 5);
        var resultado = new ResultadoBuscaDeVagas("Dra. Ana", 30, Segunda8h, Segunda8h.AddDays(60), false, ana.DescricaoJornada, vagas);

        resultado.Vazio.Should().BeTrue();
        resultado.Criterio.Should().Contain("30 min").And.Contain("Dra. Ana").And.Contain("seg, das 08:00 às 08:30");
        new Vaga(Segunda8h, Segunda8h.AddMinutes(30)).Rotulo.Should().Be("seg 14/09 · 08:00–08:30");
    }

    // ==================== O serviço ====================

    [Fact]
    public async Task O_servico_le_o_profissional_os_horarios_dele_e_os_bloqueios()
    {
        var ana = new Profissional { Nome = "Ana Lima", NomeCurto = "Dra. Ana", DuracaoPadraoMinutos = 45 };
        var paciente = new Paciente { Nome = "Maria" };
        _db.AddRange(ana, paciente);
        await _db.SaveChangesAsync();
        _db.Agendamentos.Add(new Agendamento
        {
            PacienteId = paciente.Id, ProfissionalId = ana.Id, DataHora = Segunda8h,
            ModalidadePrevista = ModalidadeAtendimento.Consulta, ModalidadeCodigo = "Consulta"
        });
        _db.BloqueiosAgenda.Add(new BloqueioAgenda
        {
            ProfissionalId = ana.Id, Inicio = Segunda8h.AddMinutes(45), Fim = Segunda8h.AddHours(2), Motivo = "curso"
        });
        await _db.SaveChangesAsync();

        var resultado = await new BuscaDeVagasService(_repo).ProximasAsync(ana.Id, Segunda8h, quantidade: 2);

        // Duração padrão DELE (45): 8h00 ocupado, 8h45 até 10h fechado, 10h00 é a primeira.
        resultado.DuracaoMinutos.Should().Be(45);
        resultado.Profissional.Should().Be("Dra. Ana");
        resultado.JornadaPresumida.Should().BeTrue();
        resultado.Vagas.Select(v => v.Inicio).Should().Equal(Segunda8h.AddHours(2), Segunda8h.AddHours(2).AddMinutes(30));
    }
}
