using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure.Tablet;

namespace Clinica.Assinaturas.Api;

internal static class RotasPostoTablet
{
    internal static void Mapear(WebApplication app, Func<HttpContext,PortalTabletService,Task<SessaoTablet>> sessao)
    {
        var g=app.MapGroup("/api/posto");
        g.MapGet("/agenda",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,DateOnly? data)
            =>Results.Ok(await svc.AgendaAsync(await sessao(c,portal),data,c.RequestAborted)));
        g.MapGet("/pacientes/{id:int}/mapas/{evolucao:int}",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,int evolucao)
            =>Results.Ok(await svc.MapaAsync(await sessao(c,portal),id,evolucao,c.RequestAborted)));
        g.MapGet("/pacientes/{id:int}/anexos/{anexo:int}/pdf",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,int anexo)
            =>Results.File(await svc.AnexoPdfAsync(await sessao(c,portal),id,anexo,c.RequestAborted),"application/pdf"));
        g.MapPost("/pacientes/buscar",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,BuscaPacienteTablet pedido)
            =>Results.Ok(await svc.BuscarAsync(await sessao(c,portal),pedido.Busca,c.RequestAborted)));
        g.MapGet("/pacientes/{id:int}",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,int? pagina)
            =>Results.Ok(await svc.FichaAsync(await sessao(c,portal),id,pagina??0,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/atender",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,IniciarAvulsoTablet pedido)
            =>Results.Ok(await svc.IniciarAsync(await sessao(c,portal),id,pedido,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/documentos",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,EmitirDocumentoTablet pedido)
            =>Results.Ok(await svc.EmitirAsync(await sessao(c,portal),id,pedido,c.RequestAborted)));
        g.MapGet("/infusoes",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int? pagina)
            =>Results.Ok(await svc.FilaAsync(await sessao(c,portal),pagina??0,c.RequestAborted)));
        g.MapGet("/infusoes/{id:int}",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id)
            =>Results.Ok(await svc.InfusaoAsync(await sessao(c,portal),id,c.RequestAborted)));
        g.MapPost("/infusoes/{id:int}/checar",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,ChecarInfusaoTablet pedido)
            =>Results.Ok(await svc.ChecarAsync(await sessao(c,portal),id,pedido,c.RequestAborted)));
        g.MapPost("/infusoes/{id:int}/encerrar",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,EncerrarInfusaoTablet pedido)
            =>Results.Ok(await svc.EncerrarAsync(await sessao(c,portal),id,pedido,c.RequestAborted)));
    }
}
public sealed record BuscaPacienteTablet(string? Busca);
