using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Recepcao.Modulo;

/// <summary>
/// As janelas e os atos WPF que a central de Documentos (Blazor, out/2026) pede — cada método
/// faz EXATAMENTE o que o <c>DocumentosViewModel</c> fazia: os mesmos ViewModels e janelas, o
/// dono pela <see cref="JanelaDona"/>, e os seis atos pelo ponto único do shell
/// (<see cref="AcoesDoDocumento"/>, onde mora a assinatura — área congelada, tocada aqui só
/// como chamador). Nada foi reescrito.
///
/// ⚠️ Tudo roda no despachante do WPF: janela modal precisa da thread da interface, e a tela
/// Blazor pode chamar de outra (o mesmo cuidado de <see cref="JanelasDaAgendaWpf"/>).
/// </summary>
public sealed class DocumentosDaRecepcaoWpf(
    IServiceScopeFactory escopos,
    ISnackbarService snackbar,
    IDialogoService dialogo) : IDocumentosDaRecepcao
{
    public bool Disponivel => true;

    public bool CaixaNesteAplicativo => NavegacaoSuite.Existe(ChavesSuite.Caixa);

    public Task<bool> EmitirEscritoAsync(int pacienteId, TipoDocumentoClinico tipo) => NaInterface(() =>
    {
        var vm = new DocumentoEdicaoViewModel(escopos, pacienteId, tipo);
        var janela = new DocumentoWindow(vm) { Owner = JanelaDona.Atual() };
        return Task.FromResult(janela.ShowDialog() == true);
    });

    public Task<string?> EmitirOrcamentoAsync(int pacienteId, string paciente) => NaInterface(() =>
    {
        var vm = new OrcamentoViewModel(escopos, pacienteId, paciente);
        var janela = new Janelas.OrcamentoWindow(vm) { Owner = JanelaDona.Atual() };
        return Task.FromResult(janela.ShowDialog() == true ? (string?)vm.NumeroEmitido : null);
    });

    public Task<bool> ColherTermoAsync(int pacienteId, string paciente, bool consentimentoLgpd) => NaInterface(() =>
        // São DOIS termos: o do procedimento copia um modelo (a janela pergunta qual), o LGPD
        // é montado das finalidades — perguntar "qual termo?" ofereceria uma lista sem ele.
        consentimentoLgpd
            ? Task.FromResult(ColetaDeTermo.AbrirConsentimentoLgpd(escopos, pacienteId, paciente))
            : ColetaDeTermo.AbrirAsync(escopos, pacienteId, paciente));

    public AtosDoDocumento Atos(DocumentoEmitido doc)
    {
        var d = Converter(doc);
        return new AtosDoDocumento(d.OferecerNovaVersao, d.OferecerAssinar, d.OferecerEnviar,
            d.OferecerRenovarLink, d.OferecerTirarDoAr, d.OferecerCancelar);
    }

    public Task<ResultadoDoAto> ExecutarAsync(AtoDoDocumento ato, DocumentoEmitido doc) => NaInterface(async () =>
    {
        var d = Converter(doc);
        var r = ato switch
        {
            AtoDoDocumento.SegundaVia => await AcoesDoDocumento.ImprimirAsync(d, escopos),
            AtoDoDocumento.NovaVersao => await AcoesDoDocumento.NovaVersaoAsync(d, escopos, snackbar),
            AtoDoDocumento.Assinar => await AcoesDoDocumento.AssinarAsync(d, escopos, snackbar),
            AtoDoDocumento.Enviar => await AcoesDoDocumento.EnviarAsync(d, escopos),
            AtoDoDocumento.RenovarLink => await AcoesDoDocumento.RenovarLinkAsync(d, escopos, snackbar),
            AtoDoDocumento.TirarDoAr => await AcoesDoDocumento.TirarDoArAsync(d, escopos, dialogo, snackbar),
            _ => await AcoesDoDocumento.CancelarAsync(d, escopos, dialogo, snackbar)
        };
        return new ResultadoDoAto(r.Mudou, r.Inline, r.EhErro, r.Silencioso);
    });

    private static DocumentoNaTela Converter(DocumentoEmitido doc) => new()
    {
        DocumentoId = doc.DocumentoId,
        PacienteId = doc.PacienteId,
        Numero = doc.Numero,
        Tipo = doc.Tipo,
        Rotulo = doc.Rotulo,
        Cancelado = doc.Cancelado,
        Assinado = doc.Assinado,
        LinkNoAr = doc.LinkNoAr,
        JaTeveLink = doc.JaTeveLink
    };

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
