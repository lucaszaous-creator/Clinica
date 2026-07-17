using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

/// <summary>
/// Gera a "Capa de Faturamento" em PDF de um atendimento — o documento/lastro que inicia o
/// processo de faturamento. Visual alinhado ao design system do app (azul #2563EB, flat,
/// cinzas frios), com os dados da clínica (configurados em Guias TISS → Dados do prestador).
/// </summary>
public sealed class CapaFaturamentoService
{
    // Tokens do design system (Styles/Tokens.xaml / tokens/colors.css)
    private const string Azul = "#2563EB";
    private const string AzulEscuro = "#1E3A8A";
    private const string TextoPrimario = "#111827";
    private const string TextoSecundario = "#6B7280";
    private const string Borda = "#E5E7EB";
    private const string FundoSuave = "#F8FAFC";
    private const string FundoCabecalhoTabela = "#F1F5F9";
    private const string VerdeForte = "#15803D";
    private const string VerdeSuave = "#DCFCE7";
    private const string LaranjaForte = "#C2410C";
    private const string LaranjaSuave = "#FFEDD5";

    private readonly IClinicaRepositorio _repo;

    static CapaFaturamentoService()
    {
        // Licença Community do QuestPDF (gratuita para pequenas empresas).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public CapaFaturamentoService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>A fatura está concluída quando todos os códigos faturáveis já tiveram baixa.</summary>
    public static bool EstaConcluido(Atendimento atendimento)
    {
        var faturaveis = atendimento.Codigos.Where(c => c.Status != StatusCodigo.NaoAplicavel).ToList();
        return faturaveis.Count > 0 && faturaveis.All(c => c.Baixado);
    }

    public async Task<byte[]> GerarPdfAsync(int atendimentoId, DadosPrestador? prestador = null, CancellationToken ct = default)
    {
        var atendimento = await _repo.ObterAtendimentoAsync(atendimentoId, ct)
            ?? throw new InvalidOperationException($"Atendimento {atendimentoId} não encontrado.");
        return GerarPdf(atendimento, prestador);
    }

    /// <summary>Gera a capa de conclusão se a fatura estiver concluída (usado ao dar a baixa da última guia).</summary>
    public async Task<CapaConclusao> GerarConclusaoAsync(int atendimentoId, DadosPrestador? prestador = null, CancellationToken ct = default)
    {
        var atendimento = await _repo.ObterAtendimentoAsync(atendimentoId, ct)
            ?? throw new InvalidOperationException($"Atendimento {atendimentoId} não encontrado.");
        var concluido = EstaConcluido(atendimento);
        return new CapaConclusao(concluido, atendimento.Numero, atendimento.Data,
            concluido ? GerarPdf(atendimento, prestador) : null);
    }

    public byte[] GerarPdf(Atendimento atendimento, DadosPrestador? prestador = null)
    {
        var paciente = atendimento.Paciente;
        var concluido = EstaConcluido(atendimento);
        var nomeClinica = PrimeiroPreenchido(prestador?.NomeFantasia, prestador?.RazaoSocial) ?? "Clínica";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(TextoPrimario));

                // ===== Cabeçalho: clínica à esquerda, documento à direita =====
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        // Bloco da clínica
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text(nomeClinica).Bold().FontSize(15).FontColor(AzulEscuro);

                            if (!string.IsNullOrWhiteSpace(prestador?.NomeFantasia) &&
                                !string.IsNullOrWhiteSpace(prestador?.RazaoSocial) &&
                                prestador!.NomeFantasia != prestador.RazaoSocial)
                                c.Item().Text(prestador.RazaoSocial!).FontSize(8.5f).FontColor(TextoSecundario);

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

                        // Bloco do documento
                        row.RelativeItem(2).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text("CAPA DE FATURAMENTO").Bold().FontSize(14).FontColor(Azul);

                            // Pílula de status (tint suave + texto forte, como os badges do app)
                            c.Item().AlignRight().PaddingTop(4)
                                .Background(concluido ? VerdeSuave : LaranjaSuave)
                                .PaddingVertical(3).PaddingHorizontal(10)
                                .Text(concluido ? "FATURA CONCLUÍDA" : "EM ANDAMENTO")
                                .Bold().FontSize(9)
                                .FontColor(concluido ? VerdeForte : LaranjaForte);

                            c.Item().AlignRight().PaddingTop(6)
                                .Text($"Atendimento nº {atendimento.Numero ?? atendimento.Id.ToString()}")
                                .Bold().FontSize(11);
                            c.Item().AlignRight()
                                .Text($"Atendido em {atendimento.Data:dd/MM/yyyy}  ·  Emitido em {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(8.5f).FontColor(TextoSecundario);
                        });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(2).LineColor(Azul);
                });

                // ===== Conteúdo =====
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    // Painel do paciente (bloco suave, como um Card do app)
                    col.Item().Background(FundoSuave).Border(1).BorderColor(Borda).Padding(12).Column(c =>
                    {
                        c.Item().Text("PACIENTE").Bold().FontSize(8).FontColor(TextoSecundario).LetterSpacing(0.08f);
                        c.Item().PaddingTop(2).Text(paciente?.Nome ?? "(desconhecido)").Bold().FontSize(12);
                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("CPF  ").FontSize(8.5f).FontColor(TextoSecundario);
                                t.Span(Cpf.Formatar(paciente?.Documento)).FontSize(9.5f);
                            });
                            r.RelativeItem(2).Text(t =>
                            {
                                t.Span("Convênio  ").FontSize(8.5f).FontColor(TextoSecundario);
                                t.Span(paciente is null ? "—" : ConvenioInfo.NomeExibicao(paciente.Convenio)).FontSize(9.5f);
                            });
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Categoria  ").FontSize(8.5f).FontColor(TextoSecundario);
                                t.Span(atendimento.Categoria.ToString()).FontSize(9.5f);
                            });
                        });
                    });

                    // Tabela de códigos/guias
                    col.Item().Text("Códigos / guias").Bold().FontSize(11);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);    // código
                            c.RelativeColumn(1.2f); // ordem
                            c.RelativeColumn(2);    // faturar em
                            c.RelativeColumn(3);    // como obter
                            c.RelativeColumn(3);    // situação
                        });

                        void HeaderCell(string s) => table.Cell()
                            .Background(FundoCabecalhoTabela)
                            .BorderBottom(1).BorderColor(Borda)
                            .PaddingVertical(6).PaddingHorizontal(6)
                            .Text(s).SemiBold().FontSize(9).FontColor(TextoSecundario);

                        HeaderCell("Código"); HeaderCell("Ordem"); HeaderCell("Faturar em");
                        HeaderCell("Como obter"); HeaderCell("Situação");

                        var linha = 0;
                        foreach (var c in atendimento.Codigos)
                        {
                            var fundo = linha++ % 2 == 1 ? FundoSuave : "#FFFFFF";

                            IContainer Cell() => table.Cell().Background(fundo)
                                .BorderBottom(1).BorderColor(Borda)
                                .PaddingVertical(6).PaddingHorizontal(6);

                            Cell().Text(RotuloTipo(c.Tipo)).FontSize(9.5f);
                            Cell().Text(c.Ordem == OrdemCodigo.Primeiro ? "1º" : "2º").FontSize(9.5f);
                            Cell().Text(c.DataPrevistaFaturamento.ToString("dd/MM/yyyy")).FontSize(9.5f);
                            Cell().Text(RotuloForma(c.FormaObtencao)).FontSize(9.5f);

                            var situacao = Cell();
                            if (c.Baixado)
                                situacao.Text($"Baixado · guia {c.NumeroGuiaReal}").FontSize(9.5f).SemiBold().FontColor(VerdeForte);
                            else if (c.Status == StatusCodigo.NaoAplicavel)
                                situacao.Text("Não aplicável").FontSize(9.5f).FontColor(TextoSecundario);
                            else
                                situacao.Text("Aberto").FontSize(9.5f).SemiBold().FontColor(LaranjaForte);
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(atendimento.Observacoes))
                        col.Item().Background(FundoSuave).Border(1).BorderColor(Borda).Padding(10).Text(t =>
                        {
                            t.Span("Observações  ").Bold().FontSize(8.5f).FontColor(TextoSecundario);
                            t.Span(atendimento.Observacoes!).FontSize(9.5f);
                        });

                    // Assinaturas / lastro
                    col.Item().PaddingTop(36).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(1).LineColor(TextoSecundario);
                            c.Item().PaddingTop(3).AlignCenter().Text("Responsável pelo faturamento")
                                .FontSize(8.5f).FontColor(TextoSecundario);
                        });
                        row.ConstantItem(40);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(1).LineColor(TextoSecundario);
                            c.Item().PaddingTop(3).AlignCenter().Text("Conferência")
                                .FontSize(8.5f).FontColor(TextoSecundario);
                        });
                    });
                });

                // ===== Rodapé =====
                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Borda);
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text($"{nomeClinica} — lastro de faturamento gerado pelo sistema.")
                            .FontSize(7.5f).FontColor(TextoSecundario);
                        row.ConstantItem(80).AlignRight().Text(t =>
                        {
                            t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(TextoSecundario));
                            t.Span("Página ");
                            t.CurrentPageNumber();
                            t.Span(" de ");
                            t.TotalPages();
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    // ===== Rótulos amigáveis (mesma linguagem do EnumDescricaoConverter do app) =====

    private static string RotuloTipo(TipoCodigo t) => t switch
    {
        TipoCodigo.ConsultaEspecialidade => "Consulta de especialidade",
        TipoCodigo.Bsv => "BSV",
        _ => t.ToString()
    };

    private static string RotuloForma(FormaObtencao f) => f switch
    {
        FormaObtencao.NaoAplica => "—",
        FormaObtencao.App => "Pelo app (QR Code)",
        FormaObtencao.Sistema => "Pelo sistema",
        FormaObtencao.Ligacao => "Ligar para o paciente",
        _ => f.ToString()
    };

    private static string? PrimeiroPreenchido(params string?[] valores)
        => valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? Prefixado(string prefixo, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : prefixo + valor;

    private static string? JuntarComSeparador(params string?[] partes)
    {
        var preenchidas = partes.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return preenchidas.Count == 0 ? null : string.Join("  ·  ", preenchidas);
    }
}

/// <summary>Resultado da tentativa de gerar a capa de conclusão após a baixa.</summary>
public sealed record CapaConclusao(bool Concluido, string? Numero, DateOnly Data, byte[]? Pdf);
