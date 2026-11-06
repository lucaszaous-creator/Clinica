using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

/// <summary>
/// A gestão no navegador entra com a SESSÃO DO PORTAL (nov/2026): a mesma que a API abre depois
/// de senha + segundo fator. Ela vale para a gestão quando, ao mesmo tempo:
/// a sessão existe, está no modo "equipe", não venceu nem foi revogada, pertence ao aparelho que
/// a apresenta, a senha não mudou desde a entrada, o usuário está ativo e destravado — e ele tem
/// a régua da gestão (<see cref="AcessoAoPortal.PodeGestao"/>).
///
/// <see cref="ConferirAsync"/> é a conferência da conexão (com o aparelho); <see cref="ReconferirAsync"/>,
/// a de cada minuto durante o uso (sem o cookie, pelo id da sessão): sair do portal, trocar a
/// senha ou perder o perfil derruba a gestão aberta em até um minuto.
/// </summary>
public sealed class SessaoDoPortalNaGestao(ClinicaDbContext db, TimeProvider relogio, OpcoesDaGestaoWeb? opcoes = null)
{
    public sealed record Valida(string SessaoId, UsuarioSistema Usuario, long ExpiraEm);

    private long Agora => relogio.GetUtcNow().ToUnixTimeMilliseconds();

    public async Task<Valida?> ConferirAsync(string? token, string? dispositivo, CancellationToken ct)
    {
        if (token?.Length != 64 || string.IsNullOrEmpty(dispositivo)) return null;
        return await PorIdAsync(ContratoTablet.Hash(token), dispositivo, ct);
    }

    public Task<Valida?> ReconferirAsync(string sessaoId, CancellationToken ct) => PorIdAsync(sessaoId, null, ct);

    private async Task<Valida?> PorIdAsync(string id, string? dispositivo, CancellationToken ct)
    {
        var s = await db.SessoesTablet.AsNoTracking()
            .Include(x => x.Usuario).ThenInclude(u => u!.Profissional)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (s?.Usuario is not { } u) return null;
        if (s.Modo != "equipe" || s.ExpiraEm <= Agora) return null;
        if (dispositivo is not null && s.Dispositivo != dispositivo) return null;
        if (!u.Ativo || u.Travado(DateTime.Now) || s.CredencialVersao != ContratoTablet.Hash(u.SenhaHash)) return null;
        if (!AcessoAoPortal.PodeGestao(u)) return null;
        // A chave da direção (Gestao:ExigirSegundoFator): com ela ligada, a gestão — o sistema
        // inteiro na internet — só abre para quem já ativou o segundo fator no portal.
        if (opcoes?.ExigirSegundoFator == true && !await db.SegundosFatoresPortal.AnyAsync(x => x.UsuarioId == u.Id, ct)) return null;
        return new Valida(s.Id, u, s.ExpiraEm);
    }

    /// <summary>Sair da gestão encerra a sessão do portal inteira (é a mesma).</summary>
    public async Task RevogarAsync(string sessaoId, CancellationToken ct)
    {
        var s = await db.SessoesTablet.SingleOrDefaultAsync(x => x.Id == sessaoId, ct);
        if (s is null || s.Modo == "revogada") return;
        s.Modo = "revogada";
        s.ExpiraEm = Agora;
        db.Auditoria.Add(new EventoAuditoria { DataHora = DateTime.Now, Operador = await db.Usuarios
            .Where(x => x.Id == s.UsuarioId).Select(x => x.Login).SingleAsync(ct), Acao = "GestaoWebSaiu", Detalhe = "saiu da gestão web" });
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>As opções da gestão web. <see cref="ExigirSegundoFator"/> desligada por padrão (decisão da direção, nov/2026).</summary>
public sealed record OpcoesDaGestaoWeb(bool ExigirSegundoFator);
