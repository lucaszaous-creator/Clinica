using Clinica.Application.Modelos;
using Clinica.Clinico.Janelas;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.Modulo;

/// <summary>
/// A ponte da tela Blazor de Exames (out/2026) até as janelas que continuam WPF. Faz
/// EXATAMENTE o que o <c>ExamesViewModel</c> fazia em cada comando — a barreira de permissão é
/// da tela que chama; aqui só se abre a janela. Tudo roda no despachante do WPF (a janela
/// modal precisa de dono e a tela Blazor pode chamar de outra thread).
/// </summary>
public sealed class JanelasDosExamesWpf(IServiceScopeFactory escopos, PacienteEmFoco foco) : IJanelasDosExames
{
    public Task<bool> RegistrarResultadoAsync(PedidoDeExameLinha pedido) => NaInterface(() =>
    {
        var vm = new ResultadoExameEdicaoViewModel(escopos, pedido.PacienteId, pedido.Paciente, pedido.DocumentoId);
        new RegistrarResultadoExameWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();
        return vm.Registrado;
    });

    public Task VerResultadosAsync(PedidoDeExameLinha pedido) => NaInterface(() =>
    {
        var vm = new ResultadosDoPedidoViewModel(escopos, foco, pedido);
        new ResultadosDoPedidoWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();
        return true;
    });

    public Task<bool> AbrirExamesDoPacienteAsync(PedidoDeExameLinha pedido) => NaInterface(() =>
    {
        foco.Definir(pedido.PacienteId, pedido.Paciente);
        return NavegacaoSuite.Ir(ModuloClinico.ChaveExamesDoPaciente);
    });

    private static Task<T> NaInterface<T>(Func<T> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return Task.FromResult(acao());
        return despachante.InvokeAsync(acao).Task;
    }
}
