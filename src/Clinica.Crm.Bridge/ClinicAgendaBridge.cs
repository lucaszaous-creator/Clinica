using Clinica.Application.Modelos;
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

// "Remarcar" vindo do WhatsApp segue a MESMA regra do link do SMS (docs/canal-sms.md §3.3):
// vira "Não vem — remarcar" na agenda e a recepção liga, remarca e oferece a vaga. "Cancelar"
// vira "Pediu para cancelar" e a recepção aceita na própria agenda (out/2026). Cancelar
// sozinho só com CRM_CANCEL_ENABLED=true (decisão da clínica).
public sealed record BridgeOptions(bool CancelEnabled=false);
public sealed class ClinicAgendaBridge(ClinicaDbContext db,AgendaService agenda,BridgeOptions? options=null)
{
    static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    static DateTime Now()=>TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,Zone).DateTime;
    public static string Phone(string? phone)
    {
        var digits=Telefone.SemPrefixoDeDiscagem(new string((phone??"").Where(char.IsAsciiDigit).ToArray()));
        if(digits.Length is 10 or 11)digits="55"+digits;
        return digits.Length is >=10 and <=15?digits:"";
    }
    public static string Revision(Agendamento a)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{a.Id,a.PacienteId,a.DataHora,a.Status,a.ProfissionalId,a.SalaId,a.DuracaoMinutos,a.Encaixe,a.Observacoes,a.ModalidadePrevista,a.ModalidadeCodigo,a.EspecialidadeConsulta,a.EspecialidadeConsultaCodigo,a.ChegadaEm,a.InicioAtendimentoEm,a.AtendimentoId,Phone=Phone(a.Paciente?.Telefone)}))));
    public async Task<ClinicSnapshot> Snapshot(CancellationToken ct)
    {
        var now=Now();var until=now.AddDays(32);
        var catalog=await db.Especialidades.AsNoTracking().OrderBy(s=>s.Nome).Select(s=>new Specialty(s.Codigo,s.Nome,s.Ativo)).ToArrayAsync(ct);
        if(catalog.Length==0)catalog=Enum.GetValues<Especialidade>().Select(e=>new Specialty(e.ToString(),EspecialidadeInfo.NomeExibicao(e))).ToArray();
        var appointments=await db.Agendamentos.AsNoTracking().Include(a=>a.Paciente).Include(a=>a.Profissional).Where(a=>a.DataHora>=now.AddDays(-1)&&a.DataHora<until).OrderBy(a=>a.DataHora).Take(5000).ToListAsync(ct);
        var ids=appointments.Select(a=>a.Id).ToArray();
        var contacts=await db.Contatos.AsNoTracking().Where(c=>c.Tipo==TipoContato.ConfirmacaoSessao&&c.AgendamentoId!=null&&ids.Contains(c.AgendamentoId.Value)).ToListAsync(ct);
        return new(DateTimeOffset.UtcNow,catalog,appointments.Select(a=>{
            var code=a.EspecialidadeConsultaCodigo??a.EspecialidadeConsulta?.ToString()??"";
            var contact=contacts.FirstOrDefault(c=>c.AgendamentoId==a.Id);
            var confirmation=contact?.Status==StatusContato.Respondido?"responded":contact?.Status==StatusContato.Enviado?"notified":contact?.Status==StatusContato.Dispensado?"dismissed":"pending";
            // A resposta do paciente pela MESMA regra do balcão (RodadaDeConfirmacao.Classificar):
            // o CRM avisa a recepção de quem confirmou e de quem não vem.
            var answer=AnswerOf(contact);
            return new ClinicAppointment(a.Id,a.PacienteId,a.Paciente?.Nome??"Paciente",Phone(a.Paciente?.Telefone),new DateTimeOffset(DateTime.SpecifyKind(a.DataHora,DateTimeKind.Unspecified),Zone.GetUtcOffset(a.DataHora)),code,catalog.FirstOrDefault(s=>s.Code==code)?.Name??code,a.Profissional?.Nome??"",a.Status==StatusAgendamento.Agendado?"scheduled":a.Status.ToString(),Revision(a),confirmation,answer,At(contact?.RespondidoEm),At(contact?.EnviadoEm),At(a.CriadoEm));
        }).ToArray());
    }
    public static string AnswerOf(ContatoCampanha? c) => c is null ? "" : RodadaDeConfirmacao.Classificar(c.Status, c.EnviadoEm, c.Comentario) switch
    {
        SituacaoConfirmacao.Confirmou => "confirmed",
        SituacaoConfirmacao.PediuRemarcar or SituacaoConfirmacao.PediuCancelar => "not-coming",
        _ => ""
    };
    static DateTimeOffset? At(DateTime? local) => local is { } d ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Unspecified), Zone.GetUtcOffset(d)) : null;
    public Task<ClinicDecisionResult> Decide(ClinicDecision input, CancellationToken ct) => TransacaoDaPonte.Executar(db, () => DecideCore(input, ct), ct);
    async Task<ClinicDecisionResult> DecideCore(ClinicDecision input, CancellationToken ct)
    {
        if(!Guid.TryParseExact(input.CommandId,"N",out _)||input.AppointmentId<=0||input.Decision is not("confirm" or "cancel" or "reschedule" or "notified")||string.IsNullOrWhiteSpace(input.MessageId)||input.MessageId.Length>300||(input.Note?.Length??0)>300||input.Phone is null||input.Phone.Length is <10 or >15||input.Revision is null||input.Revision.Length!=64)
            throw new InvalidOperationException("Comando inválido.");
        var prefix="CRM:"+input.CommandId+":";
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        var marker=prefix+hash;
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var prior=await db.Auditoria.AsNoTracking().FirstOrDefaultAsync(a=>(a.Acao=="CrmConfirmacao"||a.Acao=="CrmCancelamento"||a.Acao=="CrmLembrete"||a.Acao=="CrmPedidoRemarcar")&&a.Detalhe!=null&&a.Detalhe.StartsWith(prefix),ct);
        if(prior is not null){if(prior.Detalhe!=marker)throw new InvalidOperationException("Comando já utilizado.");return Result(input.Decision,prior.Acao=="CrmCancelamento"&&options?.CancelEnabled==true);}
        var ap=await db.Agendamentos.Include(a=>a.Paciente).SingleOrDefaultAsync(a=>a.Id==input.AppointmentId,ct)??throw new InvalidOperationException("Horário não encontrado.");
        if(ap.Status!=StatusAgendamento.Agendado||ap.DataHora<=Now()||ap.ChegadaEm is not null||ap.InicioAtendimentoEm is not null||ap.AtendimentoId is not null||Revision(ap)!=input.Revision||Phone(ap.Paciente?.Telefone)!=input.Phone)
            throw new InvalidOperationException("Horário ou contato alterado.");
        var contact=await db.Contatos.SingleOrDefaultAsync(c=>c.Tipo==TipoContato.ConfirmacaoSessao&&c.AgendamentoId==ap.Id,ct);
        if(contact is null){contact=new(){PacienteId=ap.PacienteId,AgendamentoId=ap.Id,Tipo=TipoContato.ConfirmacaoSessao,Origem=ContatoCampanha.OrigemDeAgendamento(ap.Id),Referencia=DateOnly.FromDateTime(ap.DataHora)};db.Contatos.Add(contact);}
        if(input.Decision=="notified")
        {
            if(contact.Status==StatusContato.Pendente)contact.Status=StatusContato.Enviado;
            // O remetente é a marca do lembrete AUTOMÁTICO: é por ela que o silêncio vira "sem resposta" no sino (AvisosDeConfirmacao).
            contact.EnviadoEm??=Now();contact.EnviadoPor=RodadaDeConfirmacao.LembreteAutomatico;contact.Canal=CanalContato.WhatsApp;
            db.Auditoria.Add(new(){Acao="CrmLembrete",Operador=RodadaDeConfirmacao.LembreteAutomatico,PacienteId=ap.PacienteId,Detalhe=marker,DataHora=Now()});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Result(input.Decision,false);
        }
        contact.Status=StatusContato.Respondido;contact.RespondidoEm=Now();contact.EnviadoEm??=Now();contact.Canal=CanalContato.WhatsApp;
        // O comentário é o que o classificador da clínica lê (RodadaDeConfirmacao.Classificar):
        // texto livre num "não vou" seria lido como "Confirmou" na agenda do balcão.
        var cancel=input.Decision=="cancel"&&options?.CancelEnabled==true;
        // "Cancelar" sem o cancelamento automático vira PEDIDO: a recepção aceita na própria agenda.
        contact.Comentario=input.Decision switch{"confirm"=>Clinica.Application.Modelos.RodadaDeConfirmacao.RespostaConfirmouPeloWhatsApp,
            "cancel" when !cancel=>Clinica.Application.Modelos.RodadaDeConfirmacao.RespostaCancelar,
            _=>Clinica.Application.Modelos.RodadaDeConfirmacao.RespostaRemarcar};
        db.Auditoria.Add(new(){Acao=input.Decision=="confirm"?"CrmConfirmacao":input.Decision=="cancel"?"CrmCancelamento":"CrmPedidoRemarcar",Operador="CRM · resposta do paciente",PacienteId=ap.PacienteId,Detalhe=marker,DataHora=Now()});
        // Reuse the clinic's cancellation logic: this also handles related billing guides.
        // Confirmation deliberately does not call ConfirmarPresencaAsync.
        if(cancel)await agenda.CancelarAsync(ap.Id,"CRM · cancelamento solicitado pelo paciente",ct);
        else await db.SaveChangesAsync(ct);
        // "Remarcar" vira PEDIDO no # Recepção (@SemDor, out/2026): a recepção oferece horários e o
        // paciente escolhe pelo WhatsApp. Na mesma transação — o pedido existe se, e só se, a resposta foi gravada.
        if(input.Decision=="reschedule")await RemarcacaoPeloPacienteService.RegistrarPedidoAsync(db,ap,input.Phone,Now(),ct,input.Note);
        await tx.CommitAsync(ct);return Result(input.Decision,cancel);
    }
    static ClinicDecisionResult Result(string decision,bool cancelled)=>decision switch{
        "notified"=>new("notified","Lembrete registrado na clínica"),
        "confirm"=>new("confirmed","Intenção de comparecer registrada na clínica"),
        "cancel" when cancelled=>new("cancelled","Agendamento cancelado na clínica a pedido do paciente"),
        "cancel"=>new("cancel-requested","Paciente pediu para cancelar: a agenda mostra o pedido para a recepção aceitar"),
        _=>new("reschedule-requested","Pedido de remarcação registrado na agenda da clínica")};
}
