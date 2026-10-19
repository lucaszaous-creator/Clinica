using Clinica.Clinico.Janelas;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.Modulo;

/// <summary>
/// A ponte da tela Blazor de Prescrição de infusão até o editor WPF — faz EXATAMENTE o que o
/// <c>PrescricaoInfusaoViewModel</c> fazia ao abrir a <see cref="PrescricaoInternaWindow"/>.
/// Tudo no despachante do WPF.
/// </summary>
public sealed class JanelasDaPrescricaoDeInfusaoWpf(IServiceScopeFactory escopos, IDialogoService dialogo) : IJanelasDaPrescricaoDeInfusao
{
    public bool Disponivel => true;

    public Task<bool> NovaAsync(int pacienteId, string paciente, ModeloInfusao? modelo) => NaInterface(async () =>
    {
        var vm = new PrescricaoInternaEdicaoViewModel(escopos, dialogo, pacienteId, paciente, SessaoUsuario.Atual.ProfissionalId);
        if (modelo is not null)
        {
            await vm.Inicializacao;
            vm.AplicarCopiaDaUltima(modelo);
        }
        var janela = new PrescricaoInternaWindow(vm) { Owner = JanelaDona.Atual() };
        janela.ShowDialog();
        return janela.Assinou;
    });

    public Task<bool> EditarAsync(int pacienteId, string paciente, int prescricaoId) => NaInterface(() =>
    {
        var vm = new PrescricaoInternaEdicaoViewModel(escopos, dialogo, pacienteId, paciente,
            SessaoUsuario.Atual.ProfissionalId, prescricaoId: prescricaoId);
        var janela = new PrescricaoInternaWindow(vm) { Owner = JanelaDona.Atual() };
        janela.ShowDialog();
        return Task.FromResult(janela.Assinou);
    });

    public Task<ResultadoDoAto> EnviarAoPacienteAsync(int prescricaoId, string numero) => NaInterface(async () =>
    {
        var r = await AcoesDoDocumento.EnviarPrescricaoAsync(prescricaoId, numero, escopos);
        return new ResultadoDoAto(r.Mudou, r.Inline, r.EhErro, r.Silencioso);
    });

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
