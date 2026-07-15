using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

/// <summary>
/// Gera a "Capa de Faturamento" em PDF de um atendimento — o documento/lastro que inicia o
/// processo de faturamento (número do atendimento, paciente, convênio, códigos/guias, assinatura).
/// </summary>
public sealed class CapaFaturamentoService
{
    private readonly IClinicaRepositorio _repo;

    static CapaFaturamentoService()
    {
        // Licença Community do QuestPDF (gratuita para pequenas empresas).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public CapaFaturamentoService(IClinicaRepositorio repo) => _repo = repo;

    public async Task<byte[]> GerarPdfAsync(int atendimentoId, CancellationToken ct = default)
    {
        var atendimento = await _repo.ObterAtendimentoAsync(atendimentoId, ct)
            ?? throw new InvalidOperationException($"Atendimento {atendimentoId} não encontrado.");
        return GerarPdf(atendimento);
    }

    public byte[] GerarPdf(Atendimento atendimento)
    {
        var paciente = atendimento.Paciente;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("CAPA DE FATURAMENTO").Bold().FontSize(16).FontColor(Colors.Green.Darken3);
                    col.Item().Text($"Atendimento nº {atendimento.Numero ?? atendimento.Id.ToString()}")
                        .FontSize(12).Bold();
                    col.Item().PaddingBottom(6).Text($"Data: {atendimento.Data:dd/MM/yyyy}   ·   Emitido em {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontColor(Colors.Grey.Darken1);
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(8);

                    // Dados do paciente
                    col.Item().Text("Paciente").Bold().FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"{paciente?.Nome}");
                    col.Item().Text(t =>
                    {
                        t.Span("CPF: ").SemiBold();
                        t.Span(Cpf.Formatar(paciente?.Documento));
                        t.Span("     Convênio: ").SemiBold();
                        t.Span(paciente is null ? "" : ConvenioInfo.NomeExibicao(paciente.Convenio));
                        t.Span("     Categoria: ").SemiBold();
                        t.Span(atendimento.Categoria.ToString());
                    });

                    // Tabela de códigos/guias
                    col.Item().PaddingTop(6).Text("Códigos / guias").Bold().FontColor(Colors.Grey.Darken2);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3); // código
                            c.RelativeColumn(2); // ordem
                            c.RelativeColumn(2); // faturar em
                            c.RelativeColumn(3); // como obter
                            c.RelativeColumn(3); // situação
                        });

                        void HeaderCell(string s) => table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(s).SemiBold();
                        HeaderCell("Código"); HeaderCell("Ordem"); HeaderCell("Faturar em"); HeaderCell("Como obter"); HeaderCell("Situação");

                        foreach (var c in atendimento.Codigos)
                        {
                            table.Cell().Padding(4).Text(c.Tipo.ToString());
                            table.Cell().Padding(4).Text(c.Ordem.ToString());
                            table.Cell().Padding(4).Text(c.DataPrevistaFaturamento.ToString("dd/MM/yyyy"));
                            table.Cell().Padding(4).Text(c.FormaObtencao.ToString());
                            table.Cell().Padding(4).Text(c.Baixado ? $"Baixado ({c.NumeroGuiaReal})" : "Aberto");
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(atendimento.Observacoes))
                        col.Item().PaddingTop(6).Text($"Observações: {atendimento.Observacoes}");

                    // Assinaturas / lastro
                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(1);
                            c.Item().Text("Responsável pelo faturamento").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(30);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(1);
                            c.Item().Text("Conferência").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Documento gerado pelo sistema da clínica — lastro de faturamento.")
                        .FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }
}
