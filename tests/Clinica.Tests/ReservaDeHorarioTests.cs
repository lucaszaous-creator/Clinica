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
/// RESERVA DE HORÁRIO SEM FICHA (nov/2026, pedido do dono, caminho B). A reserva ocupa o horário de verdade, não cria
/// paciente, nada se apaga e "virar paciente" é uma transação só. Dados fictícios.
/// </summary>
public sealed class ReservaDeHorarioTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly ReservaDeHorarioService _reservas;
    private readonly int _medica;
    private static readonly DateTime Agora = new(2026, 11, 26, 9, 0, 0);
    private static readonly DateTime Amanha10 = new(2026, 11, 27, 10, 0, 0);
    private const string Fone = "(22) 99999-0001";

    public ReservaDeHorarioTests()
    {
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        var medica = new Profissional { Nome = "Dra. Fictícia", Ativo = true, DuracaoPadraoMinutos = 30 };
        _db.Profissionais.Add(medica);
        _db.SaveChanges();
        _medica = medica.Id;
        _repo = new ClinicaRepositorio(_db);
        var parametros = new ParametrosService(_repo);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo, parametros: parametros), parametros);
        _reservas = new ReservaDeHorarioService(_db, _agenda, new PacienteService(_repo), () => Agora);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    private NovaReserva Nova(DateTime? quando = null, string nome = "Maria Fictícia", string telefone = Fone, bool particular = true,
        string? convenio = null)
        => new(nome, telefone, "conversa-1", _medica, null, quando ?? Amanha10, 30, nameof(ModalidadeAtendimento.AcupunturaSimples),
            null, particular, convenio);

    private async Task<Paciente> PacienteAsync()
    {
        var p = new Paciente { Nome = "Paciente Existente", Telefone = "(22) 98888-0000", Convenio = Convenio.UnimedIntercambio };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task A_reserva_ocupa_o_horario_e_nao_cria_paciente()
    {
        var r = await _reservas.ReservarAsync(Nova(), "rayssa");

        r.Situacao.Should().Be(SituacaoDaReserva.Reservada);
        r.Telefone.Should().Be("5522999990001", "o telefone da conversa, normalizado");
        (await _db.Pacientes.CountAsync()).Should().Be(0, "reserva não é ficha");
        (await _db.Agendamentos.CountAsync()).Should().Be(0, "reserva não é agendamento");

        // Some da busca de vagas dos apps…
        var vagas = await new BuscaDeVagasService(_repo).ProximasAsync(_medica, Agora, 30, 400);
        vagas.Vagas.Select(v => v.Inicio).Should().NotContain(Amanha10);
        // …e trava a marcação comum por cima, com a frase que diz o que fazer.
        var p = await PacienteAsync();
        var marcar = () => _agenda.AgendarAsync(p.Id, Amanha10, ModalidadeAtendimento.AcupunturaSimples, null,
            profissionalId: _medica, duracaoMinutos: 30, particular: true);
        (await marcar.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*reservado*Virar paciente*");
        var conflitos = await _agenda.ConflitosAsync(Amanha10, 30, _medica);
        conflitos.Should().ContainSingle(c => c.Recurso == RecursoAgenda.Reserva && c.ImpedeMarcar);
    }

    [Fact]
    public async Task Nao_reserva_por_cima_de_horario_marcado_nem_de_outra_reserva()
    {
        var p = await PacienteAsync();
        await _agenda.AgendarAsync(p.Id, Amanha10, ModalidadeAtendimento.AcupunturaSimples, null, profissionalId: _medica,
            duracaoMinutos: 30, particular: true);
        await FluentActions.Awaiting(() => _reservas.ReservarAsync(Nova(), "rayssa"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Este horário não está livre*");

        await _reservas.ReservarAsync(Nova(Amanha10.AddHours(2)), "rayssa");
        await FluentActions.Awaiting(() => _reservas.ReservarAsync(Nova(Amanha10.AddHours(2).AddMinutes(15), telefone: "22999990002"), "ana"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*reservado*");
    }

    [Fact]
    public async Task So_o_minimo_e_valido_e_o_telefone_tem_limite_de_reservas_abertas()
    {
        await FluentActions.Awaiting(() => _reservas.ReservarAsync(Nova(nome: "  "), "rayssa"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Informe o nome*");
        await FluentActions.Awaiting(() => _reservas.ReservarAsync(Nova(particular: false), "rayssa"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Diga se é particular ou qual convênio.");
        await FluentActions.Awaiting(() => _reservas.ReservarAsync(Nova(Agora.AddHours(-1)), "rayssa"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*ainda não passou*");

        await _reservas.ReservarAsync(Nova(Amanha10, particular: false, convenio: "Unimed"), "rayssa");
        await _reservas.ReservarAsync(Nova(Amanha10.AddHours(1)), "rayssa");
        await FluentActions.Awaiting(() => _reservas.ReservarAsync(Nova(Amanha10.AddHours(2)), "rayssa"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage($"*já tem {ReservaDeHorarioRegras.LimiteAbertasPorTelefone} reservas abertas*");
    }

    [Fact]
    public async Task Remarcar_mantem_a_reserva_e_libera_o_horario_antigo()
    {
        var r = await _reservas.ReservarAsync(Nova(), "rayssa");
        var nova = await _reservas.RemarcarAsync(r.Id, Amanha10.AddHours(3), "rayssa");
        nova.Id.Should().Be(r.Id);
        (await _reservas.RemarcarAsync(r.Id, Amanha10.AddHours(3), "rayssa")).Versao.Should().Be(nova.Versao, "repetir não muda nada");
        (await _agenda.ConflitosAsync(Amanha10, 30, _medica)).Should().BeEmpty("o horário antigo voltou a ficar livre");
        (await _agenda.ConflitosAsync(Amanha10.AddHours(3), 30, _medica)).Should().ContainSingle(c => c.Recurso == RecursoAgenda.Reserva);
    }

    [Fact]
    public async Task Desmarcar_libera_a_vaga_e_a_reserva_fica_como_historico()
    {
        var r = await _reservas.ReservarAsync(Nova(), "rayssa");
        await FluentActions.Awaiting(() => _reservas.DesmarcarAsync(r.Id, "qualquer coisa", "rayssa"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Escolha o motivo*");

        await _reservas.DesmarcarAsync(r.Id, "A pessoa pediu", "rayssa");
        (await _reservas.DesmarcarAsync(r.Id, "A pessoa pediu", "rayssa")).Situacao.Should().Be(SituacaoDaReserva.Desmarcada, "idempotente");

        (await _agenda.ConflitosAsync(Amanha10, 30, _medica)).Should().BeEmpty();
        var guardada = await _db.ReservasDeHorario.AsNoTracking().SingleAsync();
        guardada.Situacao.Should().Be(SituacaoDaReserva.Desmarcada, "os dados não se apagam — fica como histórico");
        guardada.Nome.Should().Be("Maria Fictícia");
        guardada.MotivoDesmarcacao.Should().Be("A pessoa pediu");
    }

    [Fact]
    public async Task Virar_paciente_novo_vira_agendamento_no_mesmo_horario_numa_tacada_so_e_e_idempotente()
    {
        var r = await _reservas.ReservarAsync(Nova(), "rayssa");
        var novo = new Paciente { Documento = "52998224725", Convenio = Convenio.UnimedIntercambio, DataNascimento = new DateOnly(1990, 5, 1) }; // CPF fictício válido: o cadastro do balcão o exige

        var ag = await _reservas.VirarPacienteAsync(r.Id, new ComoVirarPaciente(null, novo), "rayssa");

        var ficha = await _db.Pacientes.AsNoTracking().SingleAsync();
        ficha.Nome.Should().Be("Maria Fictícia", "o nome vem da reserva");
        ficha.Telefone.Should().Be("5522999990001", "o telefone vem da reserva");
        ag.PacienteId.Should().Be(ficha.Id);
        ag.DataHora.Should().Be(Amanha10);
        ag.ProfissionalId.Should().Be(_medica);
        var reserva = await _db.ReservasDeHorario.AsNoTracking().SingleAsync();
        reserva.Situacao.Should().Be(SituacaoDaReserva.VirouAgendamento);
        reserva.AgendamentoId.Should().Be(ag.Id);
        reserva.PacienteId.Should().Be(ficha.Id);

        (await _reservas.VirarPacienteAsync(r.Id, new ComoVirarPaciente(null, new Paciente()), "rayssa")).Id.Should().Be(ag.Id);
        (await _db.Agendamentos.CountAsync()).Should().Be(1, "repetir devolve o mesmo agendamento");
        (await _db.Auditoria.CountAsync(e => e.Acao == "ReservaSemFichaVirouPaciente" && e.PacienteId == ficha.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Virar_paciente_com_ficha_existente_e_falha_no_cadastro_nao_deixa_nada()
    {
        var r = await _reservas.ReservarAsync(Nova(), "rayssa");
        var invalido = new Paciente { Documento = "123", Convenio = Convenio.UnimedIntercambio };
        await FluentActions.Awaiting(() => _reservas.VirarPacienteAsync(r.Id, new ComoVirarPaciente(null, invalido), "rayssa"))
            .Should().ThrowAsync<ArgumentException>();
        _db.ChangeTracker.Clear();
        (await _db.Pacientes.CountAsync()).Should().Be(0, "o cadastro falhou: nada fica");
        (await _db.ReservasDeHorario.AsNoTracking().SingleAsync()).Situacao.Should().Be(SituacaoDaReserva.Reservada);

        var existente = await PacienteAsync();
        var ag = await _reservas.VirarPacienteAsync(r.Id, new ComoVirarPaciente(existente.Id, null), "rayssa");
        ag.PacienteId.Should().Be(existente.Id);
        (await _db.Pacientes.CountAsync()).Should().Be(1, "com ficha existente nenhuma ficha nova nasce");
    }

    [Fact]
    public async Task A_trilha_registra_quem_criou_viu_e_alterou_sem_o_nome_nem_o_telefone()
    {
        var r = await _reservas.ReservarAsync(Nova(), "rayssa");
        await _reservas.RemarcarAsync(r.Id, Amanha10.AddHours(1), "ana");
        (await _reservas.DoTelefoneAsync(Fone, "bianca")).Should().ContainSingle();

        var trilha = await _db.Auditoria.AsNoTracking().Where(e => e.Acao.StartsWith("ReservaSemFicha")).OrderBy(e => e.Id).ToListAsync();
        trilha.Select(e => (e.Acao, e.Operador)).Should().Equal(
            ("ReservaSemFichaCriada", "rayssa"), ("ReservaSemFichaRemarcada", "ana"), ("ReservaSemFichaConsultada", "bianca"));
        trilha.Should().OnlyContain(e => !e.Detalhe!.Contains("Maria") && !e.Detalhe.Contains("99999"), "dado mínimo na trilha");
    }

    [Fact]
    public async Task Do_dia_traz_todas_as_situacoes_para_a_grade()
    {
        var a = await _reservas.ReservarAsync(Nova(), "rayssa");
        await _reservas.ReservarAsync(Nova(Amanha10.AddHours(2), telefone: "22999990002"), "rayssa");
        await _reservas.DesmarcarAsync(a.Id, "Não respondeu", "rayssa");
        var doDia = await _reservas.DoDiaAsync(DateOnly.FromDateTime(Amanha10));
        doDia.Select(r => r.Situacao).Should().Equal(SituacaoDaReserva.Desmarcada, SituacaoDaReserva.Reservada);
        doDia.Should().OnlyContain(r => r.Profissional != null);
    }
}
