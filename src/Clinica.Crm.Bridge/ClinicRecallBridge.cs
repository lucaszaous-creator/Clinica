using Clinica.Crm.Contracts;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Clinica.Crm.Bridge;

public sealed class ClinicRecallBridge(ClinicaDbContext db)
{
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    static bool Matches(string kind, ModalidadeAtendimento m) => kind switch {
        "return" => true,
        "bsv" => m is ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura,
        "acupuncture" => m is ModalidadeAtendimento.AcupunturaSimples or ModalidadeAtendimento.AcupunturaComEletro or ModalidadeAtendimento.BsvComAcupuntura,
        _ => false
    };
    static DateTimeOffset? Offset(DateTime? value) => value is {} d ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Unspecified), Zone.GetUtcOffset(d)) : null;
    public async Task<RecallSnapshot> Snapshot(CancellationToken ct)
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone);
        var today = DateOnly.FromDateTime(now.DateTime);
        var sessions = await db.Atendimentos.AsNoTracking().Where(a => a.RealizadoEm != null && a.EstornadoEm == null && a.Data <= today)
            .GroupBy(a => new {a.PacienteId, a.Modalidade})
            .Select(g => new {g.Key.PacienteId, g.Key.Modalidade, Last = g.Max(a => a.Data)}).ToListAsync(ct);
        var cases = await db.Acompanhamentos.AsNoTracking().Where(c => c.Tipo == TipoAcompanhamento.Recall).ToListAsync(ct);
        var ids = sessions.Select(s => s.PacienteId).Concat(cases.Select(c => c.PacienteId)).Distinct().ToArray();
        var patients = await db.Pacientes.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new {p.Id,p.Nome,p.Telefone}).ToDictionaryAsync(p => p.Id,ct);
        var consent = (await new ClinicaRepositorio(db).PacientesComConsentimentoVigenteAsync(FinalidadeConsentimento.ComunicacaoEMarketing,ids,ct)).ToHashSet();
        var future = await db.Agendamentos.AsNoTracking().Where(a => ids.Contains(a.PacienteId) && a.Status == StatusAgendamento.Agendado && a.DataHora >= today.ToDateTime(TimeOnly.MinValue))
            .Select(a => new {a.PacienteId,a.ModalidadePrevista}).ToListAsync(ct);
        var contacts = await db.ContatosAcompanhamento.AsNoTracking().Where(c => c.Canal != null && ids.Contains(c.AcompanhamentoPaciente!.PacienteId))
            .Select(c => new {PatientId = c.AcompanhamentoPaciente!.PacienteId,c.Em}).ToListAsync(ct);
        var legacy = await db.Contatos.AsNoTracking().Where(c => c.Tipo == TipoContato.Recall && c.EnviadoEm != null && ids.Contains(c.PacienteId))
            .Select(c => new {PatientId=c.PacienteId,Em=c.EnviadoEm!.Value}).ToListAsync(ct);
        var lastContact = contacts.Concat(legacy).GroupBy(c => c.PatientId).ToDictionary(g => g.Key,g => g.Max(c => c.Em));
        var allPhones = await db.Pacientes.AsNoTracking().Select(p=>p.Telefone).ToListAsync(ct);
        var shared = allPhones.GroupBy(ClinicAgendaBridge.Phone).Where(g => g.Count()>1).Select(g=>g.Key).ToHashSet();
        var result = new List<RecallCandidate>();
        void Add(int pid, string kind, ModalidadeAtendimento? modality, DateOnly reference, AcompanhamentoPaciente? source)
        {
            if (!patients.TryGetValue(pid,out var patient)) return;
            bool Match(ModalidadeAtendimento m) => modality is {} exact ? m==exact : Matches(kind,m);
            var related = cases.Where(c=>c.PacienteId==pid && Match(c.Modalidade) && c.ReferenciaEm.Date>=reference.ToDateTime(TimeOnly.MinValue)).ToArray();
            var phone=ClinicAgendaBridge.Phone(patient.Telefone);
            var block = !consent.Contains(pid)?"Contato não autorizado no cadastro":phone.Length==0?"Telefone ausente ou inválido":shared.Contains(phone)?"Telefone compartilhado: conferir paciente":
                future.Any(a=>a.PacienteId==pid&&Match(a.ModalidadePrevista))?"Retorno já agendado":
                source is not null && sessions.Any(s=>s.PacienteId==pid&&Match(s.Modalidade)&&s.Last>reference)?"Sessão já realizada":
                related.Any(c=>c.EncerradoEm!=null)?"Recall encerrado no sistema":
                related.Any(c=>c.Etapa==EtapaAcompanhamento.AguardandoPlano)?"Aguardando autorização do plano":
                related.Any(c=>c.ProximoContato>today)?"Respeitar próxima data de contato":"";
            var key = source is null?$"{kind}:{pid}":$"existing:{source.Id}";
            var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {key,reference,phone,block,Versions=related.Select(c=>c.Versao).Order().ToArray()}))));
            result.Add(new(key,pid,patient.Nome,phone,kind,modality is {} m?RotulosEnum.De(m):kind=="bsv"?"BSV (inclui BSV + acupuntura)":kind=="acupuncture"?"Acupuntura (todas as variantes)":"Qualquer atendimento",reference,today.DayNumber-reference.DayNumber,source?.Id,revision,consent.Contains(pid),block,lastContact.TryGetValue(pid,out var last)?Offset(last):null,source?.ProximoContato));
        }
        foreach(var patient in sessions.GroupBy(s=>s.PacienteId))
            foreach(var kind in new[]{"return","bsv","acupuncture"}) {
                var relevant=patient.Where(s=>Matches(kind,s.Modalidade)).ToArray();
                if(relevant.Length>0)Add(patient.Key,kind,null,relevant.Max(s=>s.Last),null);
            }
        foreach(var c in cases)Add(c.PacienteId,"existing",c.Modalidade,DateOnly.FromDateTime(c.ReferenciaEm),c);
        return new(DateTimeOffset.UtcNow,result.ToArray());
    }
    public async Task<ClinicDecisionResult> Notice(RecallNotice input,CancellationToken ct)
    {
        if(!Guid.TryParseExact(input.CommandId,"N",out var command)||input.PatientId<=0||string.IsNullOrWhiteSpace(input.MessageId)||input.MessageId.Length>300||input.SentAt>DateTimeOffset.UtcNow.AddMinutes(5))
            throw new InvalidOperationException("Registro de envio inválido.");
        var origin="CRM:RECALL:"+input.CommandId;
        var marker=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var previous=await db.Contatos.SingleOrDefaultAsync(c=>c.Tipo==TipoContato.Recall&&c.Origem==origin,ct);
        if(previous is not null) {
            if(previous.Comentario!=marker)throw new InvalidOperationException("Identificador reutilizado.");
            return new("recorded","Envio de recall registrado na clínica");
        }
        if(!await db.Pacientes.AnyAsync(p=>p.Id==input.PatientId,ct))throw new InvalidOperationException("Paciente não encontrado.");
        var at=TimeZoneInfo.ConvertTime(input.SentAt,Zone).DateTime;
        db.Contatos.Add(new(){PacienteId=input.PatientId,Tipo=TipoContato.Recall,Origem=origin,Referencia=DateOnly.FromDateTime(at),Status=StatusContato.Enviado,Canal=input.Channel=="sms"?CanalContato.Sms:CanalContato.WhatsApp,EnviadoEm=at,EnviadoPor=input.Channel=="sms"?"CRM · SMS":"CRM · WhatsApp",Comentario=marker});
        if(input.CaseId is {} caseId) {
            var c=await db.Acompanhamentos.SingleOrDefaultAsync(c=>c.Id==caseId&&c.PacienteId==input.PatientId&&c.Tipo==TipoAcompanhamento.Recall,ct)??throw new InvalidOperationException("Recall não encontrado.");
            db.ContatosAcompanhamento.Add(new(){AcompanhamentoPacienteId=c.Id,Idempotencia=command,Em=at,Operador=input.Channel=="sms"?"CRM · SMS":"CRM · WhatsApp",Canal=input.Channel=="sms"?CanalContato.Sms:CanalContato.WhatsApp,Resultado=input.Channel=="sms"?"SMS de recall aceito pelo provedor":"Mensagem de recall aceita pelo provedor de WhatsApp",Observacao="Envio automático registrado. Aceite não comprova entrega ou resposta. ID: "+input.MessageId});
        }
        db.Auditoria.Add(new(){PacienteId=input.PatientId,Acao="CrmRecall",Operador="CRM · WhatsApp",DataHora=at,Detalhe=origin});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return new("recorded","Envio de recall registrado na clínica");
    }
}
