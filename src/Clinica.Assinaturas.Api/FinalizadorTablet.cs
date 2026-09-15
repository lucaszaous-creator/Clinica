using Clinica.Infrastructure;
using Clinica.Infrastructure.Tablet;
using Microsoft.EntityFrameworkCore;

namespace Clinica.Assinaturas.Api;

public sealed class FinalizadorTablet(IServiceScopeFactory scopes,ILogger<FinalizadorTablet> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try { await RodarAsync(stoppingToken); }
            catch(Exception) when(!stoppingToken.IsCancellationRequested)
            { log.LogWarning("Finalizador indisponível; submissões permanecem no banco para retomada."); }
            await Task.Delay(TimeSpan.FromSeconds(4),stoppingToken);
        }
    }
    private async Task RodarAsync(CancellationToken ct)
    {
        Guid? id;
        using(var scope=scopes.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var agora=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach(var c in await db.ColetasTablet.Where(c=>c.Estado=="preparado" && c.ExpiraEm<agora).Take(20).ToListAsync(ct))
            { c.Estado="expirado"; c.ChaveAtiva=null; }
            await db.SaveChangesAsync(ct);
            id=await db.ColetasTablet.Where(c=>c.Estado=="recebido" && c.Tentativas<5)
                .OrderBy(c=>c.RecebidoEm).Select(c=>(Guid?)c.Id).FirstOrDefaultAsync(ct);
        }
        if(id is null) return;
        try
        {
            using var scope=scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PortalTabletService>().FinalizarAsync(id.Value,ct);
        }
        catch(Exception) when(!ct.IsCancellationRequested)
        {
            // Contexto novo: as alterações da transação falha não podem vazar no retry.
            using var scope=scopes.CreateScope();
            var db=scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var c=await db.ColetasTablet.SingleAsync(c=>c.Id==id,ct);
            if(c.Estado!="recebido") return;
            c.Tentativas++; c.Falha="ARQUIVAMENTO_PENDENTE";
            if(c.Tentativas>=5) c.Estado="falha";
            await db.SaveChangesAsync(ct);
            log.LogWarning("Arquivamento pendente na coleta {ColetaId}; tentativa {Tentativa}.",id,c.Tentativas);
        }
    }
}
