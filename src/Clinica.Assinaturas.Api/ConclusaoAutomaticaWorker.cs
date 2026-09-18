using Clinica.Infrastructure;
namespace Clinica.Assinaturas.Api;
public sealed class ConclusaoAutomaticaWorker(IServiceScopeFactory scopes) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Falhar na inicialização permite ao deploy reverter; não ocultar configuração inválida.
        using (var scope = scopes.CreateScope())
            _ = scope.ServiceProvider.GetRequiredService<ConclusaoAutomaticaService>();
        return ConclusaoAutomaticaService.AcompanharAsync(scopes, stoppingToken);
    }
}
