using Clinica.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Clinica.Desktop.Shell.Configuracao;

/// <summary>
/// O relógio do lembrete por SMS (out/2026). O e-mail roda UMA vez na abertura; o SMS
/// precisa sair 24h antes de CADA sessão — a das 9h e a das 17h têm momentos diferentes —,
/// então a rodada bate a cada <see cref="Intervalo"/> enquanto o app está aberto.
///
/// Roda na Recepção (o app que fica aberto o dia inteiro) e no Gerente. As duas máquinas
/// rodando ao mesmo tempo não mandam dois SMS: a reserva é de BANCO
/// (<c>MensagemSms.ChaveIdempotencia</c>, índice único).
///
/// <b>Sem provedor configurado, a rodada termina na primeira leitura</b> e não reclama —
/// canal desligado é estado legítimo. Falha não derruba nada: vai para o log, e a próxima
/// batida tenta de novo. Escopo NOVO por batida (a checagem 37: tela e relógio de vida
/// longa não seguram o <c>DbContext</c> da abertura).
/// </summary>
public static class RelogioDoSms
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(5);

    /// <summary>Liga o relógio. A primeira batida espera 1 minuto: a abertura tem prioridade.</summary>
    public static void Iniciar(IHost host, string app)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(1));
            using var timer = new PeriodicTimer(Intervalo);
            do
            {
                try
                {
                    using var escopo = host.Services.CreateScope();
                    var r = await escopo.ServiceProvider.GetRequiredService<LembreteSmsService>().RodarAsync();
                    // Só a FALHA vai para o log de erros: o envio bem-sucedido fica na tabela
                    // MensagensSms, que é onde a tela do canal o mostra.
                    if (r.Falhas > 0)
                        LogSuite.Registrar($"{app} — SMS", new InvalidOperationException(r.Descricao));
                }
                catch (Exception ex)
                {
                    LogSuite.Registrar($"{app} — rodada de SMS", ex);
                }
            }
            while (await timer.WaitForNextTickAsync());
        });
    }
}
