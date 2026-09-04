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
/// A ATOMICIDADE do lançamento (parcela 70 — Fase 1 de `docs/guia-no-agendamento.md`).
///
/// Antes, confirmar a presença era uma corrente de gravações separadas — guias primeiro,
/// número, carimbo por último — e cada vão era um meio-estado possível: se o carimbo
/// falhasse, as guias existiam, o agendamento não sabia, e o segundo clique gerava OUTRO
/// jogo de guias. Estes testes provam o contrato novo pelo único caminho que o SQLite
/// alcança: um contexto que FALHA num SaveChanges programado. O que se afirma não é a
/// mensagem de erro — é o ESTADO que sobra, porque é ele que decide se o próximo clique
/// duplica.
/// </summary>
public class AtomicidadeDoLancamentoTests : IDisposable
{
    /// <summary>Contexto que falha no N-ésimo SaveChanges depois de armado.</summary>
    private sealed class ContextoComFalhaProgramada(DbContextOptions<ClinicaDbContext> options)
        : ClinicaDbContext(options)
    {
        private int _restantes = -1;

        /// <summary>Arma a falha: o <paramref name="numero"/>-ésimo SaveChanges a partir de agora estoura.</summary>
        public void FalharNoSalvar(int numero) => _restantes = numero;

        public void Desarmar() => _restantes = -1;

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (_restantes > 0 && --_restantes == 0)
                throw new InvalidOperationException("falha simulada de gravação");
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }

    private readonly SqliteConnection _conn;
    private readonly ContextoComFalhaProgramada _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;


    /// <summary>
    /// Todo horário MARCADO precisa de dono desde a parcela 95 — o fixture cria um para os
    /// cenários que não se importam com QUEM atende.
    /// </summary>
    private readonly int _profPadrao;

    public AtomicidadeDoLancamentoTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ContextoComFalhaProgramada(options);
        _db.Database.EnsureCreated();
        var profPadrao = new Profissional { Nome = "Dra. Padrão" };
        _db.Profissionais.Add(profPadrao);
        _db.SaveChanges();
        _profPadrao = profPadrao.Id;
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
    }

    private async Task<int> CriarPacienteAsync()
    {
        var p = new Paciente { Nome = "Maria", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task Guias_e_carimbo_nascem_no_MESMO_SaveChanges()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, DateTime.Today.AddHours(9), ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profPadrao);

        // O 1º save a partir daqui é o commit atômico; o 2º é o do NÚMERO — que falha.
        _db.FalharNoSalvar(2);
        var resultado = await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "ana");
        _db.Desarmar();

        // A falha do número NÃO vira erro (a guia existe; dizer "não foi lançado" seria o
        // gesto que produziu os três encaixes de 12/08) — vira aviso.
        resultado.Avisos.Should().Contain(a => a.Contains("número/protocolo"));

        var deNovo = await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == ag.Id);
        deNovo.Status.Should().Be(StatusAgendamento.Realizado,
            "o carimbo viaja NO MESMO commit das guias — é isso que mata a duplicidade");
        deNovo.AtendimentoId.Should().NotBeNull();

        (await _db.Codigos.AsNoTracking().CountAsync()).Should().BeGreaterThan(0);

        // O segundo clique não tem o que duplicar: a presença já consta como confirmada.
        var segundoClique = () => _agenda.ConfirmarPresencaAsync(ag.Id, operador: "ana");
        await segundoClique.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já teve a presença confirmada*");
        (await _db.Atendimentos.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Falha_no_commit_atomico_nao_deixa_meio_estado_nenhum()
    {
        var pacienteId = await CriarPacienteAsync();
        var ag = await _agenda.AgendarAsync(
            pacienteId, DateTime.Today.AddHours(9), ModalidadeAtendimento.AcupunturaComEletro, null, profissionalId: _profPadrao);

        _db.FalharNoSalvar(1);
        var acao = () => _agenda.ConfirmarPresencaAsync(ag.Id, operador: "ana");
        await acao.Should().ThrowAsync<Exception>();
        _db.Desarmar();
        _db.ChangeTracker.Clear();

        // Nada pela metade: nem atendimento, nem guia, nem carimbo. O clique seguinte
        // parte do zero e cria UMA vez.
        var deNovo = await _db.Agendamentos.AsNoTracking().SingleAsync(a => a.Id == ag.Id);
        deNovo.Status.Should().Be(StatusAgendamento.Agendado);
        deNovo.AtendimentoId.Should().BeNull();
        (await _db.Atendimentos.AsNoTracking().CountAsync()).Should().Be(0);
        (await _db.Codigos.AsNoTracking().CountAsync()).Should().Be(0);

        var resultado = await _agenda.ConfirmarPresencaAsync(ag.Id, operador: "ana");
        resultado.Atendimento.Codigos.Should().NotBeEmpty();
        (await _db.Atendimentos.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Avulso_cria_horario_checkin_atendimento_e_guias_num_gesto_so()
    {
        var pacienteId = await CriarPacienteAsync();
        var profissional = new Profissional { Nome = "Dra. Paula" };
        var sala = new Sala { Nome = "Sala 2" };
        _db.Profissionais.Add(profissional);
        _db.Salas.Add(sala);
        await _db.SaveChangesAsync();

        var (ag, lancamento) = await _agenda.LancarAvulsoAsync(
            pacienteId, DateTime.Today.AddHours(10), ModalidadeAtendimento.AcupunturaComEletro,
            "chegou sem hora", operador: "ana",
            profissionalId: profissional.Id, salaId: sala.Id);

        ag.Encaixe.Should().BeTrue();
        ag.ChegadaEm.Should().NotBeNull("o paciente está no balcão — o check-in é o fato");
        ag.Status.Should().Be(StatusAgendamento.Realizado);
        ag.AtendimentoId.Should().Be(lancamento.Atendimento.Id);

        // Profissional e sala amarrados ao encaixe (parcela 70): é o que faz a sessão
        // aparecer no "Meu dia" do médico, entrar no repasse dele e a chamada da Fila
        // anunciar "para a sala X" — os três leem o AGENDAMENTO.
        ag.ProfissionalId.Should().Be(profissional.Id);
        ag.SalaId.Should().Be(sala.Id);
        lancamento.Atendimento.Numero.Should().NotBeNullOrEmpty();
        lancamento.Atendimento.Codigos.Should().NotBeEmpty();

        // A trilha dos dois atos, gravada junto: o check-in e a presença que gera guias.
        var acoes = await _db.Auditoria.Select(e => e.Acao).ToListAsync();
        acoes.Should().Contain("FilaChegada").And.Contain("PresencaConfirmada");
        (await _db.Auditoria.SingleAsync(e => e.Acao == "PresencaConfirmada"))
            .Operador.Should().Be("ana", "o ato que gera as guias era o único da agenda sem autoria");
    }

    [Fact]
    public async Task Avulso_que_falha_no_commit_nao_grava_NADA()
    {
        var pacienteId = await CriarPacienteAsync();

        _db.FalharNoSalvar(1);
        var acao = () => _agenda.LancarAvulsoAsync(
            pacienteId, DateTime.Today.AddHours(10), ModalidadeAtendimento.AcupunturaComEletro,
            null, operador: "ana");
        await acao.Should().ThrowAsync<Exception>();
        _db.Desarmar();
        _db.ChangeTracker.Clear();

        // Ou existe tudo, ou não existe nada: sem encaixe fantasma na fila, sem guia
        // órfã no faturamento — o meio-estado do incidente de 12/08 deixou de ser possível.
        (await _db.Agendamentos.AsNoTracking().CountAsync()).Should().Be(0);
        (await _db.Atendimentos.AsNoTracking().CountAsync()).Should().Be(0);
        (await _db.Codigos.AsNoTracking().CountAsync()).Should().Be(0);

        var (ag, _) = await _agenda.LancarAvulsoAsync(
            pacienteId, DateTime.Today.AddHours(10), ModalidadeAtendimento.AcupunturaComEletro,
            null, operador: "ana");
        ag.Status.Should().Be(StatusAgendamento.Realizado);
        (await _db.Atendimentos.AsNoTracking().CountAsync()).Should().Be(1);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
