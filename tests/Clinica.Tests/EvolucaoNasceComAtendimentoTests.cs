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
/// A EVOLUÇÃO NASCE COM O NÚMERO DO ATENDIMENTO (set/2026 — pedido da direção: "a evolução
/// precisa nascer com número do atendimento, o mesmo que é gerado quando é feita a
/// agenda/atendimento na recepção").
///
/// O buraco que estes testes fecham: no regime atual (chave "guia no agendamento"
/// DESLIGADA) o médico escreve a sessão ANTES de o atendimento existir — o Finalizar grava
/// primeiro e conclui depois —, e a evolução ficava com <c>AtendimentoId</c> nulo para
/// sempre. Nada ligava os dois depois. São duas metades, e uma sem a outra deixa um caso
/// de fora:
/// <list type="bullet">
///   <item>o atendimento que NASCE amarra as evoluções já escritas do horário, no mesmo
///   commit (<c>AgendaService.ConfirmarNucleoAsync</c>);</item>
///   <item>a evolução gravada quando o atendimento JÁ EXISTE resolve o número pelo horário,
///   mesmo que a tela não o saiba (<c>ProntuarioService.SalvarAsync</c>).</item>
/// </list>
/// </summary>
public class EvolucaoNasceComAtendimentoTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly ParametrosService _parametros;
    private readonly AgendaService _agenda;
    private readonly ProntuarioService _prontuario;
    private readonly int _profissionalId;

    public EvolucaoNasceComAtendimentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(options);
        _db.Database.EnsureCreated();
        var prof = new Profissional { Nome = "Dra. Ana" };
        _db.Profissionais.Add(prof);
        _db.SaveChanges();
        _profissionalId = prof.Id;
        _repo = new ClinicaRepositorio(_db);
        _parametros = new ParametrosService(_repo);
        _agenda = new AgendaService(
            _repo, new AtendimentoService(_repo, parametros: _parametros), _parametros);
        _prontuario = new ProntuarioService(_repo);
    }

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    private static DateTime HojeAs9 => DateTime.Today.AddHours(9);

    private Task<Evolucao> EscreverSessaoAsync(int pacienteId, int agendamentoId, int? profissionalId = null)
        => _prontuario.SalvarAsync(new Evolucao
        {
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            AgendamentoId = agendamentoId,
            // A tela do médico manda o AtendimentoId de quando ABRIU: aqui ela abriu antes
            // de ele existir, e é esse o caso que o pedido da direção cobre.
            AtendimentoId = null,
            Data = DateOnly.FromDateTime(HojeAs9),
            QueixaPrincipal = "lombalgia",
            Conduta = "agulhamento lombar"
        }, "medica");

    [Fact]
    public async Task Sessao_escrita_ANTES_de_concluir_ganha_o_atendimento_no_instante_em_que_ele_nasce()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, HojeAs9, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profissionalId);
        ag.AtendimentoId.Should().BeNull("chave desligada: o atendimento só nasce na conclusão");

        // O fluxo do médico: escreve a sessão (passo 1)…
        var evolucao = await EscreverSessaoAsync(pacienteId, ag.Id, _profissionalId);
        evolucao.AtendimentoId.Should().BeNull("ainda não há atendimento a que apontar");

        // …e conclui (passo 3): o atendimento nasce, com o número.
        var resultado = await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "medica");

        var relida = await _db.Evolucoes.AsNoTracking().SingleAsync(e => e.Id == evolucao.Id);
        relida.AtendimentoId.Should().Be(resultado.Atendimento.Id,
            "a evolução escrita antes tem de sair amarrada ao atendimento que acabou de nascer");
        resultado.Atendimento.Numero.Should().NotBeNullOrEmpty("o número é o do atendimento da recepção");
    }

    [Fact]
    public async Task Sessao_gravada_quando_o_atendimento_JA_EXISTE_nasce_com_ele_mesmo_sem_a_tela_saber()
    {
        var pacienteId = await CriarPacienteAsync();
        // Regime "guia no agendamento": o atendimento nasce na MARCAÇÃO.
        await _parametros.DefinirGuiaNoAgendamentoAsync(true);
        var ag = await _agenda.AgendarAsync(
            pacienteId, HojeAs9, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profissionalId);
        ag.AtendimentoId.Should().NotBeNull();

        var evolucao = await EscreverSessaoAsync(pacienteId, ag.Id, _profissionalId);

        evolucao.AtendimentoId.Should().Be(ag.AtendimentoId,
            "quem sabe o atendimento é o HORÁRIO — a tela mandou nulo e a gravação resolveu");
    }

    [Fact]
    public async Task A_evolucao_do_COLEGA_que_cobriu_o_horario_tambem_e_amarrada()
    {
        var pacienteId = await CriarPacienteAsync();
        var colega = new Profissional { Nome = "Dr. Bruno" };
        _db.Profissionais.Add(colega);
        await _db.SaveChangesAsync();

        var ag = await _agenda.AgendarAsync(
            pacienteId, HojeAs9, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profissionalId);
        var evolucao = await EscreverSessaoAsync(pacienteId, ag.Id, colega.Id);

        var resultado = await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "bruno");

        (await _db.Evolucoes.AsNoTracking().SingleAsync(e => e.Id == evolucao.Id))
            .AtendimentoId.Should().Be(resultado.Atendimento.Id,
                "o vínculo é pelo HORÁRIO, não pelo autor — 'todos atendem todos'");
    }

    [Fact]
    public async Task Sessao_CANCELADA_e_sessao_de_OUTRO_horario_nao_sao_amarradas()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, HojeAs9, ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profissionalId);
        var outro = await _agenda.AgendarAsync(
            pacienteId, HojeAs9.AddDays(7), ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profissionalId);

        var cancelada = await EscreverSessaoAsync(pacienteId, ag.Id, _profissionalId);
        await _prontuario.CancelarAsync(cancelada.Id, motivo: "registrada no paciente errado", operador: "medica");
        var deOutroHorario = await EscreverSessaoAsync(pacienteId, outro.Id, _profissionalId);

        await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "medica");

        var evolucoes = await _db.Evolucoes.AsNoTracking().ToListAsync();
        evolucoes.Single(e => e.Id == cancelada.Id).AtendimentoId.Should().BeNull(
            "registro desdito não sustenta guia nenhuma");
        evolucoes.Single(e => e.Id == deOutroHorario.Id).AtendimentoId.Should().BeNull(
            "a sessão da semana que vem não é deste atendimento");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
