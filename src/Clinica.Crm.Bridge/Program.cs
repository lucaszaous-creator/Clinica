using Clinica.Crm.Bridge;
using Clinica.Crm.Contracts;
using Clinica.Infrastructure;
using System.Security.Cryptography;
using System.Text;

var builder=WebApplication.CreateBuilder(args);
// Serviço privado: sem escuta pública e sem cookies de navegador. Fica ao lado do banco da
// clínica. Escuta no 127.0.0.1 (padrão) ou no IP do túnel até o CRM — nunca na internet
// (ver RedeDaPonte e docs/vps-do-crm.md).
var escuta=RedeDaPonte.Escuta(builder.Configuration["CRM_BRIDGE_URL"]);
var clientes=RedeDaPonte.Clientes(builder.Configuration["CRM_BRIDGE_ALLOWED_CLIENTS"]);
builder.WebHost.UseUrls(escuta.ToString().TrimEnd('/'));
var key=builder.Configuration["CRM_BRIDGE_KEY"]??"";
if(key.Length<32)throw new InvalidOperationException("Configure CRM_BRIDGE_KEY com ao menos 32 caracteres.");
var connection=builder.Configuration.GetConnectionString("Clinica");
if(string.IsNullOrWhiteSpace(connection))throw new InvalidOperationException("Configure a conexão da clínica. A ponte não cria nem migra bancos.");
builder.Services.AddClinica(connection);
builder.Services.AddSingleton(new BridgeOptions(builder.Configuration["CRM_CANCEL_ENABLED"]=="true"));
builder.Services.AddScoped<ClinicAgendaBridge>();
builder.Services.AddScoped<ClinicRecallBridge>();
builder.Services.AddScoped<ClinicSchedulingBridge>();
builder.Services.AddScoped<ClinicBookingBridge>();
builder.Services.AddScoped<ClinicRelationshipBridge>();
builder.Services.AddScoped<ClinicTeamBridge>();
builder.Services.AddScoped<ClinicPatientBridge>();
builder.Services.AddScoped<ClinicSummaryBridge>();
builder.Services.AddScoped<ClinicDispatchBridge>();
// O documento pelo WhatsApp (@SemDor fase 4, out/2026): "atestado Maria 2 dias" vira um RASCUNHO e
// a resposta leva um link de uso único para o portal, onde o médico revisa, emite e assina. Desligado
// sem CRM_STAFF_DOCUMENTS_ENABLED=true e o endereço https do portal (CRM_STAFF_DOCUMENTS_PORTAL_URL).
builder.Services.AddSingleton(new OpcoesDocumentoPeloWhatsApp(builder.Configuration["CRM_STAFF_DOCUMENTS_ENABLED"]=="true",
    builder.Configuration["CRM_STAFF_DOCUMENTS_PORTAL_URL"]??""));
builder.Services.AddScoped<AssistenteWhatsAppService>();
// O @SemDor com IA nos apps (Clinica#307): a fila de perguntas do chat dos apps e os avisos do CRM.
builder.Services.AddScoped(sp=>new PerguntasAoSemDorService(sp.GetRequiredService<ClinicaDbContext>()));
builder.Services.AddScoped<ClinicSemDorBridge>();
// O lembrete de SMS 24h antes (LembreteSms__Ligado) SAIU na fase 3 Recepção×CRM (out/2026): a
// confirmação é só a do WhatsApp no dia, pelo CRM, e quem manda SMS é só o CRM (CRM_SMS_*).
// Não há lembrete por e-mail (out/2026, decisão da direção): a clínica não colhe e-mail no
// cadastro, e o e-mail ficou só para o documento que a equipe envia ao paciente.
builder.Logging.AddFilter("Microsoft.AspNetCore",LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore",LogLevel.Critical);
var app=builder.Build();
app.Use(async(ctx,next)=>{
    if(!RedeDaPonte.Permitido(ctx.Connection.RemoteIpAddress,clientes)){ctx.Response.StatusCode=403;return;}
    var expected=SHA256.HashData(Encoding.UTF8.GetBytes("Bearer "+key));
    var actual=SHA256.HashData(Encoding.UTF8.GetBytes(ctx.Request.Headers.Authorization.ToString()));
    if(!CryptographicOperations.FixedTimeEquals(expected,actual)){ctx.Response.StatusCode=401;return;}
    ctx.Response.Headers.CacheControl="no-store";
    try{await next();}
    // A recusa vai ao CRM com frase genérica, mas o MOTIVO fica no log da ponte: sem ele,
    // "a operação não é permitida" na VPS não diz a ninguém o que conferir.
    catch(InvalidOperationException ex){app.Logger.LogWarning("Recusa em {Rota}: {Motivo}",ctx.Request.Path.Value,ex.Message);ctx.Response.StatusCode=409;await ctx.Response.WriteAsJsonAsync(new{error="A agenda mudou ou a operação não é permitida. Confira o horário."});}
    catch(Microsoft.EntityFrameworkCore.DbUpdateException ex){app.Logger.LogWarning("Gravação recusada em {Rota}: {Motivo}",ctx.Request.Path.Value,ex.GetBaseException().Message);ctx.Response.StatusCode=409;await ctx.Response.WriteAsJsonAsync(new{error="Conflito na agenda. Atualize antes de continuar."});}
});
app.MapGet("/crm/snapshot",(ClinicAgendaBridge bridge,CancellationToken ct)=>bridge.Snapshot(ct));
app.MapGet("/crm/recall",(ClinicRecallBridge bridge,CancellationToken ct)=>bridge.Snapshot(ct));
// Relacionamento: as filas da secretária (sem vir, novos no BSV, consulta a renovar). Só leitura.
app.MapGet("/crm/relationship",(ClinicRelationshipBridge bridge,CancellationToken ct)=>bridge.Snapshot(ct));
// O acesso ao CRM é o do sistema: quem pode entrar e a conferência da senha (ver ClinicTeamBridge).
app.MapGet("/crm/team",(ClinicTeamBridge bridge,CancellationToken ct)=>bridge.Team(ct));
app.MapPost("/crm/login",(ClinicLogin input,ClinicTeamBridge bridge,CancellationToken ct)=>bridge.Enter(input,ct));
// A equipe pergunta ao sistema pelo WhatsApp da clínica (out/2026, AssistenteWhatsAppService): o
// CRM reconhece o celular cadastrado em Acessos e a ponte responde em nome daquele login, com as
// regras do Sistema SemDor. A escrita (a conversa com o sistema e o pedido) pede o papel-ponte-crm.sql.
app.MapGet("/crm/v1/staff/numbers",async(AssistenteWhatsAppService s,CancellationToken ct)=>new StaffNumbers((await s.NumerosAsync(ct)).ToArray()));
app.MapPost("/crm/v1/staff/ask",async(StaffAsk input,AssistenteWhatsAppService s,CancellationToken ct)=>{
    // Probe: só "as regras entendem?", sem gravar (o CRM decide se chama a IA — @SemDor fase 2).
    var r=input.Probe?await s.EntendeAsync(input.Phone??"",input.Text??"",ct):await s.ResponderAsync(input.Phone??"",input.Text??"",input.MessageId??"",ct);
    return new StaffAnswer(r.DaEquipe,r.Texto,r.Nome,r.Entendeu);
});
app.MapGet("/crm/v1/staff/notices",async(AssistenteWhatsAppService s,CancellationToken ct)=>
    (await s.AvisosPendentesAsync(ct)).Select(a=>new StaffNotice(a.PedidoId,a.Telefone,a.Texto)).ToArray());
app.MapPost("/crm/v1/staff/notices/ack",async(StaffNoticeAck input,AssistenteWhatsAppService s,CancellationToken ct)=>{
    await s.ConfirmarAvisoAsync(input.RequestId,input.Delivered,input.Reason,ct);
    return Results.Ok(new{ok=true});
});
// O @SemDor com IA nos apps (out/2026 — Clinica#307): o CRM lê a fila de perguntas do chat dos apps que as
// regras não entenderam (a leitura é o pulso que os apps conferem), devolve a releitura da IA — e quem
// responde é a clínica —, e manda a lista dos avisos "Atenção" para o sino da Recepção.
app.MapGet("/crm/v1/staff/questions",(ClinicSemDorBridge bridge,CancellationToken ct)=>bridge.Questions(ct));
app.MapPost("/crm/v1/staff/questions/answer",(StaffRewrite input,ClinicSemDorBridge bridge,CancellationToken ct)=>bridge.Answer(input,ct));
app.MapPost("/crm/v1/semdor/alerts",(SemDorAlerts input,ClinicSemDorBridge bridge,CancellationToken ct)=>bridge.Alerts(input,ct));
// A remarcação pedida pelo paciente (@SemDor, out/2026): a fila de mensagens ao paciente (ofertas com
// botões e confirmações), o resultado de cada uma e o toque do paciente num horário. Sem chave de ligar
// aqui: quem decide mandar é o CRM (CRM_PATIENT_OFFERS_ENABLED). RemarcacaoPeloPacienteService já vem do AddClinica.
app.MapGet("/crm/v1/patient/messages",async(RemarcacaoPeloPacienteService s,CancellationToken ct)=>
    (await s.MensagensPendentesAsync(ct)).Select(m=>new PatientMessage(m.Id,m.Telefone,m.Texto,m.Opcoes.Select(o=>new PatientOption(o.Id,o.Titulo)).ToArray())).ToArray());
app.MapPost("/crm/v1/patient/messages/ack",async(PatientMessageAck input,RemarcacaoPeloPacienteService s,CancellationToken ct)=>{
    await s.ConfirmarMensagemAsync(input.Id??"",input.Delivered,input.Reason,ct);
    return Results.Ok(new{ok=true});
});
app.MapPost("/crm/v1/patient/choice",async(PatientChoice input,RemarcacaoPeloPacienteService s,CancellationToken ct)=>{
    if(Clinica.Application.Servicos.RemarcacaoPeloPacienteRegras.LerOpcao(input.Payload) is not { } opcao)return new PatientChoiceAnswer(false);
    return new PatientChoiceAnswer(true,await s.EscolherAsync(opcao.Chave,opcao.Indice,input.Phone??"",ct));
});
app.MapPost("/crm/v1/patients/lookup",(ClinicPatientLookup input,ClinicPatientBridge bridge,CancellationToken ct)=>bridge.Lookup(input,ct));
// O dia do CRM para a direção (caminho A): só contagens, numa tabela própria. Sem chave de
// ligar: quem decide enviar é o CRM (CRM_DAILY_SUMMARY_ENABLED), e a ponte só grava o resumo.
app.MapPost("/crm/v1/summary/daily",(CrmDailySummary input,ClinicSummaryBridge bridge,CancellationToken ct)=>bridge.Daily(input,ct));
// A fila de envios ao paciente (out/2026 — documentos pelo WhatsApp oficial). O desktop põe o
// pedido; o CRM lista, reserva, pede a autorização final (uso único) e conta o resultado. Sem
// chave de ligar aqui: quem decide processar é o CRM (CRM_CLINICAL_DISPATCH_ENABLED).
RotasDaFilaDeEnvios.Mapear(app);
app.MapPost("/crm/recall/notice",(RecallNotice input,ClinicRecallBridge bridge,CancellationToken ct)=>bridge.Notice(input,ct));
// Um PARAR só (fase 3 Recepção×CRM): o PARAR do CRM revoga a autorização de mensagens no Clinica.
app.MapPost("/crm/v1/consent/optout",(ConsentOptOut input,ClinicRecallBridge bridge,CancellationToken ct)=>bridge.OptOut(input,ct));
app.MapPost("/crm/decision",(ClinicDecision input,ClinicAgendaBridge bridge,CancellationToken ct)=>bridge.Decide(input,ct));
app.MapPost("/crm/v1/appointments/slots", (ClinicSlotQuery input, ClinicSchedulingBridge bridge, CancellationToken ct) => bridge.Slots(input, ct));
app.MapPost("/crm/v1/appointments/reschedule", async (ClinicReschedule input, ClinicSchedulingBridge bridge, CancellationToken ct) => {
    if (builder.Configuration["CRM_SCHEDULING_ENABLED"] != "true") return Results.Json(new { error = "Remarcação ainda não habilitada nesta integração." }, statusCode: 409);
    return Results.Ok(await bridge.Reschedule(input, ct));
});
// Marcar pelo CRM liga com CRM_BOOKING_ENABLED (a mesma chave do CRM); cancelar pela equipe
// é alteração de horário existente e liga com CRM_SCHEDULING_ENABLED, como a remarcação.
var booking=builder.Configuration["CRM_BOOKING_ENABLED"]=="true";
var writes=builder.Configuration["CRM_SCHEDULING_ENABLED"]=="true";
IResult Off()=>Results.Json(new{error="Esta operação pelo CRM ainda não está habilitada nesta integração."},statusCode:409);
app.MapPost("/crm/v1/booking/catalog",(BookingCatalogQuery input,ClinicBookingBridge bridge,CancellationToken ct)=>bridge.Catalog(input,ct));
app.MapPost("/crm/v1/booking/calendar",(BookingCalendarQuery input,ClinicBookingBridge bridge,CancellationToken ct)=>bridge.Calendar(input,ct));
app.MapPost("/crm/v1/booking/slots",(BookingQuery input,ClinicBookingBridge bridge,CancellationToken ct)=>bridge.Slots(input,ct));
app.MapPost("/crm/v1/booking/status",(BookingAction input,ClinicBookingBridge bridge,CancellationToken ct)=>bridge.Status(input,ct));
app.MapPost("/crm/v1/booking/hold",async(BookingHold input,ClinicBookingBridge bridge,CancellationToken ct)=>booking?Results.Ok(await bridge.Hold(input,ct)):Off());
app.MapPost("/crm/v1/booking/confirm",async(BookingAction input,ClinicBookingBridge bridge,CancellationToken ct)=>booking?Results.Ok(await bridge.Confirm(input,ct)):Off());
app.MapPost("/crm/v1/booking/release",(BookingAction input,ClinicBookingBridge bridge,CancellationToken ct)=>bridge.Release(input,ct));
app.MapPost("/crm/v1/appointments/cancel",async(ClinicCancel input,ClinicBookingBridge bridge,CancellationToken ct)=>writes?Results.Ok(await bridge.Cancel(input,ct)):Off());
app.Run();
