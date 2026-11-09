using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

/// <summary>Uma tabela do relatório impresso: o título da seção, as colunas e as linhas já formatadas.</summary>
/// <param name="Titulo">O que a seção é ("Dra. Ana — 12 atendimentos"). Vazio = sem título.</param>
/// <param name="Colunas">Os cabeçalhos, na ordem.</param>
/// <param name="Pesos">A largura relativa de cada coluna (mesmo tamanho de <paramref name="Colunas"/>).</param>
/// <param name="Linhas">As células, já em texto pt-BR — o PDF não formata nada.</param>
/// <param name="Rodape">Uma linha de total ao pé da tabela (nula = sem total).</param>
public sealed record SecaoRelatorioPdf(
    string Titulo,
    IReadOnlyList<string> Colunas,
    IReadOnlyList<float> Pesos,
    IReadOnlyList<IReadOnlyList<string>> Linhas,
    IReadOnlyList<string>? Rodape = null);

/// <summary>Um número do topo do relatório ("Guias pendentes: 42").</summary>
public sealed record IndicadorRelatorioPdf(string Rotulo, string Valor);

/// <summary>
/// O relatório IMPRESSO de lista (nov/2026, pedidos da gerência: "emitir e imprimir relatórios
/// de guias pendentes" e "relatório de atendimentos por médico com os valores"). Um só desenho
/// para os dois — paisagem, cabeçalho da clínica, o RECORTE escrito por extenso (papel sem filtro
/// declarado é papel que alguém lê como "tudo"), números do topo e as tabelas, com página X de Y.
///
/// Recebe tudo já em texto: a decisão do que entra e de como se escreve mora no serviço de cada
/// relatório (com teste), e este arquivo só desenha — o mesmo padrão do <see cref="FechamentoPdfService"/>.
/// </summary>
public static class RelatorioListaPdf
{
    private const string TextoPrimario = "#111827";
    private const string TextoSecundario = "#6B7280";
    private const string Borda = "#E5E7EB";
    private const string FundoCabecalhoTabela = "#F1F5F9";

    public static byte[] Gerar(
        string nomeClinica,
        string titulo,
        string recorte,
        IReadOnlyList<IndicadorRelatorioPdf> indicadores,
        IReadOnlyList<SecaoRelatorioPdf> secoes,
        string? semLinhas = null,
        DateTime? emitidoEm = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var emissao = emitidoEm ?? DateTime.Now;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(TextoPrimario));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        if (MarcaSemDor.Logo is { } logo)
                            row.ConstantItem(110).PaddingRight(12).AlignMiddle().Image(logo);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(string.IsNullOrWhiteSpace(nomeClinica) ? "Clínica" : nomeClinica)
                                .Bold().FontSize(13).FontColor(MarcaSemDor.Navy);
                            c.Item().Text(titulo.ToUpperInvariant()).Bold().FontSize(11).FontColor(MarcaSemDor.Azul);
                            c.Item().Text($"{recorte}  ·  Emitido em {emissao:dd/MM/yyyy HH:mm}")
                                .FontSize(8).FontColor(TextoSecundario);
                        });
                    });
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Borda);
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    col.Spacing(10);
                    if (indicadores.Count > 0)
                    {
                        col.Item().Row(row =>
                        {
                            row.Spacing(8);
                            foreach (var i in indicadores)
                                row.RelativeItem().Border(1).BorderColor(Borda).Padding(6).Column(c =>
                                {
                                    c.Item().Text(i.Rotulo).FontSize(7.5f).FontColor(TextoSecundario);
                                    c.Item().Text(i.Valor).Bold().FontSize(12);
                                });
                        });
                    }

                    if (secoes.All(s => s.Linhas.Count == 0))
                    {
                        col.Item().PaddingTop(10).Text(semLinhas ?? "Nenhuma linha neste recorte.")
                            .FontSize(10).FontColor(TextoSecundario);
                        return;
                    }

                    foreach (var s in secoes.Where(s => s.Linhas.Count > 0))
                    {
                        col.Item().Column(sec =>
                        {
                            if (!string.IsNullOrWhiteSpace(s.Titulo))
                                sec.Item().PaddingBottom(3).Text(s.Titulo).Bold().FontSize(10).FontColor(MarcaSemDor.Navy);
                            sec.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    for (var i = 0; i < s.Colunas.Count; i++)
                                        c.RelativeColumn(i < s.Pesos.Count ? s.Pesos[i] : 1);
                                });
                                t.Header(h =>
                                {
                                    foreach (var texto in s.Colunas)
                                        h.Cell().Background(FundoCabecalhoTabela).Padding(4)
                                            .Text(texto).Bold().FontSize(8).FontColor(TextoSecundario);
                                });
                                foreach (var linha in s.Linhas)
                                    for (var i = 0; i < s.Colunas.Count; i++)
                                        t.Cell().BorderBottom(1).BorderColor(Borda).Padding(4)
                                            .Text(i < linha.Count ? linha[i] : string.Empty).FontSize(8.5f);
                                if (s.Rodape is { } rodape)
                                    for (var i = 0; i < s.Colunas.Count; i++)
                                        t.Cell().Background(FundoCabecalhoTabela).Padding(4)
                                            .Text(i < rodape.Count ? rodape[i] : string.Empty).Bold().FontSize(8.5f);
                            });
                        });
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Página ").FontSize(7.5f).FontColor(TextoSecundario);
                    t.CurrentPageNumber().FontSize(7.5f).FontColor(TextoSecundario);
                    t.Span(" de ").FontSize(7.5f).FontColor(TextoSecundario);
                    t.TotalPages().FontSize(7.5f).FontColor(TextoSecundario);
                });
            });
        }).GeneratePdf();
    }
}
