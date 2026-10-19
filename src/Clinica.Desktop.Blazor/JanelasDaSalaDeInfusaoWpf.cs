using Clinica.Application.Modelos;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// A ponte da Sala de infusão Blazor até as janelas WPF do shell — cada método faz EXATAMENTE
/// o que o <c>SalaInfusaoViewModel</c> fazia. Mora aqui (e não num módulo) porque a sala é
/// publicada pela Recepção e pelo Consultório com a MESMA chave. Tudo no despachante do WPF.
/// </summary>
public sealed class JanelasDaSalaDeInfusaoWpf(IServiceScopeFactory escopos, IDialogoService dialogo) : IJanelasDaSalaDeInfusao
{
    public bool Disponivel => true;

    public Task AbrirFolhaAsync(int prescricaoId) => NaInterface(() =>
    {
        var vm = new FolhaExecucaoViewModel(escopos, dialogo, prescricaoId);
        new FolhaExecucaoWindow(vm) { Owner = JanelaDona.Atual() }.ShowDialog();
        return Task.FromResult(true);
    });

    public Task AnotarAsync(LinhaSalaInfusao linha) => NaInterface(() =>
    {
        EvolucaoEnfermagemWindow.Abrir(escopos, dialogo, linha.PacienteId, linha.Paciente, linha.PrescricaoId, linha.Numero);
        return Task.FromResult(true);
    });

    public Task<bool> RegistrarInfusaoExternaAsync() => NaInterface(async () =>
    {
        var paciente = EscolherPacienteWindow.Perguntar("Registrar infusão realizada", JanelaDona.Atual(), escopos);
        if (paciente is null) return false;
        var vm = new InfusaoExternaViewModel(escopos, paciente.Id, paciente.Nome);
        var janela = new InfusaoExternaWindow(vm) { Owner = JanelaDona.Atual() };
        if (janela.ShowDialog() != true || vm.PrescricaoId is not { } id) return false;
        await AbrirFolhaAsync(id);
        return true;
    });

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
