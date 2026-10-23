using Clinica.Application.Servicos;
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
    static DateTimeOffset? Offset(DateTime? value) => value is {} d ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Unspecified), Zone.GetUtcOffset(d)) : null;
    public const string SemAutorizacao = "Sem autorização de mensagem";
    /// <summary>
    /// Os candidatos do recall do CRM (out/2026, fase 3 Recepção×CRM): SÓ quem está na FILA de
    /// recall da clínica (<c>Acompanhamentos</c> tipo Recall — a que a Recepção gera e trabalha no
    /// Acompanhamento), com três regras da clínica e não do CRM:
    /// - os DIAS sem vir são os do Gerente ("Recall: dias sem vir", <see cref="ParametrosService.ObterDiasInatividadeRecallAsync"/>),
    ///   devolvidos em <see cref="RecallSnapshot.InactivityDays"/> — o CRM não tem mais regra de dias;
    /// - sem consentimento VIGENTE de comunicação (<see cref="FinalidadeConsentimento.ComunicacaoEMarketing"/>)
    ///   o paciente fica BLOQUEADO ("Sem autorização de mensagem"): o recall usa um modelo de
    ///   marketing da Meta, que exige o aceite — colhido no termo do BSV ou no termo LGPD;
    /// - as listas que o CRM montava fora da fila ("sem retorno", "sem BSV", "sem acupuntura")
    ///   saíram: eram uma segunda definição de "quem está sumido", e a fila já é gerada pelos
    ///   mesmos dias do Gerente.
    /// </summary>
    public async Task<RecallSnapshot> Snapshot(CancellationToken ct)
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Zone);
        var today = DateOnly.FromDateTime(now.DateTime);
        var repo = new ClinicaRepositorio(db);
        var dias = await new ParametrosService(repo).ObterDiasInatividadeRecallAsync(ct);
        var cases = await db.Acompanhamentos.AsNoTracking().Where(c => c.Tipo == TipoAcompanhamento.Recall).ToListAsync(ct);
        var ids = cases.Select(c => c.PacienteId).Distinct().ToArray();
        var sessions = await db.Atendimentos.AsNoTracking().Where(a => ids.Contains(a.PacienteId) && a.RealizadoEm != null && a.EstornadoEm == null && a.Data <= today)
            .GroupBy(a => new {a.PacienteId, a.Modalidade})
            .Select(g => new {g.Key.PacienteId, g.Key.Modalidade, Last = g.Max(a => a.Data)}).ToListAsync(ct);
        var patients = await db.Pacientes.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new {p.Id,p.Nome,p.Telefone}).ToDictionaryAsync(p => p.Id,ct);
        var consent = (await repo.PacientesComConsentimentoVigenteAsync(FinalidadeConsentimento.ComunicacaoEMarketing,ids,ct)).ToHashSet();
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
        foreach (var source in cases)
        {
            if (!patients.TryGetValue(source.PacienteId,out var patient)) continue;
            var pid = source.PacienteId;
            var reference = DateOnly.FromDateTime(source.ReferenciaEm);
            var days = today.DayNumber-reference.DayNumber;
            var phone=ClinicAgendaBridge.Phone(patient.Telefone);
            var block = phone.Length==0?"Telefone ausente ou inválido":shared.Contains(phone)?"Telefone compartilhado: conferir paciente":
                !consent.Contains(pid)?SemAutorizacao:
                future.Any(a=>a.PacienteId==pid&&a.ModalidadePrevista==source.Modalidade)?"Retorno já agendado":
                sessions.Any(s=>s.PacienteId==pid&&s.Modalidade==source.Modalidade&&s.Last>reference)?"Sessão já realizada":
                source.EncerradoEm!=null?"Recall encerrado no sistema":
                source.Etapa==EtapaAcompanhamento.AguardandoPlano?"Aguardando autorização do plano":
                source.ProximoContato>today?"Respeitar próxima data de contato":
                days<dias?$"Ainda não atingiu {dias} dias sem vir (Configurações do Gerente)":"";
            var key = $"existing:{source.Id}";
            var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {key,reference,phone,block,Versions=new[]{source.Versao}}))));
            result.Add(new(key,pid,patient.Nome,phone,"existing",RotulosEnum.De(source.Modalidade),reference,days,source.Id,revision,consent.Contains(pid),block,lastContact.TryGetValue(pid,out var last)?Offset(last):null,source.ProximoContato));
        }
        return new(DateTimeOffset.UtcNow,result.ToArray(),dias);
    }
    /// <summary>
    /// UM PARAR SÓ (out/2026, fase 3 Recepção×CRM): o paciente respondeu PARAR no CRM, e a
    /// autorização de mensagens (<see cref="FinalidadeConsentimento.ComunicacaoEMarketing"/>) é
    /// revogada aqui também — senão o Clinica continuaria dizendo "autorizado" para quem pediu
    /// para parar, e o recall voltaria a convidá-lo se o CRM perdesse o próprio registro.
    ///
    /// Vale para TODA ficha com aquele telefone: quem segura o aparelho pediu para não receber,
    /// e o envio vai para o número, não para a ficha (o telefone não é identidade — por isso
    /// mesmo nenhuma ficha é escolhida). Grava como o termo grava uma revogação: a linha vigente
    /// ganha <c>RevogadoEm</c> e uma linha nova registra a recusa, com a mensagem como evidência.
    /// Repetir o mesmo pedido não faz nada (já não há o que revogar).
    /// </summary>
    public Task<ClinicDecisionResult> OptOut(ConsentOptOut input, CancellationToken ct) => TransacaoDaPonte.Executar(db, () => OptOutCore(input, ct), ct);
    async Task<ClinicDecisionResult> OptOutCore(ConsentOptOut input, CancellationToken ct)
    {
        var phone = ClinicAgendaBridge.Phone(input.Phone);
        if(!Guid.TryParseExact(input.CommandId,"N",out _)||phone.Length==0||string.IsNullOrWhiteSpace(input.MessageId)||input.MessageId.Length>300||input.At>DateTimeOffset.UtcNow.AddMinutes(5))
            throw new InvalidOperationException("Pedido de PARAR inválido.");
        var canal = input.Channel=="sms"?"SMS":"WhatsApp";
        var operador = "CRM · "+canal;
        var fichas = (await db.Pacientes.AsNoTracking().Select(p => new {p.Id,p.Telefone}).ToListAsync(ct))
            .Where(p => ClinicAgendaBridge.Phone(p.Telefone)==phone).Select(p => p.Id).ToArray();
        if (fichas.Length==0) return new("recorded","Nenhuma ficha da clínica com este telefone");
        var vigentes = await db.Consentimentos.Where(c => fichas.Contains(c.PacienteId) && c.Finalidade==FinalidadeConsentimento.ComunicacaoEMarketing).ToListAsync(ct);
        var revogadas = 0;
        foreach (var atual in vigentes.GroupBy(c => c.PacienteId)
                     .Select(g => g.OrderByDescending(c => c.RegistradoEm).ThenByDescending(c => c.Id).First())
                     .Where(c => c.Vigente))
        {
            var observacao = $"PARAR pelo {canal} (CRM, mensagem {input.MessageId})";
            atual.RevogadoEm = DateTime.Now;
            atual.Observacoes = Limitar(string.IsNullOrWhiteSpace(atual.Observacoes) ? "Revogado: "+observacao : atual.Observacoes+" — revogado: "+observacao);
            var (registro, evento) = ConsentimentoService.Montar(atual.PacienteId, FinalidadeConsentimento.ComunicacaoEMarketing, false, null, operador, Limitar(observacao));
            // A ação da trilha é a da REVOGAÇÃO (a mesma do ConsentimentoService.RevogarAsync): é
            // pelo nome que uma investigação procura "quando ele pediu para parar".
            evento.Acao = "ConsentimentoRevogado";
            evento.Detalhe = Limitar($"{FinalidadeConsentimento.ComunicacaoEMarketing} — {observacao}");
            db.Consentimentos.Add(registro);
            db.Auditoria.Add(evento);
            revogadas++;
        }
        if (revogadas==0) return new("recorded","Autorização de mensagens já não estava vigente na clínica");
        await db.SaveChangesAsync(ct);
        return new("recorded",revogadas==1?"Autorização de mensagens revogada na clínica":$"Autorização de mensagens revogada em {revogadas} fichas da clínica");
    }
    static string Limitar(string texto) => texto.Length<=500?texto:texto[..500];
    public Task<ClinicDecisionResult> Notice(RecallNotice input, CancellationToken ct) => TransacaoDaPonte.Executar(db, () => NoticeCore(input, ct), ct);
    async Task<ClinicDecisionResult> NoticeCore(RecallNotice input, CancellationToken ct)
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
