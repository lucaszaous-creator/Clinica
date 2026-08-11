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

    /// <summary>
    /// Confirmar presença gera o atendimento e os códigos — e NÃO cria horário nenhum.
    ///
    /// GUIA NÃO É ATENDIMENTO. O 2º código é obtido +24h depois pela SECRETÁRIA, no
    /// sistema do convênio; o paciente não volta para nada. Enquanto isso virava um
    /// `Agendamento`, a fila do balcão e a agenda dos MÉDICOS mostravam uma pessoa sem
    /// horário marcado — e o cartão fantasma vinha com "Entrou", que lançaria um
    /// atendimento novo e guias novas para uma sessão que nunca houve.
    /// </summary>
    [Fact]
    public async Task ConfirmarPresenca_GeraAtendimento_ENaoCriaHorarioParaOSegundoCodigo()
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

        // O 2º código existe como PENDÊNCIA de faturamento, com a data prevista…
        resultado.Atendimento.Codigos
            .Should().ContainSingle(c => c.Ordem == OrdemCodigo.Segundo)
            .Which.DataPrevistaFaturamento.Should().Be(DateOnly.FromDateTime(dia.Date.AddDays(1)));

        // …e NÃO como horário na agenda: o paciente não volta para obter guia.
        (await _db.Agendamentos.AsNoTracking().CountAsync()).Should().Be(1,
            "o único horário é o que a paciente de fato tinha");
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

    // ================================================================
    // QUEM LANÇOU (parcela 58)
    //
    // A direção pediu para ver de quem é cada lançamento. A trilha de auditoria já
    // responde "quem fez isso?" desde a parcela 21, mas ela é uma tela à parte, filtrada
    // por período — e a pergunta que se faz olhando a agenda é sobre AQUELA linha, agora.
    // ================================================================

    /// <summary>
    /// O horário guarda quem o marcou e quando. É o operador do LOGIN, e é a TELA que o
    /// informa: no balcão duas pessoas dividem a máquina, e o serviço não lê a sessão.
    /// </summary>
    [Fact]
    public async Task Agendar_GuardaQuemMarcouEQuando()
    {
        var pacienteId = await CriarPacienteAsync();
        var antes = DateTime.Now.AddSeconds(-1);

        var ag = await _agenda.AgendarAsync(
            pacienteId, new DateTime(2026, 7, 20, 14, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null, operador: "  Ana Paula  ");

        var gravado = await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == ag.Id);
        gravado.CriadoPor.Should().Be("Ana Paula", "o nome é aparado antes de gravar");
        gravado.CriadoEm.Should().NotBeNull().And.BeAfter(antes);
    }

    /// <summary>
    /// Sem operador informado, a autoria fica NULA — nunca uma string vazia nem o usuário
    /// do Windows. Nulo é o que faz a tela escrever "marcado antes de o sistema registrar
    /// quem lança"; "" apareceria como um nome em branco, indistinguível de "não carregou".
    /// </summary>
    [Fact]
    public async Task Agendar_SemOperador_NaoInventaAutoria()
    {
        var pacienteId = await CriarPacienteAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, new DateTime(2026, 7, 20, 14, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null, operador: "   ");

        var gravado = await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == ag.Id);
        gravado.CriadoPor.Should().BeNull();
    }

    /// <summary>
    /// O atendimento gerado pela confirmação de presença herda a autoria de quem clicou —
    /// e não a de quem marcou o horário semanas atrás. São dois atos e duas pessoas: o
    /// segundo é o que gera as GUIAS, e é sobre ele que a direção pergunta.
    /// </summary>
    [Fact]
    public async Task ConfirmarPresenca_GuardaQuemConcluiuOAtendimento()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, new DateTime(2026, 7, 20, 14, 0, 0),
            ModalidadeAtendimento.AcupunturaSimples, null, operador: "quem marcou");

        var resultado = await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "quem atendeu");

        var atendimento = await _db.Atendimentos.AsNoTracking()
            .SingleAsync(a => a.Id == resultado.Atendimento.Id);
        atendimento.LancadoPor.Should().Be("quem atendeu");
        atendimento.LancadoEm.Should().NotBeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
