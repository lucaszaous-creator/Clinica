using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

/// <summary>
/// Imprime a guia do atendimento no leiaute padrão ANS (Guia de Consulta ou Guia de
/// SP/SADT) — para operadoras que ainda exigem a guia impressa/anexada. Campos numerados
/// como no formulário oficial; os não aplicáveis à clínica saem em branco.
/// </summary>
public sealed class GuiaTissPdfService
{
    // Tons neutros do formulário oficial + azul do design system nos títulos.
    private const string Azul = "#2563EB";
    private const string TextoPrimario = "#111827";
    private const string TextoSecundario = "#6B7280";
    private const string Borda = "#9CA3AF";
    private const string FundoRotulo = "#F1F5F9";

    private readonly IClinicaRepositorio _repo;

    static GuiaTissPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public GuiaTissPdfService(IClinicaRepositorio repo) => _repo = repo;

    public async Task<byte[]> GerarPdfAsync(int atendimentoId, DadosPrestador prestador, CancellationToken ct = default)
    {
        var atendimento = await _repo.ObterAtendimentoAsync(atendimentoId, ct)
            ?? throw new InvalidOperationException($"Atendimento {atendimentoId} não encontrado.");
        return GerarPdf(atendimento, prestador);
    }

    public byte[] GerarPdf(Atendimento atendimento, DadosPrestador prestador)
    {
        var codigos = atendimento.Codigos.Where(c => c.Status != StatusCodigo.NaoAplicavel).ToList();
        var somenteConsulta = codigos.Count > 0 &&
            codigos.All(c => c.Tipo is TipoCodigo.Consulta or TipoCodigo.ConsultaEspecialidade);
        var titulo = somenteConsulta ? "GUIA DE CONSULTA" : "GUIA DE SERVIÇO PROFISSIONAL / SADT";

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(t => t.FontSize(8).FontColor(TextoPrimario));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(titulo).FontSize(12).Bold().FontColor(Azul);
                    col.Item().Text($"Padrão TISS {TissExportService.VersaoPadrao}")
                        .FontSize(7).FontColor(TextoSecundario);
                });
                row.ConstantItem(220).Column(col =>
                {
                    col.Item().AlignRight().Text(prestador.NomeFantasia ?? prestador.RazaoSocial ?? string.Empty)
                        .FontSize(9).SemiBold();
                    col.Item().AlignRight().Text($"Registro ANS: {prestador.RegistroAnsOperadora}")
                        .FontSize(7).FontColor(TextoSecundario);
                });
            });

            page.Content().PaddingVertical(8).Column(col =>
            {
                col.Spacing(6);

                // 1. Identificação da guia
                col.Item().Row(row =>
                {
                    Campo(row.RelativeItem(), "1 - Registro ANS", prestador.RegistroAnsOperadora);
                    Campo(row.RelativeItem(), "2 - Nº guia no prestador",
                        codigos.FirstOrDefault(c => c.NumeroGuiaReal != null)?.NumeroGuiaReal ?? atendimento.Numero);
                    Campo(row.RelativeItem(), "3 - Data do atendimento", atendimento.Data.ToString("dd/MM/yyyy"));
                });

                // Dados do beneficiário
                Secao(col, "Dados do beneficiário");
                col.Item().Row(row =>
                {
                    Campo(row.RelativeItem(2), "4 - Nº da carteira",
                        atendimento.Paciente?.Carteirinha ?? atendimento.Paciente?.Documento);
                    Campo(row.RelativeItem(), "5 - Validade",
                        atendimento.Paciente?.ValidadeCarteirinha?.ToString("dd/MM/yyyy"));
                    Campo(row.RelativeItem(3), "6 - Nome",
                        atendimento.Paciente?.Nome);
                });

                // Dados do contratado
                Secao(col, "Dados do contratado executante");
                col.Item().Row(row =>
                {
                    Campo(row.RelativeItem(), "7 - Código na operadora", prestador.CodigoNaOperadora);
                    Campo(row.RelativeItem(2), "8 - Razão social", prestador.RazaoSocial);
                    Campo(row.RelativeItem(), "9 - CNPJ", prestador.Cnpj);
                    Campo(row.RelativeItem(), "10 - CNES", prestador.Cnes);
                });

                // Procedimentos
                Secao(col, somenteConsulta ? "Dados do atendimento" : "Procedimentos executados");
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(28);
                        c.ConstantColumn(70);
                        c.ConstantColumn(60);
                        c.ConstantColumn(85);
                        c.RelativeColumn();
                        c.ConstantColumn(45);
                    });

                    void Cab(string t) => table.Cell().Background(FundoRotulo).Border(0.5f).BorderColor(Borda)
                        .Padding(3).Text(t).FontSize(6.5f).SemiBold().FontColor(TextoSecundario);
                    Cab("Item");
                    Cab("Data");
                    Cab("Tabela");
                    Cab("Código TUSS");
                    Cab("Descrição");
                    Cab("Qtde");

                    var i = 0;
                    foreach (var c in codigos)
                    {
                        i++;
                        void Cel(string? t) => table.Cell().Border(0.5f).BorderColor(Borda)
                            .Padding(3).Text(t ?? string.Empty).FontSize(7.5f);
                        Cel(i.ToString());
                        Cel(atendimento.Data.ToString("dd/MM/yyyy"));
                        Cel("22");
                        Cel(prestador.CodigoTuss(c.Tipo));
                        Cel(c.Especialidade is { } esp ? $"{Rotulos.Tipo(c.Tipo)} — {esp}" : Rotulos.Tipo(c.Tipo));
                        Cel("1");
                    }
                });

                // Assinaturas
                col.Item().PaddingTop(18).Row(row =>
                {
                    row.Spacing(30);
                    Assinatura(row.RelativeItem(), "Assinatura do beneficiário");
                    Assinatura(row.RelativeItem(), "Assinatura do profissional executante");
                    Assinatura(row.RelativeItem(), "Data / carimbo da operadora");
                });
            });

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"Emitida em {DateTime.Now:dd/MM/yyyy HH:mm} — atendimento {atendimento.Numero}")
                    .FontSize(7).FontColor(TextoSecundario);
                row.ConstantItem(140).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7).FontColor(TextoSecundario));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Secao(ColumnDescriptor col, string titulo)
        => col.Item().PaddingTop(4).Text(titulo).FontSize(8).SemiBold().FontColor(Azul);

    private static void Campo(IContainer container, string rotulo, string? valor)
        => container.Border(0.5f).BorderColor(Borda).Column(c =>
        {
            c.Item().Background(FundoRotulo).PaddingHorizontal(4).PaddingVertical(1)
                .Text(rotulo).FontSize(6.5f).FontColor(TextoSecundario);
            c.Item().PaddingHorizontal(4).PaddingVertical(3)
                .Text(string.IsNullOrWhiteSpace(valor) ? " " : valor).FontSize(8.5f);
        });

    private static void Assinatura(IContainer container, string rotulo)
        => container.Column(c =>
        {
            c.Item().BorderBottom(0.8f).BorderColor(TextoPrimario).Height(24);
            c.Item().PaddingTop(2).AlignCenter().Text(rotulo).FontSize(7).FontColor(TextoSecundario);
        });

    /// <summary>Rótulos amigáveis dos tipos de código na guia impressa.</summary>
    private static class Rotulos
    {
        public static string Tipo(TipoCodigo tipo) => tipo switch
        {
            TipoCodigo.Consulta => "Consulta",
            TipoCodigo.Acupuntura => "Acupuntura",
            TipoCodigo.Eletroacupuntura => "Eletroacupuntura",
            TipoCodigo.Bsv => "Bloqueio Simpático Venoso",
            TipoCodigo.ConsultaEspecialidade => "Consulta de especialidade",
            _ => tipo.ToString()
        };
    }
}
