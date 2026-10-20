using Clinica.Application.Modelos;
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
/// <c>DocumentoWindow</c> com a herança da sessão, o <c>VinculoDeConvenio</c> e a pendência da
/// conclusão automática (a prescrição de infusão e a coleta do termo viraram formulários Blazor
/// em out/2026). Nada é reescrito aqui: a ponte só leva o pedido à janela que já existe.
/// </summary>
public sealed class JanelasDoAtendimentoClinicoWpf(IServiceScopeFactory escopos)
    : IJanelasDoAtendimentoClinico
{
    public bool Disponivel => true;

    public Task<bool> EmitirAsync(int pacienteId, TipoDocumentoClinico tipo, HerancaDaSessao heranca) => NaInterface(() =>
    {
        var vm = new DocumentoEdicaoViewModel(escopos, pacienteId, tipo, heranca);
        var janela = new DocumentoWindow(vm) { Owner = JanelaDona.Atual() };
        return Task.FromResult(janela.ShowDialog() == true);
    });

    public Task<bool> EscolherConvenioAsync(int pacienteId)
        => NaInterface(() => VinculoDeConvenio.GarantirAsync(escopos, pacienteId, SessaoUsuario.Atual.Operador));

    public async Task<string?> PendenciaDeConclusaoAutomaticaAsync(int agendamentoId)
    {
        using var escopo = escopos.CreateScope();
        return (await escopo.ServiceProvider.GetRequiredService<Clinica.Infrastructure.ConclusaoAutomaticaService>()
            .PendenciasAsync(Clinica.Infrastructure.ConclusaoAutomaticaService.Agora, agendamentoId)).FirstOrDefault()?.Motivo;
    }

    public Task<ResultadoDoAto> EnviarAoPacienteAsync(int documentoId) => NaInterface(async () =>
    {
        DocumentoNaTela doc;
        using (var escopo = escopos.CreateScope())
        {
            var d = await escopo.ServiceProvider.GetRequiredService<Clinica.Application.Abstracoes.IClinicaRepositorio>()
                .ObterDocumentoAsync(documentoId);
            if (d is null) return new ResultadoDoAto(Frase: "Documento não encontrado.", EhErro: true);
            doc = DocumentoNaTela.De(d, Clinica.Application.Servicos.CentralDocumentosService.RotularClinico(d.Tipo));
        }
        var r = await AcoesDoDocumento.EnviarAsync(doc, escopos);
        return new ResultadoDoAto(r.Mudou, r.Inline, r.EhErro, r.Silencioso);
    });

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
