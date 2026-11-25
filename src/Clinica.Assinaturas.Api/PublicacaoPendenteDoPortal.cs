using Clinica.Application.Servicos;

namespace Clinica.Assinaturas.Api;

/// <summary>
/// Esvazia a fila da publicação (nov/2026, autorização do dono — docs/safeid-congelado.md, ponto 1). O portal assina
/// receitas pelo SafeID/A1 (Consultório e gestão web) e passou a criar o token ANTES do PDF; a publicação tenta
/// subir na hora, e o que não subiu (rede, proxy, armazenamento fora do ar) volta aqui a cada 2 minutos, até subir.
/// Idempotente: o publicado sai da fila (<see cref="PublicacaoDocumentoService.PublicarPendentesAsync"/>).
/// </summary>
public sealed class PublicacaoPendenteDoPortal(IServiceScopeFactory escopos, ILogger<PublicacaoPendenteDoPortal> log) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var relogio = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                using var escopo = escopos.CreateScope();
                var subiram = await escopo.ServiceProvider.GetRequiredService<PublicacaoDocumentoService>().PublicarPendentesAsync(20, ct);
                if (subiram > 0) log.LogInformation("Publicação pendente: {Quantos} documento(s) publicado(s).", subiram);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning("Publicação pendente interrompida ({Tipo}); nova tentativa em {Intervalo}.", e.GetType().Name, Intervalo);
            }
        } while (await relogio.WaitForNextTickAsync(ct));
    }
}
