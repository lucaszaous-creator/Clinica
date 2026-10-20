using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Clinico.Modulo;

/// <summary>
/// A ponte da tela Blazor de Prontuários (out/2026) até o que continua WPF: a assinatura da
/// anamnese e a tela de atendimento (o quick-view virou o <c>ResumoProntuarioForm</c> Blazor). Faz EXATAMENTE o que o
/// <c>ProntuariosViewModel</c> fazia — a barreira de permissão é da tela que chama. A
/// assinatura usa o MESMO serviço e o MESMO seletor de certificado de todas as portas (o motor
/// da assinatura não muda — ver docs/safeid-congelado.md). Tudo no despachante do WPF.
/// </summary>
public sealed class JanelasDosProntuariosWpf(IServiceScopeFactory escopos) : IJanelasDosProntuarios
{
    public Task<ResultadoDaAssinatura?> AssinarAnamneseAsync(LinhaProntuario linha) => NaInterface(async () =>
    {
        if (!linha.MostraAssinar || linha.DocumentoId is not { } docId) return (ResultadoDaAssinatura?)null;

        // A sequência (certificado → um escopo → o serviço, mesmos argumentos) mora na Application
        // desde out/2026; o seletor do A1 é o formulário Blazor (a janela WPF saiu).
        var assinado = await AssinaturaDeDocumentoNaTela.AssinarAsync(
            escopos, docId, AssinaturaDeDocumentoNaTela.AssuntoDaAnamnese(linha.Numero),
            assunto => ColetaDeTermo.EscolherCertificadoAsync(escopos, assunto),
            SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
            SessaoUsuario.Atual.Operador);
        if (assinado is null) return null;

        // É o ARQUIVO que vale: salva e abre; a frase diz se o salvamento falhou.
        var erro = await ImpressaoPdf.SalvarEAbrirAsync(assinado.Pdf, ImpressaoPdf.NomeSeguro(assinado.NomeArquivo));
        return new ResultadoDaAssinatura(erro ?? $"Anamnese {linha.Numero} assinada.", erro is not null);
    });

    public Task<bool> AbrirAtendimentoAsync() => NaInterface(() =>
        Task.FromResult(NavegacaoSuite.Ir(PostoClinico.ChaveDoAtendimento())));

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
