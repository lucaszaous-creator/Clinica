using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure.Tablet;

/// <summary>A direção zera o autenticador perdido (ver <see cref="ISegundoFatorDoPortal"/>).</summary>
public sealed class AdministracaoDoSegundoFator(ClinicaDbContext db, TimeProvider relogio) : ISegundoFatorDoPortal
{
    private long Agora => relogio.GetUtcNow().ToUnixTimeMilliseconds();

    public async Task ZerarAsync(int usuarioId, string operador, CancellationToken ct = default)
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == usuarioId, ct)) throw new InvalidOperationException("Usuário não encontrado.");
        // Zera o que houver: o autenticador, os códigos de recuperação e a confiança nos aparelhos.
        var fator = await db.SegundosFatoresPortal.SingleOrDefaultAsync(x => x.UsuarioId == usuarioId, ct);
        if (fator is not null) db.SegundosFatoresPortal.Remove(fator);
        db.CodigosRecuperacaoPortal.RemoveRange(await db.CodigosRecuperacaoPortal.Where(x => x.UsuarioId == usuarioId).ToListAsync(ct));
        var agora = Agora;
        foreach (var d in await db.DispositivosConfiaveisPortal.Where(x => x.UsuarioId == usuarioId && x.ExpiraEm > agora).ToListAsync(ct))
            d.ExpiraEm = agora;
        db.Auditoria.Add(new EventoAuditoria
        {
            DataHora = DateTime.Now, Operador = operador, Acao = "PortalSegundoFatorZerado",
            Detalhe = $"usuário {usuarioId}" + (fator is null ? " · não havia autenticador" : "")
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, SituacaoDoSegundoFator>> SituacoesAsync(CancellationToken ct = default)
        => (await db.SegundosFatoresPortal.AsNoTracking().Select(x => new { x.UsuarioId, x.FalhasSeguidas }).ToListAsync(ct))
            .ToDictionary(f => f.UsuarioId, f => f.FalhasSeguidas >= SegundoFatorPortalService.FalhasAteTravar
                ? SituacaoDoSegundoFator.Travado : SituacaoDoSegundoFator.Ativo);
}
