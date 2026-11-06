using System.Net;
using Clinica.Infrastructure.Tablet;

namespace Clinica.Gestao.Web.Hospedagem;

/// <summary>O que a página guarda da requisição que a abriu: a sessão do portal já conferida.</summary>
public static class GestaoNaRequisicao
{
    public const string Chave = "gestao.sessao";

    public static SessaoDoPortalNaGestao.Valida? De(HttpContext? ctx)
        => ctx?.Items.TryGetValue(Chave, out var v) == true ? v as SessaoDoPortalNaGestao.Valida : null;
}

/// <summary>
/// A origem de quem chama, para o limite de requisições. Atrás do túnel (socket privado), o
/// endereço vem do <c>CF-Connecting-IP</c> que o cloudflared põe — confiável só nessa fronteira,
/// como na API do portal; fora dela, o endereço da conexão.
/// </summary>
public static class OrigemDaRequisicao
{
    public static string Obter(HttpContext ctx, bool socketPrivado)
    {
        if (socketPrivado)
        {
            var cf = ctx.Request.Headers["CF-Connecting-IP"].ToString();
            return IPAddress.TryParse(cf, out var ip) ? ip.ToString() : "origem-ausente";
        }
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "origem-ausente";
    }
}
