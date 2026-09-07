using System.Security.Claims;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Web;

/// <summary>
/// Quem está lendo, e o que ele alcança.
///
/// A permissão é uma FOTOGRAFIA gravada no cookie no momento do login — a mesma decisão do
/// <c>SessaoUsuario.Entrar</c> do desktop, e pela mesma razão: reler o banco a cada
/// requisição pagaria uma consulta por clique. O preço é o mesmo do desktop, e está
/// escrito: uma permissão retirada pela direção passa a valer no próximo login, e o cookie
/// dura 2 horas.
/// </summary>
public static class Sessao
{
    public const string ClaimLogin = "clinica:login";
    public const string ClaimPermissoes = "clinica:permissoes";

    /// <summary>
    /// As permissões efetivas de quem está logado. Sem cookie, <see cref="Permissao.Nenhuma"/>
    /// — e é uma diferença deliberada em relação ao desktop, onde "sem sessão autenticada,
    /// `Pode` LIBERA" (porque lá o login é obrigatório e tela vazia parece defeito). Aqui a
    /// porta está na rede: liberar por omissão seria a web inteira aberta a quem não entrou.
    /// </summary>
    public static Permissao Permissoes(ClaimsPrincipal? usuario)
        => usuario?.FindFirst(ClaimPermissoes)?.Value is { } bruto
           && long.TryParse(bruto, out var bits)
            ? (Permissao)bits
            : Permissao.Nenhuma;

    /// <summary>O login de quem está lendo — é ele que assina a trilha de acesso.</summary>
    public static string Operador(ClaimsPrincipal? usuario)
        => usuario?.FindFirst(ClaimLogin)?.Value ?? "?";

    public static string Nome(ClaimsPrincipal? usuario)
        => usuario?.Identity?.Name ?? string.Empty;

    /// <summary>
    /// O token do formulário desta página — o "Sair" do cabeçalho é um POST, e todo POST
    /// desta web é validado.
    /// </summary>
    public static string Token(
        HttpContext ctx, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
        => antiforgery.GetAndStoreTokens(ctx).RequestToken ?? string.Empty;

    /// <summary>
    /// Confere o token do formulário. Devolve o resultado da RECUSA, ou nulo quando o
    /// pedido é legítimo.
    ///
    /// ⚠️ A validação mora aqui, num lugar só, porque são dois POST e duas cópias
    /// divergiriam — e a que ficasse para trás seria a que ninguém releria. O
    /// `DisableAntiforgery()` nas rotas desliga o filtro AUTOMÁTICO do framework (que
    /// recusaria antes de a rota rodar, com uma página de erro que não diz nada a quem
    /// está tentando entrar); quem valida é esta função, e ela responde com a mesma tela
    /// de login e uma frase que a pessoa entende.
    /// </summary>
    public static async Task<IResult?> PedidoForjadoAsync(
        HttpContext ctx, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(ctx);
            return null;
        }
        catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
        {
            // Acontece de verdade sem ninguém atacar nada: a aba aberta desde ontem, com o
            // token vencido. A frase manda recarregar em vez de acusar.
            return Results.Redirect("/entrar?erro=" + Uri.EscapeDataString(
                "A página expirou. Recarregue e tente de novo."));
        }
    }

    /// <summary>
    /// A segunda barreira, como no desktop: o menu não OFERECE o que a pessoa não alcança
    /// (<see cref="AcessoWeb.Do"/>), e aqui a rota RECUSA — endereço digitado à mão não
    /// pode passar por cima da permissão. Devolve nulo quando pode seguir.
    /// </summary>
    public static IResult? Recusar(HttpContext ctx, Permissao exigida)
        => Permissoes(ctx.User).HasFlag(exigida)
            ? null
            : Results.Content(
                Paginas.Corpo(ctx.User, "Sem acesso",
                    "<p class=\"aviso\">Seu acesso não alcança esta tela. Fale com a direção da clínica.</p>"),
                "text/html; charset=utf-8", null, StatusCodes.Status403Forbidden);
}
