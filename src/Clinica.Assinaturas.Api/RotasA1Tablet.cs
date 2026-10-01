using Clinica.Application.Servicos;
using Clinica.Infrastructure.Tablet;
using Clinica.Domain.Entities;

namespace Clinica.Assinaturas.Api;

internal static class RotasA1Tablet
{
    internal static void Mapear(WebApplication app, Func<HttpContext, PortalTabletService, Task<SessaoTablet>> sessao)
    {
        var grupo = app.MapGroup("/api/clinico");
        grupo.MapGet("/a1", async (HttpContext c, PortalTabletService portal, CofreA1Tablet svc)
            => Results.Ok(await svc.Estado(await sessao(c, portal), c.RequestAborted)));
        grupo.MapPost("/a1/cadastrar", async (HttpContext c, PortalTabletService portal, CofreA1Tablet svc, CadastroA1Tablet pedido) =>
        { await svc.Cadastrar(await sessao(c, portal), pedido, c.RequestAborted); return Results.NoContent(); }).RequireRateLimiting("a1");
        grupo.MapPost("/a1/remover", async (HttpContext c, PortalTabletService portal, CofreA1Tablet svc) =>
        { await svc.Remover(await sessao(c, portal), c.RequestAborted); return Results.NoContent(); });
        grupo.MapPost("/atendimentos/{id:int}/{tipo}/{documento:int}/a1", async (HttpContext c, PortalTabletService portal,
            A1TabletService svc, int id, string tipo, int documento, PedidoAssinaturaA1 pedido)
            => Results.Ok(await svc.Preparar(await sessao(c, portal), id, tipo, documento, pedido.ConfirmouAlergia, c.RequestAborted)));
        grupo.MapPost("/a1/{id:guid}/assinar", async (HttpContext c, PortalTabletService portal,
            A1TabletService svc, Guid id, SenhaA1Tablet pedido)
            => Results.Ok(await svc.Assinar(await sessao(c, portal), id, pedido.Senha, c.RequestAborted))).RequireRateLimiting("a1");
    }
}

public sealed record PedidoAssinaturaA1(bool ConfirmouAlergia);
