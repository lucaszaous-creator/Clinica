using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using PdfSharp.Pdf.Signatures;

namespace Clinica.Application.Assinatura.SafeID;

/// <summary>
/// A ponte entre o PDFsharp e o certificado em nuvem: implementa
/// <see cref="IDigitalSigner"/> pedindo a assinatura ao PSC em vez de usar uma chave local.
///
/// Por que cabe numa classe só
/// ---------------------------
/// Porque o PDFsharp já publica a costura no formato certo — <c>GetSignatureAsync</c> é
/// assíncrona, recebe o <see cref="Stream"/> do conteúdo coberto pelo <c>/ByteRange</c> e
/// devolve os bytes que vão para o <c>/Contents</c>. Tudo o que muda em relação ao
/// certificado de token é <b>quem faz a conta</b>: aqui o hash é calculado nesta máquina e
/// só ele viaja.
///
/// O que continua sendo nosso: o desenho da folha, o carimbo visual, o posicionamento por
/// <c>AreaAssinatura</c>, o <c>Conferir</c> e a reimpressão pelos bytes guardados.
/// </summary>
public sealed class AssinadorSafeID : IDigitalSigner
{
    private readonly ClienteSafeID _cliente;
    private readonly string _token;
    private readonly CertificadoDeNuvem _certificado;
    private readonly string _rotulo;

    /// <param name="rotulo">
    /// Forma legível do que está sendo assinado (o número da folha, por exemplo). O PSC a
    /// registra e ela pode aparecer na confirmação do celular — então é o que diz à médica
    /// <b>o que</b> ela está autorizando, e não serve texto genérico.
    /// </param>
    public AssinadorSafeID(
        ClienteSafeID cliente, string token, CertificadoDeNuvem certificado, string rotulo)
    {
        _cliente = cliente;
        _token = token;
        _certificado = certificado;
        _rotulo = string.IsNullOrWhiteSpace(rotulo) ? "Documento da clínica" : rotulo;
    }

    public string CertificateName => _certificado.Certificado.Titular;

    /// <summary>
    /// Espaço reservado no <c>/Contents</c> ANTES de assinar.
    ///
    /// A doc da Safeweb não publica o tamanho máximo do PKCS#7, e este número não é
    /// chute confortável: reservar de MENOS quebra a assinatura (o PDFsharp não consegue
    /// encaixar o que voltou), enquanto reservar de mais custa alguns kilobytes de PDF. Um
    /// CMS destacado com a cadeia ICP-Brasil fica na casa de 4–8 KB; 32 KB dá folga de
    /// quatro vezes sobre o pior caso conhecido.
    ///
    /// Na homologação isto se mede e vira número medido em vez de número prudente.
    /// </summary>
    public const int TamanhoReservado = 32 * 1024;

    /// <summary>SHA-256 da cadeia vazia — o hash que um stream não lido produz.</summary>
    private static readonly byte[] HashDeNada = SHA256.HashData([]);

    public Task<int> GetSignatureSizeAsync() => Task.FromResult(TamanhoReservado);

    /// <summary>
    /// Calcula o SHA-256 do conteúdo coberto e pede ao PSC o PKCS#7 destacado.
    ///
    /// O identificador da requisição é sorteado por assinatura: o PSC casa a resposta pelo
    /// <c>id</c>, e reaproveitar um valor fixo faria duas assinaturas simultâneas do mesmo
    /// consultório trocarem de resposta entre si.
    /// </summary>
    public async Task<byte[]> GetSignatureAsync(Stream conteudoCoberto)
    {
        ArgumentNullException.ThrowIfNull(conteudoCoberto);

        // ⚠️ O RangedStream do PDFsharp chega SEM POSIÇÃO, e ler antes de posicionar devolve
        // ZERO bytes — sem erro, sem exceção, sem aviso. O `Position` nem tem getter que
        // funcione (lança NullReferenceException); o setter, sim.
        //
        // Sem esta linha, o que sobe para o PSC é o SHA-256 de NADA
        // (e3b0c442…b7852b855, o hash da cadeia vazia) — a MESMA constante para toda folha
        // da clínica. O PSC assina esse hash corretamente e devolve um PKCS#7 perfeito, e é
        // isso que torna o defeito tão caro: não há erro em lugar nenhum até a conferência
        // dizer que o documento "foi alterado", porque a assinatura de fato cobre outra
        // coisa. Assinatura válida sobre conteúdo nenhum é a garantia aparente na sua forma
        // mais perigosa.
        conteudoCoberto.Position = 0;

        var hash = await SHA256.HashDataAsync(conteudoCoberto);

        // A rede que faltava, e ela é barata: assinar o hash da cadeia VAZIA nunca é um
        // pedido legítimo. Se o stream voltar a chegar vazio (outra versão do PDFsharp, outro
        // caminho de salvamento), a folha NÃO é assinada e alguém lê o motivo — em vez de a
        // clínica descobrir semanas depois, no validador do ITI, com as receitas na rua.
        if (hash.SequenceEqual(HashDeNada))
            throw new InvalidOperationException(
                "O conteúdo coberto pela assinatura veio VAZIO — a folha NÃO foi assinada. "
                + "Assinar o hash da cadeia vazia produziria um documento com assinatura "
                + "válida que não cobre coisa alguma.");

        var pkcs7 = await _cliente.AssinarHashAsync(
            _token, hash,
            identificador: Guid.NewGuid().ToString("N"),
            rotulo: _rotulo,
            certificadoAlias: string.IsNullOrWhiteSpace(_certificado.Alias)
                ? null
                : _certificado.Alias);

        if (pkcs7.Length > TamanhoReservado)
            throw new InvalidOperationException(
                $"O SafeID devolveu uma assinatura de {pkcs7.Length} bytes, maior que o "
                + $"espaço reservado ({TamanhoReservado}). A folha NÃO foi assinada — "
                + "aumente TamanhoReservado.");

        // Confere que é PKCS#7 E normaliza para DER (ver ExigirPkcs7EnormalizarParaDer).
        var der = ExigirPkcs7EnormalizarParaDer(pkcs7);

        if (der.Length > TamanhoReservado)
            throw new InvalidOperationException(
                $"O SafeID devolveu uma assinatura de {der.Length} bytes, maior que o "
                + $"espaço reservado ({TamanhoReservado}). A folha NÃO foi assinada — "
                + "aumente TamanhoReservado.");

        return der;
    }

    /// <summary>
    /// O que voltou do PSC é mesmo um CMS destacado?
    ///
    /// Por que a pergunta precisa ser feita AQUI
    /// -----------------------------------------
    /// Estes bytes vão para o <c>/Contents</c> do PDF sem ninguém mais olhar para eles. Se
    /// não forem um PKCS#7, o arquivo sai "assinado" e o erro só aparece na conferência,
    /// como <b>"ASN1 corrupted data"</b> — uma frase que não distingue <i>"o PSC devolveu
    /// outra coisa"</i> de <i>"o arquivo foi adulterado"</i> e de <i>"nós lemos errado de
    /// volta"</i>. Foram duas rodadas de diagnóstico às cegas por causa disso.
    ///
    /// A conferência usa o MESMO <c>SignedCms</c> que <c>AssinaturaDigitalService.Conferir</c>
    /// vai usar depois — é essa a razão de ela valer: o que passa aqui passa lá.
    ///
    /// Ela mora neste ponto, e não no <c>ClienteSafeID</c>, porque aquele é transporte: ele
    /// entrega o que o PSC mandou. Aqui é onde a assinatura vira PDF, e é o único caminho
    /// pelo qual uma assinatura em nuvem chega a um documento da clínica.
    ///
    /// E ela NORMALIZA PARA DER — a metade que faltava
    /// -----------------------------------------------
    /// O SafeID devolve o CMS em <b>BER com comprimento indefinido</b> (<c>30 80 … 00 00</c>).
    /// Isso é legal em CMS (RFC 5652) e o nosso <c>Conferir</c> passou a aceitar; só que
    /// <b>PDF assinado exige DER</b> (ISO 32000-1 e PAdES/ETSI EN 319 142). Embutir o BER
    /// produziria um arquivo que o NOSSO validador aprova e que o Adobe e o validador do ITI
    /// podem recusar — e quem descobre isso é o farmacêutico, com a receita na mão.
    /// Fazer só o leitor da casa aceitar seria a garantia aparente de novo, agora do lado de
    /// fora: o sistema diria "assinatura conferida" sobre um arquivo que ninguém mais lê.
    ///
    /// A conversão é <b>segura e medida</b>: a assinatura cobre os atributos assinados, não a
    /// codificação de fora, e <c>Decode</c> + <c>Encode</c> devolve exatamente o DER que o
    /// PSC teria produzido — byte a byte, com <c>CheckSignature</c> passando.
    /// </summary>
    /// <returns>Os mesmos bytes em DER.</returns>
    private static byte[] ExigirPkcs7EnormalizarParaDer(byte[] pkcs7)
    {
        var cms = new SignedCms();

        // ⚠️ As duas etapas têm `catch` SEPARADOS de propósito. Juntas, um erro de reencode
        // sairia com a frase "não são um PKCS#7" sobre bytes que SÃO um PKCS#7 válido — e
        // mensagem plausível e errada é o que fez esta integração custar seis rodadas de
        // diagnóstico. Cada falha diz o que de fato aconteceu.
        try
        {
            cms.Decode(pkcs7);
        }
        catch (CryptographicException ex)
        {
            // O começo em hexa vai na mensagem porque é o que nomeia a causa: um CMS abre em
            // 30 82 (ou 30 80, em BER), e o que vier diferente disto diz de imediato que o
            // PSC respondeu noutro formato. Não há dado de paciente aqui — isto cobre um HASH.
            var inicio = Convert.ToHexString(pkcs7.AsSpan(0, Math.Min(8, pkcs7.Length)));

            throw new InvalidOperationException(
                $"O SafeID devolveu {pkcs7.Length} byte(s) que não são um PKCS#7 "
                + $"(começam em {inicio}). A folha NÃO foi assinada. Detalhe: {ex.Message}",
                ex);
        }

        if (cms.SignerInfos.Count == 0)
            throw new InvalidOperationException(
                "O SafeID devolveu um PKCS#7 SEM assinante. A folha NÃO foi assinada.");

        try
        {
            return cms.Encode();
        }
        catch (CryptographicException ex)
        {
            // Medido: BER indefinido em até quatro níveis — muito além do que um PSC emite —
            // reencoda para o DER byte a byte idêntico. Só um CMS indefinido até o miolo
            // (inclusive o certificado embutido, que nenhum PSC reescreve) derruba o reencode.
            //
            // Recusar é o certo mesmo assim: os bytes originais são um CMS válido, mas em BER,
            // e PDF assinado exige DER — embuti-los produziria um arquivo que a NOSSA
            // conferência aprova e que o Adobe e o ITI podem recusar. Melhor a clínica saber
            // agora, com a via em papel valendo, do que o farmacêutico descobrir depois.
            throw new InvalidOperationException(
                $"O SafeID devolveu uma assinatura VÁLIDA, mas em BER que não foi possível "
                + $"converter para DER — e o PDF assinado exige DER. A folha NÃO foi "
                + $"assinada; ela continua valendo impressa e assinada à caneta. "
                + $"Detalhe: {ex.Message}",
                ex);
        }
    }
}
