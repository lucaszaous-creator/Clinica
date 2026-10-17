using Clinica.Application.Servicos;

namespace Clinica.Web;

/// <summary>
/// O relógio do SMS rodando NO SERVIDOR (out/2026) — opcional, ligado por
/// <c>LembreteSms:Ligado=true</c> no appsettings ou na variável <c>LembreteSms__Ligado</c>.
///
/// <b>Por que existe</b>: nos apps do desktop o relógio só bate com a máquina ligada, e a
/// sessão de segunda às 9h teria o lembrete "devido" no domingo — por isso o desktop adianta
/// para a sexta. Na VPS, que fica ligada, o lembrete sai na hora certa todo dia, e o
/// desktop continua rodando como caminho de baixo: a reserva é de BANCO (índice único na
/// chave de idempotência), então os dois juntos não mandam dois SMS.
///
/// <b>Desligado por padrão</b>: a web de leitura é SOMENTE LEITURA (docs/web-de-leitura.md),
/// e ligá-la para mandar SMS é decisão da direção, não efeito colateral de publicá-la.
/// </summary>
public sealed class RelogioDoSmsNaWeb(IServiceScopeFactory escopos, ILogger<RelogioDoSmsNaWeb> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var escopo = escopos.CreateScope();
                var r = await escopo.ServiceProvider.GetRequiredService<LembreteSmsService>().RodarAsync(ct);
                if (r.Falhas > 0) log.LogWarning("SMS: {Descricao}", r.Descricao);
                else if (r.Enviados > 0 || r.RespostasRecebidas > 0) log.LogInformation("SMS: {Descricao}", r.Descricao);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Rodada de SMS falhou");
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }
}
