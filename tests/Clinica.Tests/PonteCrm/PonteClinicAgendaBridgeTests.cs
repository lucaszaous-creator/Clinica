using Clinica.Application.Servicos;
using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

public sealed class PonteClinicAgendaBridgeTests : IDisposable
{
    readonly SqliteConnection connection;readonly ClinicaDbContext db;readonly ClinicAgendaBridge bridge;
    public PonteClinicAgendaBridgeTests(){connection=new("Data Source=:memory:");connection.Open();db=new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();var repo=new ClinicaRepositorio(db);bridge=new(db,new AgendaService(repo,new AtendimentoService(repo)));}
    async Task<Agendamento> Seed(){var p=new Paciente{Nome="Pessoa fictícia",Telefone="(11) 99999-0001"};db.Pacientes.Add(p);var a=new Agendamento{Paciente=p,DataHora=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime.AddHours(23)};db.Agendamentos.Add(a);await db.SaveChangesAsync();return a;}
    ClinicDecision Command(Agendamento a,string decision="confirm")=>new(Guid.NewGuid().ToString("N"),a.Id,ClinicAgendaBridge.Revision(a),ClinicAgendaBridge.Phone(a.Paciente!.Telefone),decision,"incoming-test");
    [Fact] public async Task ConfirmationDoesNotGenerateAttendanceOrBilling(){var a=await Seed();var c=Command(a);var result=await bridge.Decide(c,default);Assert.Equal("confirmed",result.Status);Assert.Equal(StatusAgendamento.Agendado,a.Status);Assert.Null(a.AtendimentoId);Assert.Null(a.ChegadaEm);Assert.Equal(StatusContato.Respondido,(await db.Contatos.SingleAsync()).Status);Assert.Empty(await db.Atendimentos.ToListAsync());Assert.Single(await db.Auditoria.Where(e=>e.Acao=="CrmConfirmacao").ToListAsync());await bridge.Decide(c,default);Assert.Single(await db.Auditoria.Where(e=>e.Acao=="CrmConfirmacao").ToListAsync());}
    // A regra do link do SMS (canal-sms §3.3) vale para o WhatsApp: "não vou" vira "Não vem — remarcar"
    // na agenda do balcão, e o horário continua até a recepção falar com o paciente.
    [Theory] [InlineData("cancel","cancel-requested")] [InlineData("reschedule","reschedule-requested")]
    public async Task NotComingBecomesTheSameRescheduleSealAsTheSmsLink(string decision,string status){var a=await Seed();var c=Command(a,decision);Assert.Equal(status,(await bridge.Decide(c,default)).Status);Assert.Equal(StatusAgendamento.Agendado,a.Status);var contato=await db.Contatos.SingleAsync();Assert.Equal(Clinica.Application.Modelos.SituacaoConfirmacao.PediuRemarcar,Clinica.Application.Modelos.RodadaDeConfirmacao.Classificar(contato.Status,contato.EnviadoEm,contato.Comentario));Assert.Equal(status,(await bridge.Decide(c,default)).Status);Assert.Single(await db.Auditoria.Where(e=>e.Acao.StartsWith("Crm")).ToListAsync());}
    [Fact] public async Task CancellationWhenEnabledPreservesScheduleRowAndIsIdempotent(){var repo=new ClinicaRepositorio(db);var enabled=new ClinicAgendaBridge(db,new AgendaService(repo,new AtendimentoService(repo)),new BridgeOptions(CancelEnabled:true));var a=await Seed();var c=Command(a,"cancel");Assert.Equal("cancelled",(await enabled.Decide(c,default)).Status);Assert.Equal(StatusAgendamento.Cancelado,a.Status);Assert.Single(await db.Agendamentos.ToListAsync());await enabled.Decide(c,default);Assert.Single(await db.Auditoria.Where(e=>e.Acao=="AgendamentoCancelado").ToListAsync());}
    [Fact] public async Task MovedAppointmentRejectsOldResponse(){var a=await Seed();var c=Command(a,"cancel");a.DataHora=a.DataHora.AddHours(2);await db.SaveChangesAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>bridge.Decide(c,default));Assert.Equal(StatusAgendamento.Agendado,a.Status);}
    [Fact] public async Task WrongPhoneCannotCancel(){var a=await Seed();await Assert.ThrowsAsync<InvalidOperationException>(()=>bridge.Decide(Command(a,"cancel") with{Phone="5511988888888"},default));Assert.Equal(StatusAgendamento.Agendado,a.Status);}
    [Fact] public async Task CheckedInPatientCannotBeCancelledByReminder(){var a=await Seed();a.ChegadaEm=DateTime.Now;await db.SaveChangesAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>bridge.Decide(Command(a,"cancel"),default));}
    [Fact] public async Task CommandIdCannotBeReusedForAnotherAction(){var a=await Seed();var c=Command(a);await bridge.Decide(c,default);await Assert.ThrowsAsync<InvalidOperationException>(()=>bridge.Decide(c with{Decision="cancel"},default));Assert.Equal(StatusAgendamento.Agendado,a.Status);}
    [Fact] public async Task SnapshotUsesClinicCatalogAndSaoPauloOffset(){var a=await Seed();db.Especialidades.Add(new(){Codigo="Fisioterapia",Nome="Fisioterapia",Ativo=true});a.EspecialidadeConsultaCodigo="Fisioterapia";await db.SaveChangesAsync();var snap=await bridge.Snapshot(default);Assert.Single(snap.Appointments);Assert.Equal("Fisioterapia",snap.Appointments[0].SpecialtyName);Assert.Equal(TimeSpan.FromHours(-3),snap.Appointments[0].StartsAt.Offset);Assert.Equal("5511999990001",snap.Appointments[0].Phone);}
    [Fact] public async Task NotificationMarksCampaignWithoutConfirmingAttendance(){var a=await Seed();var cmd=Command(a,"notified");await bridge.Decide(cmd,default);await bridge.Decide(cmd,default);Assert.Equal(StatusContato.Enviado,(await db.Contatos.SingleAsync()).Status);Assert.Equal("notified",(await bridge.Snapshot(default)).Appointments[0].Confirmation);Assert.Equal(StatusAgendamento.Agendado,a.Status);Assert.Null(a.AtendimentoId);Assert.Single(await db.Auditoria.Where(e=>e.Acao=="CrmLembrete").ToListAsync());}
    public void Dispose(){db.Dispose();connection.Dispose();}
}
