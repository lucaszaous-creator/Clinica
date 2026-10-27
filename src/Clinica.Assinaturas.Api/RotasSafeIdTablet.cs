using Clinica.Domain.Entities;
using Clinica.Application.Tablet;
using Clinica.Infrastructure.Tablet;

namespace Clinica.Assinaturas.Api;

/// <summary>
/// Rotas do SafeID no portal — o MESMO contrato de antes de 01/10/2026, para a página do
/// clinica-site voltar sem adaptação. Ao lado das do A1 (<see cref="RotasA1Tablet"/>).
/// </summary>
internal static class RotasSafeIdTablet
{
    internal static void Mapear(WebApplication app, Func<HttpContext, PortalTabletService, Task<SessaoTablet>> sessao)
    {
        var grupo = app.MapGroup("/api/clinico");
        grupo.MapGet("/safeid",async(HttpContext c,PortalTabletService portal,AtendimentoTabletService acesso,SafeIdTabletService svc)=>
        {await acesso.AutorizarAsync(await sessao(c,portal),c.RequestAborted,Permissao.VerProntuario);return Results.Ok(new {habilitado=svc.Habilitado,tipos=SafeIdTabletService.Tipos});});
        // O endereço da receita era conferido em .../safeid/endereco; hoje mora em .../assinatura/endereco.
        // As duas respondem o mesmo serviço, para a página antiga e a atual.
        grupo.MapGet("/atendimentos/{id:int}/documento/{documento:int}/safeid/endereco",
            async(HttpContext c, PortalTabletService portal, EnderecoPrescricaoTabletService svc, int id, int documento)
                => Results.Ok(await svc.ConferirAsync(await sessao(c,portal),id,documento,c.RequestAborted)));
        grupo.MapPost("/atendimentos/{id:int}/documento/{documento:int}/safeid/endereco",
            async(HttpContext c, PortalTabletService portal, EnderecoPrescricaoTabletService svc, int id, int documento, EnderecoPrescricaoTablet pedido) =>
            { await svc.CompletarAsync(await sessao(c,portal),id,documento,pedido.Endereco,c.RequestAborted); return Results.NoContent(); });
        grupo.MapPost("/atendimentos/{id:int}/{tipo}/{documento:int}/safeid", async(HttpContext c,PortalTabletService portal,
            SafeIdTabletService svc,int id,string tipo,int documento,PedidoSafeIdTablet pedido)
            => Results.Ok(await svc.IniciarAsync(await sessao(c,portal),id,tipo,documento,pedido.ConfirmouAlergia,c.RequestAborted)))
            .RequireRateLimiting("safeid");
        // O retorno não assina: state/PKCE são de uso único; assinatura exige a sessão original e CSRF em POST.
        app.MapGet("/safeid/retorno", (HttpContext c, AutorizacoesSafeIdTablet autorizacoes, string? state,string? code,string? error) =>
        {
            if(state is null) throw new RecursoClinicoIndisponivel();
            var id=autorizacoes.Receber(state,code,error);
            return Results.Redirect("/profissional/?assinatura="+id);
        });
        grupo.MapGet("/safeid/{id:guid}", async(HttpContext c,PortalTabletService portal,AtendimentoTabletService svc,
            AutorizacoesSafeIdTablet autorizacoes,Guid id) =>
        {
            var s=await sessao(c,portal); await svc.AutorizarAsync(s,c.RequestAborted,Permissao.VerProntuario);
            var a=autorizacoes.Obter(id,s.Id);
            var (_,paciente)=await svc.ExigirDocumentoAsync(s,a.Agendamento,a.Tipo,a.Documento,true,c.RequestAborted);
            return Results.Ok(new {a.Id,a.Agendamento,a.Documento,a.Tipo,a.Situacao,PacienteId=paciente});
        });
        grupo.MapPost("/safeid/{id:guid}/concluir",async(HttpContext c,PortalTabletService portal,SafeIdTabletService svc,Guid id)
            => Results.Ok(await svc.ConcluirAsync(await sessao(c,portal),id,c.RequestAborted))).RequireRateLimiting("safeid");
    }
}

public sealed record PedidoSafeIdTablet(bool ConfirmouAlergia);
