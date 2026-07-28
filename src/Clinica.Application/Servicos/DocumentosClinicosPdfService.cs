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
/// Os sete documentos clínicos da página 21 da proposta, em PDF no leiaute da marca
/// SemDor: receita, atestado, declaração de comparecimento, pedido de exame, relatório
/// de evolução, termo de consentimento e anamnese.
///
/// É UM serviço para os sete porque o papel é o mesmo: cabeçalho da clínica,
/// identificação do paciente, corpo, linhas, carimbo e assinatura. O que muda é o miolo
/// — e um arquivo por documento seria sete cópias do mesmo cabeçalho para manter em dia.
///
/// O que ele NÃO faz: assinatura digital com certificado ICP-Brasil. O que sai impresso
/// é o carimbo do profissional (nome e registro no conselho), a linha de assinatura e um
/// CÓDIGO DE CONFERÊNCIA que permite achar o documento no sistema e comparar com o
/// papel. Chamar isso de assinatura digital seria mentir sobre o que a via garante.
/// </summary>
public sealed class DocumentosClinicosPdfService
{
    private const string Azul = MarcaSemDor.Azul;
    private const string AzulEscuro = MarcaSemDor.Navy;
    private const string TextoPrimario = "#111827";
    private const string TextoSecundario = "#6B7280";
    private const string Borda = "#E5E7EB";
    private const string FundoSuave = "#F8FAFC";
    private const string FundoCabecalhoTabela = "#F1F5F9";
    private const string VerdeForte = "#15803D";
    private const string VermelhoForte = "#B91C1C";
    private const string VermelhoSuave = "#FEE2E2";

    private readonly IClinicaRepositorio _repo;

    public DocumentosClinicosPdfService(IClinicaRepositorio repo) => _repo = repo;

    public async Task<byte[]> GerarAsync(
        int documentoId, DadosPrestador? prestador = null, CancellationToken ct = default)
    {
        var documento = await _repo.ObterDocumentoAsync(documentoId, ct)
            ?? throw new InvalidOperationException($"Documento {documentoId} não encontrado.");

        return Gerar(documento, prestador);
    }

    public byte[] Gerar(DocumentoClinico documento, DadosPrestador? prestador = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var paciente = documento.Paciente;
        var nomeClinica = PrimeiroPreenchido(prestador?.NomeFantasia, prestador?.RazaoSocial) ?? "Clínica";
        var itens = documento.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id).ToList();

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

                    PainelPaciente(col, paciente);

                    if (!string.IsNullOrWhiteSpace(documento.Corpo))
                        Paragrafos(col, documento.Corpo!);

                    switch (documento.Tipo)
                    {
                        case TipoDocumentoClinico.Receita:
                            ListaPrescrita(col, itens, "Prescrição");
                            break;

                        case TipoDocumentoClinico.PedidoExame:
                            ListaPrescrita(col, itens, "Exames solicitados");
                            break;

                        case TipoDocumentoClinico.Atestado:
                            BlocoAtestado(col, documento);
                            break;

                        case TipoDocumentoClinico.Comparecimento:
                            BlocoComparecimento(col, documento);
                            break;

                        case TipoDocumentoClinico.RelatorioEvolucao:
                            TabelaSessoes(col, itens);
                            break;

                        case TipoDocumentoClinico.Consentimento:
                            ListaFinalidades(col, itens);
                            break;

                        case TipoDocumentoClinico.Anamnese:
                            RoteiroComLinhas(col, itens);
                            break;
                    }

                    if (!string.IsNullOrWhiteSpace(documento.Observacoes))
                        col.Item().Background(FundoSuave).Border(1).BorderColor(Borda).Padding(10).Text(t =>
                        {
                            t.Span("Observações  ").Bold().FontSize(8.5f).FontColor(TextoSecundario);
                            t.Span(documento.Observacoes!).FontSize(9.5f);
                        });

                    Assinaturas(col, documento);
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
                            t.Span("  ·  confira este código na ficha do paciente.")
                                .FontSize(8).FontColor(TextoSecundario);
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

    // ==================== Blocos ====================

    private static void PainelPaciente(ColumnDescriptor col, Paciente? paciente)
        => col.Item().Background(FundoSuave).Border(1).BorderColor(Borda).Padding(12).Column(c =>
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
                r.RelativeItem().Text(t =>
                {
                    t.Span("Nascimento  ").FontSize(8.5f).FontColor(TextoSecundario);
                    t.Span(paciente?.DataNascimento is { } n
                        ? $"{n:dd/MM/yyyy} ({Idade(n)} anos)"
                        : "—").FontSize(9.5f);
                });
                r.RelativeItem(2).Text(t =>
                {
                    t.Span("Convênio  ").FontSize(8.5f).FontColor(TextoSecundario);
                    t.Span(paciente is null
                        ? "—"
                        : CatalogoConvenios.Nome(paciente.ConvenioCodigo ?? paciente.Convenio.ToString()))
                        .FontSize(9.5f);
                });
            });
        });

    /// <summary>Receita e pedido de exame: item numerado, quantidade à direita, detalhe embaixo.</summary>
    private static void ListaPrescrita(ColumnDescriptor col, IReadOnlyList<ItemDocumento> itens, string titulo)
    {
        col.Item().Text(titulo).Bold().FontSize(11).FontColor(AzulEscuro);

        var numero = 0;
        foreach (var item in itens)
        {
            numero++;
            col.Item().PaddingTop(2).BorderBottom(1).BorderColor(Borda).PaddingBottom(8).Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text(t =>
                    {
                        t.Span($"{numero}.  ").SemiBold().FontSize(10.5f).FontColor(TextoSecundario);
                        t.Span(item.Descricao).SemiBold().FontSize(10.5f);
                    });

                    if (!string.IsNullOrWhiteSpace(item.Quantidade))
                        r.ConstantItem(110).AlignRight()
                            .Text(item.Quantidade!).FontSize(9.5f).FontColor(TextoSecundario);
                });

                if (!string.IsNullOrWhiteSpace(item.Detalhe))
                    c.Item().PaddingLeft(18).PaddingTop(2)
                        .Text(item.Detalhe!).FontSize(9.5f).FontColor(TextoSecundario);
            });
        }
    }

    private static void BlocoAtestado(ColumnDescriptor col, DocumentoClinico documento)
    {
        var afastamento = documento.DiasAfastamento is { } dias
            ? $"{dias} dia(s)"
            : documento.PeriodoInicio is { } de && documento.PeriodoFim is { } ate
                ? $"de {de:dd/MM/yyyy} a {ate:dd/MM/yyyy}"
                : "—";

        col.Item().Border(1).BorderColor(Borda).Padding(12).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("AFASTAMENTO").Bold().FontSize(8)
                    .FontColor(TextoSecundario).LetterSpacing(0.08f);
                c.Item().PaddingTop(2).Text(afastamento).Bold().FontSize(13).FontColor(AzulEscuro);
            });

            // O CID só sai quando o paciente autorizou — é sigilo dele, não do sistema.
            if (documento.CidImpresso is { } cid)
                row.ConstantItem(180).Column(c =>
                {
                    c.Item().Text("CID").Bold().FontSize(8)
                        .FontColor(TextoSecundario).LetterSpacing(0.08f);
                    c.Item().PaddingTop(2).Text(cid).Bold().FontSize(13);
                });
        });

        if (documento.PeriodoInicio is { } inicio && documento.DiasAfastamento is not null)
            col.Item().Text($"A partir de {inicio:dd/MM/yyyy}.").FontSize(9.5f).FontColor(TextoSecundario);
    }

    private static void BlocoComparecimento(ColumnDescriptor col, DocumentoClinico documento)
    {
        var horario = (documento.HoraChegada, documento.HoraSaida) switch
        {
            ({ } chegada, { } saida) => $"das {chegada:HH\\:mm} às {saida:HH\\:mm}",
            ({ } chegada, null) => $"a partir das {chegada:HH\\:mm}",
            (null, { } saida) => $"até as {saida:HH\\:mm}",
            _ => "no horário do atendimento"
        };

        var dia = documento.PeriodoInicio ?? documento.Data;

        col.Item().Border(1).BorderColor(Borda).Padding(12).Text(t =>
        {
            t.Span("Declaro para os devidos fins que o(a) paciente acima compareceu a esta clínica em ")
                .FontSize(10.5f);
            t.Span($"{dia:dd/MM/yyyy}").SemiBold().FontSize(10.5f);
            t.Span($", {horario}, para atendimento.").FontSize(10.5f);
        });
    }

    /// <summary>Relatório de evolução: uma linha por sessão, com a EVA do dia.</summary>
    private static void TabelaSessoes(ColumnDescriptor col, IReadOnlyList<ItemDocumento> itens)
    {
        col.Item().Text("Sessões do período").Bold().FontSize(11).FontColor(AzulEscuro);
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.4f); // data + EVA
                c.RelativeColumn(5);    // queixa/conduta/evolução
                c.RelativeColumn(1.8f); // profissional
            });

            void HeaderCell(string s) => table.Cell()
                .Background(FundoCabecalhoTabela).BorderBottom(1).BorderColor(Borda)
                .PaddingVertical(6).PaddingHorizontal(6)
                .Text(s).SemiBold().FontSize(9).FontColor(TextoSecundario);

            HeaderCell("Sessão"); HeaderCell("Registro clínico"); HeaderCell("Profissional");

            var linha = 0;
            foreach (var item in itens)
            {
                var fundo = linha++ % 2 == 1 ? FundoSuave : "#FFFFFF";

                IContainer Cell() => table.Cell().Background(fundo)
                    .BorderBottom(1).BorderColor(Borda)
                    .PaddingVertical(6).PaddingHorizontal(6);

                Cell().Text(item.Descricao).FontSize(9.5f).SemiBold();
                Cell().Text(item.Detalhe ?? "—").FontSize(9.5f);
                Cell().Text(item.Quantidade ?? "—").FontSize(9.5f).FontColor(TextoSecundario);
            }
        });
    }

    /// <summary>Termo de consentimento: uma finalidade por linha, com quadrado para assinalar.</summary>
    private static void ListaFinalidades(ColumnDescriptor col, IReadOnlyList<ItemDocumento> itens)
    {
        col.Item().Text("Finalidades").Bold().FontSize(11).FontColor(AzulEscuro);

        foreach (var item in itens)
        {
            var autorizado = string.Equals(item.Quantidade, "Autorizado", StringComparison.OrdinalIgnoreCase);

            col.Item().BorderBottom(1).BorderColor(Borda).PaddingVertical(8).Row(row =>
            {
                row.ConstantItem(22).AlignTop()
                    .Border(1).BorderColor(TextoSecundario).Width(14).Height(14)
                    .AlignCenter().AlignMiddle()
                    .Text(autorizado ? "X" : " ").Bold().FontSize(9).FontColor(AzulEscuro);

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(item.Descricao).SemiBold().FontSize(10.5f);
                    if (!string.IsNullOrWhiteSpace(item.Detalhe))
                        c.Item().Text(item.Detalhe!).FontSize(9).FontColor(TextoSecundario);
                });

                row.ConstantItem(90).AlignRight().AlignMiddle()
                    .Text(autorizado ? "Autorizado" : "Pendente")
                    .SemiBold().FontSize(9)
                    .FontColor(autorizado ? VerdeForte : TextoSecundario);
            });
        }
    }

    /// <summary>
    /// Anamnese: o que o prontuário já respondeu vem escrito; o que ele não guarda sai
    /// em linhas, para a entrevista acontecer com o papel na mão.
    /// </summary>
    private static void RoteiroComLinhas(ColumnDescriptor col, IReadOnlyList<ItemDocumento> itens)
    {
        foreach (var item in itens)
        {
            col.Item().PaddingTop(6).Text(item.Descricao)
                .SemiBold().FontSize(10).FontColor(AzulEscuro);

            if (!string.IsNullOrWhiteSpace(item.Detalhe))
            {
                col.Item().PaddingTop(2).Text(item.Detalhe!).FontSize(9.5f);
                continue;
            }

            for (var i = 0; i < 2; i++)
                col.Item().PaddingTop(16).LineHorizontal(0.75f).LineColor(Borda);
        }
    }

    /// <summary>Carimbo do profissional e linhas de assinatura.</summary>
    private static void Assinaturas(ColumnDescriptor col, DocumentoClinico documento)
    {
        var assinaPaciente = documento.Tipo is TipoDocumentoClinico.Consentimento
                                 or TipoDocumentoClinico.Anamnese;

        col.Item().PaddingTop(36).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().LineHorizontal(1).LineColor(TextoSecundario);
                c.Item().PaddingTop(3).AlignCenter()
                    .Text(documento.Profissional?.Nome ?? "Profissional responsável")
                    .SemiBold().FontSize(9.5f);

                var registro = documento.Profissional?.RegistroConselho;
                c.Item().AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(registro) ? "Assinatura e carimbo" : registro!)
                    .FontSize(8.5f).FontColor(TextoSecundario);
            });

            if (!assinaPaciente) return;

            row.ConstantItem(40);
            row.RelativeItem().Column(c =>
            {
                c.Item().LineHorizontal(1).LineColor(TextoSecundario);
                c.Item().PaddingTop(3).AlignCenter()
                    .Text(documento.Paciente?.Nome ?? "Paciente").SemiBold().FontSize(9.5f);
                c.Item().AlignCenter().Text("Paciente (ou responsável)")
                    .FontSize(8.5f).FontColor(TextoSecundario);
            });
        });
    }

    // ==================== Auxiliares ====================

    private static void Paragrafos(ColumnDescriptor col, string texto)
    {
        foreach (var paragrafo in texto.Split('\n'))
        {
            var limpo = paragrafo.Trim();
            if (limpo.Length == 0) continue;
            col.Item().Text(limpo).FontSize(10.5f);
        }
    }

    private static int Idade(DateOnly nascimento)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var idade = hoje.Year - nascimento.Year;
        if (nascimento > hoje.AddYears(-idade)) idade--;
        return idade;
    }

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
