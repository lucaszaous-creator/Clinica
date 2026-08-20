using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf.Signatures;

namespace Clinica.Application.Assinatura;

/// <summary>
/// A SEGUNDA assinatura no MESMO PDF, por atualização incremental.
///
/// O problema, e por que a resposta antiga estava errada pela metade
/// -----------------------------------------------------------------
/// O comentário do <see cref="AssinaturaDigitalService"/> dizia, desde a parcela 42, que
/// "duas assinaturas no mesmo PDF não existem". Medido (16/08/2026): o PDFsharp de fato
/// REESCREVE o arquivo ao salvar — assinar por cima do já assinado devolve um arquivo cujo
/// prefixo mudou, e a assinatura de quem assinou primeiro deixa de fechar. Até aí a
/// premissa estava certa.
///
/// O que estava errado era a CONCLUSÃO. A limitação é da biblioteca, não do formato: o PDF
/// prevê múltiplas assinaturas exatamente para este caso, e o mecanismo é a <b>atualização
/// incremental</b> — os bytes já assinados não se tocam, e a revisão nova é ANEXADA ao fim
/// do arquivo. A assinatura da médica cobre os bytes 0..N; a da enfermeira cobre 0..M, com
/// M &gt; N. As duas continuam fechando, porque nenhuma delas teve um byte alterado.
///
/// É o que a clínica pediu e é o fluxo real: o médico prescreve e assina, a folha vai para
/// a sala, a enfermagem executa e assina A MESMA prescrição — que é o que a legalidade e o
/// fluxo de trabalho pedem.
///
/// O que este arquivo NÃO faz, de propósito
/// ----------------------------------------
/// Ele não calcula assinatura nenhuma. Quem produz o PKCS#7 continua sendo o
/// <see cref="IDigitalSigner"/> — o mesmo do token e o mesmo do SafeID, sem uma linha de
/// diferença. É o que garante que a segunda assinatura tenha exatamente as propriedades da
/// primeira (inclusive a normalização para DER e a recusa do hash vazio), e é o que permite
/// mexer aqui sem encostar no motor congelado.
///
/// A forma do arquivo de entrada é CONFERIDA, não adivinhada
/// ---------------------------------------------------------
/// Um meio-parser de PDF que "dá um jeito" quando não reconhece a estrutura produziria um
/// arquivo que a nossa conferência aprova e o Adobe recusa — a garantia aparente que este
/// projeto recusa desde a parcela 3. Aqui, o que não bate com o esperado <b>recusa e diz o
/// quê</b>: a via em papel continua valendo, e ninguém fica com um PDF que parece assinado.
/// </summary>
public static class RevisaoIncrementalPdf
{
    /// <summary>
    /// Anexa uma revisão com um campo de assinatura novo e devolve os bytes do PDF com as
    /// DUAS assinaturas.
    /// </summary>
    /// <param name="pdfAssinado">O PDF que já tem pelo menos uma assinatura.</param>
    /// <param name="assinador">
    /// Quem faz a conta: <c>PdfSharpDefaultSigner</c> (token/arquivo) ou
    /// <c>AssinadorSafeID</c> (nuvem). O mesmo dos dois caminhos de sempre.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// O arquivo não tem a forma que esta função sabe estender com segurança.
    /// </exception>
    public static async Task<byte[]> AnexarAssinaturaAsync(
        byte[] pdfAssinado,
        PedidoAssinatura pedido,
        CertificadoAssinatura certificado,
        IDigitalSigner assinador,
        string nomeCampo)
    {
        ArgumentNullException.ThrowIfNull(pdfAssinado);
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(certificado);
        ArgumentNullException.ThrowIfNull(assinador);

        var doc = EstruturaPdf.Ler(pdfAssinado);

        var reserva = await assinador.GetSignatureSizeAsync();
        if (reserva <= 0) reserva = 32 * 1024;

        // ---- Números dos objetos novos ----
        var nSig = doc.ProximoObjeto;
        var nWidget = nSig + 1;
        var nAparencia = nSig + 2;
        var nFonte = nSig + 3;
        var nFonteNegrito = nSig + 4;

        var pagina = doc.PaginaDe(pedido.Area.Pagina);

        // ---- Os objetos MODIFICADOS mantêm o número e são reescritos inteiros ----
        var catalogoNovo = doc.CatalogoComCampo(nWidget);
        var paginaNova = doc.PaginaComAnotacao(pagina, nWidget);

        var corpo = new StringBuilder();
        var offsets = new List<(int Numero, int Offset)>();
        var baseOffset = pdfAssinado.Length;

        void Escrever(int numero, string conteudo)
        {
            offsets.Add((numero, baseOffset + corpo.Length));
            corpo.Append(numero).Append(" 0 obj\n").Append(conteudo).Append("\nendobj\n");
        }

        // A revisão começa numa linha nova para não colar no "%%EOF" anterior.
        corpo.Append('\n');

        // ⚠️ O /ByteRange é escrito com ESPAÇO de sobra e preenchido depois. Ele declara
        // offsets do arquivo final, e o arquivo final só existe depois de ele estar escrito
        // — a saída é reservar largura fixa e remendar os números NO LUGAR, sem mover um
        // byte. É o que o próprio PDFsharp faz.
        var marcaByteRange = new string(' ', LarguraByteRange);
        var vazioContents = new string('0', reserva * 2);

        var sig = new StringBuilder()
            .Append("<</Type/Sig/Filter/Adobe.PPKLite/SubFilter/adbe.pkcs7.detached")
            .Append("/M(").Append(DataPdf(DateTime.Now)).Append(')')
            .Append("/Name(").Append(TextoPdf(pedido.NomeExibido)).Append(')')
            .Append("/Reason(").Append(TextoPdf(pedido.Motivo)).Append(')')
            .Append("/Location(").Append(TextoPdf(pedido.Local ?? string.Empty)).Append(')')
            .Append("/ContactInfo(").Append(TextoPdf(pedido.Contato ?? string.Empty)).Append(')')
            .Append("/Contents<").Append(vazioContents).Append('>')
            .Append("/ByteRange").Append(marcaByteRange)
            .Append(">>")
            .ToString();

        Escrever(nSig, sig);

        var r = pedido.Area;
        Escrever(nWidget,
            $"<</Type/Annot/Subtype/Widget/FT/Sig/T({TextoPdf(nomeCampo)})/Ff 132/F 4"
            + $"/V {nSig} 0 R/P {pagina.Numero} 0 R"
            + $"/Rect[{Num(r.X)} {Num(r.Y)} {Num(r.X + r.Largura)} {Num(r.Y + r.Altura)}]"
            + $"/AP<</N {nAparencia} 0 R>>>>");

        var desenho = Carimbo(certificado, pedido, r.Largura, r.Altura);
        var bytesDesenho = Encoding.Latin1.GetBytes(desenho);
        Escrever(nAparencia,
            $"<</Type/XObject/Subtype/Form/FormType 1"
            + $"/BBox[0 0 {Num(r.Largura)} {Num(r.Altura)}]"
            + $"/Resources<</ProcSet[/PDF/Text]/Font<</Helv {nFonte} 0 R/HelvB {nFonteNegrito} 0 R>>>>"
            + $"/Length {bytesDesenho.Length}>>\nstream\n{desenho}\nendstream");

        // Helvetica é uma das 14 fontes-padrão do PDF: nenhum arquivo embutido, nenhuma
        // dependência do que existe na máquina, e todo leitor a desenha. Reaproveitar a
        // fonte embutida da primeira revisão funcionaria e amarraria esta função ao que a
        // outra ferramenta produziu.
        Escrever(nFonte,
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica/Encoding/WinAnsiEncoding>>");

        // A face NEGRITO do título. Os dois carimbos ficam lado a lado na folha: se um
        // tivesse hierarquia e o outro não, a diferença se leria como se um deles viesse
        // de outro sistema.
        Escrever(nFonteNegrito,
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica-Bold/Encoding/WinAnsiEncoding>>");

        Escrever(doc.CatalogoNumero, catalogoNovo);
        Escrever(pagina.Numero, paginaNova);

        // ---- xref da revisão ----
        var offsetXref = baseOffset + corpo.Length;
        var xref = MontarXref(offsets, doc);

        var total = Encoding.Latin1.GetBytes(corpo.ToString() + xref
            + $"startxref\n{offsetXref}\n%%EOF\n");

        var arquivo = new byte[pdfAssinado.Length + total.Length];
        Buffer.BlockCopy(pdfAssinado, 0, arquivo, 0, pdfAssinado.Length);
        Buffer.BlockCopy(total, 0, arquivo, pdfAssinado.Length, total.Length);

        return await SelarAsync(arquivo, assinador, reserva);
    }

    // ---------------------------------------------------------------------------------
    // O selo: acha o buraco do /Contents, escreve o /ByteRange, assina e preenche.
    // ---------------------------------------------------------------------------------

    /// <summary>Largura reservada para os quatro números do /ByteRange, em caracteres.</summary>
    private const int LarguraByteRange = 48;

    private static async Task<byte[]> SelarAsync(byte[] arquivo, IDigitalSigner assinador, int reserva)
    {
        var texto = Encoding.Latin1.GetString(arquivo);

        // O ÚLTIMO /Contents do arquivo é o desta revisão — as anteriores já estão seladas.
        var abre = texto.LastIndexOf("/Contents<", StringComparison.Ordinal);
        if (abre < 0)
            throw new InvalidOperationException(
                "A revisão nova saiu sem o espaço do /Contents. A folha NÃO foi assinada.");

        var inicioHex = abre + "/Contents<".Length;
        var fechaHex = texto.IndexOf('>', inicioHex);
        if (fechaHex < 0 || fechaHex - inicioHex != reserva * 2)
            throw new InvalidOperationException(
                "O espaço do /Contents da revisão nova não tem o tamanho reservado. "
                + "A folha NÃO foi assinada.");

        var marca = texto.IndexOf("/ByteRange", fechaHex, StringComparison.Ordinal);
        if (marca < 0)
            throw new InvalidOperationException(
                "A revisão nova saiu sem o /ByteRange. A folha NÃO foi assinada.");

        // O buraco vai do '<' ao '>', os dois INCLUSIVE: é o que fica fora da assinatura.
        var tamanho1 = inicioHex - 1;
        var inicio2 = fechaHex + 1;
        var tamanho2 = arquivo.Length - inicio2;

        var faixa = $"[0 {tamanho1} {inicio2} {tamanho2}]";
        if (faixa.Length > LarguraByteRange)
            throw new InvalidOperationException(
                "O /ByteRange não coube no espaço reservado. A folha NÃO foi assinada.");

        var faixaBytes = Encoding.Latin1.GetBytes(faixa.PadRight(LarguraByteRange));
        Buffer.BlockCopy(faixaBytes, 0,
            arquivo, marca + "/ByteRange".Length, faixaBytes.Length);

        // Os dois trechos cobertos, na ordem — é exatamente o que a conferência vai refazer.
        var cobertos = new byte[tamanho1 + tamanho2];
        Buffer.BlockCopy(arquivo, 0, cobertos, 0, tamanho1);
        Buffer.BlockCopy(arquivo, inicio2, cobertos, tamanho1, tamanho2);

        using var fluxo = new MemoryStream(cobertos, writable: false);
        var pkcs7 = await assinador.GetSignatureAsync(fluxo);

        if (pkcs7 is null || pkcs7.Length == 0)
            throw new InvalidOperationException(
                "O assinador devolveu uma assinatura vazia. A folha NÃO foi assinada.");

        if (pkcs7.Length > reserva)
            throw new InvalidOperationException(
                $"A assinatura tem {pkcs7.Length} bytes e o espaço reservado é {reserva}. "
                + "A folha NÃO foi assinada.");

        var hexa = Convert.ToHexString(pkcs7);
        var preenchido = Encoding.Latin1.GetBytes(hexa.PadRight(reserva * 2, '0'));
        Buffer.BlockCopy(preenchido, 0, arquivo, inicioHex, preenchido.Length);

        return arquivo;
    }

    // ---------------------------------------------------------------------------------
    // xref
    // ---------------------------------------------------------------------------------

    private static string MontarXref(
        List<(int Numero, int Offset)> objetos, EstruturaPdf doc)
    {
        // ⚠️ O /Size sai dos objetos REALMENTE escritos, nunca de uma conta à mão.
        // Ele era "nSig + 4", que estava certo enquanto o último objeto novo fosse a
        // fonte; ao entrar a face negrito, a tabela passou a declarar um objeto que o
        // trailer dizia não existir — e o arquivo ficou ILEGÍVEL para um leitor estrito,
        // com a nossa conferência e os testes verdes. Foi o pyhanko que recusou.
        var tamanhoNovo = objetos.Max(o => o.Numero) + 1;

        var sb = new StringBuilder("xref\n");

        // As entradas vão em FAIXAS de números consecutivos, como manda a especificação.
        foreach (var faixa in Agrupar(objetos.OrderBy(o => o.Numero).ToList()))
        {
            sb.Append(faixa[0].Numero).Append(' ').Append(faixa.Count).Append('\n');
            foreach (var (_, offset) in faixa)
                sb.Append(offset.ToString("D10", CultureInfo.InvariantCulture))
                  .Append(" 00000 n \n");
        }

        sb.Append("trailer\n<</Size ").Append(Math.Max(tamanhoNovo, doc.Tamanho))
          .Append("/Root ").Append(doc.CatalogoNumero).Append(" 0 R");

        if (doc.InfoNumero is { } info) sb.Append("/Info ").Append(info).Append(" 0 R");
        if (doc.Id is { } id) sb.Append("/ID").Append(id);

        // /Prev é o que encadeia esta revisão à anterior: sem ele o leitor não enxerga um
        // único objeto do arquivo original.
        sb.Append("/Prev ").Append(doc.OffsetXrefAnterior).Append(">>\n");

        return sb.ToString();
    }

    private static List<List<(int Numero, int Offset)>> Agrupar(
        List<(int Numero, int Offset)> ordenados)
    {
        var faixas = new List<List<(int, int)>>();
        foreach (var item in ordenados)
        {
            if (faixas.Count > 0 && faixas[^1][^1].Item1 + 1 == item.Numero)
                faixas[^1].Add(item);
            else
                faixas.Add([item]);
        }
        return faixas;
    }

    // ---------------------------------------------------------------------------------
    // Aparência
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// O carimbo visível, no mesmo espírito do <c>CarimboDeAssinatura</c>: ele NÃO imita
    /// tinta. O que está escrito é o que só a assinatura digital oferece — titular, o CPF
    /// que veio de DENTRO do certificado, e o emissor.
    /// </summary>
    private static string Carimbo(
        CertificadoAssinatura certificado, PedidoAssinatura pedido, double largura, double altura)
    {
        var identificacao = certificado.Cpf is null
            ? pedido.RegistroConselho ?? string.Empty
            : string.Join(" · ", new[]
                {
                    pedido.RegistroConselho,
                    "CPF " + Domain.Cpf.Formatar(certificado.Cpf)
                }.Where(p => !string.IsNullOrWhiteSpace(p)));

        // ⚠️ Estas quatro linhas são as MESMAS do carimbo da 1ª assinatura, na mesma ordem,
        // com o mesmo tamanho e a mesma cor — os dois ficam LADO A LADO na folha, e
        // qualquer diferença aqui se lê como se um deles fosse de outro sistema.
        const string escuro = "0.12 0.16 0.22 rg";
        const string claro = "0.39 0.45 0.55 rg";

        var linhas = new (string Texto, double Tamanho, string Tinta, bool Negrito)[]
        {
            ("ASSINADO DIGITALMENTE — ICP-Brasil", 7.5, escuro, true),
            (pedido.NomeExibido, 6.5, escuro, false),
            (identificacao, 6.5, claro, false),
            ($"{DateTime.Now:dd/MM/yyyy HH:mm} · emissor: {certificado.Emissor}", 6.5, claro, false)
        };

        var sb = new StringBuilder()
            .Append("q\n0.58 0.64 0.72 RG\n0.6 w\n")
            .Append($"0.3 0.3 {Num(largura - 0.6)} {Num(altura - 0.6)} re\nS\n");

        var y = altura - 4;
        foreach (var (texto, tamanho, tinta, negrito) in linhas)
        {
            if (string.IsNullOrWhiteSpace(texto)) continue;
            y -= tamanho + 1.5;
            if (y < 1) break;

            sb.Append("BT\n").Append(tinta).Append('\n')
              .Append($"/{(negrito ? "HelvB" : "Helv")} {Num(tamanho)} Tf\n")
              .Append($"4 {Num(y)} Td\n")
              .Append('(')
              .Append(TextoPdf(Caber(texto, tamanho, largura - 8, negrito)))
              .Append(") Tj\nET\n");
        }

        return sb.Append('Q').ToString();
    }

    // ---------------------------------------------------------------------------------
    // Utilidades de escrita
    // ---------------------------------------------------------------------------------

    private static string Num(double valor)
        => Math.Round(valor, 2).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Escapa o que quebraria uma string literal de PDF.
    ///
    /// ⚠️ A fonte do carimbo é Helvetica com <b>WinAnsiEncoding</b>, e o arquivo é escrito
    /// byte a byte em Latin-1: as duas tabelas só divergem na faixa 0x80–0x9F, onde a
    /// WinAnsi guarda a tipografia. Sem esta tradução, o travessão do título vira '?' — e a
    /// diferença aparece justamente ao lado do carimbo da 1ª assinatura, que sai certo.
    /// Acentuação não precisa de nada: "é" é 0xE9 nas duas.
    /// </summary>
    private static string TextoPdf(string? texto)
        => ParaWinAnsi(texto ?? string.Empty)
            .Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")
            .Replace("\r", " ").Replace("\n", " ");

    /// <summary>
    /// Traduz a tipografia para a faixa <b>0x80–0x9F</b> da WinAnsiEncoding.
    ///
    /// A fonte do carimbo é Helvetica com WinAnsiEncoding, e o arquivo é escrito byte a
    /// byte em Latin-1: as duas tabelas só divergem nessa faixa, que é onde a WinAnsi
    /// guarda travessão, aspas curvas e reticências. Sem isto, o travessão do título vira
    /// '?' — e a diferença aparece justamente ao lado do carimbo da 1ª assinatura, que sai
    /// certo. Acentuação não precisa de nada: "é" é 0xE9 nas duas.
    /// </summary>
    private static string ParaWinAnsi(string texto) => texto
        .Replace('\u2014', '\u0097').Replace('\u2013', '\u0096')
        .Replace('\u201c', '\u0093').Replace('\u201d', '\u0094')
        .Replace('\u2018', '\u0091').Replace('\u2019', '\u0092')
        .Replace('\u2026', '\u0085');

    /// <summary>
    /// Largura de cada caractere da <b>Helvetica</b> em milésimos de em, do código WinAnsi
    /// 32 ao 255 — as métricas das 14 fontes-padrão do PDF, que são fixas e públicas.
    ///
    /// ⚠️ A tabela foi <b>medida</b>, não lembrada: ela sai das métricas base-14 e foi
    /// conferida contra a renderização real (a linha do emissor mais longo dá 215,32
    /// pontos a 6,5 — o mesmo número que o poppler devolve para o arquivo gerado).
    /// Aqui não dá para chamar o <c>MeasureString</c> do PDFsharp como no carimbo do
    /// prescritor: a aparência é escrita à mão, e o resolvedor de fontes do projeto só
    /// conhece a Segoe WP, que é ~9% mais estreita — medir com ela deixaria o texto
    /// estourar justamente no caso apertado.
    /// </summary>
    private static readonly int[] LarguraHelvetica =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584, 761,
        556, 761, 222, 556, 333, 1000, 556, 556, 333, 1000, 667, 333, 1000, 761, 611, 761,
        761, 222, 222, 333, 333, 350, 556, 1000, 333, 1000, 500, 333, 944, 761, 500, 667,
        278, 333, 556, 556, 556, 556, 260, 556, 333, 737, 370, 556, 584, 333, 737, 333,
        400, 584, 333, 333, 333, 556, 537, 278, 333, 333, 365, 556, 834, 834, 834, 611,
        667, 667, 667, 667, 667, 667, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
        722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
        556, 556, 556, 556, 556, 556, 889, 500, 556, 556, 556, 556, 278, 278, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 584, 611, 556, 556, 556, 556, 500, 556, 500
    ];

    /// <summary>
    /// A mesma régua para a face <b>Helvetica-Bold</b>, do título — ela é mais larga que a
    /// regular, e medir o título com a tabela errada deixaria justamente a linha de cima
    /// estourar a caixa.
    /// </summary>
    private static readonly int[] LarguraHelveticaNegrito =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584, 761,
        556, 761, 278, 556, 500, 1000, 556, 556, 333, 1000, 667, 333, 1000, 761, 611, 761,
        761, 278, 278, 500, 500, 350, 556, 1000, 333, 1000, 556, 333, 944, 761, 500, 667,
        278, 333, 556, 556, 556, 556, 280, 556, 333, 737, 370, 556, 584, 333, 737, 333,
        400, 584, 333, 333, 333, 611, 556, 278, 333, 333, 365, 556, 834, 834, 834, 611,
        722, 722, 722, 722, 722, 722, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
        722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
        556, 556, 556, 556, 556, 556, 889, 556, 556, 556, 556, 556, 278, 278, 278, 278,
        611, 611, 611, 611, 611, 611, 611, 584, 611, 611, 611, 611, 611, 556, 611, 556
    ];

    /// <summary>
    /// Quanto o texto ocupa, em pontos, escrito em Helvetica no tamanho pedido.
    /// Exposto porque é o que um teste consegue conferir contra a régua de fora.
    /// </summary>
    public static double LarguraNaHelvetica(string texto, double tamanho, bool negrito = false)
    {
        var tabela = negrito ? LarguraHelveticaNegrito : LarguraHelvetica;

        var soma = 0;
        foreach (var c in ParaWinAnsi(texto ?? string.Empty))
        {
            var i = c - 32;
            soma += i >= 0 && i < tabela.Length ? tabela[i] : 556;
        }

        return soma * tamanho / 1000.0;
    }

    /// <summary>
    /// Corta o texto com reticências até caber na largura útil do bloco — a mesma regra do
    /// carimbo do prescritor, e pela mesma razão: o operador <c>Tj</c> do PDF não quebra
    /// linha nem corta, e os dois carimbos ficam LADO A LADO com 10 pontos entre eles.
    /// Um emissor longo escreveria por cima do vizinho.
    /// </summary>
    private static string Caber(string texto, double tamanho, double largura, bool negrito)
    {
        if (LarguraNaHelvetica(texto, tamanho, negrito) <= largura) return texto;

        var corte = texto;
        while (corte.Length > 1
               && LarguraNaHelvetica(corte + "…", tamanho, negrito) > largura)
            corte = corte[..^1];

        return corte.TrimEnd() + "…";
    }

    private static string DataPdf(DateTime quando)
        => quando.ToString("'D:'yyyyMMddHHmmss", CultureInfo.InvariantCulture)
           + quando.ToString("zzz", CultureInfo.InvariantCulture)
               .Replace(":", "'", StringComparison.Ordinal) + "'";

    // ---------------------------------------------------------------------------------
    // Leitura do que já existe
    // ---------------------------------------------------------------------------------

    /// <summary>Uma página, com o número do objeto e o texto do dicionário dela.</summary>
    internal sealed record PaginaPdf(int Numero, string Dicionario);

    /// <summary>
    /// O mínimo do PDF de entrada que a revisão precisa conhecer — e nada além.
    ///
    /// Deliberadamente NÃO é um parser de PDF: é um leitor que reconhece a forma que este
    /// sistema produz (QuestPDF → PDFsharp, xref clássico, sem object stream) e RECUSA o
    /// resto em vez de improvisar. Assinar um arquivo cuja estrutura não se entendeu é como
    /// se produz o PDF que a nossa conferência aprova e o Adobe recusa.
    /// </summary>
    internal sealed class EstruturaPdf
    {
        public required string Texto { get; init; }
        public required int OffsetXrefAnterior { get; init; }
        public required int Tamanho { get; init; }
        public required int CatalogoNumero { get; init; }
        public int? InfoNumero { get; init; }
        public string? Id { get; init; }
        public required Dictionary<int, int> Offsets { get; init; }

        public int ProximoObjeto => Math.Max(Tamanho, Offsets.Count == 0 ? 1 : Offsets.Keys.Max() + 1);

        public static EstruturaPdf Ler(byte[] pdf)
        {
            var texto = Encoding.Latin1.GetString(pdf);

            var inicio = Regex.Matches(texto, @"startxref\s+(\d+)").LastOrDefault()
                ?? throw new InvalidOperationException(
                    "O PDF não tem 'startxref' — não é um arquivo que dê para estender com "
                    + "uma assinatura nova.");

            var offsetXref = int.Parse(inicio.Groups[1].Value, CultureInfo.InvariantCulture);

            var offsets = new Dictionary<int, int>();
            int? raiz = null, info = null, tamanho = null;
            string? id = null;

            // Percorre a CADEIA de revisões (/Prev). Um arquivo já assinado duas vezes tem
            // mais de uma, e ignorar as anteriores perderia os objetos que só existem lá.
            var visitados = new HashSet<int>();
            var atual = offsetXref;
            while (atual > 0 && visitados.Add(atual))
            {
                if (atual >= texto.Length)
                    throw new InvalidOperationException(
                        "O 'startxref' aponta para fora do arquivo.");

                if (!texto.AsSpan(atual).TrimStart().StartsWith("xref"))
                    throw new InvalidOperationException(
                        "Este PDF usa tabela de referência cruzada em FLUXO (xref stream), e "
                        + "esta função só sabe estender a tabela clássica com segurança. "
                        + "A folha NÃO foi assinada.");

                var (lidos, trailer) = LerSecaoXref(texto, atual);
                foreach (var (numero, offset) in lidos) offsets.TryAdd(numero, offset);

                raiz ??= NumeroDaReferencia(trailer, "Root");
                info ??= NumeroDaReferencia(trailer, "Info");
                id ??= Regex.Match(trailer, @"/ID\s*(\[[^\]]*\])").Groups[1].Value is { Length: > 0 } v
                    ? v : null;
                tamanho ??= Regex.Match(trailer, @"/Size\s+(\d+)").Groups[1].Value is { Length: > 0 } s
                    ? int.Parse(s, CultureInfo.InvariantCulture) : null;

                var prev = Regex.Match(trailer, @"/Prev\s+(\d+)");
                atual = prev.Success ? int.Parse(prev.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            }

            if (raiz is null)
                throw new InvalidOperationException(
                    "O PDF não declara /Root no trailer. A folha NÃO foi assinada.");

            return new EstruturaPdf
            {
                Texto = texto,
                OffsetXrefAnterior = offsetXref,
                Tamanho = tamanho ?? (offsets.Count + 1),
                CatalogoNumero = raiz.Value,
                InfoNumero = info,
                Id = id,
                Offsets = offsets
            };
        }

        private static (List<(int, int)>, string) LerSecaoXref(string texto, int offset)
        {
            var entradas = new List<(int, int)>();
            var i = texto.IndexOf("xref", offset, StringComparison.Ordinal) + 4;

            while (true)
            {
                var cabecalho = Regex.Match(texto[i..Math.Min(i + 64, texto.Length)],
                    @"^\s*(\d+)\s+(\d+)\s*[\r\n]+");
                if (!cabecalho.Success) break;

                var primeiro = int.Parse(cabecalho.Groups[1].Value, CultureInfo.InvariantCulture);
                var quantos = int.Parse(cabecalho.Groups[2].Value, CultureInfo.InvariantCulture);
                i += cabecalho.Length;

                for (var k = 0; k < quantos; k++, i += 20)
                {
                    if (i + 18 > texto.Length) break;
                    var linha = texto.Substring(i, 18);
                    if (linha[17] == 'n' || linha.EndsWith('n'))
                        entradas.Add((primeiro + k,
                            int.Parse(linha[..10], CultureInfo.InvariantCulture)));
                }
            }

            var t = texto.IndexOf("trailer", i, StringComparison.Ordinal);
            if (t < 0)
                throw new InvalidOperationException(
                    "A seção xref não tem 'trailer'. A folha NÃO foi assinada.");

            return (entradas, RecortarDicionario(texto, texto.IndexOf("<<", t, StringComparison.Ordinal)));
        }

        private static int? NumeroDaReferencia(string dicionario, string chave)
        {
            var m = Regex.Match(dicionario, $@"/{chave}\s+(\d+)\s+\d+\s+R");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
        }

        /// <summary>O texto de um objeto, do "N 0 obj" até o "endobj".</summary>
        public string ObjetoTexto(int numero)
        {
            if (!Offsets.TryGetValue(numero, out var offset) || offset >= Texto.Length)
                throw new InvalidOperationException(
                    $"O objeto {numero} não foi encontrado no PDF. A folha NÃO foi assinada.");

            var abre = Texto.IndexOf("<<", offset, StringComparison.Ordinal);
            if (abre < 0)
                throw new InvalidOperationException(
                    $"O objeto {numero} não é um dicionário. A folha NÃO foi assinada.");

            return RecortarDicionario(Texto, abre);
        }

        /// <summary>
        /// O catálogo com o campo novo dentro do <c>/AcroForm</c>.
        ///
        /// O <c>/SigFlags 3</c> é o que diz ao leitor que o documento tem assinatura e não
        /// pode ser salvo de forma que a invalide. Ele já vem da primeira assinatura; o que
        /// se acrescenta é a referência do campo em <c>/Fields</c>.
        /// </summary>
        public string CatalogoComCampo(int campo)
        {
            var catalogo = ObjetoTexto(CatalogoNumero);

            var acro = Regex.Match(catalogo, @"/AcroForm\s*<<");
            if (!acro.Success)
                throw new InvalidOperationException(
                    "O PDF não tem /AcroForm no catálogo — ele não está assinado, e esta "
                    + "função só acrescenta uma assinatura a quem já tem a primeira.");

            var dicAcro = RecortarDicionario(catalogo, acro.Index + acro.Length - 2);
            var comCampo = AcrescentarNaArray(dicAcro, "Fields", campo);

            return catalogo[..acro.Index]
                   + "/AcroForm" + comCampo
                   + catalogo[(acro.Index + acro.Length - 2 + dicAcro.Length)..];
        }

        public PaginaPdf PaginaDe(int indice)
        {
            var paginas = Descendentes(NumeroDaReferencia(ObjetoTexto(CatalogoNumero), "Pages")
                ?? throw new InvalidOperationException(
                    "O catálogo não aponta para /Pages. A folha NÃO foi assinada."));

            if (paginas.Count == 0)
                throw new InvalidOperationException(
                    "O PDF não tem páginas legíveis. A folha NÃO foi assinada.");

            // A página pedida pode não existir se a folha encolheu — cair na última é melhor
            // que estourar na hora de assinar (a mesma escolha do AssinaturaDigitalService).
            var numero = paginas[Math.Clamp(indice, 0, paginas.Count - 1)];
            return new PaginaPdf(numero, ObjetoTexto(numero));
        }

        /// <summary>
        /// ⚠️ O espaço entre <c>/Type</c> e o nome é OPCIONAL no PDF, e os dois produtores
        /// deste projeto discordam: o QuestPDF escreve <c>/Type /Page</c> e o PDFsharp
        /// escreve <c>/Type/Page</c>. Casar o texto cru funcionaria por sorte — e a falha
        /// seria "o PDF não tem páginas legíveis" num arquivo cheio delas.
        /// </summary>
        private static readonly Regex EhPagina = new(@"/Type\s*/Page(?![a-zA-Z])", RegexOptions.Compiled);
        private static readonly Regex EhArvoreDePaginas = new(@"/Type\s*/Pages\b", RegexOptions.Compiled);

        private List<int> Descendentes(int no)
        {
            var texto = ObjetoTexto(no);
            if (EhPagina.IsMatch(texto) && !EhArvoreDePaginas.IsMatch(texto))
                return [no];

            var kids = Regex.Match(texto, @"/Kids\s*\[([^\]]*)\]");
            if (!kids.Success) return [];

            var filhos = new List<int>();
            foreach (Match m in Regex.Matches(kids.Groups[1].Value, @"(\d+)\s+\d+\s+R"))
                filhos.AddRange(Descendentes(
                    int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)));

            return filhos;
        }

        public string PaginaComAnotacao(PaginaPdf pagina, int widget)
            => AcrescentarNaArray(pagina.Dicionario, "Annots", widget);

        /// <summary>
        /// Acrescenta <c>N 0 R</c> a uma array do dicionário, criando-a quando não existe.
        ///
        /// ⚠️ A array pode ser uma REFERÊNCIA em vez de estar escrita ali (<c>/Annots 9 0 R</c>).
        /// Reescrever o dicionário nesse caso trocaria a referência por uma array com um
        /// item só e <b>apagaria em silêncio as anotações que já existiam</b> — inclusive o
        /// campo da primeira assinatura. Aqui isso RECUSA.
        /// </summary>
        private static string AcrescentarNaArray(string dicionario, string chave, int objeto)
        {
            var referencia = Regex.Match(dicionario, $@"/{chave}\s+\d+\s+\d+\s+R");
            if (referencia.Success)
                throw new InvalidOperationException(
                    $"Neste PDF a lista /{chave} é uma referência indireta, e estender isso "
                    + "com segurança exigiria reescrever outro objeto. A folha NÃO foi "
                    + "assinada — a via em papel continua valendo.");

            var array = Regex.Match(dicionario, $@"/{chave}\s*\[([^\]]*)\]");
            if (!array.Success)
                return dicionario[..^2] + $"/{chave}[{objeto} 0 R]>>";

            var dentro = array.Groups[1].Value.Trim();
            var novo = dentro.Length == 0 ? $"{objeto} 0 R" : $"{dentro} {objeto} 0 R";

            return dicionario[..array.Index] + $"/{chave}[{novo}]"
                   + dicionario[(array.Index + array.Length)..];
        }
    }

    /// <summary>
    /// Recorta um dicionário PDF a partir do <c>&lt;&lt;</c>, contando os pares aninhados.
    ///
    /// As strings literais e hexadecimais são PULADAS: um <c>&gt;</c> dentro de
    /// <c>(texto&gt;)</c> ou de um <c>/Contents&lt;30820…&gt;</c> fecharia o dicionário cedo
    /// demais e devolveria um pedaço — que é como se produz um PDF quebrado sem erro nenhum.
    /// </summary>
    private static string RecortarDicionario(string texto, int inicio)
    {
        if (inicio < 0 || inicio + 1 >= texto.Length || texto[inicio] != '<' || texto[inicio + 1] != '<')
            throw new InvalidOperationException(
                "Não foi possível ler um dicionário do PDF. A folha NÃO foi assinada.");

        var profundidade = 0;
        for (var i = inicio; i < texto.Length; i++)
        {
            var c = texto[i];

            if (c == '(')
            {
                // String literal: parênteses aninham e a barra invertida escapa.
                var nivel = 1;
                for (i++; i < texto.Length && nivel > 0; i++)
                {
                    if (texto[i] == '\\') { i++; continue; }
                    if (texto[i] == '(') nivel++;
                    else if (texto[i] == ')') nivel--;
                }
                i--;
                continue;
            }

            if (c == '<' && i + 1 < texto.Length && texto[i + 1] == '<') { profundidade++; i++; continue; }
            if (c == '>' && i + 1 < texto.Length && texto[i + 1] == '>')
            {
                profundidade--;
                i++;
                if (profundidade == 0) return texto[inicio..(i + 1)];
                continue;
            }

            // String hexadecimal — vai até o '>' e nunca contém '<<'.
            if (c == '<')
            {
                var fim = texto.IndexOf('>', i + 1);
                if (fim < 0) break;
                i = fim;
            }
        }

        throw new InvalidOperationException(
            "Um dicionário do PDF não fecha. A folha NÃO foi assinada.");
    }
}
