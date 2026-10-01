using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Signatures;

namespace Clinica.Application.Assinatura;

/// <summary>Onde o carimbo visual da assinatura fica na página, em pontos.</summary>
/// <param name="Pagina">Índice da página (0 = primeira).</param>
public sealed record AreaAssinatura(int Pagina, double X, double Y, double Largura, double Altura);

/// <summary>O que a assinatura precisa saber sobre o ato que ela está selando.</summary>
/// <param name="CarimbadoraDeTempo">
/// URL de uma ACT (RFC 3161). Null produz PAdES-B: assinatura válida, mas com a data por
/// conta do relógio de quem assinou — e o PDF diz isso, em vez de fingir precisão.
/// </param>
public sealed record PedidoAssinatura(
    string Motivo,
    string NomeExibido,
    string? RegistroConselho,
    AreaAssinatura Area,
    string? Local = null,
    string? Contato = null,
    Uri? CarimbadoraDeTempo = null);

/// <summary>O PDF assinado e o que ficou provado sobre ele.</summary>
public sealed record ResultadoAssinatura(
    byte[] Pdf,
    string Hash,
    DateTime? CarimboTempoEm,
    string? CarimboTempoAutoridade);

/// <summary>
/// O que a conferência de um PDF assinado descobriu.
///
/// Note os DOIS booleanos, e que eles não são um só: <see cref="Conferida"/> diz se a
/// checagem chegou a rodar, e <see cref="Integra"/> diz o que ela concluiu. É a regra da
/// casa — falha de conferência nunca pode ser exibida como sucesso, e "não consegui olhar"
/// é um terceiro estado, não um "está tudo bem".
/// </summary>
public sealed record ConferenciaAssinatura(
    bool Conferida,
    bool Integra,
    string? Motivo = null,
    string? Titular = null,
    string? Emissor = null,
    DateTime? CarimboTempoEm = null)
{
    public static ConferenciaAssinatura NaoConferida(string motivo)
        => new(Conferida: false, Integra: false, motivo);

    /// <summary>A frase que a tela e o rodapé do PDF escrevem.</summary>
    public string Frase => (Conferida, Integra) switch
    {
        (false, _) => $"Não foi possível conferir a assinatura ({Motivo}).",
        (true, true) => "Assinatura conferida: o documento está íntegro.",
        _ => $"ATENÇÃO: o documento foi ALTERADO depois de assinado ({Motivo})."
    };
}

/// <summary>
/// Assinatura digital de PDF com certificado ICP-Brasil (parcela 42).
///
/// Por que o projeto passou a precisar disto
/// -----------------------------------------
/// A clínica pediu a folha de infusão "carimbada" pela médica e pela enfermagem, e a ideia
/// inicial era <b>escanear o carimbo</b> e colar a imagem no PDF. Isso é pior do que não
/// assinar: uma imagem não prova autoria (qualquer um copia e cola noutro documento), e a
/// partir do dia em que o JPG está no banco existe uma assinatura da médica reutilizável
/// por quem tiver acesso ao sistema. O pior é que ela PARECE uma garantia — que é
/// exatamente o que <c>DocumentosClinicosPdfService</c> se recusa a fazer desde a parcela 3.
///
/// O que este serviço faz, e o que ele não faz
/// -------------------------------------------
/// Faz: PKCS#7 destacado SHA-256 dentro do PDF, cobrindo o arquivo inteiro por um
/// <c>/ByteRange</c>; carimbo do tempo RFC 3161 quando a clínica configura uma ACT; e a
/// conferência de volta, que detecta um único bit trocado.
///
/// Não faz: LTV (embutir CRL/OCSP para o documento continuar verificável depois de o
/// certificado expirar). Isso é PAdES-LT e depende de infraestrutura que a clínica não
/// tem hoje; anunciá-lo sem implementá-lo seria a mesma mentira do carimbo escaneado.
///
/// Duas assinaturas no mesmo PDF: a premissa que estava certa pela metade
/// -----------------------------------------------------------------------
/// Este comentário dizia, da parcela 42 até a 68, que "duas assinaturas no mesmo PDF não
/// existem, porque o PDFsharp reescreve o arquivo ao salvar". A primeira metade foi
/// MEDIDA e confirmada: assinar por cima do já assinado devolve um arquivo cujo prefixo
/// mudou, e a assinatura de quem assinou primeiro deixa de fechar.
///
/// A CONCLUSÃO é que estava errada. A limitação é da biblioteca, não do formato — o PDF
/// prevê múltiplas assinaturas exatamente para este caso, por <b>atualização
/// incremental</b>: a revisão nova é anexada ao fim e os bytes já assinados não se tocam.
/// É o que <see cref="AnexarAssinaturaAsync"/> faz, e é o fluxo que a clínica descreveu —
/// o médico prescreve e assina, a folha vai para a sala, a enfermagem assina A MESMA
/// prescrição.
///
/// ⚠️ A lição vale além daqui: <b>quando uma limitação de ferramenta vira decisão de
/// desenho, escreva qual das duas você mediu.</b> Esta ficou seis parcelas de pé sem
/// ninguém tentar o caminho que o formato já oferecia.
/// </summary>
public sealed class AssinaturaDigitalService
{
    /// <summary>
    /// Recusa assinar com certificado cuja cadeia não valida na máquina.
    ///
    /// Ligado em produção, porque uma assinatura que não encadeia abre como INVÁLIDA em
    /// qualquer leitor de PDF, e o profissional só descobriria numa auditoria — com meses
    /// de folhas para refazer. Desligado nos testes, que assinam com certificado
    /// autoassinado construído na memória.
    /// </summary>
    private readonly bool _exigirCadeiaConfiavel;

    public AssinaturaDigitalService(bool exigirCadeiaConfiavel = true)
        => _exigirCadeiaConfiavel = exigirCadeiaConfiavel;

    /// <summary>SHA-256 em hexadecimal minúsculo — o formato gravado em <c>AssinaturaDocumento</c>.</summary>
    public static string Hash(byte[] conteudo)
        => Convert.ToHexString(SHA256.HashData(conteudo)).ToLowerInvariant();

    public async Task<ResultadoAssinatura> AssinarAsync(
        byte[] pdf, CertificadoAssinatura certificado, PedidoAssinatura pedido,
        IDigitalSigner? assinadorPersonalizado = null)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(certificado);
        ArgumentNullException.ThrowIfNull(pedido);

        // Ponto de extensão do motor para conferir formatos CMS nos testes.
        // O fluxo clínico usa a chave privada do A1 importado.
        var personalizado = assinadorPersonalizado;

        Criticar(certificado, exigirChaveLocal: personalizado is null);
        GarantirFonte();

        using var entrada = new MemoryStream(pdf, writable: false);
        var documento = PdfReader.Open(entrada, PdfDocumentOpenMode.Modify);

        // A página pedida pode não existir se o conteúdo encolheu (uma folha com menos
        // itens). Cair na última é melhor que estourar na hora de assinar.
        var pagina = Math.Clamp(pedido.Area.Pagina, 0, documento.PageCount - 1);

        // ⚠️ O bloco visível é CONTEÚDO DA PÁGINA, e não a aparência do widget da
        // assinatura. Foram DOIS defeitos medidos em 20/08/2026, e cada um sozinho já
        // apagava o carimbo:
        //
        //  1. a aparência do widget é um form XObject de BBox [0 0 largura altura], e o
        //     retângulo que o PDFsharp entrega ao desenhador é o da PÁGINA — desenhar ali
        //     punha o traço inteiro fora da BBox, e o form recorta pela BBox;
        //  2. anotação sem o bit Print (/F 4) é desenhada na TELA e não é IMPRESSA, e o
        //     PDFsharp não escreve o /F (o padrão é 0). O campo só existe durante o
        //     salvamento, depois de a aparência ter sido desenhada, então não há onde
        //     ligar o bit.
        //
        // Conteúdo de página não tem nenhuma das duas armadilhas: ele aparece no leitor,
        // sai na impressora e é COBERTO pela assinatura, porque entra antes dela. Saiu
        // errado da parcela 42 até aqui — todo documento assinado foi para a clínica sem o
        // bloco visível, com a assinatura criptográfica perfeita por baixo.
        DesenharCarimbo(documento.Pages[pagina], certificado, pedido);

        var opcoes = new DigitalSignatureOptions
        {
            Reason = pedido.Motivo,
            Location = pedido.Local ?? string.Empty,
            ContactInfo = pedido.Contato ?? string.Empty,
            PageIndex = pagina,

            // Campo de assinatura INVISÍVEL (retângulo de área zero): quem desenha é a
            // página, logo acima. Deixar o widget com o mesmo retângulo faria o leitor
            // desenhar o bloco DUAS vezes sobre si mesmo — o texto sai mais grosso e a
            // moldura, mais escura.
            Rectangle = new XRect(0, 0, 0, 0)
        };

        var assinador = personalizado ?? new PdfSharpDefaultSigner(
            certificado.Certificado, PdfMessageDigestType.SHA256, pedido.CarimbadoraDeTempo);

        DigitalSignatureHandler.ForDocument(documento, assinador, opcoes);

        using var saida = new MemoryStream();

        // O PDFsharp conclui a assinatura ao salvar; aguardar a operação antes de arquivar.
        await documento.SaveAsync(saida, false);
        var assinado = saida.ToArray();

        // O carimbo do tempo é LIDO de volta do que foi realmente produzido, nunca
        // assumido a partir do relógio local: se a ACT respondeu, a data é dela; se não
        // respondeu, o campo fica nulo e o PDF escreve que a data é declarada. Gravar
        // DateTime.Now aqui e chamá-lo de carimbo do tempo seria inventar a prova.
        var carimbo = pedido.CarimbadoraDeTempo is null ? null : CarimboDeTempo(assinado);

        return new ResultadoAssinatura(
            assinado,
            Hash(assinado),
            carimbo,
            carimbo is null ? null : pedido.CarimbadoraDeTempo?.ToString());
    }

    /// <summary>Acrescenta uma assinatura por revisão incremental, preservando todos os bytes já assinados.</summary>
    public async Task<ResultadoAssinatura> AnexarAssinaturaAsync(
        byte[] pdfAssinado, CertificadoAssinatura certificado, PedidoAssinatura pedido,
        string nomeCampo, IDigitalSigner? assinadorPersonalizado = null)
    {
        ArgumentNullException.ThrowIfNull(pdfAssinado);
        ArgumentNullException.ThrowIfNull(certificado);
        ArgumentNullException.ThrowIfNull(pedido);

        var personalizado = assinadorPersonalizado;

        Criticar(certificado, exigirChaveLocal: personalizado is null);
        GarantirFonte();

        var assinador = personalizado ?? new PdfSharpDefaultSigner(
            certificado.Certificado, PdfMessageDigestType.SHA256, pedido.CarimbadoraDeTempo);

        var anexado = await RevisaoIncrementalPdf.AnexarAssinaturaAsync(
            pdfAssinado, pedido, certificado, assinador, nomeCampo);

        var carimbo = pedido.CarimbadoraDeTempo is null ? null : CarimboDeTempo(anexado);

        return new ResultadoAssinatura(
            anexado,
            Hash(anexado),
            carimbo,
            carimbo is null ? null : pedido.CarimbadoraDeTempo?.ToString());
    }

    /// <summary>
    /// Refaz a conta da assinatura sobre os bytes do arquivo e diz se ele continua o
    /// mesmo. É o que a tela de conferência e o rodapé da reimpressão usam.
    /// </summary>
    public ConferenciaAssinatura Conferir(byte[] pdfAssinado)
    {
        var todas = ConferirTodas(pdfAssinado);

        // ⚠️ O documento vale pela PIOR das assinaturas, não pela primeira. Desde que a
        // prescrição passou a levar a assinatura da médica E a da enfermagem (revisão
        // incremental), olhar só a primeira responderia "íntegro" a um arquivo cuja segunda
        // assinatura não fecha — falha exibida como sucesso, no documento que a clínica usa
        // para provar quem mandou e quem executou.
        return todas.FirstOrDefault(c => c.Conferida && !c.Integra)
               ?? todas.FirstOrDefault(c => !c.Conferida)
               ?? todas.FirstOrDefault()
               ?? ConferenciaAssinatura.NaoConferida("o arquivo não tem assinatura");
    }

    /// <summary>
    /// Uma conferência POR ASSINATURA, na ordem em que elas aparecem no arquivo — a
    /// primeira é a de quem assinou primeiro.
    ///
    /// Existe porque a folha de infusão passou a ter duas: a prescritora sela a revisão
    /// dela, e a enfermagem anexa a sua sem tocar num byte do que já estava assinado. As
    /// duas precisam ser mostradas, porque elas respondem perguntas diferentes — "quem
    /// mandou" e "quem executou".
    /// </summary>
    public IReadOnlyList<ConferenciaAssinatura> ConferirTodas(byte[] pdfAssinado)
    {
        if (pdfAssinado is null || pdfAssinado.Length == 0)
            return [ConferenciaAssinatura.NaoConferida("arquivo vazio")];

        var faixas = LerByteRanges(pdfAssinado);
        if (faixas.Count == 0)
            return [ConferenciaAssinatura.NaoConferida("o arquivo não tem assinatura")];

        return [.. faixas.Select(f => ConferirUma(pdfAssinado, f))];
    }

    private ConferenciaAssinatura ConferirUma(
        byte[] pdfAssinado, (int, int, int, int) faixa)
    {
        try
        {
            var (inicio1, tamanho1, inicio2, tamanho2) = faixa;

            // O buraco ENTRE os dois trechos é o /Contents <...> com o PKCS#7 em hexa. É
            // por ficar fora do ByteRange que a assinatura consegue estar dentro do
            // arquivo que ela assina.
            var (pkcs7, porque) = LerConteudoAssinatura(pdfAssinado, inicio1 + tamanho1, inicio2);
            if (pkcs7 is null)
                return ConferenciaAssinatura.NaoConferida(
                    $"o bloco de assinatura está ilegível — {porque}");

            var cobertos = new byte[tamanho1 + tamanho2];
            Buffer.BlockCopy(pdfAssinado, inicio1, cobertos, 0, tamanho1);
            Buffer.BlockCopy(pdfAssinado, inicio2, cobertos, tamanho1, tamanho2);

            var cms = new SignedCms(new ContentInfo(cobertos), detached: true);
            cms.Decode(pkcs7);

            var signatario = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0] : null;
            var certificado = signatario?.Certificate;

            try
            {
                // verifySignatureOnly: a validade da CADEIA é assunto do momento de
                // ASSINAR (ver Criticar). Aqui a pergunta é outra e mais simples: este
                // arquivo é o mesmo que foi assinado? Misturar as duas faria uma folha
                // legítima de dois anos atrás aparecer como adulterada no dia em que o
                // certificado da médica vencesse.
                cms.CheckSignature(verifySignatureOnly: true);
            }
            catch (CryptographicException ex)
            {
                return new ConferenciaAssinatura(
                    Conferida: true, Integra: false, ex.Message,
                    certificado?.Subject, certificado?.Issuer);
            }

            return new ConferenciaAssinatura(
                Conferida: true,
                Integra: true,
                Motivo: null,
                Titular: certificado is null ? null : CertificadoIcpBrasil.Ler(certificado).Titular,
                Emissor: certificado?.GetNameInfo(X509NameTypeEmissor, forIssuer: true),
                CarimboTempoEm: CarimboDeTempo(pdfAssinado));
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("AssinaturaDigitalService.Conferir", ex);
            return ConferenciaAssinatura.NaoConferida(ex.Message);
        }
    }

    private const System.Security.Cryptography.X509Certificates.X509NameType X509NameTypeEmissor
        = System.Security.Cryptography.X509Certificates.X509NameType.SimpleName;

    /// <summary>Recusa o que não pode assinar, com a frase que a tela mostra.</summary>
    /// <param name="exigirChaveLocal">Exigido no fluxo clínico A1; o motor de testes pode fornecer um assinador CMS próprio.</param>
    private void Criticar(CertificadoAssinatura certificado, bool exigirChaveLocal)
    {
        if (exigirChaveLocal && !certificado.Certificado.HasPrivateKey)
            throw new InvalidOperationException(
                "O certificado escolhido não tem chave privada nesta máquina — se for um A3, " +
                "confira se o token está conectado.");

        if (!certificado.Vigente)
            throw new InvalidOperationException(
                $"O certificado de {certificado.Titular} está fora da validade " +
                $"({certificado.ValidoDe:dd/MM/yyyy} a {certificado.ValidoAte:dd/MM/yyyy}). " +
                "Assinar com ele produziria um documento que abre como inválido.");

        if (!_exigirCadeiaConfiavel) return;

        var impedimento = CertificadoIcpBrasil.ConferirCadeia(certificado.Certificado);
        if (impedimento is not null)
            throw new InvalidOperationException(
                "A cadeia deste certificado não é reconhecida por esta máquina, então a " +
                "assinatura abriria como inválida em qualquer leitor de PDF. Instale a " +
                $"cadeia da ICP-Brasil e tente de novo. Detalhe: {impedimento.Motivo}");
    }

    // ---- Leitura do PDF assinado ----

    private static readonly Regex PadraoByteRange = new(
        @"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]",
        RegexOptions.Compiled);

    private static (int, int, int, int)? LerByteRange(byte[] pdf)
        => LerByteRanges(pdf) is [var primeira, ..] ? primeira : null;

    /// <summary>
    /// TODOS os <c>/ByteRange</c> do arquivo — um por assinatura —, na ordem em que
    /// aparecem. Num PDF com revisão incremental a ordem do arquivo é a ordem cronológica:
    /// a revisão nova é anexada ao fim.
    /// </summary>
    private static List<(int, int, int, int)> LerByteRanges(byte[] pdf)
    {
        // Latin1 porque a leitura é sobre BYTES: qualquer decodificação que junte ou
        // descarte bytes (UTF-8 faz as duas coisas com sequência inválida) desalinharia
        // os índices que o /ByteRange declara, e a conferência passaria a acusar
        // adulteração em arquivo íntegro.
        var texto = Encoding.Latin1.GetString(pdf);

        return [.. PadraoByteRange.Matches(texto).Select(m => (
            int.Parse(m.Groups[1].Value),
            int.Parse(m.Groups[2].Value),
            int.Parse(m.Groups[3].Value),
            int.Parse(m.Groups[4].Value)))];
    }

    /// <summary>
    /// Os bytes do PKCS#7, ou <c>null</c> mais o MOTIVO — e o motivo é a metade que importa:
    /// "o bloco de assinatura está ilegível" sozinho não distingue "o espaço está todo em
    /// zeros" de "o cabeçalho DER não bate", que pedem investigações opostas. Foi essa frase
    /// muda que chegou à clínica em 14/08/2026 e não deu para diagnosticar.
    /// </summary>
    private static (byte[]? Bytes, string Porque) LerConteudoAssinatura(
        byte[] pdf, int inicio, int fim)
    {
        if (inicio < 0 || fim > pdf.Length || fim <= inicio)
            return (null, $"a faixa do /Contents não fecha (de {inicio} a {fim}, "
                          + $"arquivo de {pdf.Length} bytes)");

        var bruto = Encoding.Latin1.GetString(pdf, inicio, fim - inicio).Trim();
        var hexa = bruto.TrimStart('<').TrimEnd('>');

        // Um nibble solto no fim só pode ser lixo do enchimento: o PKCS#7 é byte-alinhado.
        if (hexa.Length % 2 == 1) hexa = hexa[..^1];
        if (hexa.Length == 0) return (null, "o /Contents veio vazio");

        byte[] comFolga;
        try { comFolga = Convert.FromHexString(hexa); }
        catch (FormatException)
        {
            var ruim = hexa.FirstOrDefault(c => !Uri.IsHexDigit(c));
            return (null, $"o /Contents tem caractere fora do hexadecimal ('{ruim}')");
        }

        // Tudo zero significa que o espaço foi RESERVADO e a assinatura nunca foi escrita
        // nele. É um estado bem diferente de "o cabeçalho não bate", e confundir os dois foi
        // o que fez esta frase não ajudar em nada quando ela apareceu na clínica.
        if (comFolga.All(b => b == 0))
            return (null, $"o espaço da assinatura ({comFolga.Length} bytes) está todo em "
                          + "zeros — a assinatura não chegou a ser escrita no arquivo");

        var recortado = RecortarAsn1(comFolga);

        return recortado is not null
            ? (recortado, string.Empty)
            : (null, $"a estrutura ASN.1 não é legível (começa em "
                     + $"{Convert.ToHexString(comFolga.AsSpan(0, Math.Min(8, comFolga.Length)))}, "
                     + $"{comFolga.Length} bytes disponíveis)");
    }

    /// <summary>Recorta o CMS pelo comprimento ASN.1, aceitando BER e DER dos PDFs arquivados.
    /// Não remover zeros finais: eles podem pertencer à própria assinatura.</summary>
    public static byte[]? RecortarAsn1(byte[] comFolga)
    {
        if (comFolga is null || comFolga.Length < 2) return null;

        try
        {
            AsnDecoder.ReadEncodedValue(
                comFolga, AsnEncodingRules.BER,
                out _, out _, out var consumidos);

            return consumidos <= 0 || consumidos > comFolga.Length
                ? null
                : comFolga[..consumidos];
        }
        catch (AsnContentException) { return null; }
    }

    /// <summary>
    /// A data do carimbo do tempo RFC 3161, quando existe.
    ///
    /// Sai do <c>TSTInfo</c> emitido pela ACT, não do relógio local — é essa a diferença
    /// entre uma data provada e uma data declarada, e é a única razão de valer o custo de
    /// contratar uma ACT.
    /// </summary>
    private static DateTime? CarimboDeTempo(byte[] pdfAssinado)
    {
        const string OidTimeStampToken = "1.2.840.113549.1.9.16.2.14";

        try
        {
            var faixa = LerByteRange(pdfAssinado);
            if (faixa is null) return null;

            var (inicio1, tamanho1, inicio2, _) = faixa.Value;
            var (pkcs7, _) = LerConteudoAssinatura(pdfAssinado, inicio1 + tamanho1, inicio2);
            if (pkcs7 is null) return null;

            var cms = new SignedCms();
            cms.Decode(pkcs7);
            if (cms.SignerInfos.Count == 0) return null;

            var atributo = cms.SignerInfos[0].UnsignedAttributes
                .Cast<CryptographicAttributeObject>()
                .FirstOrDefault(a => a.Oid.Value == OidTimeStampToken);

            if (atributo is null || atributo.Values.Count == 0) return null;

            var token = new SignedCms();
            token.Decode(atributo.Values[0].RawData);

            return LerGenTime(token.ContentInfo.Content);
        }
        catch (Exception ex)
        {
            // Sem carimbo legível o documento continua válido — só passa a declarar a
            // data em vez de prová-la, e o rodapé escreve isso. Silenciar seria transformar
            // "não consegui ler" em "não tem", que são coisas diferentes.
            Diagnostico.Registrar("AssinaturaDigitalService.CarimboDeTempo", ex);
            return null;
        }
    }

    /// <summary>
    /// <c>TSTInfo ::= SEQUENCE { version, policy, messageImprint, serialNumber, genTime, … }</c>
    /// — só interessa o quinto campo.
    /// </summary>
    private static DateTime? LerGenTime(byte[] tstInfo)
    {
        var seq = new AsnReader(tstInfo, AsnEncodingRules.BER).ReadSequence();
        seq.ReadInteger();              // version
        seq.ReadObjectIdentifier();     // policy
        seq.ReadEncodedValue();         // messageImprint
        seq.ReadInteger();              // serialNumber
        return seq.ReadGeneralizedTime().LocalDateTime;
    }

    // ---- Aparência e fonte ----

    private static readonly object TravaFonte = new();
    private static bool _fonteResolvida;

    /// <summary>
    /// O PDFsharp exige um resolvedor de fonte GLOBAL para desenhar o carimbo, e o padrão
    /// dele procura Verdana — que não existe em Linux (e a suíte de testes roda em Linux).
    /// A fonte usada vem embutida no próprio pacote, então nada é baixado nem lido do disco.
    ///
    /// Escrever num estático de processo é feio, e é o contrato da biblioteca. O que dá
    /// para fazer é não atropelar quem já configurou o seu.
    /// </summary>
    private static void GarantirFonte()
    {
        if (_fonteResolvida) return;

        lock (TravaFonte)
        {
            if (_fonteResolvida) return;
            GlobalFontSettings.FontResolver ??= new FonteDoCarimbo();
            _fonteResolvida = true;
        }
    }

    /// <summary>
    /// A fonte do carimbo, embutida no pacote (não depende do que a máquina tem instalado).
    ///
    /// ⚠️ O <c>bold</c> tem de ser HONRADO. A primeira versão devolvia sempre a face
    /// regular e descartava o parâmetro: o título "ASSINADO DIGITALMENTE — ICP-Brasil" saía
    /// com o mesmo peso do nome e da data, e o bloco ficava sem hierarquia nenhuma.
    /// Enquanto o carimbo era invisível ninguém via; agora ele é a única linha de cabeçalho
    /// do bloco, no papel que a clínica entrega.
    /// </summary>
    private sealed class FonteDoCarimbo : IFontResolver
    {
        private const string Regular = "SegoeWP#";
        private const string Negrito = "SegoeWPBold#";

        public byte[]? GetFont(string faceName) => faceName == Negrito
            ? PdfSharp.WPFonts.FontDataHelper.SegoeWPBold
            : PdfSharp.WPFonts.FontDataHelper.SegoeWP;

        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
            => new FontResolverInfo(bold ? Negrito : Regular);
    }

    /// <summary>
    /// Cola o bloco visível da assinatura no conteúdo da página, ANTES de assinar.
    /// </summary>
    private static void DesenharCarimbo(
        PdfPage pagina, CertificadoAssinatura certificado, PedidoAssinatura pedido)
    {
        using var gfx = XGraphics.FromPdfPage(pagina, XGraphicsPdfPageOptions.Append);

        // ⚠️ O pedido traz o retângulo em coordenada de PDF — origem no PÉ da página, a
        // mesma régua do /Rect (a lição de 20/08/2026). O XGraphics desenha com a origem
        // no TOPO. A conversão mora aqui, num lugar só.
        var caixa = new XRect(
            pedido.Area.X,
            pagina.Height.Point - pedido.Area.Y - pedido.Area.Altura,
            pedido.Area.Largura,
            pedido.Area.Altura);

        CarimboDeAssinatura.Desenhar(gfx, caixa, certificado, pedido);
    }

    /// <summary>
    /// O bloco visível da assinatura, desenhado NA PÁGINA.
    ///
    /// Ele NÃO imita um carimbo de tinta, e isso é decisão: um retângulo que parece um
    /// carimbo faz o leitor conferir com o olho e parar por aí. O que está escrito aqui é
    /// o que só a assinatura digital oferece — o titular do certificado, o CPF que veio
    /// DE DENTRO dele e o emissor —, porque é isso que se confere no leitor de PDF.
    /// </summary>
    private static class CarimboDeAssinatura
    {
        /// <summary>
        /// Desenha o bloco dentro de <paramref name="caixa"/>, nas coordenadas do próprio
        /// <paramref name="gfx"/> — quem converte de PDF para tela é o chamador.
        /// </summary>
        public static void Desenhar(
            XGraphics gfx, XRect caixa, CertificadoAssinatura certificado, PedidoAssinatura pedido)
        {
            var titulo = new XFont("SegoeWP", 7.5, XFontStyleEx.Bold);
            var corpo = new XFont("SegoeWP", 6.5, XFontStyleEx.Regular);
            var tinta = new XSolidBrush(XColor.FromArgb(30, 41, 59));
            var suave = new XSolidBrush(XColor.FromArgb(100, 116, 139));

            // A moldura recua meia espessura da caneta para dentro, que é como a revisão
            // incremental desenha a da 2ª assinatura: traço centrado na borda deixaria um
            // bloco 0,6 ponto maior que o outro, e lado a lado a diferença se vê.
            const double caneta = 0.6;
            gfx.DrawRectangle(
                new XPen(XColor.FromArgb(148, 163, 184), caneta),
                new XRect(caixa.X + caneta / 2, caixa.Y + caneta / 2,
                    caixa.Width - caneta, caixa.Height - caneta));

            var margem = 4.0;
            var largura = caixa.Width - margem * 2;
            var linha = caixa.Y + margem;

            void Escrever(string texto, XFont fonte, XBrush pincel, double altura)
            {
                gfx.DrawString(Caber(gfx, texto, fonte, largura), fonte, pincel,
                    new XRect(caixa.X + margem, linha, largura, altura),
                    XStringFormats.TopLeft);
                linha += altura;
            }

            Escrever("ASSINADO DIGITALMENTE — ICP-Brasil", titulo, tinta, 9);
            Escrever(pedido.NomeExibido, corpo, tinta, 8);

            var identificacao = certificado.Cpf is null
                ? pedido.RegistroConselho ?? string.Empty
                : string.Join(" · ", new[] { pedido.RegistroConselho, "CPF " + Domain.Cpf.Formatar(certificado.Cpf) }
                    .Where(p => !string.IsNullOrWhiteSpace(p)));

            if (identificacao.Length > 0) Escrever(identificacao, corpo, suave, 8);

            Escrever($"{DateTime.Now:dd/MM/yyyy HH:mm} · emissor: {certificado.Emissor}", corpo, suave, 8);
        }

        /// <summary>
        /// Corta o texto com reticências até caber na largura útil do bloco.
        ///
        /// ⚠️ <c>DrawString</c> com <c>TopLeft</c> num <c>XRect</c> NÃO quebra linha nem
        /// corta: o que não cabe sai por cima do que estiver ao lado. Enquanto o carimbo
        /// era invisível isso não aparecia; agora ele é conteúdo da página, e as quatro
        /// linhas levam dado de tamanho imprevisível — o nome do profissional e o EMISSOR
        /// do certificado, que numa cadeia ICP-Brasil chega a "Autoridade Certificadora do
        /// SERPRO Final v5".
        ///
        /// Medido no pior caso realista: sobravam 28 pontos na prescrição e <b>8 na
        /// receita</b>, onde o bloco tem 220 e o QR fica logo ao lado. Oito pontos são dois
        /// caracteres — e o que estouraria é a folha que vai à farmácia.
        /// </summary>
        private static string Caber(XGraphics gfx, string texto, XFont fonte, double largura)
        {
            if (gfx.MeasureString(texto, fonte).Width <= largura) return texto;

            var corte = texto;
            while (corte.Length > 1
                   && gfx.MeasureString(corte + "…", fonte).Width > largura)
                corte = corte[..^1];

            return corte.TrimEnd() + "…";
        }
    }
}
