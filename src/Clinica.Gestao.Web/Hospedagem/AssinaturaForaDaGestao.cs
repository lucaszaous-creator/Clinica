using Clinica.Application.Assinatura;
using Clinica.Ui.Blazor.Hospedagem;

namespace Clinica.Gestao.Web.Hospedagem;

/// <summary>
/// A gestão web NÃO assina documento clínico (nov/2026). A assinatura digital é área CONGELADA
/// (docs/safeid-congelado.md): cada tentativa é cobrada pelo PSC e só sete rodadas em produção a
/// fizeram funcionar. Levá-la ao navegador da gestão pede autorização expressa da direção. Até lá,
/// quem assina usa o aplicativo do computador ou o Consultório do portal — e a tela diz isso.
/// </summary>
public sealed class AssinaturaForaDaGestao : IAssinaturaNaTela
{
    public const string Frase = "A assinatura digital é feita no aplicativo do computador ou no Consultório do portal.";

    public Task<CertificadoAssinatura?> EscolherCertificadoAsync(string assunto) => Task.FromResult<CertificadoAssinatura?>(null);

    public Task<CertificadoAssinatura?> EscolherCertificadoParaDocumentoAsync(string assunto) => Task.FromResult<CertificadoAssinatura?>(null);

    public Task<ResultadoDoAto> AssinarDocumentoAsync(DocumentoEmitido doc) => Task.FromResult(new ResultadoDoAto(Frase: Frase, EhErro: true));

    public Task<(string Frase, bool EhErro)?> AssinarDocumentoPorIdAsync(int documentoId, string assunto)
        => Task.FromResult<(string, bool)?>((Frase, true));
}
