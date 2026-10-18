using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Clinica.Tests;

// A ponte em produção usa o AddClinica, que liga o EnableRetryOnFailure do Npgsql — e o EF
// recusa transação aberta à mão sob estratégia que repete. Os outros testes da ponte montam
// o DbContext SEM estratégia e passavam com o defeito: confirmar, cancelar, marcar e
// remarcar pelo CRM devolviam 409 em toda chamada na VPS (achado em out/2026, rodando a
// ponte contra um Postgres de verdade). Aqui o SQLite ganha uma estratégia que repete,
// que é o que dispara a mesma recusa do EF.
public sealed class PonteComRepeticaoTests : IDisposable
{
    sealed class RepeteComoONpgsql(ExecutionStrategyDependencies d) : ExecutionStrategy(d, 3, TimeSpan.FromMilliseconds(10))
    {
        protected override bool ShouldRetryOn(Exception exception) => false;
    }

    const string Fone = "5511999990001";
    readonly SqliteConnection connection = new("Data Source=:memory:");
    readonly ClinicaDbContext db;
    readonly ClinicaRepositorio repo;
    readonly AgendaService agenda;

    public PonteComRepeticaoTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(connection, o => o.ExecutionStrategy(d => new RepeteComoONpgsql(d))).Options);
        db.Database.EnsureCreated();
        repo = new(db); agenda = new(repo, new AtendimentoService(repo));
    }

    async Task<Agendamento> Horario()
    {
        var p = new Paciente { Nome = "Pessoa fictícia", Telefone = "11999990001" };
        var m = new Profissional { Nome = "Profissional fictício", DuracaoPadraoMinutos = 30 };
        db.Pacientes.Add(p); db.Profissionais.Add(m); await db.SaveChangesAsync();
        var a = new Agendamento { PacienteId = p.Id, ProfissionalId = m.Id, DataHora = DateTime.Today.AddDays(3).AddHours(10), DuracaoMinutos = 30 };
        db.Agendamentos.Add(a); await db.SaveChangesAsync();
        return await db.Agendamentos.Include(x => x.Paciente).SingleAsync(x => x.Id == a.Id);
    }

    // A recusa vem ao GRAVAR dentro da transação aberta à mão — é a frase que a VPS mostrou.
    [Fact] public void A_estrategia_do_teste_reproduz_a_recusa_do_EF()
    {
        using var tx = db.Database.BeginTransaction();
        db.Profissionais.Add(new() { Nome = "x" });
        var e = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Contains("does not support user-initiated transactions", e.Message);
    }

    [Fact] public async Task Confirmar_pelo_lembrete_funciona_com_a_estrategia()
    {
        var a = await Horario();
        var r = await new ClinicAgendaBridge(db, agenda).Decide(new(Guid.NewGuid().ToString("N"), a.Id, ClinicAgendaBridge.Revision(a), Fone, "confirm", "msg-1"), default);
        Assert.Equal("confirmed", r.Status);
    }

    [Fact] public async Task Cancelar_pela_equipe_funciona_com_a_estrategia()
    {
        var a = await Horario();
        var r = await new ClinicBookingBridge(db, agenda, repo).Cancel(new(Guid.NewGuid().ToString("N"), a.Id, a.PacienteId, ClinicAgendaBridge.Revision(a), Fone, "Teste", "Ana"), default);
        Assert.Equal("cancelled", r.Status);
        Assert.Equal(StatusAgendamento.Cancelado, (await db.Agendamentos.AsNoTracking().SingleAsync()).Status);
    }

    [Fact] public async Task Registrar_o_recall_funciona_com_a_estrategia()
    {
        var a = await Horario();
        await new ClinicRecallBridge(db).Notice(new(Guid.NewGuid().ToString("N"), a.PacienteId, null, "sms:1", DateTimeOffset.UtcNow, "sms"), default);
        Assert.Single(await db.Contatos.ToListAsync());
    }

    [Fact] public async Task Marcar_pelo_CRM_funciona_com_a_estrategia()
    {
        var a = await Horario();
        var bridge = new ClinicBookingBridge(db, agenda, repo);
        var dia = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var q = new BookingQuery(Fone, a.PacienteId, a.ProfissionalId!.Value, null, "Consulta", null, 30, dia, dia.AddDays(5), new(8, 0), new(18, 0), []);
        var slots = await bridge.Slots(q, default); Assert.NotEmpty(slots.Slots);
        var hold = new BookingHold(Guid.NewGuid().ToString("N"), q, slots.Slots[0], "Ana");
        await bridge.Hold(hold, default);
        Assert.Equal("confirmed", (await bridge.Confirm(new(hold.Id, Fone, "Ana"), default)).State);
    }

    [Fact] public async Task Remarcar_pelo_CRM_funciona_com_a_estrategia()
    {
        var a = await Horario();
        var bridge = new ClinicSchedulingBridge(db, agenda, new BuscaDeVagasService(repo));
        var from = new DateTimeOffset(DateTime.SpecifyKind(DateTime.Today.AddDays(1).AddHours(9), DateTimeKind.Unspecified), TimeSpan.FromHours(-3));
        var q = new ClinicSlotQuery(a.Id, a.PacienteId, ClinicAgendaBridge.Revision(a), Fone, from);
        var slots = await bridge.Slots(q, default); Assert.NotEmpty(slots.StartsAt);
        var r = await bridge.Reschedule(new(Guid.NewGuid().ToString("N"), a.Id, a.PacienteId, q.Revision, Fone, slots.StartsAt[0], "Ana"), default);
        Assert.Equal(slots.StartsAt[0], r.StartsAt);
    }

    [Fact] public async Task Gravar_o_resumo_do_dia_funciona_com_a_estrategia()
    {
        var r = await new ClinicSummaryBridge(db).Daily(new(DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), DateTimeOffset.UtcNow,
            3, 2, 1, 10, 9, 90, 4, 2, 2, 0, 3, 2, 1, 1, 0), default);
        Assert.Equal("recorded", r.Status);
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
