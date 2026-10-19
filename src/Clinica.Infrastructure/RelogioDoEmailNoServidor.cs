using Clinica.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clinica.Infrastructure;

/// <summary>
/// O relógio do E-MAIL rodando NUM SERVIDOR (out/2026) — opcional, ligado por
/// <c>LembreteEmail__Ligado=true</c>, e hospedado pela ponte do CRM, como o do SMS
/// (<see cref="RelogioDoSmsNoServidor"/>, cuja razão vale aqui inteira: o desktop só bate com a
/// máquina ligada, e "às 06:00 do dia" é antes de qualquer máquina da clínica abrir).
///
/// A cada 5 minutos: o pedido de confirmação do DIA (a partir das 06:00) e o "sua sessão foi
/// agendada" do que acabou de ser marcado. Os dois são idempotentes pela trilha, então a
/// abertura dos apps (o caminho de baixo) e o servidor juntos não mandam dois e-mails. O
/// servidor de saída é o MESMO que a clínica configurou no Gerente: sem ele, nada sai.
/// </summary>
public sealed class RelogioDoEmailNoServidor(IServiceScopeFactory escopos, ILogger<RelogioDoEmailNoServidor> log) : BackgroundService
{
    public const string Chave = "LembreteEmail:Ligado";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var escopo = escopos.CreateScope();
                var servico = escopo.ServiceProvider.GetRequiredService<LembreteEmailService>();
                var dia = await servico.EnviarLembretesDoDiaAsync(ct);
                if (dia.TeveFalha) log.LogWarning("E-mail do dia: {Descricao}", dia.Descricao);
                else if (dia.Enviados > 0) log.LogInformation("E-mail do dia: {Descricao}", dia.Descricao);
                var marcados = await servico.EnviarConfirmacoesDeAgendamentoAsync(ct);
                if (marcados > 0) log.LogInformation("E-mail de agendamento: {Quantos} enviado(s)", marcados);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Rodada de e-mail falhou");
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }
}
