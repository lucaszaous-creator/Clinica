using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Regras;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

/// <summary>
/// Fechamento do período (semana ou mês): um PDF-resumo com a conferência que a
/// faturista faz antes de "encerrar" o período — taxa de baixa, quebra por convênio,
/// comparativo mensal, pendências vencidas nominais e glosas em aberto.
/// Objetivo: nada esquecido sai do período sem virar linha impressa na mesa de alguém.
/// </summary>
public sealed class FechamentoPdfService
{
    private const string Azul = "#2563EB";
    private const string AzulEscuro = "#1E3A8A";
    private const string TextoPrimario = "#111827";
    private const string TextoSecundario = "#6B7280";
    private const string Borda = "#E5E7EB";
    private const string FundoCabecalhoTabela = "#F1F5F9";
    private const string VerdeForte = "#15803D";
    private const string VermelhoForte = "#B91C1C";
    private const string VermelhoSuave = "#FEE2E2";
    private const string LaranjaForte = "#C2410C";

    private readonly IClinicaRepositorio _repo;
    private readonly RelatorioService _relatorios;

    public FechamentoPdfService(IClinicaRepositorio repo, RelatorioService relatorios)
    {
        _repo = repo;
        _relatorios = relatorios;
    }

    public async Task<byte[]> GerarAsync(DateOnly inicio, DateOnly fim, DadosPrestador prestador, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var relatorio = await _relatorios.GerarAsync(inicio, fim, hoje, ct);
        var comparativo = await _relatorios.ComparativoMensalAsync(fim, 6, ct);

        // Pendências vencidas (em aberto e com a data prevista já passada) — a lista nominal
        // que a secretária precisa atacar antes de considerar o período fechado.
        var emAberto = await _repo.CodigosEmAbertoAsync(ct);
        var vencidas = emAberto
            .Where(c => c.EstaPendente(hoje))
            .OrderBy(c => c.DataPrevistaFaturamento)
            .ToList();

        var glosasAbertas = (await _repo.CodigosGlosadosAsync(somenteEmAberto: true, ct)).ToList();

        var nomeClinica = string.IsNullOrWhiteSpace(prestador.NomeFantasia)
            ? (string.IsNullOrWhiteSpace(prestador.RazaoSocial) ? "Clínica" : prestador.RazaoSocial!)
            : prestador.NomeFantasia!;

        var periodoRotulo = $"{inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}";
        var tudoOk = vencidas.Count == 0 && glosasAbertas.Count == 0;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(TextoPrimario));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(nomeClinica).Bold().FontSize(15).FontColor(AzulEscuro);
                            c.Item().Text("FECHAMENTO DO PERÍODO").Bold().FontSize(12).FontColor(Azul);
                            c.Item().Text($"Período: {periodoRotulo}  ·  Emitido em {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(8.5f).FontColor(TextoSecundario);
                        });
                        row.ConstantItem(170).AlignRight().AlignMiddle()
                            .Background(tudoOk ? "#DCFCE7" : VermelhoSuave)
                            .PaddingVertical(6).PaddingHorizontal(12)
                            .Text(tudoOk ? "SEM PENDÊNCIAS VENCIDAS" : "HÁ ITENS A RESOLVER")
                            .Bold().FontSize(9)
                            .FontColor(tudoOk ? VerdeForte : VermelhoForte);
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Borda);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(14);

                    // ===== Resumo do período =====
                    col.Item().Element(e => Titulo(e, "Resumo do período"));
                    col.Item().Row(row =>
                    {
                        Kpi(row, "Códigos gerados", relatorio.Resumo.TotalCodigos.ToString());
                        Kpi(row, "Baixados", relatorio.Resumo.Baixados.ToString(), VerdeForte);
                        Kpi(row, "Pendentes", relatorio.Resumo.Pendentes.ToString(),
                            relatorio.Resumo.Pendentes > 0 ? LaranjaForte : TextoPrimario);
                        Kpi(row, "Taxa de baixa", $"{relatorio.Resumo.TaxaBaixa:0.#}%",
                            relatorio.Resumo.TaxaBaixa >= 90 ? VerdeForte : LaranjaForte);
                    });

                    // ===== Por convênio =====
                    col.Item().Element(e => Titulo(e, "Faturamento por convênio"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });
                        Cabecalho(t, "Convênio", "Gerados", "Baixados", "Pendentes", "Taxa");
                        foreach (var c in relatorio.PorConvenio)
                            Linha(t, ConvenioInfo.NomeExibicao(c.Convenio), c.TotalCodigos.ToString(),
                                c.Baixados.ToString(), c.Pendentes.ToString(), $"{c.TaxaBaixa:0.#}%");
                    });

                    // ===== Comparativo mensal =====
                    col.Item().Element(e => Titulo(e, "Evolução mensal (últimos 6 meses)"));
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });
                        Cabecalho(t, "Mês", "Gerados", "Baixados", "Pendentes", "Taxa");
                        foreach (var m in comparativo)
                            Linha(t, m.Rotulo, m.TotalCodigos.ToString(), m.Baixados.ToString(),
                                m.Pendentes.ToString(), $"{m.TaxaBaixa:0.#}%");
                    });

                    // ===== Pendências vencidas (nominais) =====
                    col.Item().Element(e => Titulo(e,
                        $"Pendências vencidas em aberto ({vencidas.Count})", vencidas.Count > 0 ? VermelhoForte : VerdeForte));
                    if (vencidas.Count == 0)
                    {
                        col.Item().Text("Nenhuma — todas as guias previstas até hoje foram baixadas.")
                            .FontColor(VerdeForte).FontSize(9.5f);
                    }
                    else
                    {
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn();
                            });
                            Cabecalho(t, "Paciente", "Convênio", "Código", "Previsto para", "Atraso");
                            foreach (var c in vencidas)
                            {
                                var atraso = hoje.DayNumber - c.DataPrevistaFaturamento.DayNumber;
                                Linha(t,
                                    c.Atendimento?.Paciente?.Nome ?? "?",
                                    ConvenioInfo.NomeExibicao(c.Atendimento?.Paciente?.Convenio ?? default),
                                    c.Tipo.ToString(),
                                    c.DataPrevistaFaturamento.ToString("dd/MM/yyyy"),
                                    $"{atraso} d");
                            }
                        });
                    }

                    // ===== Glosas em aberto =====
                    col.Item().Element(e => Titulo(e,
                        $"Glosas em aberto ({glosasAbertas.Count})", glosasAbertas.Count > 0 ? LaranjaForte : VerdeForte));
                    if (glosasAbertas.Count == 0)
                    {
                        col.Item().Text("Nenhuma glosa aguardando recurso ou recuperação.")
                            .FontColor(VerdeForte).FontSize(9.5f);
                    }
                    else
                    {
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(3);
                            });
                            Cabecalho(t, "Paciente", "Nº guia", "Glosada em", "Motivo");
                            foreach (var g in glosasAbertas)
                                Linha(t,
                                    g.Atendimento?.Paciente?.Nome ?? "?",
                                    g.NumeroGuiaReal ?? "—",
                                    g.DataGlosa?.ToString("dd/MM/yyyy") ?? "—",
                                    g.MotivoGlosa ?? "—");
                        });
                    }
                });

                page.Footer().AlignCenter()
                    .Text(t =>
                    {
                        t.Span("Página ").FontSize(8).FontColor(TextoSecundario);
                        t.CurrentPageNumber().FontSize(8).FontColor(TextoSecundario);
                        t.Span(" de ").FontSize(8).FontColor(TextoSecundario);
                        t.TotalPages().FontSize(8).FontColor(TextoSecundario);
                    });
            });
        }).GeneratePdf();
    }

    private static void Titulo(IContainer container, string texto, string cor = AzulEscuro)
        => container.Text(texto).Bold().FontSize(11).FontColor(cor);

    private static void Kpi(RowDescriptor row, string rotulo, string valor, string cor = TextoPrimario)
        => row.RelativeItem().Border(1).BorderColor(Borda).Padding(8).Column(c =>
        {
            c.Item().Text(rotulo).FontSize(8).FontColor(TextoSecundario);
            c.Item().Text(valor).Bold().FontSize(14).FontColor(cor);
        });

    private static void Cabecalho(TableDescriptor t, params string[] textos)
    {
        t.Header(h =>
        {
            foreach (var texto in textos)
                h.Cell().Background(FundoCabecalhoTabela).Padding(5)
                    .Text(texto).Bold().FontSize(8.5f).FontColor(TextoSecundario);
        });
    }

    private static void Linha(TableDescriptor t, params string[] valores)
    {
        foreach (var v in valores)
            t.Cell().BorderBottom(1).BorderColor(Borda).Padding(5)
                .Text(v).FontSize(9);
    }
}
