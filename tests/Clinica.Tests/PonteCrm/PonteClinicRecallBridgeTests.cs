using Clinica.Application.Servicos;
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
    AcompanhamentoPaciente Case(Paciente p,ModalidadeAtendimento m,int days)=>new(){Paciente=p,Tipo=TipoAcompanhamento.Recall,Modalidade=m,ReferenciaEm=Today.AddDays(-days).ToDateTime(TimeOnly.MinValue),ProximoContato=Today};
    // Fase 3 Recepção×CRM (out/2026): o CRM só convida quem está na FILA de recall da clínica; as
    // listas "sem retorno / sem BSV / sem acupuntura" que a ponte montava fora dela saíram.
    [Fact] public async Task SoQuemEstaNaFilaDeRecallViraCandidato(){var p=await Seed();var q=await Seed();db.Atendimentos.AddRange(Session(p,ModalidadeAtendimento.BsvComAcupuntura,90),Session(q,ModalidadeAtendimento.BsvApenas,90));var c=Case(q,ModalidadeAtendimento.BsvApenas,90);db.Acompanhamentos.Add(c);await db.SaveChangesAsync();var s=await bridge.Snapshot(default);var x=Assert.Single(s.Candidates);Assert.Equal("existing",x.Kind);Assert.Equal(c.Id,x.CaseId);Assert.Equal(90,x.Days);}
    // Os dias são os do Gerente ("Recall: dias sem vir") e voltam no snapshot para o CRM mostrar.
    [Fact] public async Task OsDiasSaoOsDoGerente(){var p=await Seed();db.Acompanhamentos.Add(Case(p,ModalidadeAtendimento.BsvApenas,30));await db.SaveChangesAsync();
        var s=await bridge.Snapshot(default);Assert.Equal(CampanhaService.DiasInatividadePadrao,s.InactivityDays);Assert.Contains("Ainda não atingiu",s.Candidates.Single().BlockReason);
        await new ParametrosService(new ClinicaRepositorio(db)).SalvarDiasInatividadeRecallAsync(20);s=await bridge.Snapshot(default);Assert.Equal(20,s.InactivityDays);Assert.Equal("",s.Candidates.Single().BlockReason);}
    [Fact] public async Task UnperformedAndReversedSessionsDoNotResetRecall(){var p=await Seed();var reversed=Session(p,ModalidadeAtendimento.BsvApenas,1);reversed.EstornadoEm=DateTime.Now;db.Atendimentos.AddRange(Session(p,ModalidadeAtendimento.BsvApenas,80),Session(p,ModalidadeAtendimento.BsvApenas,2,false),reversed);db.Acompanhamentos.Add(Case(p,ModalidadeAtendimento.BsvApenas,80));await db.SaveChangesAsync();var c=(await bridge.Snapshot(default)).Candidates.Single();Assert.Equal(80,c.Days);Assert.Equal("",c.BlockReason);}
    [Fact] public async Task FutureAppointmentOfTheSameModalitySuppressesTheRecall(){var p=await Seed();db.Acompanhamentos.Add(Case(p,ModalidadeAtendimento.BsvComAcupuntura,90));db.Agendamentos.Add(new(){Paciente=p,DataHora=Today.AddDays(3).ToDateTime(TimeOnly.MinValue),ModalidadePrevista=ModalidadeAtendimento.BsvComAcupuntura});await db.SaveChangesAsync();Assert.All((await bridge.Snapshot(default)).Candidates,c=>Assert.Contains("agendado",c.BlockReason));}
    [Fact] public async Task RevokedConsentWins(){var p=await Seed();db.Acompanhamentos.Add(Case(p,ModalidadeAtendimento.Consulta,80));db.Consentimentos.Add(new(){Paciente=p,Finalidade=FinalidadeConsentimento.ComunicacaoEMarketing,Concedido=false,RegistradoEm=DateTime.Now.AddSeconds(1)});await db.SaveChangesAsync();var c=(await bridge.Snapshot(default)).Candidates.Single();Assert.False(c.Consent);Assert.Equal(ClinicRecallBridge.SemAutorizacao,c.BlockReason);}
    // Fase 3 Recepção×CRM (out/2026): a trava voltou. O recall usa modelo de MARKETING da Meta, que
    // exige o aceite; sem consentimento vigente de comunicação o paciente fica bloqueado com o motivo.
    [Fact] public async Task SemConsentimento_o_recall_bloqueia(){var p=new Paciente{Nome="Sem termo",Telefone="11999990002"};db.Pacientes.Add(p);db.Acompanhamentos.Add(Case(p,ModalidadeAtendimento.Consulta,80));await db.SaveChangesAsync();var c=(await bridge.Snapshot(default)).Candidates.Single();Assert.False(c.Consent);Assert.Equal("Sem autorização de mensagem",c.BlockReason);}
    [Fact] public async Task ExistingClosedRecallAndFutureContactAreRespected(){var p=await Seed();var c=Case(p,ModalidadeAtendimento.BsvApenas,90);c.EncerradoEm=DateTime.Now;var futuro=Case(p,ModalidadeAtendimento.AcupunturaSimples,90);futuro.ProximoContato=Today.AddDays(5);db.Acompanhamentos.AddRange(c,futuro);await db.SaveChangesAsync();var s=await bridge.Snapshot(default);Assert.Contains(s.Candidates,x=>x.CaseId==c.Id&&x.BlockReason.Contains("encerrado"));Assert.Contains(s.Candidates,x=>x.CaseId==futuro.Id&&x.BlockReason.Contains("próxima data"));}
    [Fact] public async Task SmsRecallIsRecordedAsTheSmsChannel(){var p=await Seed();var n=new RecallNotice(Guid.NewGuid().ToString("N"),p.Id,null,"sms:1",DateTimeOffset.UtcNow,"sms");await bridge.Notice(n,default);var contato=await db.Contatos.SingleAsync();Assert.Equal(CanalContato.Sms,contato.Canal);Assert.Equal("CRM · SMS",contato.EnviadoPor);}
    [Fact] public async Task SentNoticeIsIdempotentAndAppendsWithoutChangingFollowup(){var p=await Seed();var c=new AcompanhamentoPaciente{Paciente=p,Tipo=TipoAcompanhamento.Recall,Modalidade=ModalidadeAtendimento.BsvApenas,ReferenciaEm=Today.AddDays(-90).ToDateTime(TimeOnly.MinValue),ProximoContato=Today};db.Acompanhamentos.Add(c);await db.SaveChangesAsync();var version=c.Versao;var n=new RecallNotice(Guid.NewGuid().ToString("N"),p.Id,c.Id,"fake-message",DateTimeOffset.UtcNow);await bridge.Notice(n,default);await bridge.Notice(n,default);Assert.Single(await db.Contatos.ToArrayAsync());Assert.Single(await db.ContatosAcompanhamento.ToArrayAsync());Assert.Equal(version,c.Versao);Assert.Equal(EtapaAcompanhamento.AContatar,c.Etapa);await Assert.ThrowsAsync<InvalidOperationException>(()=>bridge.Notice(n with{MessageId="other"},default));}
    // Um PARAR só (fase 3 Recepção×CRM): o PARAR do CRM revoga a autorização de mensagens de toda
    // ficha com aquele telefone; a linha antiga fica (com RevogadoEm) e a recusa é uma linha nova.
    [Fact] public async Task ParaRevogaAAutorizacaoDeTodaFichaComOTelefone(){var p=await Seed();var q=await Seed();var outro=new Paciente{Nome="Outro número",Telefone="11988887777"};db.Pacientes.Add(outro);db.Consentimentos.Add(new(){Paciente=outro,Finalidade=FinalidadeConsentimento.ComunicacaoEMarketing,Concedido=true});await db.SaveChangesAsync();
        var cmd=new ConsentOptOut(Guid.NewGuid().ToString("N"),"5511999990001","wamid.1",DateTimeOffset.UtcNow);var r=await bridge.OptOut(cmd,default);Assert.Contains("2 fichas",r.Description);
        var repo=new ClinicaRepositorio(db);var vigentes=await repo.PacientesComConsentimentoVigenteAsync(FinalidadeConsentimento.ComunicacaoEMarketing,[p.Id,q.Id,outro.Id]);Assert.Equal(new[]{outro.Id},vigentes.ToArray());
        Assert.Equal(2,await db.Consentimentos.CountAsync(c=>c.RevogadoEm!=null));Assert.Equal(2,await db.Consentimentos.CountAsync(c=>!c.Concedido));Assert.Equal(2,await db.Auditoria.CountAsync(a=>a.Acao=="ConsentimentoRevogado"));
        Assert.Contains("já não estava vigente",(await bridge.OptOut(cmd,default)).Description);Assert.Equal(2,await db.Consentimentos.CountAsync(c=>!c.Concedido));}
    [Fact] public async Task ParaComTelefoneInvalidoERecusado(){await Assert.ThrowsAsync<InvalidOperationException>(()=>bridge.OptOut(new(Guid.NewGuid().ToString("N"),"abc","wamid.1",DateTimeOffset.UtcNow),default));}
    public void Dispose(){db.Dispose();connection.Dispose();}
}
