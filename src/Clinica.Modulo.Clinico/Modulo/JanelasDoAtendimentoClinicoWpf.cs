using Clinica.Application.Modelos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.Modulo;

/// <summary>
/// A ponte WPF da folha do médico Blazor (out/2026): chama EXATAMENTE o que a
/// <c>AtendimentoViewModel</c> / <c>PacienteWorkspaceViewModel</c> chamavam — a
/// pendência da conclusão automática (o documento escrito, a prescrição de infusão, a coleta do
/// termo e a escolha do convênio "a definir" viraram formulários Blazor em out/2026). Nada é
/// reescrito aqui: a ponte só leva o pedido ao que já existe.
/// </summary>
public sealed class JanelasDoAtendimentoClinicoWpf(IServiceScopeFactory escopos)
    : IJanelasDoAtendimentoClinico
{
    public bool Disponivel => true;


    public async Task<string?> PendenciaDeConclusaoAutomaticaAsync(int agendamentoId)
    {
        using var escopo = escopos.CreateScope();
        return (await escopo.ServiceProvider.GetRequiredService<Clinica.Infrastructure.ConclusaoAutomaticaService>()
            .PendenciasAsync(Clinica.Infrastructure.ConclusaoAutomaticaService.Agora, agendamentoId)).FirstOrDefault()?.Motivo;
    }

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
