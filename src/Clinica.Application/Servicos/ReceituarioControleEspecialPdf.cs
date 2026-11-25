using Clinica.Application.Assinatura;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using QRCoder;

namespace Clinica.Application.Servicos;

/// <summary>
/// O PDF do RECEITUÁRIO DE CONTROLE ESPECIAL (nov/2026) — o modelo do ITI, versão 2.0 (abril/2020), em DUAS vias
/// (farmácia e paciente), com a geometria EXATA do modelo aprovado pelo dono
/// (Shared/Clinica/receitas/Receituario-Controle-Especial-…pdf). Desenhado com PdfSharp, e não com o fluxo do QuestPDF,
/// porque o campo "ASSINATURA MÉDICO(A)" é um RETÂNGULO FIXO: o carimbo da assinatura digital cai dentro dele
/// (<see cref="AreaDaAssinatura"/>), nas duas vias.
///
/// Emitente: SEMPRE quem emite (o profissional do documento) e a clínica — ninguém digita. Comprador: o paciente
/// (a tela completa o que faltar antes de emitir). Fornecedor: em branco, para a farmácia.
/// </summary>
public static class ReceituarioControleEspecialPdf
{
    public const double Largura = 595, Altura = 842;

    /// <summary>
    /// O campo "ASSINATURA MÉDICO(A)": entre o quadro do logo (que termina em y 182) e a linha da assinatura (y 228),
    /// x 392–552 (pontos, topo da página). O carimbo fica <see cref="Folga"/> para dentro em cima e embaixo, para não
    /// encostar no quadro nem na linha (QA de design, 25/11: pedia 4 pt ou mais de cada lado).
    /// </summary>
    public const double CampoTopo = 182, CampoBase = 228, Folga = 4;
    public const double AssinaturaX = 392, AssinaturaLargura = 160;
    public const double AssinaturaTopo = CampoTopo + Folga, AssinaturaAltura = CampoBase - Folga - AssinaturaTopo;

    /// <summary>
    /// Onde o carimbo da assinatura é colado — o retângulo do campo, em coordenada de PDF (origem no PÉ), na 1ª via e,
    /// pelo <see cref="AreaAssinatura.OutrasPaginas"/>, na 2ª. Letra ajustada para caber inteira.
    /// </summary>
    public static AreaAssinatura AreaDaAssinatura()
        => new(Pagina: 0, X: AssinaturaX, Y: Altura - AssinaturaTopo - AssinaturaAltura,
            Largura: AssinaturaLargura, Altura: AssinaturaAltura, OutrasPaginas: [1], AjustarFonte: true);

    public static readonly string[] Vias = ["1ª VIA – FARMÁCIA", "2ª VIA – PACIENTE"];

    private static readonly XColor Preto = XColors.Black;
    private static readonly XColor Cinza = XColor.FromArgb(89, 89, 89);
    private static readonly XColor CinzaLinha = XColor.FromArgb(191, 191, 191);

    public static byte[] Gerar(DocumentoClinico documento, DadosPrestador? prestador, string? urlDoArquivo, bool previa,
        string validadorPadrao)
    {
        AssinaturaDigitalService.GarantirFonte();
        var pdf = new PdfDocument();
        pdf.Info.Title = "Receituário de Controle Especial";
        pdf.Info.Author = documento.Profissional?.Nome ?? "";
        pdf.Info.Subject = "Receituário de Controle Especial" + (documento.Numero is { Length: > 0 } n ? " nº " + n : "");
        var qr = Qr(urlDoArquivo ?? validadorPadrao);
        foreach (var via in Vias)
        {
            var pagina = pdf.AddPage();
            pagina.Width = XUnit.FromPoint(Largura); pagina.Height = XUnit.FromPoint(Altura);
            using var g = XGraphics.FromPdfPage(pagina);
            Desenhar(g, documento, prestador, via, qr, previa);
        }
        using var saida = new MemoryStream();
        pdf.Save(saida, false);
        return saida.ToArray();
    }

    private static XFont F(double tamanho, bool negrito = false)
        => new("SegoeWP", tamanho, negrito ? XFontStyleEx.Bold : XFontStyleEx.Regular);

    private static void T(XGraphics g, double x, double y, string s, double tam = 8.5, bool negrito = false, XColor? cor = null)
        => g.DrawString(s, F(tam, negrito), new XSolidBrush(cor ?? Preto), x, y);

    /// <summary>Rótulo em negrito, valor e o traço do preenchimento até <paramref name="largura"/> — o desenho do modelo.</summary>
    private static void Linha(XGraphics g, double x, double y, string rotulo, string? valor, double largura)
    {
        T(g, x, y, rotulo, 7.5, true);
        var lx = x + g.MeasureString(rotulo, F(7.5, true)).Width + 8;
        var espaco = x + largura - lx;
        if (!string.IsNullOrWhiteSpace(valor)) T(g, lx, y, Caber(g, valor.Trim(), F(8.5), espaco), 8.5);
        g.DrawLine(new XPen(CinzaLinha, 0.5), lx, y + 2, x + largura, y + 2);
    }

    private static string Caber(XGraphics g, string texto, XFont fonte, double largura)
    {
        if (g.MeasureString(texto, fonte).Width <= largura) return texto;
        var corte = texto;
        while (corte.Length > 1 && g.MeasureString(corte + "…", fonte).Width > largura) corte = corte[..^1];
        return corte.TrimEnd() + "…";
    }

    private static void Desenhar(XGraphics g, DocumentoClinico d, DadosPrestador? prestador, string via, bool[][]? qr, bool previa)
    {
        var preta = new XPen(Preto, 0.8);
        var prof = d.Profissional;
        var paciente = d.Paciente;
        var (cidadeClinica, ufClinica) = ReceituarioControleEspecialRegras.CidadeUfDaClinica(prestador);

        // Cabeçalho: título, a caixa "VIA DIGITAL" e o QR da conferência (o do arquivo publicado, ou o do validador).
        if (qr is not null)
        {
            DesenharQr(g, qr, 40, 30, 56);
            // Sem código (prévia ou rascunho), não há o que conferir: o rótulo some em vez de sair vazio.
            if (!string.IsNullOrWhiteSpace(d.CodigoVerificacao))
                T(g, 40, 94, "Conferência: " + d.CodigoVerificacao, 6, false, Cinza);
        }
        T(g, 170, 58, "RECEITUÁRIO DE", 17, true);
        T(g, 150, 80, "CONTROLE ESPECIAL", 17, true);
        g.DrawRectangle(preta, 430, 40, 130, 38);
        T(g, 462, 52, "VIA DIGITAL", 8, true);
        T(g, 470, 62, "VALIDAR EM:", 6);
        T(g, 436, 72, "https://validar.iti.gov.br", 6.5);
        T(g, 430, 95, via, 8, true, Cinza);

        // Emitente — quem emite e a clínica.
        g.DrawRectangle(preta, 36, 102, 524, 148);
        T(g, 215, 113, "IDENTIFICAÇÃO DO EMITENTE", 8.5, true);
        g.DrawLine(new XPen(Preto, 0.6), 36, 117, 560, 117);
        Linha(g, 44, 135, "NOME COMPLETO:", prof?.Nome, 330);
        // Sigla e UF por extenso: o cadastro guarda os códigos TISS ("06", "33"), o papel diz "CRM" e "RJ".
        var registro = RegistroDoPrescritor.De(prof);
        Linha(g, 44, 153, (registro.Conselho ?? "CRM") + ":", registro.Numero ?? prof?.RegistroConselho, 230);
        Linha(g, 290, 153, "UF:", registro.Uf, 84);
        Linha(g, 44, 171, "ENDEREÇO COMPLETO:", ReceituarioControleEspecialRegras.EnderecoSemCidade(prestador?.Endereco), 330);
        var clinica = string.Join(" · ", new[]
        {
            prestador?.RazaoSocial?.Trim(), prestador?.Cnpj is { Length: > 0 } cnpj ? "CNPJ " + FormatarCnpj(cnpj) : null,
            prestador?.Cnes is { Length: > 0 } cnes ? "CNES " + cnes.Trim() : null
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (clinica.Length > 0) T(g, 44, 186, Caber(g, clinica, F(7.5), 340), 7.5, false, Cinza);
        Linha(g, 44, 204, "CIDADE:", cidadeClinica, 230);
        Linha(g, 290, 204, "UF:", ufClinica, 84);
        Linha(g, 44, 222, "TELEFONE:", (prestador?.Telefone ?? prof?.Telefone)?.Trim(), 200);
        Linha(g, 260, 222, "DATA:", d.Data.ToString("dd/MM/yyyy"), 114);
        g.DrawRectangle(new XPen(XColor.FromArgb(179, 179, 179), 0.5), 392, 124, 160, 58);
        if (MarcaSemDor.Logo is { } logo)
        {
            using var img = XImage.FromStream(new MemoryStream(logo));
            var escala = Math.Min(144 / img.PointWidth, 46 / img.PointHeight);
            var lw = img.PointWidth * escala; var lh = img.PointHeight * escala;
            g.DrawImage(img, 400 + (144 - lw) / 2, 130 + (46 - lh) / 2, lw, lh);
        }
        // O campo da assinatura: o retângulo acima da linha fica LIVRE — é onde o carimbo digital cai.
        g.DrawLine(new XPen(Preto, 0.5), 392, 228, 552, 228);
        T(g, 420, 240, "ASSINATURA MÉDICO(A)", 7.5, true);

        // Paciente.
        Linha(g, 40, 272, "NOME PACIENTE:", paciente?.Nome, 520);
        Linha(g, 40, 290, "ENDEREÇO COMPLETO:", EnderecoDoPaciente(paciente), 520);

        // Prescrição.
        T(g, 40, 310, "PRESCRIÇÃO:", 7.5, true);
        g.DrawRectangle(new XPen(XColor.FromArgb(204, 204, 204), 0.5), 36, 316, 524, 274);
        DesenharPrescricao(g, d);

        // Comprador (o paciente) e fornecedor (em branco).
        var comprador = new (string Rotulo, string? Valor)[]
        {
            ("NOME COMPLETO:", paciente?.Nome), ("RG:", paciente?.Rg), ("ÓRGÃO EMISSOR:", paciente?.RgOrgaoEmissor),
            ("ENDEREÇO COMPLETO:", paciente?.Endereco), ("", null),
            ("CIDADE:", JuntarCidadeUf(paciente?.Cidade, paciente?.Uf)), ("TELEFONE:", paciente?.TelefoneFormatado)
        };
        var fornecedor = new (string Rotulo, string? Valor)[]
        {
            ("NOME FARMACÊUTICO(A):", null), ("CRF:", null), ("NOME FARMÁCIA:", null), ("ENDEREÇO:", null),
            ("CIDADE:", null), ("CNPJ:", null), ("TELEFONE:", null)
        };
        foreach (var (x0, titulo, campos, assinatura) in new[]
                 {
                     (36.0, "IDENTIFICAÇÃO DO COMPRADOR", comprador, (string?)null),
                     (304.0, "IDENTIFICAÇÃO DO FORNECEDOR", fornecedor, "ASSINATURA FARMACÊUTICO(A)")
                 })
        {
            g.DrawRectangle(preta, x0, 605, 256, 185);
            T(g, x0 + 40, 617, titulo, 8.5, true);
            g.DrawLine(new XPen(Preto, 0.6), x0, 621, x0 + 256, 621);
            var yy = 638.0;
            foreach (var (rotulo, valor) in campos)
            {
                if (rotulo.Length > 0) Linha(g, x0 + 8, yy, rotulo, valor, 240);
                else g.DrawLine(new XPen(CinzaLinha, 0.5), x0 + 8, yy + 2, x0 + 248, yy + 2);
                yy += 19;
            }
            if (assinatura is not null)
            {
                g.DrawLine(new XPen(Preto, 0.5), x0 + 40, 775, x0 + 216, 775);
                T(g, x0 + 62, 786, assinatura, 7.5, true);
            }
        }

        var rodape = string.Join(" · ", new[]
        {
            d.Numero is { Length: > 0 } num ? "Nº " + num : null,
            d.CodigoVerificacao is { Length: > 0 } cod ? "Código de conferência " + cod : null,
            "VIA DIGITAL – validar em validar.iti.gov.br"
        }.Where(s => s is not null));
        T(g, 40, 806, rodape, 7, false, Cinza);
        T(g, 220, 818, "Modelo ITI · Versão 2.0 | Abril de 2020", 7.5, false, Cinza);

        if (previa)
        {
            var estado = g.Save();
            g.TranslateTransform(Largura / 2, Altura / 2); g.RotateTransform(-40);
            g.DrawString("PRÉVIA", F(110, true), new XSolidBrush(XColor.FromArgb(30, 0, 0, 0)), 0, 0, XStringFormats.Center);
            g.Restore(estado);
        }
    }

    private static string FormatarCnpj(string cnpj)
    {
        var d = new string(cnpj.Where(char.IsDigit).ToArray());
        return d.Length == 14 ? $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}" : cnpj.Trim();
    }

    private static string? EnderecoDoPaciente(Paciente? p)
    {
        if (p is null) return null;
        var cidadeUf = JuntarCidadeUf(p.Cidade, p.Uf);
        return string.Join(", ", new[] { p.Endereco?.Trim(), cidadeUf }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static string? JuntarCidadeUf(string? cidade, string? uf)
        => string.Join(" – ", new[] { cidade?.Trim(), uf?.Trim().ToUpperInvariant() }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } s ? s : null;

    /// <summary>As linhas da prescrição: o texto livre e os itens, quebrados na largura do campo.</summary>
    public static IReadOnlyList<(string Texto, bool Destaque)> LinhasDaPrescricao(DocumentoClinico d)
    {
        var linhas = new List<(string, bool)>();
        foreach (var l in (d.Corpo ?? "").Replace("\r", "").Split('\n'))
            linhas.Add((l.TrimEnd(), Destacada(l)));
        var n = 0;
        foreach (var item in d.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id))
        {
            n++;
            var cabeca = $"{n}. {item.Descricao}".Trim();
            if (!string.IsNullOrWhiteSpace(item.Quantidade)) cabeca += " ........ " + item.Quantidade!.Trim();
            linhas.Add((cabeca, true));
            if (!string.IsNullOrWhiteSpace(item.Detalhe))
                foreach (var det in item.Detalhe!.Replace("\r", "").Split('\n')) linhas.Add(("   " + det.TrimEnd(), false));
        }
        while (linhas.Count > 0 && string.IsNullOrWhiteSpace(linhas[^1].Item1)) linhas.RemoveAt(linhas.Count - 1);
        return linhas;
    }

    /// <summary>
    /// Negrito só onde o modelo do dono põe: o cabeçalho da via ("USO ORAL", "USO TÓPICO", a linha inteira) e o
    /// medicamento numerado ("1. Pyloripac®…"). A posologia ("Uso oral, 1 comprimido de 12/12 h") NÃO é cabeçalho.
    /// </summary>
    internal static bool Destacada(string linha)
    {
        var t = linha.Trim();
        return System.Text.RegularExpressions.Regex.IsMatch(t, @"^USO(\s+[^\s,;.]+){1,2}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
               || System.Text.RegularExpressions.Regex.IsMatch(t, @"^\d+[.)]\s");
    }

    private static void DesenharPrescricao(XGraphics g, DocumentoClinico d)
    {
        // Quebra na largura do campo (500 pt) e encolhe a letra até caber — o mínimo legível é 7,5.
        var fontes = new[] { 9.5, 9.0, 8.5, 8.0, 7.5 };
        foreach (var tam in fontes)
        {
            var quebradas = Quebrar(g, LinhasDaPrescricao(d), tam, 500);
            var altura = tam * 1.6;
            if (336 + quebradas.Count * altura <= 585 || tam == fontes[^1])
            {
                var y = 336.0;
                foreach (var (texto, destaque) in quebradas)
                {
                    if (y > 585) break;
                    T(g, 48, y, texto, tam, destaque);
                    y += altura;
                }
                return;
            }
        }
    }

    private static List<(string, bool)> Quebrar(XGraphics g, IReadOnlyList<(string Texto, bool Destaque)> linhas, double tam, double largura)
    {
        var saida = new List<(string, bool)>();
        foreach (var (texto, destaque) in linhas)
        {
            var fonte = F(tam, destaque);
            if (texto.Length == 0 || g.MeasureString(texto, fonte).Width <= largura) { saida.Add((texto, destaque)); continue; }
            var recuo = texto.Length - texto.TrimStart().Length;
            var atual = "";
            foreach (var palavra in texto.Split(' '))
            {
                var tentativa = atual.Length == 0 ? palavra : atual + " " + palavra;
                if (g.MeasureString(tentativa, fonte).Width > largura && atual.Length > 0)
                {
                    saida.Add((atual, destaque));
                    atual = new string(' ', recuo + 3) + palavra.TrimStart();
                }
                else atual = tentativa;
            }
            if (atual.Length > 0) saida.Add((atual, destaque));
        }
        return saida;
    }

    private static void DesenharQr(XGraphics g, bool[][] modulos, double x, double y, double lado)
    {
        var n = modulos.Length;
        if (n == 0) return;
        var m = lado / n;
        g.DrawRectangle(XBrushes.White, x, y, lado, lado);
        for (var i = 0; i < n; i++)
            for (var j = 0; j < modulos[i].Length; j++)
                if (modulos[i][j]) g.DrawRectangle(XBrushes.Black, x + j * m, y + i * m, m + 0.01, m + 0.01);
    }

    /// <summary>
    /// Os módulos do QR (verdadeiro = escuro). Desenhado em VETOR, módulo a módulo: o PDFsharp desta montagem não lê
    /// o PNG indexado que o QRCoder gera ("Unsupported image format" — pego pelo teste), e vetor sai nítido na
    /// impressora de qualquer resolução.
    /// </summary>
    private static bool[][]? Qr(string url)
    {
        try
        {
            using var gerador = new QRCodeGenerator();
            using var dados = gerador.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            return dados.ModuleMatrix.Select(linha => linha.Cast<bool>().ToArray()).ToArray();
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("ReceituarioControleEspecialPdf.Qr", ex);
            return null;
        }
    }
}
