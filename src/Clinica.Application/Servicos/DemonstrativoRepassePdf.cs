using System.Globalization;
using System.Text;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

/// <summary>
/// O PDF do demonstrativo de repasse médico — o papel que vai ao profissional, no leiaute
/// que a clínica já usava (cabeçalho, resumo por convênio, valores por consulta, anexo por
/// convênio e pagamento, assinatura de quem conferiu).
/// ⚠️ CONTRATO FIXADO — a tela chama exatamente estas assinaturas.
///
/// Dinheiro e data em pt-BR FIXO: o papel sai igual no Windows da clínica e na prévia em
/// Linux, onde a cultura do processo é a invariante.
/// </summary>
public static class DemonstrativoRepassePdf
{
    private const string AzulEscuro = MarcaSemDor.Navy;
    private const string Azul = MarcaSemDor.Azul;
    private const string TextoPrimario = "#111827";
    private const string TextoSecundario = "#6B7280";
    private const string Borda = "#E5E7EB";
    private const string FundoSuave = "#F8FAFC";
    private const string FundoCabecalhoTabela = "#F1F5F9";
    private const string VermelhoForte = "#B91C1C";
    private const string VermelhoSuave = "#FEE2E2";
    private const string MarcaDagua = "#1FB91C1C"; // vermelho a ~12% de opacidade

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static byte[] Gerar(DemonstrativoRepasse d)
    {
        ArgumentNullException.ThrowIfNull(d);
        QuestPDF.Settings.License = LicenseType.Community;
        var cancelado = d.Situacao.StartsWith("Cancelado", StringComparison.Ordinal);
        var totalCreditos = d.Resumo.Sum(r => r.Total);
        var totalEstornos = d.Estornos.Sum(e => e.Valor);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(TextoPrimario));

                if (cancelado)
                    page.Foreground().AlignCenter().AlignMiddle().Rotate(-40)
                        .Text("CANCELADO").Bold().FontSize(110).FontColor(MarcaDagua);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(d.Clinica).SemiBold().FontSize(9).FontColor(TextoSecundario);
                        row.RelativeItem().AlignRight().Text(d.Profissional).FontSize(9).FontColor(TextoSecundario);
                    });
                    col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Borda);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(10);

                    // ===== Faixa de título =====
                    col.Item().Background(AzulEscuro).PaddingVertical(10).PaddingHorizontal(14).Column(c =>
                    {
                        c.Item().Text("DEMONSTRATIVO DE REPASSE MÉDICO").Bold().FontSize(15).FontColor(Colors.White);
                        c.Item().Text($"Competência {Competencia(d.Competencia)}").FontSize(10.5f).FontColor(Colors.White);
                    });

                    if (cancelado)
                        col.Item().Background(VermelhoSuave).Border(1).BorderColor(VermelhoForte).Padding(8)
                            .Text("FECHAMENTO CANCELADO — este demonstrativo não vale para pagamento.")
                            .Bold().FontSize(9.5f).FontColor(VermelhoForte);

                    // ===== Quadro de identificação =====
                    col.Item().Border(1).BorderColor(Borda).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                        void Campo(string rotulo, string valor) => t.Cell().Padding(8).Column(c =>
                        {
                            c.Item().Text(rotulo.ToUpperInvariant()).FontSize(7.5f).SemiBold().FontColor(TextoSecundario);
                            c.Item().Text(valor).FontSize(10.5f).SemiBold();
                        });
                        Campo("Médico(a)", string.IsNullOrWhiteSpace(d.Registro) ? d.Profissional : $"{d.Profissional} · {d.Registro}");
                        Campo("Competência", $"{Competencia(d.Competencia)} (recebidos até {Data(d.RecebidosAte)})");
                        Campo("Data de conferência", Data(DateOnly.FromDateTime(d.ConferidoEm)));
                        Campo("Responsável pela conferência", d.ConferidoPor);
                        Campo("Situação", d.Situacao);
                        Campo("Total a pagar", Dinheiro(d.Total));
                    });

                    // ===== Resumo =====
                    col.Item().PaddingTop(4).Text("Resumo do repasse").Bold().FontSize(12).FontColor(AzulEscuro);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4); c.RelativeColumn(2); c.RelativeColumn(2.4f); c.RelativeColumn(2.2f);
                        });
                        Cabecalho(t, ("Convênio", false), ("Nº de consultas", true), ("Repasse por consulta", true), ("Total", true));
                        var linha = 0;
                        foreach (var r in d.Resumo)
                        {
                            var fundo = linha++ % 2 == 1 ? FundoSuave : "#FFFFFF";
                            Celula(t, fundo).Text(r.Convenio);
                            Celula(t, fundo).AlignRight().Text(r.Quantidade.ToString(PtBr));
                            Celula(t, fundo).AlignRight().Text(r.ValorUnitario is { } u ? Dinheiro(u) : "variado");
                            Celula(t, fundo).AlignRight().Text(Dinheiro(r.Total)).SemiBold();
                        }
                        Rodape(t, "Total geral", d.Quantidade.ToString(PtBr), Dinheiro(totalCreditos));
                        if (d.Estornos.Count > 0)
                        {
                            Rodape(t, "Estornos (recebimentos desfeitos)", d.Estornos.Count.ToString(PtBr), Dinheiro(totalEstornos));
                            Rodape(t, "Total a pagar", string.Empty, Dinheiro(d.Total));
                        }
                    });

                    // ===== Observação: valores por consulta =====
                    if (d.RegrasAplicadas.Count > 0)
                        col.Item().Background(FundoSuave).Border(1).BorderColor(Borda).Padding(10).Column(c =>
                        {
                            c.Item().Text("Observação — Valor de repasse por consulta").Bold().FontSize(9.5f).FontColor(AzulEscuro);
                            foreach (var regra in d.RegrasAplicadas)
                                c.Item().PaddingTop(2).Text($"• {regra}").FontSize(9.5f);
                        });

                    if (!string.IsNullOrWhiteSpace(d.Observacoes))
                        col.Item().Border(1).BorderColor(Borda).Padding(10).Text(t =>
                        {
                            t.Span("Observações  ").Bold().FontSize(8.5f).FontColor(TextoSecundario);
                            t.Span(d.Observacoes!).FontSize(9.5f);
                        });

                    // ===== Informações anexadas =====
                    if (d.Blocos.Count > 0)
                        col.Item().PaddingTop(6).Text("Informações anexadas").Bold().FontSize(12).FontColor(AzulEscuro);

                    foreach (var bloco in d.Blocos)
                    {
                        col.Item().Column(c =>
                        {
                            c.Item().PaddingBottom(3).Text(TituloDoBloco(bloco)).SemiBold().FontSize(10.5f).FontColor(Azul);
                            c.Item().Table(t => TabelaDeItens(t, bloco.Itens, $"Total {bloco.Convenio}", bloco.Total));
                        });
                    }

                    if (d.Estornos.Count > 0)
                        col.Item().Column(c =>
                        {
                            c.Item().PaddingBottom(3).Text("Estornos — recebimentos fechados antes e desfeitos depois")
                                .SemiBold().FontSize(10.5f).FontColor(VermelhoForte);
                            c.Item().Table(t => TabelaDeItens(t, d.Estornos, "Total de estornos", totalEstornos, comConvenio: true));
                        });

                    // ===== Assinatura =====
                    col.Item().PaddingTop(36).ShowEntire().Row(row =>
                    {
                        row.RelativeItem();
                        row.RelativeItem(2).Column(c =>
                        {
                            c.Item().LineHorizontal(1).LineColor(TextoSecundario);
                            c.Item().PaddingTop(3).AlignCenter().Text(d.ConferidoPor).SemiBold();
                            c.Item().AlignCenter()
                                .Text($"Responsável pela conferência · {Data(DateOnly.FromDateTime(d.ConferidoEm))}")
                                .FontSize(8.5f).FontColor(TextoSecundario);
                        });
                        row.RelativeItem();
                    });
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"{d.Profissional} · Competência {Competencia(d.Competencia)}")
                        .FontSize(8).FontColor(TextoSecundario);
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("Página ").FontSize(8).FontColor(TextoSecundario);
                        t.CurrentPageNumber().FontSize(8).FontColor(TextoSecundario);
                        t.Span(" de ").FontSize(8).FontColor(TextoSecundario);
                        t.TotalPages().FontSize(8).FontColor(TextoSecundario);
                    });
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Nome de arquivo sugerido: Demonstrativo_Repasse_{Nome}_{MM-aaaa}.pdf</summary>
    public static string NomeDoArquivo(DemonstrativoRepasse demonstrativo)
    {
        ArgumentNullException.ThrowIfNull(demonstrativo);
        var nome = SemAcentosNemEspacos(demonstrativo.Profissional);
        return $"Demonstrativo_Repasse_{(nome.Length == 0 ? "Profissional" : nome)}_"
               + $"{demonstrativo.Competencia.Month:00}-{demonstrativo.Competencia.Year:0000}.pdf";
    }

    /// <summary>O mesmo conteúdo em CSV (pt-BR, ';'), uma linha por item.</summary>
    public static string Csv(DemonstrativoRepasse demonstrativo)
    {
        ArgumentNullException.ThrowIfNull(demonstrativo);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', "Competencia", "Profissional", "Natureza", "Convenio", "Referencia do pagamento",
            "Paciente", "Data do atendimento", "Tipo", "Numero da guia", "Data do recebimento",
            "Valor recebido", "Regra", "Valor do repasse"));

        var linhas = demonstrativo.Blocos.SelectMany(b => b.Itens).Concat(demonstrativo.Estornos);
        foreach (var i in linhas)
        {
            sb.AppendLine(string.Join(';',
                Campo(Competencia(demonstrativo.Competencia)),
                Campo(demonstrativo.Profissional),
                Campo(i.Natureza == NaturezaItemRepasse.Estorno ? "Estorno" : "Crédito"),
                Campo(i.ConvenioNome),
                Campo(i.ReferenciaPagamento),
                Campo(i.PacienteNome),
                Campo(Data(i.DataAtendimento)),
                Campo(i.Descricao),
                Campo(i.NumeroGuia),
                Campo(Data(i.DataRecebimento)),
                Campo(i.ValorRecebido.ToString("0.00", PtBr)),
                Campo(i.RegraDescricao),
                Campo(i.Valor.ToString("0.00", PtBr))));
        }
        return sb.ToString();
    }

    // ==================== Peças ====================

    private static void TabelaDeItens(TableDescriptor t, IReadOnlyList<ItemRepasse> itens, string rotuloTotal,
        decimal total, bool comConvenio = false)
    {
        t.ColumnsDefinition(c =>
        {
            c.ConstantColumn(28);
            c.RelativeColumn(comConvenio ? 3.6f : 4.6f);
            if (comConvenio) c.RelativeColumn(2);
            c.RelativeColumn(2);
            c.RelativeColumn(2);
            c.RelativeColumn(1.8f);
        });

        if (comConvenio)
            Cabecalho(t, ("Nº", false), ("Beneficiário", false), ("Convênio", false), ("Data atendimento", false), ("Tipo", false), ("Valor", true));
        else
            Cabecalho(t, ("Nº", false), ("Beneficiário", false), ("Data atendimento", false), ("Tipo", false), ("Valor", true));

        var n = 0;
        foreach (var i in itens)
        {
            var fundo = n % 2 == 1 ? FundoSuave : "#FFFFFF";
            n++;
            Celula(t, fundo).Text(n.ToString(PtBr)).FontColor(TextoSecundario);
            Celula(t, fundo).Text(i.PacienteNome);
            if (comConvenio) Celula(t, fundo).Text(i.ConvenioNome);
            Celula(t, fundo).Text(Data(i.DataAtendimento));
            Celula(t, fundo).Text(i.Descricao);
            Celula(t, fundo).AlignRight().Text(Dinheiro(i.Valor));
        }

        t.Cell().ColumnSpan(comConvenio ? 5u : 4u).Background(FundoCabecalhoTabela).Padding(5)
            .Text(rotuloTotal).Bold();
        t.Cell().Background(FundoCabecalhoTabela).Padding(5).AlignRight().Text(Dinheiro(total)).Bold();
    }

    private static void Cabecalho(TableDescriptor t, params (string Texto, bool Direita)[] colunas)
    {
        t.Header(h =>
        {
            foreach (var (texto, direita) in colunas)
            {
                var c = h.Cell().Background(FundoCabecalhoTabela).BorderBottom(1).BorderColor(Borda)
                    .PaddingVertical(5).PaddingHorizontal(5);
                (direita ? c.AlignRight() : c).Text(texto).SemiBold().FontSize(8.5f).FontColor(TextoSecundario);
            }
        });
    }

    private static IContainer Celula(TableDescriptor t, string fundo)
        => t.Cell().Background(fundo).BorderBottom(1).BorderColor(Borda).PaddingVertical(4).PaddingHorizontal(5);

    private static void Rodape(TableDescriptor t, string rotulo, string quantidade, string total)
    {
        t.Cell().Background(FundoCabecalhoTabela).Padding(5).Text(rotulo).Bold();
        t.Cell().Background(FundoCabecalhoTabela).Padding(5).AlignRight().Text(quantidade).Bold();
        t.Cell().Background(FundoCabecalhoTabela).Padding(5).Text(string.Empty);
        t.Cell().Background(FundoCabecalhoTabela).Padding(5).AlignRight().Text(total).Bold();
    }

    private static string TituloDoBloco(BlocoDoDemonstrativo b)
        => string.IsNullOrWhiteSpace(b.ReferenciaPagamento) ? b.Convenio : $"{b.Convenio} · {b.ReferenciaPagamento}";

    private static string Competencia(DateOnly d) => d.ToString("MM/yyyy", PtBr);
    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);
    private static string Dinheiro(decimal v) => v.ToString("C", PtBr);

    /// <summary>Campo de CSV: aspas quando há separador, aspas ou quebra de linha.</summary>
    private static string Campo(string? valor)
    {
        var v = valor ?? string.Empty;
        return v.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    private static string SemAcentosNemEspacos(string texto)
    {
        var sb = new StringBuilder();
        foreach (var ch in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
            else if (char.IsWhiteSpace(ch) || ch is '-' or '_' or '.') sb.Append('_');
        }
        return string.Join('_', sb.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries));
    }
}
