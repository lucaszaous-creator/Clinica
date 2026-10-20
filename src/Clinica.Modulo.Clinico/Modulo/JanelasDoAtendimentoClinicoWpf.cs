using Clinica.Application.Modelos;
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
/// A ponte WPF da folha do médico Blazor (out/2026): chama EXATAMENTE o que a
/// <c>AtendimentoViewModel</c> / <c>PacienteWorkspaceViewModel</c> chamavam — a
/// <c>DocumentoWindow</c> com a herança da sessão, a <c>PrescricaoInternaWindow</c> ligada ao
/// horário e à evolução, a <c>ColetaDeTermo</c> e a pendência da conclusão automática (a escolha
/// do convênio "a definir" é o formulário Blazor de <c>FormulariosDoPaciente</c> desde out/2026). Nada é reescrito aqui: a ponte só leva o pedido à janela que já existe.
/// </summary>
public sealed class JanelasDoAtendimentoClinicoWpf(IServiceScopeFactory escopos, IDialogoService dialogo)
    : IJanelasDoAtendimentoClinico
{
    public bool Disponivel => true;

    public Task<bool> EmitirAsync(int pacienteId, TipoDocumentoClinico tipo, HerancaDaSessao heranca) => NaInterface(() =>
    {
        var vm = new DocumentoEdicaoViewModel(escopos, pacienteId, tipo, heranca);
        var janela = new DocumentoWindow(vm) { Owner = JanelaDona.Atual() };
        return Task.FromResult(janela.ShowDialog() == true);
    });

    public Task<bool> PrescreverInfusaoAsync(int pacienteId, string paciente, int? agendamentoId, int? evolucaoId)
        => NaInterface(() =>
        {
            var vm = new PrescricaoInternaEdicaoViewModel(
                escopos, dialogo, pacienteId, paciente, SessaoUsuario.Atual.ProfissionalId,
                agendamentoId, evolucaoId: evolucaoId);
            var janela = new PrescricaoInternaWindow(vm) { Owner = JanelaDona.Atual() };
            janela.ShowDialog();
            return Task.FromResult(janela.Assinou);
        });

    public Task<bool> ColherTermoAsync(int pacienteId, string paciente, int? modeloId, int? documentoId,
        int? profissionalId, int? agendamentoId)
        => NaInterface(() => ColetaDeTermo.AbrirAsync(
            escopos, pacienteId, paciente, modeloId, documentoId, profissionalId, agendamentoId));

    public async Task<string?> PendenciaDeConclusaoAutomaticaAsync(int agendamentoId)
    {
        using var escopo = escopos.CreateScope();
        return (await escopo.ServiceProvider.GetRequiredService<Clinica.Infrastructure.ConclusaoAutomaticaService>()
            .PendenciasAsync(Clinica.Infrastructure.ConclusaoAutomaticaService.Agora, agendamentoId)).FirstOrDefault()?.Motivo;
    }

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
