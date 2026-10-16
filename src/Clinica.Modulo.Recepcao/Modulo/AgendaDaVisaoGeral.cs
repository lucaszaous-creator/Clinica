using Clinica.Desktop.Shell.Modulos;
using Clinica.Recepcao.ViewModels;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Modulo;

/// <summary>
/// A agenda WPF a serviço da Visão geral BLAZOR (out/2026). É o mesmo caminho que o
/// <c>OcupacaoAgendaViewModel</c> fazia: o <see cref="AgendaViewModel"/> continua DONO da
/// janela do horário e das cinco ações de "Mais ações" (sem ser desenhado), e a marcação no
/// vão passa pela ponte <see cref="PreenchimentoNovoAtendimento"/> + a aba Marcar.
///
/// Tudo roda no Dispatcher: as ações abrem janelas WPF, e o Blazor pede de outra thread.
/// </summary>
public sealed class AgendaDaVisaoGeral(IServiceProvider servicos) : IAgendaDaVisaoGeral
{
    public bool MarcacaoDisponivel => NoDespachante(() => NavegacaoSuite.Existe(ModuloRecepcao.ChaveMarcarHorario));

    public Task<RespostaDaAgenda> AbrirHorarioAsync(DateOnly dia, int agendamentoId) =>
        ComAgendaAsync(async agenda => await agenda.AbrirJanelaDoHorarioAsync(dia, agendamentoId)
            ? null
            : new RespostaDaAgenda(false, "Este horário não está mais neste dia — ele pode ter sido remarcado em outra máquina."));

    public Task<RespostaDaAgenda> ExecutarAsync(AcaoDaAgenda acao, DateOnly dia, int? profissionalId) =>
        ComAgendaAsync(async agenda =>
        {
            await agenda.PosicionarAsync(dia, profissionalId);
            await (acao switch
            {
                AcaoDaAgenda.FecharAgenda => agenda.FecharAgendaCommand.ExecuteAsync(null),
                AcaoDaAgenda.HorariosETravas => agenda.ConfigurarHorariosCommand.ExecuteAsync(null),
                AcaoDaAgenda.ListaDeEspera => agenda.AbrirEsperaCommand.ExecuteAsync(null),
                AcaoDaAgenda.ConciliarHorarios => agenda.ConciliarAgendaCommand.ExecuteAsync(null),
                _ => agenda.ImprimirAgendaCommand.ExecuteAsync(null)
            });
            return null;
        });

    public Task<bool> MarcarAsync(DateTime dataHora, int? profissionalId) => NoDespachanteAsync(() =>
    {
        if (!NavegacaoSuite.Existe(ModuloRecepcao.ChaveMarcarHorario)) return Task.FromResult(false);
        var ponte = servicos.GetRequiredService<PreenchimentoNovoAtendimento>();
        ponte.Definir(new PedidoNovoAtendimento(true, dataHora, profissionalId, null));
        if (NavegacaoSuite.Ir(ModuloRecepcao.ChaveMarcarHorario)) return Task.FromResult(true);
        ponte.Consumir(); // não deixa o pedido pendurado
        return Task.FromResult(false);
    });

    public Task<bool> AbrirDiaAsync(DateOnly dia, int? profissionalId) => NoDespachanteAsync(() =>
    {
        // Pergunta antes: pedido definido e navegação recusada deixariam um pedido ÓRFÃO.
        if (!NavegacaoSuite.Existe(ModuloRecepcao.ChaveFila)) return Task.FromResult(false);
        var ponte = servicos.GetRequiredService<PedidoDaAgendaDoDia>();
        ponte.Definir(dia, profissionalId);
        if (NavegacaoSuite.Ir(ModuloRecepcao.ChaveFila)) return Task.FromResult(true);
        ponte.Consumir();
        return Task.FromResult(false);
    });

    public PedidoDeDiaDaAgenda? ConsumirPedidoDeDia()
    {
        var pedido = servicos.GetService<PedidoAgenda>();
        if (pedido is null) return null;
        var aplicar = pedido.AplicarFiltro;
        var profissional = pedido.ProfissionalId;
        return pedido.Consumir() is { } dia ? new PedidoDeDiaDaAgenda(dia, aplicar, profissional) : null;
    }

    /// <summary>
    /// Resolve a agenda num escopo próprio (a tela é de vida longa — checagem 37), executa e
    /// devolve o recado que ela deixou. <paramref name="acao"/> devolve uma resposta própria
    /// quando quer dizer outra coisa (o horário que sumiu do dia).
    /// </summary>
    private Task<RespostaDaAgenda> ComAgendaAsync(Func<AgendaViewModel, Task<RespostaDaAgenda?>> acao) => NoDespachanteAsync(async () =>
    {
        using var scope = servicos.CreateScope();
        var agenda = scope.ServiceProvider.GetRequiredService<AgendaViewModel>();
        var propria = await acao(agenda);
        if (propria is not null) return propria;
        return string.IsNullOrWhiteSpace(agenda.Mensagem)
            ? new RespostaDaAgenda(true)
            : new RespostaDaAgenda(true, agenda.Mensagem, agenda.MensagemEhErro);
    });

    private static T NoDespachante<T>(Func<T> f)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        return d is null || d.CheckAccess() ? f() : d.Invoke(f);
    }

    private static Task<T> NoDespachanteAsync<T>(Func<Task<T>> f)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        return d is null || d.CheckAccess() ? f() : d.InvokeAsync(f).Task.Unwrap();
    }
}
