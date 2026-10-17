using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

// A secretária marca, lê a agenda e cancela pelo CRM; quem decide é o Clinica.
public sealed class PonteClinicBookingTests : IDisposable
{
    const string Fone = "5511999990001";
    readonly SqliteConnection connection = new("Data Source=:memory:");
    readonly ClinicaDbContext db;
    readonly ClinicBookingBridge bridge;
    readonly Paciente paciente;
    readonly Profissional medico;
    readonly DateOnly dia;

    public PonteClinicBookingTests()
    {
        connection.Open(); db = new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options); db.Database.EnsureCreated();
        var repo = new ClinicaRepositorio(db);
        bridge = new(db, new AgendaService(repo, new AtendimentoService(repo)), repo);
        var d = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Sao_Paulo").Date.AddDays(3);
        while (d.DayOfWeek is DayOfWeek.Sunday or DayOfWeek.Saturday) d = d.AddDays(1);
        dia = DateOnly.FromDateTime(d);
        paciente = new() { Nome = "Pessoa fictícia", Telefone = "(11) 99999-0001" };
        medico = new() { Nome = "Profissional fictício", DuracaoPadraoMinutos = 30 };
        db.Pacientes.Add(paciente); db.Profissionais.Add(medico);
        db.Pacientes.Add(new() { Nome = "Outra pessoa", Telefone = "11988880000" });
        db.SaveChanges();
    }

    BookingQuery Query() => new(Fone, paciente.Id, medico.Id, null, "Consulta", null, 30, dia, dia, new(8, 0), new(18, 0), []);

    async Task<BookingHold> Hold()
    {
        var slots = await bridge.Slots(Query(), default); Assert.NotEmpty(slots.Slots);
        return new(Guid.NewGuid().ToString("N"), Query(), slots.Slots[0], "Ana");
    }

    [Fact] public async Task Catalogo_traz_so_os_pacientes_deste_telefone()
    {
        var c = await bridge.Catalog(new(Fone), default);
        Assert.Equal([paciente.Id], c.Patients.Select(p => p.Id));
        Assert.Contains(c.Professionals, p => p.Id == medico.Id && p.Duration == 30);
    }

    [Fact] public async Task Marcar_pelo_CRM_cria_o_horario_na_agenda_e_repete_o_mesmo_recibo()
    {
        var hold = await Hold();
        var held = await bridge.Hold(hold, default);
        Assert.Equal("held", held.State);
        Assert.Empty(db.Agendamentos);
        var action = new BookingAction(hold.Id, Fone, "Ana");
        var done = await bridge.Confirm(action, default);
        Assert.Equal("confirmed", done.State);
        var a = await db.Agendamentos.AsNoTracking().SingleAsync();
        Assert.Equal(paciente.Id, a.PacienteId); Assert.Equal(medico.Id, a.ProfissionalId);
        Assert.Equal(hold.StartsAt.DateTime, a.DataHora); Assert.Equal(done.Receipt!.AppointmentId, a.Id);
        Assert.Equal(done, await bridge.Confirm(action, default));
        Assert.Single(db.Agendamentos);
    }

    [Fact] public async Task Vaga_tomada_no_balcao_depois_da_reserva_recusa_a_confirmacao()
    {
        var hold = await Hold(); await bridge.Hold(hold, default);
        db.Agendamentos.Add(new() { Paciente = new() { Nome = "Balcão" }, ProfissionalId = medico.Id, DataHora = hold.StartsAt.DateTime, DuracaoMinutos = 30 });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Confirm(new(hold.Id, Fone, "Ana"), default));
        Assert.Single(db.Agendamentos);
    }

    [Fact] public async Task Reserva_ativa_tira_a_vaga_da_busca_e_o_paciente_so_tem_uma()
    {
        var hold = await Hold(); await bridge.Hold(hold, default);
        Assert.DoesNotContain(hold.StartsAt, (await bridge.Slots(Query(), default)).Slots);
        var slots = (await bridge.Slots(Query(), default)).Slots;
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Hold(new(Guid.NewGuid().ToString("N"), Query(), slots[0], "Bia"), default));
        await bridge.Release(new(hold.Id, Fone, "Ana"), default);
        Assert.Contains(hold.StartsAt, (await bridge.Slots(Query(), default)).Slots);
    }

    [Fact] public async Task Filtro_de_fim_de_tarde_acha_vaga_mesmo_com_a_manha_livre()
    {
        var late = await bridge.Slots(Query() with { After = new(17, 0) }, default);
        Assert.NotEmpty(late.Slots);
        Assert.All(late.Slots, t => Assert.True(t.Hour >= 17));
    }

    [Fact] public async Task Outro_telefone_nao_marca_para_o_paciente()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Slots(Query() with { Phone = "5511988880000" }, default));
    }

    [Fact] public async Task Agenda_esconde_o_nome_e_o_id_dos_outros_pacientes()
    {
        db.Agendamentos.Add(new() { PacienteId = paciente.Id, ProfissionalId = medico.Id, DataHora = dia.ToDateTime(new(9, 0)) });
        db.Agendamentos.Add(new() { Paciente = new() { Nome = "Terceiro", Telefone = "11977770000" }, ProfissionalId = medico.Id, DataHora = dia.ToDateTime(new(10, 0)) });
        await db.SaveChangesAsync();
        var cal = await bridge.Calendar(new(Fone, dia, 1, null, null), default);
        Assert.Equal(2, cal.Items.Length);
        Assert.True(cal.Items[0].ThisContact); Assert.NotNull(cal.Items[0].AppointmentId);
        Assert.False(cal.Items[1].ThisContact); Assert.Null(cal.Items[1].AppointmentId);
    }

    [Fact] public async Task Cancelar_pela_equipe_usa_a_regra_da_agenda_com_motivo_e_e_idempotente()
    {
        var a = new Agendamento { PacienteId = paciente.Id, ProfissionalId = medico.Id, DataHora = dia.ToDateTime(new(9, 0)) };
        db.Agendamentos.Add(a); await db.SaveChangesAsync();
        a = await db.Agendamentos.Include(x => x.Paciente).SingleAsync(x => x.Id == a.Id);
        var cmd = new ClinicCancel(Guid.NewGuid().ToString("N"), a.Id, paciente.Id, ClinicAgendaBridge.Revision(a), Fone, "Paciente pediu por telefone", "Ana");
        Assert.Equal("cancelled", (await bridge.Cancel(cmd, default)).Status);
        Assert.Equal(StatusAgendamento.Cancelado, (await db.Agendamentos.AsNoTracking().SingleAsync()).Status);
        Assert.Equal("cancelled", (await bridge.Cancel(cmd, default)).Status);
        Assert.Single(db.Auditoria.Where(e => e.Acao == "CrmCancelamentoEquipe"));
        Assert.Contains(db.Auditoria, e => e.Detalhe != null && e.Detalhe.Contains("Paciente pediu por telefone"));
    }

    [Fact] public async Task Cancelar_sem_motivo_e_recusado()
    {
        var a = new Agendamento { PacienteId = paciente.Id, ProfissionalId = medico.Id, DataHora = dia.ToDateTime(new(9, 0)) };
        db.Agendamentos.Add(a); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.Cancel(new(Guid.NewGuid().ToString("N"), a.Id, paciente.Id, new string('0', 64), Fone, " ", "Ana"), default));
    }

    public void Dispose() { db.Dispose(); connection.Dispose(); }
}
