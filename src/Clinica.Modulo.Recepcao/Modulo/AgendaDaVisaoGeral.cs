using Clinica.Desktop.Shell.Modulos;
using Clinica.Recepcao.ViewModels;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Modulo;

/// <summary>
/// A NAVEGAÇÃO que a Visão geral BLAZOR (out/2026) pede à Recepção: a marcação no vão passa
/// pela ponte <see cref="PreenchimentoNovoAtendimento"/> + a aba Marcar, e o clique na célula
/// abre a aba Dia pela <see cref="PedidoDaAgendaDoDia"/>. A janela do horário e "Mais ações"
/// deixaram de passar por aqui (out/2026): a tela abre os formulários Blazor da Grade, e o
/// <c>AgendaViewModel</c> WPF que ficava montado sem desenho saiu.
///
/// Tudo roda no Dispatcher: a navegação do shell é da thread da interface, e o Blazor pede de outra.
/// </summary>
public sealed class AgendaDaVisaoGeral(IServiceProvider servicos) : IAgendaDaVisaoGeral
{
    public bool MarcacaoDisponivel => NoDespachante(() => NavegacaoSuite.Existe(ModuloRecepcao.ChaveMarcarHorario));

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
