using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// A ponte da ENFERMAGEM Blazor (a seção "Atendimento de enfermagem" e a tela Enfermagem, out/2026)
/// até as janelas WPF do shell — cada método faz EXATAMENTE o que o
/// <c>AtendimentoEnfermagemViewModel</c> / <c>EnfermagemViewModel</c> faziam. Mora aqui (e não
/// num módulo) porque a tela Enfermagem é publicada pelo Consultório E pela Recepção com a MESMA
/// chave — como a <see cref="JanelasDaSalaDeInfusaoWpf"/>. Tudo no despachante do WPF.
/// </summary>
public sealed class JanelasDaEnfermagemWpf(IServiceScopeFactory escopos, IDialogoService dialogo) : IJanelasDaEnfermagem
{
    public bool Disponivel => true;

    public Task<bool> ColherTermoAsync(int pacienteId, string paciente, int? modeloId, int? documentoId, int? agendamentoId)
        => NaInterface(() => ColetaDeTermo.AbrirAsync(
            escopos, pacienteId, paciente, modeloId, documentoId, agendamentoId: agendamentoId));

    public Task<bool> RegistrarInfusaoExternaAsync(int pacienteId, string paciente, int? agendamentoId) => NaInterface(() =>
    {
        var vm = new InfusaoExternaViewModel(escopos, pacienteId, paciente, agendamentoId);
        var janela = new InfusaoExternaWindow(vm) { Owner = JanelaDona.Atual() };
        if (janela.ShowDialog() != true || vm.PrescricaoId is not { } id) return Task.FromResult(false);
        new FolhaExecucaoWindow(new FolhaExecucaoViewModel(escopos, dialogo, id)) { Owner = JanelaDona.Atual() }.ShowDialog();
        return Task.FromResult(true);
    });

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
