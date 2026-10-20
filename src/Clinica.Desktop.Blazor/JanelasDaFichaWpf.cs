using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Componentes.Cadastro;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// As janelas do shell que as abas administrativas da ficha (Blazor) pedem — EXATAMENTE a que o
/// <c>FichaPacienteViewModel</c> abria: a mesma janela de cadastro (a cobrança do balcão é Blazor),
/// com o dono pela <see cref="JanelaDona"/>. Mora aqui, e não num módulo, porque a tela do
/// paciente roda nos quatro aplicativos da suíte.
///
/// ⚠️ Tudo roda no despachante do WPF: janela modal precisa da thread da interface.
/// </summary>
public sealed class JanelasDaFichaWpf(IServiceScopeFactory escopos) : IJanelasDaFicha
{
    public bool Disponivel => true;

    public Task<bool> EditarCadastroAsync(int pacienteId) => NaInterface(() =>
    {
        var vm = new CadastroPacienteViewModel(escopos, pacienteId);
        var janela = new CadastroPacienteWindow(vm) { Owner = JanelaDona.Atual() };
        return janela.ShowDialog() == true;
    });

    private static Task<bool> NaInterface(Func<bool> acao)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) return Task.FromResult(acao());
        return d.InvokeAsync(acao).Task;
    }
}
