using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// A janela do shell que as abas administrativas da ficha (Blazor) pedem — EXATAMENTE a que o
/// <c>FichaPacienteViewModel</c> abria: a mesma cobrança do balcão (o cadastro é Blazor desde out/2026),
/// com o dono pela <see cref="JanelaDona"/>. Mora aqui, e não num módulo, porque a tela do
/// paciente roda nos quatro aplicativos da suíte.
///
/// ⚠️ Tudo roda no despachante do WPF: janela modal precisa da thread da interface.
/// </summary>
public sealed class JanelasDaFichaWpf(IServiceScopeFactory escopos, IDialogoService dialogo) : IJanelasDaFicha
{
    public bool Disponivel => true;

    public Task<bool> ReceberDividaAsync(int pacienteId, string paciente, string? telefone) => NaInterface(() =>
    {
        var vm = new CobrancaDoPacienteViewModel(escopos, dialogo, pacienteId, paciente, telefone);
        new CobrancaDoPacienteWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();
        return vm.Mudou;
    });

    private static Task<bool> NaInterface(Func<bool> acao)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) return Task.FromResult(acao());
        return d.InvokeAsync(acao).Task;
    }
}
