using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clinica.Tiss.Conector;

/// <summary>
/// O laço do conector: enquanto houver transação, processa uma atrás da outra; fila vazia,
/// espera alguns segundos. Cada volta abre o seu escopo (um <c>DbContext</c> por transação):
/// o que uma transação deixou rastreado não vaza para a seguinte.
/// </summary>
public sealed class ServicoDoConector(IServiceScopeFactory escopos, OpcoesDoConector opcoes, ILogger<ServicoDoConector> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken parar)
    {
        log.LogInformation("Conector TISS no ar. Envio de guias neste servidor: {Envio}. Intervalo: {Segundos}s.",
            opcoes.EnvioHabilitado ? "HABILITADO (ainda depende da chave de cada convênio)" : "desligado", opcoes.IntervaloSegundos);

        var falhasSeguidas = 0;
        while (!parar.IsCancellationRequested)
        {
            var trabalhou = false;
            try
            {
                using var escopo = escopos.CreateScope();
                trabalhou = await escopo.ServiceProvider.GetRequiredService<ExecutorDeTransacoesTiss>().ProcessarProximaAsync(parar);
                falhasSeguidas = 0;
            }
            catch (OperationCanceledException) when (parar.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Banco fora do ar, por exemplo: não derruba o serviço, espera mais a cada falha.
                falhasSeguidas++;
                log.LogError("Conector TISS: a volta falhou ({Excecao}). Falhas seguidas: {Falhas}.", ex.GetType().Name, falhasSeguidas);
            }

            if (trabalhou) continue;
            var espera = TimeSpan.FromSeconds(Math.Min(opcoes.IntervaloSegundos * Math.Max(1, falhasSeguidas), 300));
            try { await Task.Delay(espera, parar); }
            catch (OperationCanceledException) { break; }
        }
        log.LogInformation("Conector TISS parado.");
    }
}
