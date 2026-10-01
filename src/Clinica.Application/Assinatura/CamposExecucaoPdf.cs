using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinica.Domain;
using Clinica.Domain.Entities;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace Clinica.Application.Assinatura;

/// <summary>
/// Campos de formulário reservados ANTES da assinatura médica. A execução preenche
/// somente esses campos em revisão incremental; a enfermagem sela essa revisão.
/// Nunca redesenha conteúdo de página já assinado nem sobrescreve o arquivo original.
/// </summary>
public static class CamposExecucaoPdf
{
    public const string Prefixo = "https://campos.invalid/execucao/";
    private static string Numero(double n) => n.ToString("0.###", CultureInfo.InvariantCulture);

    public static byte[] Preparar(byte[] pdf)
    {
        using var entrada = new MemoryStream(pdf);
        using var doc = PdfReader.Open(entrada, PdfDocumentOpenMode.Modify);
        var campos = new PdfArray(doc);
        var fonte = new PdfDictionary(doc);
        fonte.Elements.SetName("/Type", "/Font");
        fonte.Elements.SetName("/Subtype", "/Type1");
        fonte.Elements.SetName("/BaseFont", "/Helvetica");
        fonte.Elements.SetName("/Encoding", "/WinAnsiEncoding");
        doc.Internals.AddObject(fonte);
        foreach (var pagina in doc.Pages)
        {
            for (var i = pagina.Annotations.Count - 1; i >= 0; i--)
            {
                var anotacao = pagina.Annotations[i];
                var uri = anotacao.Elements.GetDictionary("/A")?.Elements.GetString("/URI");
                if (uri is null || !uri.StartsWith(Prefixo, StringComparison.Ordinal)) continue;
                var nome = uri[Prefixo.Length..];
                if (!Regex.IsMatch(nome, @"^(hora|situacao|detalhe)_[0-9]+$"))
                    throw new InvalidOperationException("Campo de execução inválido.");
                var retangulo = anotacao.Elements.GetRectangle("/Rect");
                pagina.Annotations.Remove(anotacao);
                var campo = new PdfDictionary(doc);
                campo.Elements.SetName("/Type", "/Annot");
                campo.Elements.SetName("/Subtype", "/Widget");
                campo.Elements.SetName("/FT", "/Tx");
                campo.Elements.SetString("/T", nome);
                campo.Elements.SetString("/V", "");
                campo.Elements.SetInteger("/F", 4);
                campo.Elements.SetInteger("/Ff", 4096); // multiline, habilitado para a execução
                campo.Elements.SetRectangle("/Rect", retangulo);
                campo.Elements["/P"] = pagina.Reference!;
                campo.Elements.SetString("/DA", "/ExecucaoHelvetica 8 Tf 0 g");
                doc.Internals.AddObject(campo);
                var aparencia = new PdfDictionary(doc);
                aparencia.Elements.SetName("/Type", "/XObject");
                aparencia.Elements.SetName("/Subtype", "/Form");
                aparencia.Elements["/BBox"] = new PdfArray(doc, new PdfReal(0), new PdfReal(0), new PdfReal(retangulo.Width), new PdfReal(retangulo.Height));
                aparencia.CreateStream(Encoding.ASCII.GetBytes("q Q"));
                doc.Internals.AddObject(aparencia);
                var ap = new PdfDictionary(doc);
                ap.Elements["/N"] = aparencia.Reference!;
                campo.Elements["/AP"] = ap;
                campos.Elements.Add(campo.Reference!);
                // A coleção de anotações pode já existir como array direto ou indireto.
                var annots = pagina.Elements.GetArray("/Annots") ?? new PdfArray(doc);
                annots.Elements.Add(campo.Reference!);
                pagina.Elements["/Annots"] = annots;
            }
        }
        if (campos.Elements.Count == 0) throw new InvalidOperationException("A prescrição não reservou campos de execução.");
        var fonts = new PdfDictionary(doc);
        fonts.Elements["/ExecucaoHelvetica"] = fonte.Reference!;
        var recursos = new PdfDictionary(doc);
        recursos.Elements["/Font"] = fonts;
        var formulario = new PdfDictionary(doc);
        formulario.Elements["/Fields"] = campos;
        formulario.Elements["/DR"] = recursos;
        formulario.Elements.SetString("/DA", "/ExecucaoHelvetica 8 Tf 0 g");
        formulario.Elements.SetBoolean("/NeedAppearances", false);
        doc.Internals.Catalog.Elements["/AcroForm"] = formulario;
        using var saida = new MemoryStream();
        doc.Save(saida, false);
        return saida.ToArray();
    }

    public static bool TemCampos(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        using var doc = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        return doc.Internals.Catalog.Elements.GetDictionary("/AcroForm")?.Elements.GetArray("/Fields")?.Elements
            .OfType<PdfReference>().Any(r => r.Value is PdfDictionary d && d.Elements.GetString("/T").StartsWith("detalhe_", StringComparison.Ordinal)) == true;
    }

    public static IReadOnlyDictionary<string, string> Valores(PrescricaoInterna prescricao)
    {
        var valores = new Dictionary<string, string>();
        foreach (var item in prescricao.Itens)
        {
            var c = item.ChecagemVigente;
            var situacao = item.Suspenso ? "Suspenso" : c is null ? "Não indicado (SOS)" : RotulosEnum.De(c.Situacao);
            if (c is null && !item.Suspenso && !item.SeNecessario)
                throw new InvalidOperationException("Confira todos os itens antes de assinar a execução.");
            if (c is not null && c.Situacao != SituacaoChecagem.Realizado && string.IsNullOrWhiteSpace(c.Justificativa))
                throw new InvalidOperationException("Não e Não executável exigem justificativa antes de assinar.");
            valores[$"hora_{item.Id}"] = c is null ? "—" : $"{c.HoraRealizacao:HH:mm}";
            valores[$"situacao_{item.Id}"] = item.Suspenso ? "SUSP" : c is null ? "SOS" : c.Situacao switch
            { SituacaoChecagem.Realizado => "Sim", SituacaoChecagem.NaoExecutavel => "NE", _ => "Não" };
            valores[$"detalhe_{item.Id}"] = c is null ? $"{situacao}. {item.MotivoSuspensao}" :
                $"Situação: {situacao}\nData da execução: {(c.DataRealizacao ?? DateOnly.FromDateTime(c.RegistradoEm)):dd/MM/yyyy} — Horário: {c.HoraRealizacao:HH:mm}\n"
                + $"Executante: {c.ExecutanteNome} — {c.ExecutanteConselho}\n"
                + $"Registrado no sistema: {c.RegistradoEm:dd/MM/yyyy HH:mm}\n"
                + (string.IsNullOrWhiteSpace(c.Justificativa) ? "" : $"Justificativa / intercorrência: {c.Justificativa}\n")
                + (c.RetificaChecagemId is null ? "" : $"Retificação #{c.RetificaChecagemId}: {c.MotivoRetificacao}");
        }
        return valores;
    }

    public static byte[] Preencher(byte[] original, PrescricaoInterna prescricao)
    {
        var valores = Valores(prescricao);
        var estrutura = RevisaoIncrementalPdf.EstruturaPdf.Ler(original);
        using var stream = new MemoryStream(original);
        using var doc = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        var campos = doc.Internals.Catalog.Elements.GetDictionary("/AcroForm")?.Elements.GetArray("/Fields")
            ?? throw new InvalidOperationException("Esta prescrição antiga não tem campos de execução reservados.");
        var corpo = new StringBuilder("\n");
        var offsets = new List<(int Numero, int Offset)>();
        var novo = estrutura.ProximoObjeto;
        var preenchidos = new HashSet<string>();
        void Escrever(int numero, string conteudo)
        {
            offsets.Add((numero, original.Length + corpo.Length));
            corpo.Append(numero).Append(" 0 obj\n").Append(conteudo).Append("\nendobj\n");
        }
        foreach (var referencia in campos.Elements.OfType<PdfReference>())
        {
            if (referencia.Value is not PdfDictionary campo) continue;
            var nome = campo.Elements.GetString("/T");
            if (!valores.TryGetValue(nome, out var valor)) continue;
            if (!preenchidos.Add(nome) || campo.Elements.GetString("/V").Length != 0)
                throw new InvalidOperationException("Campo de execução duplicado ou já preenchido; preserve o documento assinado.");
            var rect = campo.Elements.GetRectangle("/Rect");
            var desenho = Desenhar(valor, rect.Width, rect.Height);
            var nAp = novo++;
            var nFonte = novo++;
            Escrever(nFonte, "<</Type/Font/Subtype/Type1/BaseFont/Helvetica/Encoding/WinAnsiEncoding>>");
            Escrever(nAp, $"<</Type/XObject/Subtype/Form/BBox[0 0 {Numero(rect.Width)} {Numero(rect.Height)}]"
                + $"/Resources<</Font<</ExecucaoHelvetica {nFonte} 0 R>>>>/Length {desenho.Length}>>\nstream\n{desenho}\nendstream");
            // Reescreve somente o widget conhecido; página, retângulo, tipo e nome permanecem.
            var pagina = campo.Elements.GetReference("/P")!.ObjectNumber;
            var utf16 = Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(valor));
            Escrever(referencia.ObjectNumber,
                $"<</Type/Annot/Subtype/Widget/FT/Tx/T({nome})/V<FEFF{utf16}>/F 4/Ff 4097"
                + $"/P {pagina} 0 R/Rect[{Numero(rect.X1)} {Numero(rect.Y1)} {Numero(rect.X2)} {Numero(rect.Y2)}]"
                + $"/DA(/ExecucaoHelvetica 8 Tf 0 g)/AP<</N {nAp} 0 R>>>>");
        }
        if (!preenchidos.SetEquals(valores.Keys)) throw new InvalidOperationException("Os campos do PDF não correspondem aos itens da prescrição.");
        var xref = original.Length + corpo.Length;
        corpo.Append(RevisaoIncrementalPdf.MontarXref(offsets, estrutura));
        corpo.Append($"startxref\n{xref}\n%%EOF\n");
        return original.Concat(Encoding.Latin1.GetBytes(corpo.ToString())).ToArray();
    }

    private static string Desenhar(string texto, double largura, double altura)
    {
        // WinAnsi cobre a escrita portuguesa. Outros caracteres são representados por
        // U+XXXX na aparência; o valor Unicode completo permanece em /V, sem perda.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var win = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var visivel = new StringBuilder();
        foreach (var rune in texto.EnumerateRunes())
        {
            try { visivel.Append(Encoding.Latin1.GetString(win.GetBytes(rune.ToString()))); }
            catch (EncoderFallbackException) { visivel.Append($"[U+{rune.Value:X4}]"); }
        }
        for (var tamanho = 8.0; tamanho >= 6; tamanho -= .5)
        {
            var linhas = new List<string>();
            foreach (var paragrafo in visivel.ToString().Replace("\r", "").Split('\n'))
            {
                var linha = "";
                foreach (var ch in paragrafo)
                {
                    if (linha.Length > 0 && RevisaoIncrementalPdf.LarguraNaHelvetica(linha + ch, tamanho) > largura - 4)
                    { linhas.Add(linha); linha = ""; }
                    linha += ch;
                }
                linhas.Add(linha);
            }
            if (linhas.Count * (tamanho + 2) > altura - 4) continue;
            var sb = new StringBuilder($"q\n0 0 {Numero(largura)} {Numero(altura)} re W n\nBT /ExecucaoHelvetica {Numero(tamanho)} Tf 0 g\n");
            var y = altura - tamanho - 2;
            foreach (var linha in linhas)
            {
                var escapada = linha.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
                sb.Append($"1 0 0 1 2 {Numero(y)} Tm ({escapada}) Tj\n");
                y -= tamanho + 2;
            }
            return sb.Append("ET Q").ToString();
        }
        throw new InvalidOperationException("O registro de execução excede o espaço reservado no PDF. Nenhuma assinatura foi solicitada.");
    }
}
