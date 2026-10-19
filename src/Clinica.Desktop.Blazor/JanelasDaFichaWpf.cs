using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Componentes.Cadastro;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// As janelas do shell que as abas administrativas da ficha (Blazor) pedem — EXATAMENTE as que o
/// <c>FichaPacienteViewModel</c> abria: a mesma janela de cadastro e a mesma cobrança do balcão,
/// com o dono pela <see cref="JanelaDona"/>. Mora aqui, e não num módulo, porque a tela do
/// paciente roda nos quatro aplicativos da suíte.
///
/// ⚠️ Tudo roda no despachante do WPF: janela modal precisa da thread da interface.
/// </summary>
public sealed class JanelasDaFichaWpf(IServiceScopeFactory escopos, IDialogoService dialogo) : IJanelasDaFicha
{
    public bool Disponivel => true;

    public Task<bool> EditarCadastroAsync(int pacienteId) => NaInterface(() =>
    {
        var vm = new CadastroPacienteViewModel(escopos, pacienteId);
        var janela = new CadastroPacienteWindow(vm) { Owner = JanelaDona.Atual() };
        return janela.ShowDialog() == true;
    });

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
