using Clinica.Infrastructure;
namespace Clinica.Assinaturas.Api;
public sealed class ConclusaoAutomaticaWorker(IServiceScopeFactory scopes) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => ConclusaoAutomaticaService.AcompanharAsync(scopes, stoppingToken);
}
