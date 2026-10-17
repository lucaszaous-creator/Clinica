using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class PonteClinicSchedulingTests : IDisposable
{
    readonly SqliteConnection connection = new("Data Source=:memory:");
    readonly ClinicaDbContext db;
    readonly ClinicSchedulingBridge bridge;
    readonly Agendamento appointment;
    readonly DateTimeOffset from;
    public PonteClinicSchedulingTests() {
        connection.Open(); db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options); db.Database.EnsureCreated();
        var repo = new ClinicaRepositorio(db); bridge = new(db, new AgendaService(repo, new AtendimentoService(repo)), new BuscaDeVagasService(repo));
        var tomorrow = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").Date.AddDays(2).AddHours(9);
        if (tomorrow.DayOfWeek == DayOfWeek.Sunday) tomorrow = tomorrow.AddDays(1);
        from = new(tomorrow, TimeSpan.FromHours(-3));
        appointment = new() { Paciente = new() { Nome = "Pessoa fictícia", Telefone = "11999990001" }, Profissional = new() { Nome = "Profissional fictício" }, DataHora = tomorrow.AddDays(1), DuracaoMinutos = 30, Observacoes = "Nota que deve ser preservada" };
        db.Agendamentos.Add(appointment); db.SaveChanges();
    }
    ClinicSlotQuery Query() => new(appointment.Id, appointment.PacienteId, ClinicAgendaBridge.Revision(appointment), "5511999990001", from);
    async Task<ClinicReschedule> Command() {
        var slots = await bridge.Slots(Query(), default); Assert.NotEmpty(slots.StartsAt);
        return new(Guid.NewGuid().ToString("N"), appointment.Id, appointment.PacienteId, Query().Revision, Query().Phone, slots.StartsAt[0], "Ana");
    }
    [Fact] public async Task ReschedulePreservesResourcesAndReplaysTheCommittedReceipt() {
        var command = await Command(); var old = appointment.DataHora;
        var result = await bridge.Reschedule(command, default);
        Assert.Equal(command.StartsAt, result.StartsAt); Assert.Equal(old, result.Previous.DateTime);
        Assert.Equal(command.StartsAt.DateTime, appointment.DataHora); Assert.Equal("Nota que deve ser preservada", appointment.Observacoes);
        Assert.Equal(30, appointment.DuracaoMinutos); Assert.NotNull(appointment.ProfissionalId); Assert.Empty(db.Atendimentos);
        Assert.Equal(result, await bridge.Reschedule(command, default));
        Assert.Single(db.Auditoria.Where(e => e.Acao == "CrmRemarcacaoV1"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Reschedule(command with { StartsAt = command.StartsAt.AddHours(1) }, default));
    }
    [Fact] public async Task OccupiedSlotAfterSearchRejectsWithoutChangingOriginal() {
        var command = await Command(); var original = appointment.DataHora;
        db.Agendamentos.Add(new() { Paciente = new() { Nome = "Outra pessoa" }, ProfissionalId = appointment.ProfissionalId, DataHora = command.StartsAt.DateTime, DuracaoMinutos = 30 });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Reschedule(command, default));
        Assert.Equal(original, appointment.DataHora); Assert.Empty(db.Auditoria.Where(e => e.Acao == "CrmRemarcacaoV1"));
    }
    [Fact] public async Task ChangedRoomOrDurationInvalidatesTheRevision() {
        var command = await Command(); appointment.DuracaoMinutos = 60; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Reschedule(command, default));
    }
    [Fact] public async Task OldConfirmationIsAuditedAndNewDateRequiresNewConfirmation() {
        var command = await Command();
        db.Contatos.Add(new() { PacienteId = appointment.PacienteId, AgendamentoId = appointment.Id, Tipo = TipoContato.ConfirmacaoSessao,
            Origem = ContatoCampanha.OrigemDeAgendamento(appointment.Id), Referencia = DateOnly.FromDateTime(appointment.DataHora),
            Status = StatusContato.Respondido, RespondidoEm = DateTime.Now, EnviadoEm = DateTime.Now, Comentario = "Comentário anterior preservado" });
        await db.SaveChangesAsync();
        await bridge.Reschedule(command, default);
        var contact = await db.Contatos.SingleAsync();
        Assert.Equal(StatusContato.Pendente, contact.Status); Assert.Null(contact.RespondidoEm); Assert.Null(contact.EnviadoEm);
        Assert.Equal(DateOnly.FromDateTime(command.StartsAt.DateTime), contact.Referencia);
        Assert.Equal("Comentário anterior preservado", contact.Comentario);
        Assert.Single(db.Auditoria.Where(a => a.Acao == "CrmConfirmacaoReaberta"));
        Assert.All(db.Auditoria, a => Assert.True((a.Detalhe?.Length ?? 0) <= 500));
    }
    [Fact] public async Task SharedPhoneStillRequiresTheSelectedPatient() {
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Slots(Query() with { PatientId = appointment.PacienteId + 1 }, default));
    }
    [Fact] public async Task CheckedInAndWrongPhoneAreRejected() {
        var command = await Command();
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Reschedule(command with { Phone = "5511888888888" }, default));
        appointment.ChegadaEm = DateTime.Now; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Reschedule(command, default));
    }
    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
