using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;

namespace Clinica.Assinaturas.Api;

/// <summary>
/// O documento pedido pelo WhatsApp (@SemDor fase 4, out/2026 — docs/semdor-assistente.md): a
/// página <c>/profissional/assinar.html</c> abre o link de uso único, mostra o rascunho, emite e
/// assina. Tudo aqui passa pela sessão do link, num cookie PRÓPRIO, e só alcança o rascunho dela:
/// o documento que ela emitiu, o PDF dele e o envio ao paciente. A emissão e o A1 são os do
/// atendimento no tablet (AtendimentoTabletService, A1TabletService) — nenhum motor novo.
/// Só existe com <c>Portal:LinkWhatsApp:Habilitado=true</c>.
/// </summary>
internal static class RotasLinkWhatsApp
{
    internal static void Mapear(WebApplication app, string cookie, bool demo)
    {
        CookieOptions Opcoes(long ms) => new() { HttpOnly = true, Secure = !demo, SameSite = SameSiteMode.Strict, Path = "/",
            MaxAge = TimeSpan.FromMilliseconds(Math.Max(0, ms)) };
        Task<(SessaoTablet Sessao, PedidoDocumentoWhatsApp Pedido)> Sessao(HttpContext c, LinkDocumentoTabletService link)
            => link.AutorizarAsync(c.Request.Cookies[cookie], c.RequestAborted);
        static int Documento(PedidoDocumentoWhatsApp p)
            => p.DocumentoClinicoId ?? throw ErroFormularioTablet.Criar("Emita o documento antes.");

        var grupo = app.MapGroup("/api/link");
        grupo.MapPost("/abrir", async (HttpContext c, LinkDocumentoTabletService link, AbrirLinkWhatsApp pedido) =>
        {
            var (token, s) = await link.AbrirAsync(pedido.Token, c.RequestAborted);
            c.Response.Cookies.Append(cookie, token, Opcoes(s.ExpiraEm - link.Agora));
            return Results.NoContent();
        }).RequireRateLimiting("link");
        grupo.MapGet("/estado", async (HttpContext c, LinkDocumentoTabletService link, CofreA1Tablet cofre) =>
        {
            var (s, p) = await Sessao(c, link);
            return Results.Ok(new { rascunho = await link.EstadoAsync(s, p, c.RequestAborted), a1 = await cofre.Estado(s, c.RequestAborted) });
        });
        grupo.MapPost("/emitir", async (HttpContext c, LinkDocumentoTabletService link, EmitirPeloLink pedido) =>
        {
            var (s, p) = await Sessao(c, link);
            if (p.ConcluidoEm is not null) throw ErroFormularioTablet.Criar("Este documento já foi concluído.");
            return Results.Ok(await link.EmitirAsync(s, p, pedido.Texto, pedido.Dias, c.RequestAborted));
        });
        grupo.MapGet("/pdf", async (HttpContext c, LinkDocumentoTabletService link, AtendimentoTabletService atendimento,
            ClinicaDbContext db, DocumentosClinicosPdfService pdf, ParametrosService parametros) =>
        {
            var (s, p) = await Sessao(c, link);
            var documento = Documento(p);
            var (u, paciente) = await atendimento.ExigirDocumentoAsync(s, 0, "documento", documento, false, c.RequestAborted);
            db.Auditoria.Add(new EventoAuditoria { Operador = u.Login, PacienteId = paciente, Acao = "TabletClinicoPdf", Detalhe = "Consulta de via clínica (link do WhatsApp)" });
            await db.SaveChangesAsync(c.RequestAborted);
            return Results.File(await pdf.GerarAsync(documento, await parametros.ObterPrestadorAsync(c.RequestAborted), c.RequestAborted),
                "application/pdf", $"documento-{documento}.pdf");
        });
        grupo.MapPost("/a1/preparar", async (HttpContext c, LinkDocumentoTabletService link, A1TabletService a1, PedidoAssinaturaA1 pedido) =>
        {
            var (s, p) = await Sessao(c, link);
            if (p.ConcluidoEm is not null) throw ErroFormularioTablet.Criar("Este documento já foi concluído.");
            return Results.Ok(await a1.Preparar(s, 0, "documento", Documento(p), pedido.ConfirmouAlergia, c.RequestAborted));
        });
        grupo.MapPost("/a1/{id:guid}/assinar", async (HttpContext c, LinkDocumentoTabletService link, A1TabletService a1, Guid id, SenhaA1Tablet pedido) =>
        {
            var (s, p) = await Sessao(c, link);
            Documento(p);
            var r = await a1.Assinar(s, id, pedido.Senha, c.RequestAborted);
            await link.ConcluirAsync(p, true, c.RequestAborted);
            return Results.Ok(r);
        }).RequireRateLimiting("link");
        // Sem A1 (ainda não homologado, ou o médico prefere): o documento fica emitido, sem
        // assinatura digital — o mesmo estado de quem emite no tablet e imprime para assinar à mão.
        grupo.MapPost("/concluir", async (HttpContext c, LinkDocumentoTabletService link) =>
        {
            var (_, p) = await Sessao(c, link);
            Documento(p);
            await link.ConcluirAsync(p, false, c.RequestAborted);
            return Results.NoContent();
        });
        grupo.MapPost("/enviar", async (HttpContext c, LinkDocumentoTabletService link, AtendimentoTabletService atendimento,
            EnvioAoPacienteService envio) =>
        {
            var (s, p) = await Sessao(c, link);
            if (p.ConcluidoEm is null) throw ErroFormularioTablet.Criar("Conclua o documento antes de enviar.");
            var (u, _) = await atendimento.ExigirDocumentoAsync(s, 0, "documento", Documento(p), false, c.RequestAborted);
            var r = await envio.EnfileirarWhatsAppAsync(ArquivoDoPaciente.Documento(Documento(p)), u.Login, c.RequestAborted);
            if (r.Ok) await link.MarcarEnviadoAsync(p, c.RequestAborted);
            return Results.Ok(new { ok = r.Ok, frase = r.Frase });
        });
        grupo.MapPost("/sair", async (HttpContext c, LinkDocumentoTabletService link) =>
        {
            var (s, _) = await Sessao(c, link);
            await link.SairAsync(s, c.RequestAborted);
            c.Response.Cookies.Delete(cookie, Opcoes(0));
            return Results.NoContent();
        });
    }
}

public sealed record AbrirLinkWhatsApp(string? Token);
public sealed record EmitirPeloLink(string? Texto, int? Dias);
