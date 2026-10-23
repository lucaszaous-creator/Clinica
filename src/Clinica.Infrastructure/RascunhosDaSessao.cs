using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Infrastructure;

/// <summary>
/// O rascunho automático da evolução no banco (out/2026). Cada guarda é UM <c>UPDATE</c> da
/// mesma linha (ou o <c>INSERT</c> da primeira): sem transação aberta à mão, então vale sob o
/// <c>EnableRetryOnFailure</c> sem estratégia própria, e sem versão — rascunho não é prontuário.
/// </summary>
public sealed class RascunhosDaSessao(ClinicaDbContext db) : IRascunhosDaSessao
{
    private static DateTime Agora => ConclusaoAutomaticaService.Agora;

    // A sessão: o horário quando há; sem horário, quem escreve + o dia.
    private IQueryable<RascunhoEvolucao> Abertos(ChaveDoRascunho c) => db.RascunhosEvolucao.Where(r =>
        r.EncerradoEm == null && r.PacienteId == c.PacienteId
        && (c.AgendamentoId != null
            ? r.AgendamentoId == c.AgendamentoId
            : r.AgendamentoId == null && r.ProfissionalId == c.ProfissionalId && r.Dia == c.Dia));

    public async Task<DateTime> GuardarAsync(ChaveDoRascunho chave, string conteudo, int? evolucaoId, string? operador,
        CancellationToken ct = default)
    {
        var agora = Agora;
        var r = await Abertos(chave).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (r is null)
        {
            r = new RascunhoEvolucao
            {
                PacienteId = chave.PacienteId, ProfissionalId = chave.ProfissionalId,
                AgendamentoId = chave.AgendamentoId, Dia = chave.Dia
            };
            db.RascunhosEvolucao.Add(r);
        }
        r.Conteudo = conteudo;
        r.EvolucaoId = evolucaoId is > 0 ? evolucaoId : r.EvolucaoId;
        r.AtualizadoEm = agora;
        r.AtualizadoPor = operador;
        await db.SaveChangesAsync(ct);
        db.Entry(r).State = EntityState.Detached; // o escopo é da guarda; nada fica rastreado
        return agora;
    }

    public async Task<RascunhoAberto?> AbertoAsync(ChaveDoRascunho chave, CancellationToken ct = default)
        => await Abertos(chave).AsNoTracking().OrderByDescending(x => x.Id)
            .Select(r => new RascunhoAberto(r.Conteudo, r.AtualizadoEm, r.EvolucaoId))
            .FirstOrDefaultAsync(ct);

    public async Task EncerrarAsync(ChaveDoRascunho chave, int evolucaoId, CancellationToken ct = default)
    {
        var agora = Agora;
        foreach (var r in await Abertos(chave).ToListAsync(ct))
        {
            r.EncerradoEm = agora;
            r.Desfecho = RascunhoEvolucao.SalvoPeloProfissional;
            r.EvolucaoId = evolucaoId;
        }
        await db.SaveChangesAsync(ct);
    }
}
