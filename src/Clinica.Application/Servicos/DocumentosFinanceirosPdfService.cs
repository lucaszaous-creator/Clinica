using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

/// <summary>
/// Recibo e orçamento em PDF, no leiaute da marca SemDor — os dois documentos que
/// fechavam a página 21 da proposta.
///
/// Separado do <see cref="DocumentosClinicosPdfService"/> de propósito: aqueles são
/// documentos de saúde e estes são de dinheiro. O que muda não é o cabeçalho, é o miolo
/// — valores, total por extenso no recibo, prazo de validade no orçamento — e misturar
/// os dois num único serviço faria cada mudança de um arriscar o outro.
/// </summary>
public sealed class DocumentosFinanceirosPdfService
{
    private const string Azul = MarcaSemDor.Azul;
    private const string AzulEscuro = MarcaSemDor.Navy;
    private const string TextoPrimario = "#111827";
    private const string TextoSecundario = "#6B7280";
    private const string Borda = "#E5E7EB";
    private const string FundoSuave = "#F8FAFC";
    private const string FundoCabecalhoTabela = "#F1F5F9";
    private const string VermelhoForte = "#B91C1C";
    private const string VermelhoSuave = "#FEE2E2";

    private readonly IClinicaRepositorio _repo;

    public DocumentosFinanceirosPdfService(IClinicaRepositorio repo) => _repo = repo;

    public async Task<byte[]> GerarAsync(
        int documentoId, DadosPrestador? prestador = null, CancellationToken ct = default)
    {
        var documento = await _repo.ObterDocumentoFinanceiroAsync(documentoId, ct)
            ?? throw new InvalidOperationException($"Documento {documentoId} não encontrado.");

        return Gerar(documento, prestador);
    }

    public byte[] Gerar(DocumentoFinanceiro documento, DadosPrestador? prestador = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var nomeClinica = PrimeiroPreenchido(prestador?.NomeFantasia, prestador?.RazaoSocial) ?? "Clínica";
        var itens = documento.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id).ToList();
        var ehRecibo = documento.Tipo == TipoDocumentoFinanceiro.Recibo;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(TextoPrimario));

                page.Header().Column(col =>
                {
                    if (MarcaSemDor.Logo is { } logo)
                        col.Item().PaddingBottom(8).Width(130).Image(logo);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text(nomeClinica).Bold().FontSize(15).FontColor(AzulEscuro);

                            var registro = JuntarComSeparador(
                                Prefixado("CNPJ ", prestador?.Cnpj),
                                Prefixado("CNES ", prestador?.Cnes));
                            if (registro is not null)
                                c.Item().Text(registro).FontSize(8.5f).FontColor(TextoSecundario);

                            if (!string.IsNullOrWhiteSpace(prestador?.Endereco))
                                c.Item().Text(prestador!.Endereco!).FontSize(8.5f).FontColor(TextoSecundario);

                            var contato = JuntarComSeparador(prestador?.Telefone, prestador?.Email);
                            if (contato is not null)
                                c.Item().Text(contato).FontSize(8.5f).FontColor(TextoSecundario);
                        });

                        row.RelativeItem(2).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text(documento.TituloImpresso.ToUpperInvariant())
                                .Bold().FontSize(14).FontColor(Azul);

                            if (documento.Cancelado)
                                c.Item().AlignRight().PaddingTop(4)
                                    .Background(VermelhoSuave).PaddingVertical(3).PaddingHorizontal(10)
                                    .Text("CANCELADO").Bold().FontSize(9).FontColor(VermelhoForte);

                            c.Item().AlignRight().PaddingTop(6)
                                .Text($"Nº {documento.Numero}").Bold().FontSize(11);
                            c.Item().AlignRight()
                                .Text($"Emitido em {documento.Data:dd/MM/yyyy}")
                                .FontSize(8.5f).FontColor(TextoSecundario);
                        });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(2).LineColor(Azul);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    if (documento.Cancelado)
                        col.Item().Background(VermelhoSuave).Border(1).BorderColor(VermelhoForte)
                            .Padding(10).Text(t =>
                            {
                                t.Span("DOCUMENTO CANCELADO  ").Bold().FontSize(9).FontColor(VermelhoForte);
                                t.Span($"em {documento.CanceladoEm:dd/MM/yyyy} — "
                                       + (documento.MotivoCancelamento ?? "sem motivo registrado"))
                                    .FontSize(9.5f).FontColor(VermelhoForte);
                            });

                    // ===== Destinatário =====
                    col.Item().Background(FundoSuave).Border(1).BorderColor(Borda).Padding(12).Column(c =>
                    {
                        c.Item().Text(ehRecibo ? "RECEBEMOS DE" : "PARA")
                            .Bold().FontSize(8).FontColor(TextoSecundario).LetterSpacing(0.08f);
                        c.Item().PaddingTop(2).Text(documento.Destinatario).Bold().FontSize(12);

                        if (!string.IsNullOrWhiteSpace(documento.DocumentoDestinatario))
                            c.Item().PaddingTop(2).Text(t =>
                            {
                                t.Span("CPF/CNPJ  ").FontSize(8.5f).FontColor(TextoSecundario);
                                t.Span(Cpf.Formatar(documento.DocumentoDestinatario)).FontSize(9.5f);
                            });
                    });

                    if (!string.IsNullOrWhiteSpace(documento.Corpo))
                        col.Item().Text(documento.Corpo!).FontSize(10.5f);

                    // ===== Itens =====
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(6);    // descrição
                            c.RelativeColumn(1.4f); // quantidade
                            c.RelativeColumn(2);    // unitário
                            c.RelativeColumn(2);    // total
                        });

                        void HeaderCell(string s, bool direita = false)
                        {
                            var celula = table.Cell()
                                .Background(FundoCabecalhoTabela).BorderBottom(1).BorderColor(Borda)
                                .PaddingVertical(6).PaddingHorizontal(6);
                            (direita ? celula.AlignRight() : celula)
                                .Text(s).SemiBold().FontSize(9).FontColor(TextoSecundario);
                        }

                        HeaderCell("Descrição");
                        HeaderCell("Qtd.", direita: true);
                        HeaderCell("Valor unit.", direita: true);
                        HeaderCell("Total", direita: true);

                        var linha = 0;
                        foreach (var item in itens)
                        {
                            var fundo = linha++ % 2 == 1 ? FundoSuave : "#FFFFFF";

                            IContainer Cell() => table.Cell().Background(fundo)
                                .BorderBottom(1).BorderColor(Borda)
                                .PaddingVertical(6).PaddingHorizontal(6);

                            Cell().Text(item.Descricao).FontSize(9.5f);
                            Cell().AlignRight().Text($"{item.Quantidade:0.##}").FontSize(9.5f);
                            Cell().AlignRight().Text($"{item.ValorUnitario:C}").FontSize(9.5f);
                            Cell().AlignRight().Text($"{item.ValorTotal:C}").SemiBold().FontSize(9.5f);
                        }
                    });

                    // ===== Total =====
                    col.Item().PaddingTop(2).Row(row =>
                    {
                        row.RelativeItem();
                        row.ConstantItem(230).Background(FundoSuave).Border(1).BorderColor(Borda)
                            .Padding(10).Row(r =>
                            {
                                r.RelativeItem().Text(ehRecibo ? "Valor recebido" : "Total do orçamento")
                                    .SemiBold().FontSize(9.5f).FontColor(TextoSecundario);
                                r.ConstantItem(110).AlignRight()
                                    .Text($"{documento.ValorTotal:C}").Bold().FontSize(13)
                                    .FontColor(AzulEscuro);
                            });
                    });

                    if (documento.FormaPagamento is { } forma)
                        col.Item().Text(t =>
                        {
                            t.Span("Forma de pagamento  ").FontSize(8.5f).FontColor(TextoSecundario);
                            t.Span(RotularForma(forma)).FontSize(9.5f);
                        });

                    if (!ehRecibo && documento.ValidoAte is { } validade)
                        col.Item().Border(1).BorderColor(Borda).Padding(10).Text(t =>
                        {
                            t.Span("Este orçamento é válido até ").FontSize(10);
                            t.Span($"{validade:dd/MM/yyyy}").Bold().FontSize(10);
                            t.Span(". Depois dessa data os valores podem mudar.").FontSize(10);
                        });

                    if (!string.IsNullOrWhiteSpace(documento.Observacoes))
                        col.Item().Background(FundoSuave).Border(1).BorderColor(Borda).Padding(10).Text(t =>
                        {
                            t.Span("Observações  ").Bold().FontSize(8.5f).FontColor(TextoSecundario);
                            t.Span(documento.Observacoes!).FontSize(9.5f);
                        });

                    // ===== Assinatura =====
                    col.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem();
                        row.RelativeItem(2).Column(c =>
                        {
                            c.Item().LineHorizontal(1).LineColor(TextoSecundario);
                            c.Item().PaddingTop(3).AlignCenter().Text(nomeClinica)
                                .SemiBold().FontSize(9.5f);
                            c.Item().AlignCenter()
                                .Text(ehRecibo ? "Assinatura e carimbo" : "Responsável pelo orçamento")
                                .FontSize(8.5f).FontColor(TextoSecundario);
                        });
                        row.RelativeItem();
                    });
                });

                page.Footer().Column(col =>
                {
                    col.Item().PaddingBottom(4).LineHorizontal(0.75f).LineColor(Borda);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("Conferência  ").FontSize(8).FontColor(TextoSecundario);
                            t.Span(documento.CodigoVerificacao).SemiBold().FontSize(8.5f);
                        });
                        row.ConstantItem(90).AlignRight().Text(t =>
                        {
                            t.Span("Página ").FontSize(8).FontColor(TextoSecundario);
                            t.CurrentPageNumber().FontSize(8).FontColor(TextoSecundario);
                            t.Span(" de ").FontSize(8).FontColor(TextoSecundario);
                            t.TotalPages().FontSize(8).FontColor(TextoSecundario);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static string RotularForma(FormaPagamento forma) => forma switch
    {
        FormaPagamento.Dinheiro => "Dinheiro",
        FormaPagamento.Pix => "PIX",
        FormaPagamento.CartaoDebito => "Cartão de débito",
        FormaPagamento.CartaoCredito => "Cartão de crédito",
        FormaPagamento.Transferencia => "Transferência",
        FormaPagamento.Boleto => "Boleto",
        FormaPagamento.Convenio => "Convênio",
        _ => "Outro"
    };

    private static string? PrimeiroPreenchido(params string?[] valores)
        => valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? Prefixado(string prefixo, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : prefixo + valor;

    private static string? JuntarComSeparador(params string?[] partes)
    {
        var texto = string.Join("  ·  ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(texto) ? null : texto;
    }
}
