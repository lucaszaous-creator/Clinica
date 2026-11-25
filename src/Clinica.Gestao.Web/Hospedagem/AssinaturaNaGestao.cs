using Clinica.Application.Assinatura;
using Clinica.Infrastructure;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace Clinica.Gestao.Web.Hospedagem;

/// <summary>
/// A gestão web ASSINA receitas e documentos clínicos (nov/2026). Autorização expressa do dono em 24/11/2026,
/// "Autorizo, na Gestão" — docs/safeid-congelado.md. O motor continua congelado: quem assina é a API do portal, pelas
/// MESMAS rotas e na MESMA ordem do Consultório (SafeIdTabletService / A1TabletService). Esta classe só leva o pedido
/// ao navegador (<c>wwwroot/assinatura-na-gestao.js</c>), que fala com a API na mesma sessão do portal.
/// <list type="bullet">
/// <item>SafeID: a página vai à Safeweb na mesma aba (sem pop-up) e volta em <c>/gestao/?assinatura=</c>, onde se
/// confirma "Assinar e arquivar". A chamada daqui nunca termina: a página trocou.</item>
/// <item>A1: senha no diálogo, assinatura e a frase de volta para a tela.</item>
/// </list>
/// A infusão (prescrição, folha de execução) segue só no Consultório, com o A1: os seletores de certificado devolvem
/// nulo aqui (desistiu), e a tela diz o motivo.
/// </summary>
public sealed class AssinaturaNaGestao(IJSRuntime js, IServiceScopeFactory escopos) : IAssinaturaNaTela
{
    /// <summary>O diálogo espera a pessoa (senha do A1, conferir o PDF): o prazo padrão de 1 min da interop não serve.</summary>
    public static readonly TimeSpan Prazo = TimeSpan.FromMinutes(20);

    public sealed record RespostaDoNavegador(string? Frase, bool Erro, bool Saiu);

    public Task<CertificadoAssinatura?> EscolherCertificadoAsync(string assunto) => Task.FromResult<CertificadoAssinatura?>(null);

    public Task<CertificadoAssinatura?> EscolherCertificadoParaDocumentoAsync(string assunto) => Task.FromResult<CertificadoAssinatura?>(null);

    public async Task<ResultadoDoAto> AssinarDocumentoAsync(DocumentoEmitido doc)
    {
        var r = await AssinarAsync(doc.DocumentoId, doc.PacienteId, doc.Numero, $"{doc.Rotulo} nº {doc.Numero}");
        return r is not { } feito ? new ResultadoDoAto(Silencioso: true) : new ResultadoDoAto(Mudou: !feito.EhErro, Frase: feito.Frase, EhErro: feito.EhErro);
    }

    public Task<(string Frase, bool EhErro)?> AssinarDocumentoPorIdAsync(int documentoId, string assunto)
        => AssinarAsync(documentoId, null, null, assunto);

    private async Task<(string Frase, bool EhErro)?> AssinarAsync(int documentoId, int? pacienteId, string? numero, string assunto)
    {
        if (pacienteId is null || numero is null)
        {
            using var escopo = escopos.CreateScope();
            var db = escopo.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var d = await db.DocumentosClinicos.AsNoTracking().Where(x => x.Id == documentoId)
                .Select(x => new { x.PacienteId, x.Numero }).SingleOrDefaultAsync();
            if (d is null) return ("Documento não encontrado.", true);
            pacienteId ??= d.PacienteId; numero ??= d.Numero;
        }
        var r = await js.InvokeAsync<RespostaDoNavegador?>("assinaturaNaGestao.assinar", Prazo,
            new { documento = documentoId, paciente = pacienteId, numero, assunto });
        if (r is null) return null;                     // desistiu
        if (r.Saiu) await new TaskCompletionSource().Task; // a página foi à Safeweb: a volta é /gestao/?assinatura=
        return (r.Frase ?? (r.Erro ? "A assinatura não foi concluída." : "Documento assinado e arquivado."), r.Erro);
    }
}
