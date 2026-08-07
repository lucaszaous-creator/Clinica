using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf.Annotations;
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
/// Uma armadilha do PDFsharp que virou decisão de projeto
/// ------------------------------------------------------
/// Ele NÃO faz atualização incremental: salvar reescreve o arquivo. Ou seja, <b>duas
/// assinaturas no mesmo PDF não existem</b> — a segunda quebraria a primeira. Isso forçou
/// (bem) o desenho de dois documentos encadeados, um por signatário. Ver o comentário de
/// <c>AssinaturaDocumento</c>.
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

    /// <summary>
    /// Assina o PDF. Devolve bytes NOVOS — os originais não servem mais para nada, porque
    /// é sobre estes que a assinatura foi calculada.
    /// </summary>
    /// <param name="assinadorEmNuvem">
    /// Quando informado, quem faz a conta da assinatura é um PSC (o SafeID), e a chave
    /// privada <b>não está nesta máquina</b> — sobe o hash, desce o PKCS#7. Nulo mantém o
    /// caminho de sempre: o certificado do token ou do arquivo, com a chave em mãos.
    ///
    /// Note que só isto muda. O desenho da folha, o carimbo visual, o posicionamento e o
    /// <see cref="Conferir"/> são os mesmos nos dois caminhos — é o que garante que uma
    /// folha assinada em nuvem seja indistinguível de uma assinada no token, inclusive na
    /// hora de conferir.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Certificado sem chave privada (só no caminho local), vencido, ou com cadeia que não
    /// valida.
    /// </exception>
    public async Task<ResultadoAssinatura> AssinarAsync(
        byte[] pdf, CertificadoAssinatura certificado, PedidoAssinatura pedido,
        IDigitalSigner? assinadorEmNuvem = null)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentNullException.ThrowIfNull(certificado);
        ArgumentNullException.ThrowIfNull(pedido);

        Criticar(certificado, exigirChaveLocal: assinadorEmNuvem is null);
        GarantirFonte();

        using var entrada = new MemoryStream(pdf, writable: false);
        var documento = PdfReader.Open(entrada, PdfDocumentOpenMode.Modify);

        // A página pedida pode não existir se o conteúdo encolheu (uma folha com menos
        // itens). Cair na última é melhor que estourar na hora de assinar.
        var pagina = Math.Clamp(pedido.Area.Pagina, 0, documento.PageCount - 1);

        var opcoes = new DigitalSignatureOptions
        {
            Reason = pedido.Motivo,
            Location = pedido.Local ?? string.Empty,
            ContactInfo = pedido.Contato ?? string.Empty,
            PageIndex = pagina,
            Rectangle = new XRect(
                pedido.Area.X, pedido.Area.Y, pedido.Area.Largura, pedido.Area.Altura),
            AppearanceHandler = new CarimboDeAssinatura(certificado, pedido)
        };

        var assinador = assinadorEmNuvem ?? new PdfSharpDefaultSigner(
            certificado.Certificado, PdfMessageDigestType.SHA256, pedido.CarimbadoraDeTempo);

        DigitalSignatureHandler.ForDocument(documento, assinador, opcoes);

        using var saida = new MemoryStream();

        // SaveAsync, e não Save: é durante o salvamento que o PDFsharp chama o assinador,
        // e no caminho da nuvem essa chamada é uma requisição HTTP. Salvar de forma síncrona
        // bloquearia a thread da tela enquanto a médica confirma no celular.
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

    /// <summary>
    /// Refaz a conta da assinatura sobre os bytes do arquivo e diz se ele continua o
    /// mesmo. É o que a tela de conferência e o rodapé da reimpressão usam.
    /// </summary>
    public ConferenciaAssinatura Conferir(byte[] pdfAssinado)
    {
        if (pdfAssinado is null || pdfAssinado.Length == 0)
            return ConferenciaAssinatura.NaoConferida("arquivo vazio");

        try
        {
            var faixa = LerByteRange(pdfAssinado);
            if (faixa is null)
                return ConferenciaAssinatura.NaoConferida("o arquivo não tem assinatura");

            var (inicio1, tamanho1, inicio2, tamanho2) = faixa.Value;

            // O buraco ENTRE os dois trechos é o /Contents <...> com o PKCS#7 em hexa. É
            // por ficar fora do ByteRange que a assinatura consegue estar dentro do
            // arquivo que ela assina.
            var pkcs7 = LerConteudoAssinatura(pdfAssinado, inicio1 + tamanho1, inicio2);
            if (pkcs7 is null)
                return ConferenciaAssinatura.NaoConferida("o bloco de assinatura está ilegível");

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
    /// <param name="exigirChaveLocal">
    /// Falso quando quem assina é um PSC: em nuvem a chave privada <b>nunca</b> está nesta
    /// máquina — é esse o ponto do produto —, e cobrar <c>HasPrivateKey</c> ali recusaria
    /// justamente o certificado que funciona. As outras duas críticas continuam valendo
    /// iguais nos dois caminhos: vencido é vencido, e cadeia que não valida produz documento
    /// que abre como inválido venha a assinatura de onde vier.
    /// </param>
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
    {
        // Latin1 porque a leitura é sobre BYTES: qualquer decodificação que junte ou
        // descarte bytes (UTF-8 faz as duas coisas com sequência inválida) desalinharia
        // os índices que o /ByteRange declara, e a conferência passaria a acusar
        // adulteração em arquivo íntegro.
        var texto = Encoding.Latin1.GetString(pdf);
        var achado = PadraoByteRange.Match(texto);
        if (!achado.Success) return null;

        return (
            int.Parse(achado.Groups[1].Value),
            int.Parse(achado.Groups[2].Value),
            int.Parse(achado.Groups[3].Value),
            int.Parse(achado.Groups[4].Value));
    }

    private static byte[]? LerConteudoAssinatura(byte[] pdf, int inicio, int fim)
    {
        if (inicio < 0 || fim > pdf.Length || fim <= inicio) return null;

        var bruto = Encoding.Latin1.GetString(pdf, inicio, fim - inicio).Trim();
        var hexa = bruto.TrimStart('<').TrimEnd('>');

        // O /Contents é dimensionado com folga e completado com zeros à direita; eles não
        // fazem parte do PKCS#7 (que é DER e sabe onde termina), mas atrapalham o
        // FromHexString se sobrarem em número ímpar.
        hexa = hexa.TrimEnd('0');
        if (hexa.Length % 2 == 1) hexa += "0";
        if (hexa.Length == 0) return null;

        try { return Convert.FromHexString(hexa); }
        catch (FormatException) { return null; }
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
            var pkcs7 = LerConteudoAssinatura(pdfAssinado, inicio1 + tamanho1, inicio2);
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

    private sealed class FonteDoCarimbo : IFontResolver
    {
        public byte[]? GetFont(string faceName) => PdfSharp.WPFonts.FontDataHelper.SegoeWP;

        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
            => new FontResolverInfo("SegoeWP#");
    }

    /// <summary>
    /// O bloco visível da assinatura.
    ///
    /// Ele NÃO imita um carimbo de tinta, e isso é decisão: um retângulo que parece um
    /// carimbo faz o leitor conferir com o olho e parar por aí. O que está escrito aqui é
    /// o que só a assinatura digital oferece — o titular do certificado, o CPF que veio
    /// DE DENTRO dele e o emissor —, porque é isso que se confere no leitor de PDF.
    /// </summary>
    private sealed class CarimboDeAssinatura : IAnnotationAppearanceHandler
    {
        private readonly CertificadoAssinatura _certificado;
        private readonly PedidoAssinatura _pedido;

        public CarimboDeAssinatura(CertificadoAssinatura certificado, PedidoAssinatura pedido)
        {
            _certificado = certificado;
            _pedido = pedido;
        }

        public void DrawAppearance(XGraphics gfx, XRect area)
        {
            var titulo = new XFont("SegoeWP", 7.5, XFontStyleEx.Bold);
            var corpo = new XFont("SegoeWP", 6.5, XFontStyleEx.Regular);
            var tinta = new XSolidBrush(XColor.FromArgb(30, 41, 59));
            var suave = new XSolidBrush(XColor.FromArgb(100, 116, 139));

            gfx.DrawRectangle(new XPen(XColor.FromArgb(148, 163, 184), 0.6), area);

            var margem = 4.0;
            var largura = area.Width - margem * 2;
            var linha = area.Y + margem;

            void Escrever(string texto, XFont fonte, XBrush pincel, double altura)
            {
                gfx.DrawString(texto, fonte, pincel,
                    new XRect(area.X + margem, linha, largura, altura),
                    XStringFormats.TopLeft);
                linha += altura;
            }

            Escrever("ASSINADO DIGITALMENTE — ICP-Brasil", titulo, tinta, 9);
            Escrever(_pedido.NomeExibido, corpo, tinta, 8);

            var identificacao = _certificado.Cpf is null
                ? _pedido.RegistroConselho ?? string.Empty
                : string.Join(" · ", new[] { _pedido.RegistroConselho, "CPF " + Domain.Cpf.Formatar(_certificado.Cpf) }
                    .Where(p => !string.IsNullOrWhiteSpace(p)));

            if (identificacao.Length > 0) Escrever(identificacao, corpo, suave, 8);

            Escrever($"{DateTime.Now:dd/MM/yyyy HH:mm} · emissor: {_certificado.Emissor}", corpo, suave, 8);
        }
    }
}
