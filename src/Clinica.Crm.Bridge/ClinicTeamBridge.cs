using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Crm.Contracts;
using Clinica.Domain.Entities;

namespace Clinica.Crm.Bridge;

/// <summary>
/// O acesso ao CRM é o acesso do sistema (out/2026, pedido da direção). O CRM não guarda
/// senha nenhuma: a lista de atendentes e a conferência da senha vêm daqui.
///
/// Quem entra: usuário ATIVO com <see cref="Permissao.EditarAgenda"/> — o bit de quem marca,
/// remarca e fala com o paciente no balcão (o enum tem um bit sobrando antes de virar long
/// numa coluna de produção; um bit "CRM" não se paga). A direção concede ou tira por pessoa
/// em Acessos, como qualquer permissão. Administra o CRM quem gerencia usuários no sistema.
///
/// A senha é conferida pelo <see cref="AcessoService.AutenticarAsync"/> — a MESMA regra dos
/// apps: errar 5 vezes trava o login por 5 minutos, e as tentativas contam juntas (errar no
/// CRM trava o login no sistema também). Sem isso, o CRM, que fica na internet, viraria uma
/// porta para testar senhas da clínica sem limite.
/// </summary>
public sealed class ClinicTeamBridge(IClinicaRepositorio repo, AcessoService acesso, IEntradaNoCrmService entrada)
{
    public const Permissao Requer = Permissao.EditarAgenda;

    public const string Recusa = "Usuário ou senha inválidos.";

    public const string SemAcesso = "Seu usuário não tem acesso ao CRM. Peça à direção para liberar em Acessos.";

    public static bool PodeEntrar(UsuarioSistema u) => u.Ativo && u.Efetivas.HasFlag(Requer);

    public static ClinicTeamMember Membro(UsuarioSistema u) =>
        new(u.Login, u.Nome, u.Efetivas.HasFlag(Permissao.GerenciarUsuarios));

    public async Task<ClinicTeamMember[]> Team(CancellationToken ct) =>
        (await repo.UsuariosAsync(ct)).Where(PodeEntrar).Select(Membro).ToArray();

    /// <summary>
    /// Confere a senha. A resposta não diz se o login existe: o que volta para quem errou é
    /// sempre a mesma frase, como nos apps.
    /// </summary>
    public async Task<IResult> Enter(ClinicLogin input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Login) || string.IsNullOrEmpty(input.Password))
            return Results.Json(new { error = Recusa }, statusCode: 401);
        var r = await acesso.AutenticarAsync(input.Login, input.Password, ct: ct, migrarHash: false);
        if (!r.Sucesso || r.Usuario is null)
            return Results.Json(new { error = r.Erro ?? Recusa }, statusCode: 401);
        if (!PodeEntrar(r.Usuario))
            return Results.Json(new { error = SemAcesso }, statusCode: 403);
        return Results.Ok(Membro(r.Usuario));
    }

    /// <summary>
    /// O bilhete da aba "WhatsApp" da Recepção (out/2026, docs/crm-na-recepcao.md): quem já entrou
    /// no app entra no CRM sem digitar a senha de novo. O bilhete é gasto aqui; quem ele aponta
    /// passa pela MESMA porta da senha (<see cref="PodeEntrar"/>) — usuário desativado ou sem o
    /// bit depois de o bilhete nascer não entra. Recusado, a frase é sempre a mesma.
    /// </summary>
    public async Task<IResult> EnterByTicket(ClinicTicketLogin input, CancellationToken ct)
    {
        if (await entrada.ConferirAsync(input.Ticket, ct) is not { } usuarioId)
            return Results.Json(new { error = EntradaNoCrm.Recusa }, statusCode: 401);
        var u = await repo.ObterUsuarioAsync(usuarioId, ct);
        if (u is null) return Results.Json(new { error = EntradaNoCrm.Recusa }, statusCode: 401);
        if (!PodeEntrar(u)) return Results.Json(new { error = SemAcesso }, statusCode: 403);
        return Results.Ok(Membro(u));
    }
}
