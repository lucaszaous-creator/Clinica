using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

/// <summary>
/// O PDF do relatório de repasse de UM médico (nov/2026) — o que a clínica manda a cada um.
/// Mesma identidade do <see cref="DemonstrativoRepassePdf"/> (o demonstrativo é UM fechamento;
/// este é o período inteiro de um médico, vários fechamentos). Fechamento cancelado só aparece
/// se o filtro o incluiu: com faixa "CANCELADO" e fora de todo total.
/// </summary>
public static class RelatorioRepassePdf
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

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly StringComparer Texto = StringComparer.Create(PtBr, CompareOptions.IgnoreCase);

    public static byte[] Gerar(RelatorioRepasse relatorio, TotalProfissionalRelatorioRepasse medico,
        DateTime emitidoEm, string emitidoPor)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var linhas = relatorio.Linhas.Where(l => l.ProfissionalId == medico.ProfissionalId).ToList();
        var vivas = linhas.Where(l => !l.Cancelado).ToList();
        var periodo = RelatorioRepasseService.Periodo(relatorio.Filtro);
        var umMes = !periodo.Contains(" a ", StringComparison.Ordinal);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(TextoPrimario));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(relatorio.Clinica).SemiBold().FontSize(9).FontColor(TextoSecundario);
                        row.RelativeItem().AlignRight().Text(medico.Profissional).FontSize(9).FontColor(TextoSecundario);
                    });
                    col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Borda);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Background(AzulEscuro).PaddingVertical(10).PaddingHorizontal(14).Column(c =>
                    {
                        c.Item().Text("RELATÓRIO DE REPASSE MÉDICO").Bold().FontSize(15).FontColor(Colors.White);
                        c.Item().Text(umMes ? $"Competência {periodo}" : periodo).FontSize(10.5f).FontColor(Colors.White);
                    });

                    // ===== Quadro =====
                    col.Item().Border(1).BorderColor(Borda).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                        void Campo(string rotulo, string valor) => t.Cell().Padding(7).Column(c =>
                        {
                            c.Item().Text(rotulo.ToUpperInvariant()).FontSize(7.5f).SemiBold().FontColor(TextoSecundario);
                            c.Item().Text(valor).FontSize(10).SemiBold();
                        });
                        Campo("Médico(a)", medico.Profissional);
                        Campo("Registro", string.IsNullOrWhiteSpace(medico.Registro) ? "—" : medico.Registro!);
                        Campo("Período", umMes ? $"Competência {periodo}" : $"Competências {periodo}");
                        Campo("Emitido em", $"{emitidoEm.ToString("dd/MM/yyyy HH:mm", PtBr)} por {emitidoPor}");
                        if (Filtros(relatorio.Filtro) is { } filtros)
                        {
                            t.Cell().ColumnSpan(2).Padding(7).Text(t2 =>
                            {
                                t2.Span("FILTRO  ").FontSize(7.5f).SemiBold().FontColor(TextoSecundario);
                                t2.Span(filtros).FontSize(9);
                            });
                        }
                    });

                    // ===== Totais =====
                    col.Item().Row(row =>
                    {
                        void Caixa(string rotulo, decimal valor, bool destaque = false) =>
                            row.RelativeItem().Background(destaque ? AzulEscuro : FundoSuave).Border(1).BorderColor(Borda)
                                .Padding(8).Column(c =>
                                {
                                    c.Item().Text(rotulo).FontSize(8).SemiBold().FontColor(destaque ? Colors.White : TextoSecundario);
                                    c.Item().Text(Dinheiro(valor)).Bold().FontSize(13).FontColor(destaque ? Colors.White : AzulEscuro);
                                });
                        row.Spacing(6);
                        Caixa("Pago", medico.Pago);
                        Caixa("A pagar", medico.APagar);
                        Caixa($"Total ({medico.Quantidade} consulta(s))", medico.Total, destaque: true);
                    });

                    // ===== Resumo competência × convênio =====
                    col.Item().PaddingTop(2).Text("Resumo por competência e convênio").Bold().FontSize(12).FontColor(AzulEscuro);
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.6f); c.RelativeColumn(3.4f); c.RelativeColumn(1.6f); c.RelativeColumn(2.2f); c.RelativeColumn(2.2f);
                        });
                        Cabecalho(t, ("Competência", false), ("Convênio", false), ("Nº", true), ("Repasse por consulta", true), ("Total", true));
                        var n = 0;
                        foreach (var g in vivas.GroupBy(l => (l.Competencia, l.Convenio))
                                     .OrderBy(g => g.Key.Competencia).ThenBy(g => g.Key.Convenio, Texto))
                        {
                            var r = RelatorioRepasseService.Resumir(medico.ProfissionalId, medico.Profissional, g.Key.Convenio, g.ToList());
                            var fundo = n++ % 2 == 1 ? FundoSuave : "#FFFFFF";
                            Celula(t, fundo).Text(Mes(g.Key.Competencia));
                            Celula(t, fundo).Text(r.Convenio);
                            Celula(t, fundo).AlignRight().Text(r.Quantidade.ToString(PtBr));
                            Celula(t, fundo).AlignRight().Text(r.ValorUnitario is { } u ? Dinheiro(u) : "variado");
                            Celula(t, fundo).AlignRight().Text(Dinheiro(r.Total)).SemiBold();
                        }
                        t.Cell().ColumnSpan(2).Background(FundoCabecalhoTabela).Padding(5).Text("Total").Bold();
                        t.Cell().Background(FundoCabecalhoTabela).Padding(5).AlignRight().Text(medico.Quantidade.ToString(PtBr)).Bold();
                        t.Cell().Background(FundoCabecalhoTabela).Padding(5).Text(string.Empty);
                        t.Cell().Background(FundoCabecalhoTabela).Padding(5).AlignRight().Text(Dinheiro(medico.Total)).Bold();
                    });

                    // ===== Detalhe: competência → fechamento → convênio · referência =====
                    col.Item().PaddingTop(4).Text("Detalhe dos recebimentos").Bold().FontSize(12).FontColor(AzulEscuro);
                    foreach (var fechamento in linhas.GroupBy(l => l.FechamentoId)
                                 .OrderBy(g => g.First().Competencia).ThenBy(g => g.Key))
                    {
                        var f = fechamento.First();
                        col.Item().Column(c =>
                        {
                            c.Spacing(4);
                            var titulo = $"Competência {Mes(f.Competencia)} · conferido por {f.ConferidoPor} em "
                                         + f.ConferidoEm.ToString("dd/MM/yyyy", PtBr)
                                         + (f.PagoEm is { } pago ? $" · pago em {pago.ToString("dd/MM/yyyy", PtBr)}" : f.Cancelado ? string.Empty : " · a pagar");
                            c.Item().BorderBottom(1).BorderColor(Azul).PaddingBottom(2)
                                .Text(titulo).SemiBold().FontSize(10).FontColor(f.Cancelado ? VermelhoForte : AzulEscuro);
                            if (f.Cancelado)
                                c.Item().Background(VermelhoSuave).Border(1).BorderColor(VermelhoForte).Padding(5)
                                    .Text("CANCELADO — este fechamento não vale e está fora dos totais.")
                                    .Bold().FontSize(9).FontColor(VermelhoForte);

                            foreach (var bloco in fechamento.GroupBy(l => (l.Convenio, Referencia: l.ReferenciaPagamento ?? string.Empty))
                                         .OrderBy(g => g.Key.Convenio, Texto).ThenBy(g => g.Key.Referencia, Texto))
                            {
                                c.Item().PaddingTop(2).Text(bloco.Key.Referencia.Length == 0
                                        ? bloco.Key.Convenio : $"{bloco.Key.Convenio} · {bloco.Key.Referencia}")
                                    .SemiBold().FontSize(9.5f).FontColor(f.Cancelado ? VermelhoForte : Azul);
                                c.Item().Table(t => Itens(t, bloco.ToList(), $"Total {bloco.Key.Convenio}", f.Cancelado));
                            }
                        });
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"{medico.Profissional} · Relatório de repasse {periodo}")
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

    private static void Itens(TableDescriptor t, IReadOnlyList<LinhaRelatorioRepasse> itens, string rotuloTotal, bool cancelado)
    {
        t.ColumnsDefinition(c =>
        {
            c.ConstantColumn(24); c.RelativeColumn(3.8f); c.RelativeColumn(1.7f); c.RelativeColumn(1.9f);
            c.RelativeColumn(1.6f); c.RelativeColumn(1.7f); c.RelativeColumn(1.7f);
        });
        Cabecalho(t, ("Nº", false), ("Beneficiário", false), ("Data atendimento", false), ("Tipo", false),
            ("Guia", false), ("Recebido em", false), ("Valor", true));

        var n = 0;
        foreach (var i in itens)
        {
            var estorno = i.Natureza == NaturezaItemRepasse.Estorno;
            var fundo = estorno ? VermelhoSuave : n % 2 == 1 ? FundoSuave : "#FFFFFF";
            var cor = estorno || cancelado ? VermelhoForte : TextoPrimario;
            n++;
            Celula(t, fundo).Text(n.ToString(PtBr)).FontColor(TextoSecundario);
            Celula(t, fundo).Text(i.Paciente).FontColor(cor);
            Celula(t, fundo).Text(i.DataAtendimento.ToString("dd/MM/yyyy", PtBr)).FontColor(cor);
            Celula(t, fundo).Text(estorno ? $"{i.Descricao} (estorno)" : i.Descricao).FontColor(cor);
            Celula(t, fundo).Text(i.NumeroGuia ?? "—").FontColor(cor);
            Celula(t, fundo).Text(i.DataRecebimento.ToString("dd/MM/yyyy", PtBr)).FontColor(cor);
            Celula(t, fundo).AlignRight().Text(Dinheiro(i.Valor)).FontColor(cor);
        }

        t.Cell().ColumnSpan(6).Background(FundoCabecalhoTabela).Padding(5)
            .Text(cancelado ? $"{rotuloTotal} (cancelado, fora do total)" : rotuloTotal).Bold();
        t.Cell().Background(FundoCabecalhoTabela).Padding(5).AlignRight().Text(Dinheiro(itens.Sum(i => i.Valor))).Bold();
    }

    private static void Cabecalho(TableDescriptor t, params (string Texto, bool Direita)[] colunas)
        => t.Header(h =>
        {
            foreach (var (texto, direita) in colunas)
            {
                var c = h.Cell().Background(FundoCabecalhoTabela).BorderBottom(1).BorderColor(Borda)
                    .PaddingVertical(5).PaddingHorizontal(4);
                (direita ? c.AlignRight() : c).Text(texto).SemiBold().FontSize(8).FontColor(TextoSecundario);
            }
        });

    private static IContainer Celula(TableDescriptor t, string fundo)
        => t.Cell().Background(fundo).BorderBottom(1).BorderColor(Borda).PaddingVertical(4).PaddingHorizontal(4);

    /// <summary>Os filtros além do período, por extenso; nulo quando não há nenhum.</summary>
    private static string? Filtros(FiltroRelatorioRepasse f)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.ConvenioCodigo)) partes.Add($"convênio {f.ConvenioCodigo}");
        if (f.Situacao != SituacaoNoRelatorioRepasse.ConferidosEPagos) partes.Add(RelatorioRepasseService.Situacao(f.Situacao));
        if (!string.IsNullOrWhiteSpace(f.Paciente)) partes.Add($"paciente contendo \"{f.Paciente.Trim()}\"");
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    private static string Mes(DateOnly d) => d.ToString("MM/yyyy", PtBr);
    private static string Dinheiro(decimal v) => v.ToString("C", PtBr);
}
