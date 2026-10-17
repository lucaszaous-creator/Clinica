using Clinica.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clinica.Infrastructure;

/// <summary>
/// O relógio do SMS rodando NUM SERVIDOR (out/2026) — opcional, ligado por
/// <c>LembreteSms__Ligado=true</c>. Mora aqui, e não num dos dois hospedeiros, porque são
/// DOIS: a ponte do CRM (<c>Clinica.Crm.Bridge</c>, o caminho escolhido para a VPS da
/// clínica — ver <c>docs/vps-do-crm.md</c>) e a web de leitura. Duas cópias do relógio
/// divergiriam na primeira correção.
///
/// <b>Por que existe</b>: nos apps do desktop o relógio só bate com a máquina ligada, e a
/// sessão de segunda às 9h teria o lembrete "devido" no domingo — por isso o desktop adianta
/// para a sexta. No servidor, que fica ligado, o lembrete sai na hora certa todo dia, e a
/// resposta do paciente pelo link (balde) é gravada no contato mesmo com a clínica fechada.
/// O desktop continua rodando como caminho de baixo: a reserva é de BANCO (índice único na
/// chave de idempotência), então os dois juntos não mandam dois SMS.
///
/// <b>Desligado por padrão</b>: mandar SMS é cobrado, e ligar o relógio num servidor é decisão
/// de quem instala, não efeito colateral de publicar o serviço.
/// </summary>
public sealed class RelogioDoSmsNoServidor(IServiceScopeFactory escopos, ILogger<RelogioDoSmsNoServidor> log) : BackgroundService
{
    public const string Chave = "LembreteSms:Ligado";

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
