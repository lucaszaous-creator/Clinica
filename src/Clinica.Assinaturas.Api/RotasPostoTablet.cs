using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure.Tablet;

namespace Clinica.Assinaturas.Api;

internal static class RotasPostoTablet
{
    internal static void Mapear(WebApplication app, Func<HttpContext,PortalTabletService,Task<SessaoTablet>> sessao)
    {
        var g=app.MapGroup("/api/posto");
        g.MapGet("/pacientes/{id:int}/enfermagem/contexto",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,DateOnly data)
            =>Results.Ok(await svc.ContextoEnfermagemAsync(await sessao(c,portal),id,data,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/infusoes-externas",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,InfusaoExternaTablet p)
            =>Results.Ok(await svc.RegistrarInfusaoExternaAsync(await sessao(c,portal),id,p,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/exames",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,ResultadoExameTablet p)
            =>Results.Ok(await svc.RegistrarExameAsync(await sessao(c,portal),id,p,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/enfermagem",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,RegistroEnfermagemTablet p)
            =>Results.Ok(await svc.RegistrarEnfermagemAsync(await sessao(c,portal),id,p,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/enfermagem/{evolucao:int}/vincular",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,int evolucao,VinculoEnfermagemTablet p)
            =>Results.Ok(await svc.VincularEnfermagemAsync(await sessao(c,portal),id,evolucao,p,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/modelos-documento",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,NovoModeloDocumentoTablet p)
            =>Results.Ok(await svc.CriarModeloAsync(await sessao(c,portal),id,p,c.RequestAborted)));
        g.MapGet("/pendencias",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int? pagina)
            =>Results.Ok(await svc.PendenciasAsync(await sessao(c,portal),pagina??0,c.RequestAborted)));
        g.MapGet("/modelos-documento",async(HttpContext c,PortalTabletService portal,PostoTabletService svc)
            =>Results.Ok(await svc.ModelosDocumentoAsync(await sessao(c,portal),c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/anamnese",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,AnamneseTablet p)
            =>Results.Ok(await svc.SalvarAnamneseAsync(await sessao(c,portal),id,p,c.RequestAborted)));
        g.MapGet("/pacientes/{id:int}/anamnese/versoes",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id)
            =>Results.Ok(await svc.VersoesAnamneseAsync(await sessao(c,portal),id,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/medidas",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,MedidaTablet p)
            =>Results.Ok(await svc.RegistrarMedidaAsync(await sessao(c,portal),id,p,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/medidas/{medida:int}/cancelar",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,int medida,CancelarRegistroTablet p)
            =>Results.Ok(await svc.CancelarMedidaAsync(await sessao(c,portal),id,medida,p,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/problemas",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,ProblemaTablet p)
            =>Results.Ok(await svc.SalvarProblemaAsync(await sessao(c,portal),id,p,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/problemas/{problema:int}/situacao",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,int problema,SituacaoProblemaTablet p)
            =>Results.Ok(await svc.SituacaoProblemaAsync(await sessao(c,portal),id,problema,p,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/anexos",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,AnexoTablet p)
            =>Results.Ok(await svc.AnexarAsync(await sessao(c,portal),id,p,c.RequestAborted)));
        g.MapGet("/pacientes/{id:int}/anexos/{anexo:int}/conteudo",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,int anexo)=> {
            var a=await svc.ConteudoAnexoAsync(await sessao(c,portal),id,anexo,c.RequestAborted);
            return Results.File(a.Conteudo,a.Tipo);
        });
        g.MapGet("/pacientes/{id:int}/rascunhos/{tipo}/{documento:int}",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,string tipo,int documento)
            =>Results.Ok(await svc.RascunhoAsync(await sessao(c,portal),id,tipo,documento,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/rascunhos/{tipo}/{documento:int}",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,string tipo,int documento,RascunhoTablet p)
            =>Results.Ok(await svc.CorrigirRascunhoAsync(await sessao(c,portal),id,tipo,documento,p,c.RequestAborted)));
        g.MapPost("/pacientes/{id:int}/rascunhos/{tipo}/{documento:int}/cancelar",async(HttpContext c,PortalTabletService portal,PostoTabletService svc,int id,string tipo,int documento,CancelarRegistroTablet p)
            =>Results.Ok(await svc.CancelarRascunhoAsync(await sessao(c,portal),id,tipo,documento,p,c.RequestAborted)));
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
