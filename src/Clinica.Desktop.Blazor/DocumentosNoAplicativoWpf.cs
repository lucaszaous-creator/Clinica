using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// As janelas e os atos WPF que a tela de Documentos (Blazor, out/2026 — a MESMA na Recepção,
/// no Gerente e no Consultório) pede. O papel ESCRITO e a nova versão do laudo NÃO estão aqui:
/// são o <c>DocumentoForm</c> Blazor, aberto pela própria tela. Mora aqui, e não num módulo, porque são duas portas e
/// uma cópia por módulo divergiria na primeira correção; só o ORÇAMENTO é da Recepção (a
/// janela dele mora lá), e ela o oferece sobrescrevendo <see cref="EmitirOrcamentoAsync"/>.
///
/// Origem: a central de Documentos (Blazor, out/2026) pede — cada método
/// faz EXATAMENTE o que o <c>DocumentosViewModel</c> fazia: os mesmos ViewModels e janelas, o
/// dono pela <see cref="JanelaDona"/>, e os seis atos pelo ponto único do shell
/// (<see cref="AcoesDoDocumento"/>, onde mora a assinatura — área congelada, tocada aqui só
/// como chamador). Nada foi reescrito.
///
/// ⚠️ Tudo roda no despachante do WPF: janela modal precisa da thread da interface, e a tela
/// Blazor pode chamar de outra.
/// </summary>
public class DocumentosNoAplicativoWpf(
    IServiceScopeFactory escopos,
    ISnackbarService snackbar,
    IDialogoService dialogo) : IDocumentosDaRecepcao
{
    public bool Disponivel => true;

    public bool CaixaNesteAplicativo => NavegacaoSuite.Existe(ChavesSuite.Caixa);

    /// <summary>
    /// Onde a janela do orçamento não está (o Consultório): a tela DIZ de quem é o papel em vez
    /// de não fazer nada — botão que volta calado é o defeito da parcela 41.
    /// </summary>
    public virtual Task<string?> EmitirOrcamentoAsync(int pacienteId, string paciente) => NaInterface(() =>
    {
        Dialogo.Aviso("Orçamento", "O orçamento é emitido pela Recepção (ou pelo Gerente).");
        return Task.FromResult<string?>(null);
    });

    /// <summary>Para quem sobrescreve: a fábrica de escopos e o despachante.</summary>
    protected IServiceScopeFactory Escopos => escopos;
    protected IDialogoService Dialogo => dialogo;

    public Task<bool> ColherTermoAsync(int pacienteId, string paciente, bool consentimentoLgpd) => NaInterface(() =>
        // São DOIS termos: o do procedimento copia um modelo (a janela pergunta qual), o LGPD
        // é montado das finalidades — perguntar "qual termo?" ofereceria uma lista sem ele.
        consentimentoLgpd
            ? Task.FromResult(ColetaDeTermo.AbrirConsentimentoLgpd(escopos, pacienteId, paciente))
            : ColetaDeTermo.AbrirAsync(escopos, pacienteId, paciente));

    public Task<bool> ColherTermoDoProcedimentoAsync(int pacienteId, string paciente, int? modeloId, int? documentoId,
        int? profissionalId, int? agendamentoId) => NaInterface(() =>
        ColetaDeTermo.AbrirAsync(escopos, pacienteId, paciente, modeloId, documentoId, profissionalId, agendamentoId));

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
            // A nova versão é o DocumentoForm (Blazor), que a tela abre — não chega aqui.
            AtoDoDocumento.NovaVersao => ResultadoAcaoDocumento.Erro("A nova versão do laudo abre na própria tela de Documentos."),
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

    protected static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
