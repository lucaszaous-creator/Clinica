using Clinica.Application.Assinatura;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Formularios.Assinatura;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Hospedagem;

/// <summary>
/// A PORTA DA ASSINATURA COM CERTIFICADO NAS TELAS BLAZOR (A1; e o SafeID nos documentos) (out/2026 — autorização da direção de
/// 19/10/2026, só a camada de tela). Substitui o <c>EscolherCertificadoWindow</c> WPF e o caminho
/// do <c>AcoesDoDocumento.AssinarAsync</c>: as telas Blazor (e as que outras frentes estão
/// convertendo — documentos, exames, prontuário) assinam POR AQUI, sem janela WPF por cima.
///
/// ⚠️ O que a porta faz é PERGUNTAR (o seletor do A1, em formulário dentro da página) e DIZER (a
/// frase, o arquivo aberto). A sequência das chamadas ao motor congelado mora em
/// <see cref="AssinaturaDeDocumentoNaTela"/> e <see cref="AssinaturaDaPrescricaoNaTela"/>, as
/// MESMAS que a ponte WPF que sobrou usa — duas definições divergiriam justamente onde cada
/// tentativa é cobrada.
/// </summary>
public interface IAssinaturaNaTela
{
    /// <summary>
    /// Abre o seletor do A1 (importar o arquivo, conferir o titular) e devolve o certificado
    /// confirmado — que passa a ser de quem chamou (use <c>using</c>). Nulo = desistiu.
    /// </summary>
    Task<CertificadoAssinatura?> EscolherCertificadoAsync(string assunto);

    /// <summary>
    /// O mesmo seletor, com o SafeID (certificado em nuvem) ao lado do A1 — o das RECEITAS E
    /// DOCUMENTOS (out/2026, a pedido da clínica). A infusão usa <see cref="EscolherCertificadoAsync"/>
    /// e segue só com o A1.
    /// </summary>
    Task<CertificadoAssinatura?> EscolherCertificadoParaDocumentoAsync(string assunto);

    /// <summary>
    /// Assina um documento clínico JÁ EMITIDO — o MESMO ato do "Assinar com o e-CPF…" do WPF:
    /// guardas de estado → bit do TIPO → certificado → serviço → o ARQUIVO salvo e aberto.
    /// </summary>
    Task<ResultadoDoAto> AssinarDocumentoAsync(DocumentoEmitido doc);

    /// <summary>
    /// Assina pelo NÚMERO do documento (a porta dos Prontuários: a anamnese). Devolve a frase e se
    /// é erro; nulo = desistiu no seletor.
    /// </summary>
    Task<(string Frase, bool EhErro)?> AssinarDocumentoPorIdAsync(int documentoId, string assunto);
}

/// <summary>A porta da assinatura nas telas Blazor — um por navegador (Scoped), como a camada dos formulários.</summary>
public sealed class AssinaturaNaTela(
    JanelasNaTela janelas, IServiceScopeFactory escopos, IHostDaTela host, Notificacoes notificacoes) : IAssinaturaNaTela
{
    public Task<CertificadoAssinatura?> EscolherCertificadoAsync(string assunto)
        => janelas.AbrirAsync<EscolherCertificadoForm, CertificadoAssinatura>(
            new Dictionary<string, object?> { ["Assunto"] = assunto });

    public Task<CertificadoAssinatura?> EscolherCertificadoParaDocumentoAsync(string assunto)
        => janelas.AbrirAsync<EscolherCertificadoForm, CertificadoAssinatura>(
            new Dictionary<string, object?> { ["Assunto"] = assunto, ["PermitirNuvem"] = true });

    public async Task<ResultadoDoAto> AssinarDocumentoAsync(DocumentoEmitido doc)
    {
        if (AssinaturaDeDocumentoNaTela.Recusa(doc.Tipo, doc.Numero, doc.Cancelado, doc.Assinado) is { } recusa)
            return new ResultadoDoAto(Frase: recusa, EhErro: true);

        try
        {
            // O bit do TIPO, nunca um fixo (parcela 60) — a mesma barreira do WPF.
            SessaoUsuario.Atual.Exigir(CentralDocumentosService.AcessoParaEmitir(doc.Tipo), "assinar documento clínico");

            var assinado = await AssinaturaDeDocumentoNaTela.AssinarAsync(
                escopos, doc.DocumentoId, AssinaturaDeDocumentoNaTela.Assunto(doc.Rotulo, doc.Numero),
                EscolherCertificadoParaDocumentoAsync,
                SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
                SessaoUsuario.Atual.Operador);

            if (assinado is null) return new ResultadoDoAto(Silencioso: true);

            // O arquivo assinado é SALVO E ABERTO, nunca mandado para a impressora: a assinatura
            // vive nos BYTES, e a via impressa de um documento assinado é CÓPIA.
            var erro = await host.SalvarEAbrirAsync(assinado.Pdf, assinado.NomeArquivo, "PDF (*.pdf)|*.pdf", ".pdf");
            if (erro is not null)
                return new ResultadoDoAto(Mudou: true,
                    Frase: $"{erro} O documento foi assinado e está guardado no sistema.", EhErro: true);

            notificacoes.Sucesso(AssinaturaDeDocumentoNaTela.Assinado);
            return new ResultadoDoAto(Mudou: true);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar($"Documento {doc.Numero} — assinatura não pôde ser concluída", ex);
            return new ResultadoDoAto(Frase: ex.Message, EhErro: true);
        }
    }

    public async Task<(string Frase, bool EhErro)?> AssinarDocumentoPorIdAsync(int documentoId, string assunto)
    {
        var assinado = await AssinaturaDeDocumentoNaTela.AssinarAsync(
            escopos, documentoId, assunto, EscolherCertificadoParaDocumentoAsync,
            SessaoUsuario.Atual.Autenticado ? SessaoUsuario.Atual.UsuarioId : null,
            SessaoUsuario.Atual.Operador);
        if (assinado is null) return null;

        // É o ARQUIVO que vale: salva e abre; a frase diz se o salvamento falhou.
        var erro = await host.SalvarEAbrirAsync(assinado.Pdf, assinado.NomeArquivo, "PDF (*.pdf)|*.pdf", ".pdf");
        return (erro ?? "Documento assinado.", erro is not null);
    }
}
