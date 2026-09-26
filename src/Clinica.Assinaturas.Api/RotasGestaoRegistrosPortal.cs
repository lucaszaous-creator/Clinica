using Clinica.Domain.Entities;
using Clinica.Infrastructure.Tablet;

namespace Clinica.Assinaturas.Api;

internal static class RotasGestaoRegistrosPortal
{
    internal static void Mapear(WebApplication app,Func<HttpContext,PortalTabletService,Task<SessaoTablet>> sessao)
    {
        var grupo=app.MapGroup("/api/gestao-registros");
        grupo.MapPost("/pacientes/buscar",async(HttpContext c,PortalTabletService portal,
            GestaoRegistrosPortalService svc,BuscaRegistrosPortal pedido)
            =>Results.Ok(await svc.BuscarPacientesAsync(await sessao(c,portal),pedido.Busca,c.RequestAborted)));
        grupo.MapGet("/pacientes/{id:int}",async(HttpContext c,PortalTabletService portal,
            GestaoRegistrosPortalService svc,int id)
            =>Results.Ok(await svc.RegistrosAsync(await sessao(c,portal),id,c.RequestAborted)));
        grupo.MapGet("/atendimentos/{id:int}/previa",async(HttpContext c,PortalTabletService portal,
            GestaoRegistrosPortalService svc,int id)
            =>Results.Ok(await svc.PreviaAtendimentoAsync(await sessao(c,portal),id,c.RequestAborted)));
        grupo.MapPost("/atendimentos/{id:int}/estornar",async(HttpContext c,PortalTabletService portal,
            GestaoRegistrosPortalService svc,int id,EstornarAtendimentoPortal pedido)
            =>Results.Ok(await svc.EstornarAtendimentoAsync(await sessao(c,portal),id,pedido,c.RequestAborted)));
        grupo.MapGet("/infusoes/{id:int}/previa",async(HttpContext c,PortalTabletService portal,
            GestaoRegistrosPortalService svc,int id)
            =>Results.Ok(await svc.PreviaInfusaoAsync(await sessao(c,portal),id,c.RequestAborted)));
        grupo.MapPost("/infusoes/{id:int}/cancelar",async(HttpContext c,PortalTabletService portal,
            GestaoRegistrosPortalService svc,int id,CancelarInfusaoPortal pedido)
            =>Results.Ok(await svc.CancelarInfusaoAsync(await sessao(c,portal),id,pedido,c.RequestAborted)));
    }
}

public sealed record BuscaRegistrosPortal(string? Busca);
