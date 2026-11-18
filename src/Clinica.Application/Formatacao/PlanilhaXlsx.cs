using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Clinica.Application.Formatacao;

/// <summary>O tipo de uma célula da planilha.</summary>
public enum TipoCelulaXlsx { Texto, Data, Hora, Numero }

/// <summary>Uma célula com o TIPO certo: data como data (o Excel ordena e filtra por mês), número como número.</summary>
public readonly record struct CelulaXlsx(TipoCelulaXlsx Tipo, string? Conteudo, double? Valor)
{
    public static CelulaXlsx Texto(string? texto) => new(TipoCelulaXlsx.Texto, texto, null);
    public static CelulaXlsx Numero(double valor) => new(TipoCelulaXlsx.Numero, null, valor);
    public static CelulaXlsx Data(DateOnly data) => new(TipoCelulaXlsx.Data, null, data.ToDateTime(TimeOnly.MinValue).ToOADate());
    public static CelulaXlsx Hora(TimeOnly hora) => new(TipoCelulaXlsx.Hora, null, hora.ToTimeSpan().TotalDays);
}

/// <summary>
/// UMA PLANILHA .XLSX, SEM BIBLIOTECA (nov/2026, relatório de atendimentos). O .xlsx é um zip de XML
/// (OpenXML/SpreadsheetML); o mínimo que o Excel, o LibreOffice e o Google Planilhas abrem sem reparo
/// cabe aqui: uma aba, cabeçalho em negrito e CONGELADO, FILTRO AUTOMÁTICO ligado nas colunas, data e
/// hora com formato, número como número e largura de coluna pelo conteúdo.
///
/// Por que sem biblioteca: as que existem (ClosedXML, EPPlus, Open XML SDK) somam megabytes aos cinco
/// apps, uma mudou de licença, e o que a clínica precisa é uma tabela com filtro — não fórmula, gráfico
/// nem estilo. <see cref="System.IO.Compression"/> já vem no .NET.
///
/// Texto vai como <c>inlineStr</c>: o Excel não o avalia — "=HYPERLINK(...)" num nome chega como texto,
/// sem a neutralização que o CSV precisa (<see cref="CsvParaExcel"/>).
/// </summary>
public static class PlanilhaXlsx
{
    public static byte[] Gerar(string nomeDaAba, IReadOnlyList<string> cabecalho, IEnumerable<IReadOnlyList<CelulaXlsx>> linhas)
    {
        var dados = linhas.ToList();
        var colunas = cabecalho.Count;
        if (colunas == 0) throw new ArgumentException("A planilha precisa de ao menos uma coluna.", nameof(cabecalho));
        if (dados.Any(l => l.Count != colunas)) throw new ArgumentException("Toda linha precisa ter uma célula por coluna.", nameof(linhas));

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            Escrever(zip, "[Content_Types].xml", TiposDeConteudo);
            Escrever(zip, "_rels/.rels", RelacoesDoPacote);
            Escrever(zip, "xl/workbook.xml", Livro(Aba(nomeDaAba), colunas, dados.Count + 1));
            Escrever(zip, "xl/_rels/workbook.xml.rels", RelacoesDoLivro);
            Escrever(zip, "xl/styles.xml", Estilos);
            Escrever(zip, "xl/worksheets/sheet1.xml", Folha(cabecalho, dados));
        }
        return memoria.ToArray();
    }

    /// <summary>"A", "B", … "Z", "AA" — a letra da coluna (1 = A).</summary>
    public static string Coluna(int numero)
    {
        var letras = string.Empty;
        while (numero > 0)
        {
            var resto = (numero - 1) % 26;
            letras = (char)('A' + resto) + letras;
            numero = (numero - 1) / 26;
        }
        return letras;
    }

    /// <summary>O nome da aba: até 31 caracteres, sem os que o Excel recusa.</summary>
    private static string Aba(string nome)
    {
        var limpo = new string(nome.Where(c => "[]:*?/\\".IndexOf(c) < 0).ToArray()).Trim();
        if (limpo.Length == 0) limpo = "Planilha";
        return limpo.Length > 31 ? limpo[..31] : limpo;
    }

    private static void Escrever(ZipArchive zip, string caminho, string conteudo)
    {
        var entrada = zip.CreateEntry(caminho, CompressionLevel.Optimal);
        using var s = entrada.Open();
        var bytes = new UTF8Encoding(false).GetBytes(conteudo);
        s.Write(bytes, 0, bytes.Length);
    }

    private static string Xml(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        // Caracteres de controle são proibidos em XML 1.0 (menos tab e quebra de linha).
        var limpo = new string(texto.Where(c => c >= ' ' || c is '\t' or '\n' or '\r').ToArray());
        return SecurityElement.Escape(limpo) ?? string.Empty;
    }

    private static string Folha(IReadOnlyList<string> cabecalho, IReadOnlyList<IReadOnlyList<CelulaXlsx>> linhas)
    {
        var colunas = cabecalho.Count;
        var ultima = $"{Coluna(colunas)}{linhas.Count + 1}";
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        sb.Append($"<dimension ref=\"A1:{ultima}\"/>");
        // O cabeçalho fica parado ao rolar.
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        sb.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
        sb.Append("<cols>");
        for (var c = 0; c < colunas; c++)
        {
            var maior = Math.Max(cabecalho[c].Length, linhas.Select(l => Largura(l[c])).DefaultIfEmpty(0).Max());
            var largura = Math.Clamp(maior + 3, 8, 60);
            sb.Append(CultureInfo.InvariantCulture, $"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{largura}\" customWidth=\"1\"/>");
        }
        sb.Append("</cols><sheetData>");

        sb.Append("<row r=\"1\">");
        for (var c = 0; c < colunas; c++)
            sb.Append($"<c r=\"{Coluna(c + 1)}1\" t=\"inlineStr\" s=\"1\"><is><t>{Xml(cabecalho[c])}</t></is></c>");
        sb.Append("</row>");

        for (var i = 0; i < linhas.Count; i++)
        {
            var r = i + 2;
            sb.Append($"<row r=\"{r}\">");
            for (var c = 0; c < colunas; c++)
            {
                var celula = linhas[i][c];
                var refCel = $"{Coluna(c + 1)}{r}";
                switch (celula.Tipo)
                {
                    case TipoCelulaXlsx.Texto:
                        if (!string.IsNullOrEmpty(celula.Conteudo))
                            sb.Append($"<c r=\"{refCel}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Xml(celula.Conteudo)}</t></is></c>");
                        break;
                    default:
                        if (celula.Valor is { } v)
                        {
                            var estilo = celula.Tipo switch { TipoCelulaXlsx.Data => 2, TipoCelulaXlsx.Hora => 3, _ => 0 };
                            sb.Append(CultureInfo.InvariantCulture, $"<c r=\"{refCel}\" s=\"{estilo}\"><v>{v.ToString("R", CultureInfo.InvariantCulture)}</v></c>");
                        }
                        break;
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");
        // O FILTRO DO EXCEL: as setinhas no cabeçalho, sobre a tabela inteira.
        sb.Append($"<autoFilter ref=\"A1:{ultima}\"/>");
        sb.Append("<pageMargins left=\"0.5\" right=\"0.5\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static int Largura(CelulaXlsx c) => c.Tipo switch
    {
        TipoCelulaXlsx.Texto => (c.Conteudo ?? string.Empty).Length,
        TipoCelulaXlsx.Data => 10,
        TipoCelulaXlsx.Hora => 5,
        _ => 6
    };

    private static string Livro(string aba, int colunas, int linhas) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
        + $"<sheets><sheet name=\"{Xml(aba)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>"
        // O nome oculto que o Excel usa para lembrar a faixa do filtro automático.
        + $"<definedNames><definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"0\" hidden=\"1\">'{Xml(aba).Replace("'", "''")}'!$A$1:${Coluna(colunas)}${linhas}</definedName></definedNames>"
        + "</workbook>";

    private const string TiposDeConteudo =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
        + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
        + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
        + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
        + "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
        + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
        + "</Types>";

    private const string RelacoesDoPacote =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
        + "</Relationships>";

    private const string RelacoesDoLivro =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
        + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
        + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
        + "</Relationships>";

    // Estilos (cellXfs): 0 normal, 1 cabeçalho em negrito, 2 data dd/mm/aaaa, 3 hora hh:mm.
    private const string Estilos =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
        + "<numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy\"/><numFmt numFmtId=\"165\" formatCode=\"hh:mm\"/></numFmts>"
        + "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
        + "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>"
        + "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"
        + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
        + "<cellXfs count=\"4\">"
        + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
        + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>"
        + "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
        + "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
        + "</cellXfs>"
        + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
        + "</styleSheet>";
}
