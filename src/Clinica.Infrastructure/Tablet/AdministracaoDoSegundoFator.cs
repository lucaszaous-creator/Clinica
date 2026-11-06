using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

/// <summary>A direção libera o primeiro acesso ao portal e zera o autenticador perdido (ver <see cref="ISegundoFatorDoPortal"/>).</summary>
public sealed class AdministracaoDoSegundoFator(ClinicaDbContext db, TimeProvider relogio) : ISegundoFatorDoPortal
{
    private long Agora => relogio.GetUtcNow().ToUnixTimeMilliseconds();

    public async Task<string> LiberarPrimeiroAcessoAsync(int usuarioId, string operador, CancellationToken ct = default)
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == usuarioId, ct)) throw new InvalidOperationException("Usuário não encontrado.");
        // Liberar zera o que houver: o autenticador, os códigos de recuperação e a confiança nos aparelhos.
        var fator = await db.SegundosFatoresPortal.SingleOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);
        if (fator is not null) db.SegundosFatoresPortal.Remove(fator);
        db.CodigosRecuperacaoPortal.RemoveRange(await db.CodigosRecuperacaoPortal.Where(x => x.UsuarioId == usuarioId).ToListAsync(ct));
        var agora = Agora;
        foreach (var d in await db.DispositivosConfiaveisPortal.Where(x => x.UsuarioId == usuarioId && x.ExpiraEm > agora).ToListAsync(ct))
            d.ExpiraEm = agora;
        var codigo = Totp.NovosCodigosDeRecuperacao()[0];
        var liberacao = await db.LiberacoesPortal.FindAsync([usuarioId], ct);
        if (liberacao is null) db.LiberacoesPortal.Add(liberacao = new LiberacaoPortal { UsuarioId = usuarioId });
        liberacao.Resumo = ResumoDaLiberacao(usuarioId, codigo);
        liberacao.CriadaEm = agora;
        liberacao.ExpiraEm = agora + (long)ISegundoFatorDoPortal.Validade.TotalMilliseconds;
        liberacao.CriadaPor = operador;
        liberacao.Falhas = 0;
        db.Auditoria.Add(new EventoAuditoria
        {
            DataHora = DateTime.Now, Operador = operador, Acao = "PortalPrimeiroAcessoLiberado",
            Detalhe = $"usuário {usuarioId}" + (fator is null ? "" : " · autenticador anterior zerado")
        });
        await db.SaveChangesAsync(ct);
        return codigo;
    }

    public async Task<IReadOnlyDictionary<int, SituacaoDoSegundoFator>> SituacoesAsync(CancellationToken ct = default)
    {
        var agora = Agora;
        var fatores = await db.SegundosFatoresPortal.AsNoTracking().Select(x => new { x.UsuarioId, x.FalhasSeguidas }).ToListAsync(ct);
        var liberados = await db.LiberacoesPortal.AsNoTracking().Where(x => x.ExpiraEm > agora && x.Falhas < SegundoFatorPortalService.FalhasDaLiberacao)
            .Select(x => x.UsuarioId).ToListAsync(ct);
        var r = liberados.ToDictionary(id => id, _ => SituacaoDoSegundoFator.Liberado);
        foreach (var f in fatores)
            r[f.UsuarioId] = f.FalhasSeguidas >= SegundoFatorPortalService.FalhasAteTravar ? SituacaoDoSegundoFator.Travado : SituacaoDoSegundoFator.Ativo;
        return r;
    }

    public static string ResumoDaLiberacao(int usuarioId, string codigo)
        => Totp.ResumoRecuperacao(usuarioId, "liberacao:" + codigo);
}
