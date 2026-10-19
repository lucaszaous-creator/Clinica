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
// O lembrete de SMS 24h antes, com a clínica fechada (out/2026): o MESMO LembreteSmsService
// do desktop, rodando aqui a cada 5 min. Liga com LembreteSms__Ligado=true. O provedor, o
// texto e o endereço do link continuam vindo das Configurações do Clinica (Relacionamento →
// Canal de SMS); a ponte não tem segunda configuração de SMS. Ligado aqui e nos apps ao
// mesmo tempo, não sai SMS duplicado: a reserva de cada mensagem é um índice único no banco.
if(builder.Configuration.GetValue<bool>(RelogioDoSmsNoServidor.Chave))builder.Services.AddHostedService<RelogioDoSmsNoServidor>();
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
app.MapPost("/crm/v1/patients/lookup",(ClinicPatientLookup input,ClinicPatientBridge bridge,CancellationToken ct)=>bridge.Lookup(input,ct));
// O dia do CRM para a direção (caminho A): só contagens, numa tabela própria. Sem chave de
// ligar: quem decide enviar é o CRM (CRM_DAILY_SUMMARY_ENABLED), e a ponte só grava o resumo.
app.MapPost("/crm/v1/summary/daily",(CrmDailySummary input,ClinicSummaryBridge bridge,CancellationToken ct)=>bridge.Daily(input,ct));
// A fila de envios ao paciente (out/2026 — documentos pelo WhatsApp oficial). O desktop põe o
// pedido; o CRM lista, reserva, pede a autorização final (uso único) e conta o resultado. Sem
// chave de ligar aqui: quem decide processar é o CRM (CRM_CLINICAL_DISPATCH_ENABLED).
RotasDaFilaDeEnvios.Mapear(app);
app.MapPost("/crm/recall/notice",(RecallNotice input,ClinicRecallBridge bridge,CancellationToken ct)=>bridge.Notice(input,ct));
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
