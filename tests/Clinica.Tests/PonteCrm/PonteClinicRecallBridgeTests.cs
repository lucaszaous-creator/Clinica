using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Clinica.Tests;
public sealed class PonteClinicRecallBridgeTests:IDisposable
{
    readonly SqliteConnection connection=new("Data Source=:memory:");readonly ClinicaDbContext db;readonly ClinicRecallBridge bridge;
    static DateOnly Today=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).DateTime);
    public PonteClinicRecallBridgeTests(){connection.Open();db=new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(connection).Options);db.Database.EnsureCreated();bridge=new(db);}
    async Task<Paciente> Seed(){var p=new Paciente{Nome="Pessoa fictícia",Telefone="11999990001"};db.Pacientes.Add(p);db.Consentimentos.Add(new(){Paciente=p,Finalidade=FinalidadeConsentimento.ComunicacaoEMarketing,Concedido=true});await db.SaveChangesAsync();return p;}
    Atendimento Session(Paciente p,ModalidadeAtendimento m,int days,bool done=true)=>new(){Paciente=p,Modalidade=m,Data=Today.AddDays(-days),RealizadoEm=done?Today.AddDays(-days).ToDateTime(TimeOnly.MinValue):null};
    [Fact] public async Task RecentConsultationDoesNotHideBsvAndCombinedSessionCountsForBoth(){var p=await Seed();db.Atendimentos.AddRange(Session(p,ModalidadeAtendimento.BsvComAcupuntura,90),Session(p,ModalidadeAtendimento.Consulta,3));await db.SaveChangesAsync();var s=await bridge.Snapshot(default);Assert.Equal(3,s.Candidates.Single(c=>c.Kind=="return").Days);Assert.Equal(90,s.Candidates.Single(c=>c.Kind=="bsv").Days);Assert.Equal(90,s.Candidates.Single(c=>c.Kind=="acupuncture").Days);}
    [Fact] public async Task UnperformedAndReversedSessionsDoNotResetRecall(){var p=await Seed();var reversed=Session(p,ModalidadeAtendimento.BsvApenas,1);reversed.EstornadoEm=DateTime.Now;db.Atendimentos.AddRange(Session(p,ModalidadeAtendimento.BsvApenas,80),Session(p,ModalidadeAtendimento.BsvApenas,2,false),reversed);await db.SaveChangesAsync();Assert.Equal(80,(await bridge.Snapshot(default)).Candidates.Single(c=>c.Kind=="bsv").Days);}
    [Fact] public async Task FutureCombinedAppointmentSuppressesBsvAndAcupuncture(){var p=await Seed();db.Atendimentos.Add(Session(p,ModalidadeAtendimento.BsvComAcupuntura,90));db.Agendamentos.Add(new(){Paciente=p,DataHora=Today.AddDays(3).ToDateTime(TimeOnly.MinValue),ModalidadePrevista=ModalidadeAtendimento.BsvComAcupuntura});await db.SaveChangesAsync();Assert.All((await bridge.Snapshot(default)).Candidates,c=>Assert.Contains("agendado",c.BlockReason));}
    [Fact] public async Task RevokedConsentWinsAndSharedNumberNeedsHumanReview(){var p=await Seed();db.Atendimentos.Add(Session(p,ModalidadeAtendimento.Consulta,80));db.Consentimentos.Add(new(){Paciente=p,Finalidade=FinalidadeConsentimento.ComunicacaoEMarketing,Concedido=false,RegistradoEm=DateTime.Now.AddSeconds(1)});await db.SaveChangesAsync();Assert.False((await bridge.Snapshot(default)).Candidates.Single().Consent);}
    // out/2026: sem consentimento de comunicação o paciente continua na lista do recall SEM
    // bloqueio — a direção tirou a trava; o campo Consent segue só como informação.
    [Fact] public async Task SemConsentimento_o_recall_nao_bloqueia(){var p=new Paciente{Nome="Sem termo",Telefone="11999990002"};db.Pacientes.Add(p);db.Atendimentos.Add(Session(p,ModalidadeAtendimento.Consulta,80));await db.SaveChangesAsync();var c=(await bridge.Snapshot(default)).Candidates.Single();Assert.False(c.Consent);Assert.Equal("",c.BlockReason);}
    [Fact] public async Task ExistingClosedRecallAndFutureContactAreRespected(){var p=await Seed();db.Atendimentos.Add(Session(p,ModalidadeAtendimento.BsvApenas,90));var c=new AcompanhamentoPaciente{Paciente=p,Tipo=TipoAcompanhamento.Recall,Modalidade=ModalidadeAtendimento.BsvApenas,ReferenciaEm=Today.AddDays(-90).ToDateTime(TimeOnly.MinValue),EncerradoEm=DateTime.Now,ProximoContato=Today};db.Acompanhamentos.Add(c);await db.SaveChangesAsync();var s=await bridge.Snapshot(default);Assert.Contains(s.Candidates,x=>x.CaseId==c.Id&&x.BlockReason.Contains("encerrado"));Assert.Contains(s.Candidates,x=>x.Kind=="bsv"&&x.BlockReason.Contains("encerrado"));}
    [Fact] public async Task SmsRecallIsRecordedAsTheSmsChannel(){var p=await Seed();var n=new RecallNotice(Guid.NewGuid().ToString("N"),p.Id,null,"sms:1",DateTimeOffset.UtcNow,"sms");await bridge.Notice(n,default);var contato=await db.Contatos.SingleAsync();Assert.Equal(CanalContato.Sms,contato.Canal);Assert.Equal("CRM · SMS",contato.EnviadoPor);}
    [Fact] public async Task SentNoticeIsIdempotentAndAppendsWithoutChangingFollowup(){var p=await Seed();var c=new AcompanhamentoPaciente{Paciente=p,Tipo=TipoAcompanhamento.Recall,Modalidade=ModalidadeAtendimento.BsvApenas,ReferenciaEm=Today.AddDays(-90).ToDateTime(TimeOnly.MinValue),ProximoContato=Today};db.Acompanhamentos.Add(c);await db.SaveChangesAsync();var version=c.Versao;var n=new RecallNotice(Guid.NewGuid().ToString("N"),p.Id,c.Id,"fake-message",DateTimeOffset.UtcNow);await bridge.Notice(n,default);await bridge.Notice(n,default);Assert.Single(await db.Contatos.ToArrayAsync());Assert.Single(await db.ContatosAcompanhamento.ToArrayAsync());Assert.Equal(version,c.Versao);Assert.Equal(EtapaAcompanhamento.AContatar,c.Etapa);await Assert.ThrowsAsync<InvalidOperationException>(()=>bridge.Notice(n with{MessageId="other"},default));}
    public void Dispose(){db.Dispose();connection.Dispose();}
}
