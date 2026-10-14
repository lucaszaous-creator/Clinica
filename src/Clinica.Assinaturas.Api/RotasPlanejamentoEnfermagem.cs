using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;
using Clinica.Infrastructure.Tablet;

namespace Clinica.Assinaturas.Api;

internal static class RotasPlanejamentoEnfermagem
{
    internal static void Mapear(WebApplication app, Func<HttpContext, PortalTabletService, Task<SessaoTablet>> sessao)
    {
        async Task<int> Usuario(HttpContext c, PortalTabletService portal, AtendimentoTabletService acesso)
        {
            var u = await acesso.AutorizarAsync(await sessao(c, portal), c.RequestAborted,
                Permissao.VerAgenda | Permissao.RegistrarEvolucaoEnfermagem);
            if (!AcessoPlanejamentoEnfermagem.Solicitar(u)) throw new UnauthorizedAccessException();
            return u.Id;
        }
        var g = app.MapGroup("/api/posto/enfermagem/planejamento");
        g.MapPost("/semana", async (HttpContext c, PortalTabletService portal, AtendimentoTabletService acesso,
            IPlanejamentoEnfermagemService svc, SemanaEnfermagem p) =>
            Results.Ok(await svc.SemanaAsync(await Usuario(c, portal, acesso), p, c.RequestAborted)));
        g.MapPost("/periodo", async (HttpContext c, PortalTabletService portal, AtendimentoTabletService acesso,
            IPlanejamentoEnfermagemService svc, PeriodoEnfermagem p) =>
            Results.Ok(await svc.PeriodoAsync(await Usuario(c, portal, acesso), p, c.RequestAborted)));
        g.MapGet("/pedidos", async (HttpContext c, PortalTabletService portal, AtendimentoTabletService acesso,
            IPlanejamentoEnfermagemService svc, DateOnly? dia) =>
            Results.Ok(await svc.ListarAsync(await Usuario(c, portal, acesso), dia, c.RequestAborted)));
        g.MapGet("/pedidos/{id:int}", async (HttpContext c, PortalTabletService portal, AtendimentoTabletService acesso,
            IPlanejamentoEnfermagemService svc, int id) =>
            Results.Ok(await svc.DetalheAsync(await Usuario(c, portal, acesso), id, c.RequestAborted)));
        g.MapPost("/pedidos", async (HttpContext c, PortalTabletService portal, AtendimentoTabletService acesso,
            IPlanejamentoEnfermagemService svc, CriarSolicitacaoEnfermagem p) =>
            Results.Ok(new { id = await svc.CriarAsync(await Usuario(c, portal, acesso), p, c.RequestAborted) }));
        g.MapPost("/pedidos/{id:int}/retirar", async (HttpContext c, PortalTabletService portal, AtendimentoTabletService acesso,
            IPlanejamentoEnfermagemService svc, int id, AcaoSolicitacaoEnfermagem p) =>
        {
            var usuario = await Usuario(c, portal, acesso);
            if (p.Acao != "retirar") throw new UnauthorizedAccessException();
            await svc.AtualizarAsync(usuario, id, p, c.RequestAborted);
            return Results.Ok(new { retirado = true });
        });
    }
}
