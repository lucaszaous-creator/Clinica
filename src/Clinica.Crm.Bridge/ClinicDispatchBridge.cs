using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;

namespace Clinica.Crm.Bridge;

/// <summary>
/// A fila de envios ao paciente (out/2026 — documentos pelo WhatsApp oficial), do lado da
/// ponte: só TRADUZ. Quem decide o que pode — reserva, autorização de uso único, relato sem
/// regressão, validade de 24 h, documento cancelado — é o <see cref="FilaEnviosCrmService"/>
/// da Application, com teste. Aqui as datas viram <see cref="DateTimeOffset"/> no fuso da
/// clínica e os registros viram o contrato que o CRM já consome (<c>ClinicalDispatchWorker</c>).
/// </summary>
public sealed class ClinicDispatchBridge(FilaEnviosCrmService fila)
{
    static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    static DateTimeOffset Offset(DateTime d)
    {
        var local = DateTime.SpecifyKind(d, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local));
    }

    public static ClinicDispatch Contrato(PedidoDeEnvio p) => new(
        p.Id, p.PacienteId, p.Nome, p.Telefone, p.Origem, p.Operador, p.Estado, p.Detalhe,
        Offset(p.CriadoEm), Offset(p.ExpiraEm), p.Texto, p.NomeArquivo, p.Arquivo,
        Marketing: false, Channel: p.Canal, Email: p.Email, Subject: p.Assunto);

    /// <summary>GET /crm/v1/dispatches?consumer= — sem texto nem PDF.</summary>
    public async Task<ClinicDispatch[]> List(string? consumer, CancellationToken ct)
        => (await fila.ListarAsync(consumer ?? string.Empty, ct)).Select(Contrato).ToArray();

    /// <summary>POST /crm/v1/dispatches/{id}/claim — a linha inteira quando o estado é <c>processing</c>.</summary>
    public async Task<ClinicDispatch> Claim(string id, ClinicDispatchClaim input, CancellationToken ct)
        => Contrato(await fila.ReservarAsync(id, input.Consumer, input.Protocol, ct));

    /// <summary>POST /crm/v1/dispatches/{id}/submit — a autorização final, de uso único.</summary>
    public async Task<ClinicDispatchPermit> Submit(string id, ClinicDispatchClaim input, CancellationToken ct)
    {
        var a = await fila.AutorizarAsync(id, input.Consumer, input.Protocol, ct);
        return new(a.Concedida, a.Estado, a.Detalhe);
    }

    /// <summary>POST /crm/v1/dispatches/{id}/report — devolve o estado que VALE na clínica.</summary>
    public async Task<ClinicDecisionResult> Report(string id, ClinicDispatchReport input, CancellationToken ct)
    {
        var r = await fila.RegistrarAsync(id, input.Consumer, input.Status, input.Detail, ct);
        return new(r.Estado, r.Detalhe);
    }
}

/// <summary>
/// As quatro rotas da fila, num método só — o <c>Program</c> as liga atrás da mesma rede,
/// chave e tradução de recusa das outras rotas <c>/crm/v1</c>, e o teste
/// (<c>PonteClinicDispatchTests</c>) as liga num servidor de verdade para conferir o que o
/// CRM manda e recebe (consumidor na consulta, JSON do corpo, PDF em base64).
/// </summary>
public static class RotasDaFilaDeEnvios
{
    public static void Mapear(IEndpointRouteBuilder app)
    {
        app.MapGet("/crm/v1/dispatches", (string? consumer, ClinicDispatchBridge bridge, CancellationToken ct) => bridge.List(consumer, ct));
        app.MapPost("/crm/v1/dispatches/{id}/claim", (string id, ClinicDispatchClaim input, ClinicDispatchBridge bridge, CancellationToken ct) => bridge.Claim(id, input, ct));
        app.MapPost("/crm/v1/dispatches/{id}/submit", (string id, ClinicDispatchClaim input, ClinicDispatchBridge bridge, CancellationToken ct) => bridge.Submit(id, input, ct));
        app.MapPost("/crm/v1/dispatches/{id}/report", (string id, ClinicDispatchReport input, ClinicDispatchBridge bridge, CancellationToken ct) => bridge.Report(id, input, ct));
    }
}
