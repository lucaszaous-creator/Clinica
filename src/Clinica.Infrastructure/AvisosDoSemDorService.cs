using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// A leitura dos avisos "Atenção" do @SemDor para o sino da Recepção (Clinica#307). Duas idas
/// pequenas: a hora da última lista e os avisos. Quem grava é a ponte (<c>ClinicSemDorBridge</c>).
/// </summary>
public sealed class AvisosDoSemDorService(ClinicaDbContext db)
{
    /// <summary>Os avisos, do mais recente ao mais antigo; <c>Atual</c> falso quando o CRM parou de mandar.</summary>
    public async Task<(IReadOnlyList<AvisoDoSemDor> Avisos, bool Atual, bool Ligado)> ListarAsync(DateTime agora, CancellationToken ct = default)
    {
        var recebida = await db.SemDorNoCrm.AsNoTracking().Where(x => x.Id == SemDorNoCrm.Unico)
            .Select(x => x.AvisosRecebidosEm).SingleOrDefaultAsync(ct);
        // Nunca recebeu lista: o @SemDor nos apps não está ligado — o sino nem mostra a seção.
        if (recebida is null) return ([], false, false);
        if (!AvisosDoSemDorRegras.ListaAtual(recebida, agora)) return ([], false, true);
        var avisos = await db.AvisosDoSemDor.AsNoTracking().OrderByDescending(a => a.Em).Take(20).ToListAsync(ct);
        return (avisos, true, true);
    }
}
