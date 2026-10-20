using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Recepcao.ViewModels;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Modulo;

/// <summary>
/// O <see cref="IBalcaoDaRecepcao"/> de verdade (out/2026): o que o Início e os Pedidos da
/// enfermagem (Blazor) pedem e que só a Recepção WPF sabe fazer — os MESMOS gestos que os
/// ViewModels WPF faziam (<c>PainelViewModel.AbrirHorarioAsync</c>, o "ver na agenda" da
/// <c>PedidosEnfermagemView</c> e o <c>NotificacoesEnfermagem</c> do shell).
///
/// Tudo o que toca WPF passa pelo Dispatcher: a tela Blazor chama do circuito dela.
/// </summary>
public sealed class BalcaoDaRecepcao(IServiceScopeFactory escopos, PedidoAgenda pedidoAgenda) : IBalcaoDaRecepcao
{
    public event Action? PedidosMudaram
    {
        add => NotificacoesEnfermagem.Mudaram += value;
        remove => NotificacoesEnfermagem.Mudaram -= value;
    }

    public void SinalizarMudancaDosPedidos() => NaUi(NotificacoesEnfermagem.SinalizarMudanca);

    public int? ConsumirAberturaDePedido() => NotificacoesEnfermagem.ConsumirAbertura();

    public bool DestinoExiste(string chave) => NaUi(() => NavegacaoSuite.Existe(chave));

    public Task<bool> AbrirDiaNaAgendaAsync(DateOnly dia) => Task.FromResult(NaUi(() =>
    {
        // Pergunta antes: pedido definido e navegação recusada deixariam um pedido ÓRFÃO.
        if (!NavegacaoSuite.Existe(ChavesSuite.AgendaRecepcao)) return false;
        pedidoAgenda.AbrirComFiltro(dia, null);
        if (NavegacaoSuite.Ir(ChavesSuite.AgendaRecepcao)) return true;
        pedidoAgenda.Consumir();
        return false;
    }));

    public async Task<ResultadoDoBalcao> AbrirHorarioAsync(DateOnly dia, int agendamentoId)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return await AbrirAsync(dia, agendamentoId);
        return await await despachante.InvokeAsync(() => AbrirAsync(dia, agendamentoId));
    }

    private async Task<ResultadoDoBalcao> AbrirAsync(DateOnly dia, int agendamentoId)
    {
        using var scope = escopos.CreateScope();
        var agenda = scope.ServiceProvider.GetRequiredService<AgendaViewModel>();
        if (!await agenda.AbrirJanelaDoHorarioAsync(dia, agendamentoId))
            return new(false, "Este horário não está mais neste dia — ele pode ter sido remarcado em outra máquina.");
        return string.IsNullOrWhiteSpace(agenda.Mensagem)
            ? new(true)
            : new(true, agenda.Mensagem, agenda.MensagemEhErro);
    }

    private static T NaUi<T>(Func<T> f)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        return d is null || d.CheckAccess() ? f() : d.Invoke(f);
    }

    private static void NaUi(Action f)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) f(); else d.Invoke(f);
    }
}
