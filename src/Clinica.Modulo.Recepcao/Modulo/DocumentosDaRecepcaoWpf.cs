using Clinica.Desktop.Blazor;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Recepcao.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Modulo;

/// <summary>
/// A ponte da tela de Documentos na Recepção: tudo de <see cref="DocumentosNoAplicativoWpf"/>
/// (a mesma do Consultório) mais o ORÇAMENTO, cuja janela mora neste módulo.
/// </summary>
public sealed class DocumentosDaRecepcaoWpf(
    IServiceScopeFactory escopos,
    ISnackbarService snackbar,
    IDialogoService dialogo) : DocumentosNoAplicativoWpf(escopos, snackbar, dialogo)
{
    public override Task<string?> EmitirOrcamentoAsync(int pacienteId, string paciente) => NaInterface(() =>
    {
        var vm = new OrcamentoViewModel(Escopos, pacienteId, paciente);
        var janela = new Janelas.OrcamentoWindow(vm) { Owner = JanelaDona.Atual() };
        return Task.FromResult(janela.ShowDialog() == true ? (string?)vm.NumeroEmitido : null);
    });
}
