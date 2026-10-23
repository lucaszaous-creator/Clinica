using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// O bilhete de entrada no CRM pela Recepção (out/2026 — <see cref="EntradaNoCrm"/> tem a regra
/// e o porquê). O app emite (e varre os bilhetes de ontem: a ponte só lê e gasta, não apaga);
/// a ponte confere e gasta com UMA escrita condicional, para dois pedidos com o mesmo bilhete
/// não entrarem os dois.
/// </summary>
public sealed class EntradaNoCrmService : IEntradaNoCrmService
{
    private readonly ClinicaDbContext db;
    private readonly Func<DateTime> agora;

    public EntradaNoCrmService(ClinicaDbContext db) : this(db, () => DateTime.Now) { }

    public EntradaNoCrmService(ClinicaDbContext db, Func<DateTime> agora)
    {
        this.db = db;
        this.agora = agora;
    }

    public async Task<string> EmitirAsync(int usuarioId, CancellationToken ct = default)
    {
        if (usuarioId <= 0) throw new InvalidOperationException("Entre no sistema antes de abrir o CRM.");
        var momento = agora();
        var limite = momento - EntradaNoCrm.Descartar;
        await db.BilhetesEntradaCrm.Where(b => b.CriadoEm < limite).ExecuteDeleteAsync(ct);
        var (bilhete, hash) = EntradaNoCrm.Novo();
        db.BilhetesEntradaCrm.Add(new BilheteEntradaCrm
        {
            TokenHash = hash, UsuarioId = usuarioId, CriadoEm = momento, VenceEm = momento + EntradaNoCrm.Validade
        });
        await db.SaveChangesAsync(ct);
        return bilhete;
    }

    public async Task<int?> ConferirAsync(string? bilhete, CancellationToken ct = default)
    {
        if (!EntradaNoCrm.FormatoValido(bilhete)) return null;
        var hash = EntradaNoCrm.Hash(bilhete!);
        var achado = await db.BilhetesEntradaCrm.AsNoTracking()
            .Where(b => b.TokenHash == hash)
            .Select(b => new { b.Id, b.UsuarioId, b.CriadoEm, b.VenceEm, b.UsadoEm })
            .FirstOrDefaultAsync(ct);
        var momento = agora();
        if (achado is null || !EntradaNoCrm.Vale(achado.CriadoEm, achado.VenceEm, achado.UsadoEm, momento)) return null;
        var gastos = await db.BilhetesEntradaCrm
            .Where(b => b.Id == achado.Id && b.UsadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.UsadoEm, momento), ct);
        return gastos == 1 ? achado.UsuarioId : null;
    }
}
