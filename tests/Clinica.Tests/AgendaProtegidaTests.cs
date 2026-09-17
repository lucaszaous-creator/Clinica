using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class AgendaProtegidaTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;
    private readonly AgendaService _agenda;
    private readonly Profissional _prof = new() { Nome = "Profissional de teste", DuracaoPadraoMinutos = 60 };
    private readonly Paciente _paciente = new() { Nome = "Paciente fictício", Convenio = Convenio.UnimedIntercambio };
    private static readonly DateTime Segunda = new(2026, 9, 21, 9, 0, 0);

    public AgendaProtegidaTests()
    {
        _conn.Open();
        _db = Contexto();
        _db.Database.EnsureCreated();
        _db.AddRange(_prof, _paciente);
        _db.SaveChanges();
        _repo = new ClinicaRepositorio(_db);
        _agenda = new AgendaService(_repo, new AtendimentoService(_repo));
    }

    private ClinicaDbContext Contexto() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
    private Task<Agendamento> Marcar(DateTime inicio, int? duracao = null, bool encaixe = false)
        => _agenda.AgendarAsync(_paciente.Id, inicio, ModalidadeAtendimento.AcupunturaComEletro,
            null, profissionalId: _prof.Id, duracaoMinutos: duracao, encaixe: encaixe);
    private Task Configurar(bool ativa, int? dias = null, TimeOnly? das = null, TimeOnly? ate = null)
        => new ConfiguracaoAgendaService(_repo).SalvarAsync(_prof.Id, ativa, dias, das, ate, "Recepção de teste");

    [Fact]
    public async Task Desativada_MantemAvisosEPermiteSobreposicao()
    {
        await Marcar(Segunda);
        var avisos = await _agenda.ConflitosAsync(Segunda.AddMinutes(30), profissionalId: _prof.Id);
        avisos.Should().NotBeEmpty().And.OnlyContain(c => !c.ImpedeMarcar);
        await Marcar(Segunda.AddMinutes(30));
        (await _db.Agendamentos.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Ativa_RecusaSobreposicaoInclusiveEncaixe_PermiteLimiteExato()
    {
        await Configurar(true);
        await Marcar(Segunda);
        var tentar = () => Marcar(Segunda.AddMinutes(59), encaixe: true);
        await tentar.Should().ThrowAsync<InvalidOperationException>().WithMessage("Agenda protegida*");
        await Marcar(Segunda.AddHours(1));
        (await _db.Agendamentos.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Ativa_ValidaDiasEIntervaloInteiroDaJornada()
    {
        await Configurar(true, Profissional.BitDe(DayOfWeek.Monday), new(8, 0), new(12, 0));
        foreach (var horario in new[] { Segunda.AddDays(1), Segunda.Date.AddHours(7.5), Segunda.Date.AddHours(11.5) })
        {
            var tentar = () => Marcar(horario);
            await tentar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*não atende*");
        }
        await Marcar(Segunda.Date.AddHours(11));
    }

    [Fact]
    public async Task Ativa_RespeitaBloqueioDaClinicaENaoBloqueioDeOutroProfissional()
    {
        await Configurar(true);
        var outro = new Profissional { Nome = "Outro profissional" };
        _db.Add(outro);
        await _db.SaveChangesAsync();
        _db.BloqueiosAgenda.Add(new() { ProfissionalId = outro.Id, Inicio = Segunda, Fim = Segunda.AddHours(2), Motivo = "Ausência" });
        await _db.SaveChangesAsync();
        await Marcar(Segunda);
        _db.BloqueiosAgenda.Add(new() { Inicio = Segunda.AddHours(2), Fim = Segunda.AddHours(3), Motivo = "Clínica fechada" });
        await _db.SaveChangesAsync();
        var tentar = () => Marcar(Segunda.AddHours(2));
        await tentar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*indisponível*");
    }

    [Fact]
    public async Task Conflito_AtravessaMeiaNoite_ECanceladoLiberaHorario()
    {
        await Configurar(true);
        var a = await Marcar(Segunda.Date.AddMinutes(-30));
        var tentar = () => Marcar(Segunda.Date);
        await tentar.Should().ThrowAsync<InvalidOperationException>();
        a.Status = StatusAgendamento.Cancelado;
        await _db.SaveChangesAsync();
        await Marcar(Segunda.Date);
    }

    [Fact]
    public async Task RemarcacaoRecusada_PreservaHorarioOriginal()
    {
        await Configurar(true);
        await Marcar(Segunda);
        var original = await Marcar(Segunda.AddHours(2));
        var tentar = () => _agenda.RemarcarAsync(original.Id, Segunda.AddMinutes(15), "não deve salvar");
        await tentar.Should().ThrowAsync<InvalidOperationException>();
        _db.ChangeTracker.Clear();
        var salvo = await _db.Agendamentos.SingleAsync(a => a.Id == original.Id);
        salvo.DataHora.Should().Be(Segunda.AddHours(2));
        salvo.Observacoes.Should().BeNull();
    }

    [Fact]
    public async Task Configuracao_CompartilhadaEAuditada_PreservaCadastroEHorarioAntigo()
    {
        var ag = await Marcar(Segunda);
        await Configurar(true, Profissional.BitDe(DayOfWeek.Tuesday), new(13, 0), new(17, 0));
        using var posto = Contexto();
        var p = await posto.Profissionais.SingleAsync();
        p.AgendaProtegida.Should().BeTrue();
        p.Nome.Should().Be(_prof.Nome);
        p.DuracaoPadraoMinutos.Should().Be(60);
        (await posto.Auditoria.CountAsync(a => a.Acao == "AgendaProfissionalConfigurada")).Should().Be(1);
        (await posto.Agendamentos.SingleAsync()).DataHora.Should().Be(Segunda);
        // Finalizar um horário anterior não é marcar outro horário.
        ag.Status = StatusAgendamento.Realizado;
        await _db.SaveChangesAsync();
        (await posto.Agendamentos.AsNoTracking().SingleAsync()).Status.Should().Be(StatusAgendamento.Realizado);
    }

    [Fact]
    public async Task ConfiguracaoInvalida_NaoAlteraTrava()
    {
        var tentar = () => Configurar(true, null, new(12, 0), new(8, 0));
        await tentar.Should().ThrowAsync<InvalidOperationException>();
        _prof.AgendaProtegida.Should().BeFalse();
    }

    [Fact]
    public async Task ConfiguracaoAbertaEmOutroPosto_NaoSobrescreveMudancaMaisRecente()
    {
        var antiga = new JornadaAnterior(false, null, null, null);
        await Configurar(true);
        using var outro = Contexto();
        var tentar = () => new ConfiguracaoAgendaService(new ClinicaRepositorio(outro))
            .SalvarAsync(_prof.Id, false, null, null, null, "Outro posto", antiga);
        await tentar.Should().ThrowAsync<InvalidOperationException>().WithMessage("Outro posto alterou*");
        (await outro.Profissionais.SingleAsync()).AgendaProtegida.Should().BeTrue();
    }

    [Fact]
    public async Task Postgres_DoisPostosConcorrentesESemServico_SoUmReserva()
    {
        if (!BancoDosTestes.NoPostgres) return; // A etapa PostgreSQL do CI executa a migration real.
        await Configurar(true);
        await using var primeiro = Contexto();
        await using var segundo = Contexto();
        await using var transacao = await primeiro.Database.BeginTransactionAsync();
        Agendamento Candidato() => new() { PacienteId = _paciente.Id, ProfissionalId = _prof.Id,
            DataHora = Segunda, DuracaoMinutos = 60, Status = StatusAgendamento.Agendado };
        primeiro.Add(Candidato());
        await primeiro.SaveChangesAsync();
        segundo.Add(Candidato());
        var salvarSegundo = new ClinicaRepositorio(segundo).SalvarAsync();
        // O primeiro ainda segura a reserva. O segundo precisa esperar, sem aceitar outro paciente.
        var completou = await Task.WhenAny(salvarSegundo, Task.Delay(250));
        completou.Should().NotBe(salvarSegundo);
        await transacao.CommitAsync();
        var tentar = async () => await salvarSegundo.WaitAsync(TimeSpan.FromSeconds(15));
        await tentar.Should().ThrowAsync<InvalidOperationException>().WithMessage("Agenda protegida*");
        (await _db.Agendamentos.CountAsync()).Should().Be(1);
        segundo.ChangeTracker.Entries().Should().BeEmpty();
        // A mesma conexão segue utilizável após a recusa, sem reapresentar o candidato recusado.
        var livre = Candidato(); livre.DataHora = Segunda.AddHours(1);
        segundo.Add(livre);
        await new ClinicaRepositorio(segundo).SalvarAsync();
        (await _db.Agendamentos.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Postgres_ClienteAntigoNaoIgnoraJornadaNemReativaConflito()
    {
        if (!BancoDosTestes.NoPostgres) return;
        var ag = await Marcar(Segunda);
        ag.Status = StatusAgendamento.Cancelado;
        await _db.SaveChangesAsync();
        await Marcar(Segunda);
        await Configurar(true, Profissional.BitDe(DayOfWeek.Monday), new(8, 0), new(12, 0));
        ag.Status = StatusAgendamento.Agendado;
        var reativar = () => _repo.SalvarAsync();
        await reativar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*já tem atendimento*");
        _db.Add(new Agendamento { PacienteId = _paciente.Id, ProfissionalId = _prof.Id,
            DataHora = Segunda.AddDays(1), Status = StatusAgendamento.Agendado });
        var fora = () => _repo.SalvarAsync();
        await fora.Should().ThrowAsync<InvalidOperationException>().WithMessage("*fora da jornada*");
    }

    [Fact]
    public async Task Postgres_ReservaConcorrenteRecusada_NaoDeixaAtendimentoOuGuiasOrfaos()
    {
        if (!BancoDosTestes.NoPostgres) return;
        await Configurar(true);
        await new ParametrosService(_repo).DefinirGuiaNoAgendamentoAsync(true);
        await using var primeiro = Contexto();
        await using var segundo = Contexto();
        await using var transacao = await primeiro.Database.BeginTransactionAsync();
        primeiro.Add(new Agendamento { PacienteId = _paciente.Id, ProfissionalId = _prof.Id,
            DataHora = Segunda, Status = StatusAgendamento.Agendado });
        await primeiro.SaveChangesAsync();
        var repo = new ClinicaRepositorio(segundo);
        var parametros = new ParametrosService(repo);
        var servico = new AgendaService(repo, new AtendimentoService(repo, parametros: parametros), parametros);
        var gravacao = servico.AgendarAsync(_paciente.Id, Segunda, ModalidadeAtendimento.AcupunturaComEletro,
            null, profissionalId: _prof.Id);
        await Task.Delay(250);
        await transacao.CommitAsync();
        var tentar = async () => await gravacao.WaitAsync(TimeSpan.FromSeconds(15));
        await tentar.Should().ThrowAsync<InvalidOperationException>();
        (await _db.Atendimentos.CountAsync()).Should().Be(0);
        (await _db.Agendamentos.CountAsync()).Should().Be(1);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
