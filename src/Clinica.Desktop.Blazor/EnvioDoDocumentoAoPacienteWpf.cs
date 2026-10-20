using Clinica.Desktop.Shell.Componentes;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Desktop.Blazor;

/// <summary>
/// "Enviar agora ao paciente?" ao fim da emissão (Blazor, out/2026): EXATAMENTE o que o
/// <c>DocumentoEdicaoViewModel</c> WPF fazia — o ponto único do shell
/// (<see cref="AcoesDoDocumento.EnviarAsync"/> com <c>aposEmitir: true</c>), que abre a
/// <c>EnvioAoPacienteWindow</c>. Mora aqui, e não num módulo, porque a emissão roda nos quatro
/// aplicativos da suíte (a central de Documentos, o atendimento, os Exames).
///
/// ⚠️ Roda no despachante do WPF: janela modal precisa da thread da interface.
/// </summary>
public sealed class EnvioDoDocumentoAoPacienteWpf(IServiceScopeFactory escopos) : IEnvioDoDocumentoAoPaciente
{
    public Task<ResultadoDoAto> PerguntarAposEmitirAsync(DocumentoEmitido documento) => NaInterface(async () =>
    {
        var doc = new DocumentoNaTela
        {
            DocumentoId = documento.DocumentoId,
            PacienteId = documento.PacienteId,
            Numero = documento.Numero,
            Tipo = documento.Tipo,
            Rotulo = documento.Rotulo,
            Cancelado = documento.Cancelado,
            Assinado = documento.Assinado,
            LinkNoAr = documento.LinkNoAr,
            JaTeveLink = documento.JaTeveLink
        };
        var r = await AcoesDoDocumento.EnviarAsync(doc, escopos, aposEmitir: true);
        return new ResultadoDoAto(r.Mudou, r.Inline, r.EhErro, r.Silencioso);
    });

    private static Task<T> NaInterface<T>(Func<Task<T>> acao)
    {
        var despachante = System.Windows.Application.Current?.Dispatcher;
        if (despachante is null || despachante.CheckAccess()) return acao();
        return despachante.InvokeAsync(acao).Task.Unwrap();
    }
}
