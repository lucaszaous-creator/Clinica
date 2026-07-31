using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

/// <summary>
/// Os dois papéis que a agenda produz e que a clínica hoje resolve à mão (parcela 28):
/// a **folha do dia** que o profissional leva para a sala, e o **comprovante** que o
/// paciente leva do balcão.
///
/// Os dois são impressos em toda clínica de acupuntura, e nesta eram: a secretária
/// anotava o horário num papel de bloco. Papel de bloco perde o telefone da clínica, não
/// diz o preparo da sessão e some da bolsa — e o paciente liga no dia seguinte para
/// perguntar a que horas era.
///
/// Duas regras do que sai impresso:
///
/// 1. **A folha do dia não leva dado clínico.** Ela circula pela sala, pela recepção e
///    às vezes pela mesa do café: nome, horário, profissional e sala bastam para conduzir
///    o dia. Queixa e evolução ficam no prontuário, onde há controle de quem vê.
/// 2. **O comprovante não leva diagnóstico nem valor.** Ele existe para o paciente não
///    esquecer o horário; preço se combina no balcão e diagnóstico não se entrega em
///    papel avulso.
/// </summary>
public sealed class AgendaPdfService
{
    private const string Azul = MarcaSemDor.Azul;
    private const string AzulEscuro = MarcaSemDor.Navy;
    private const string TextoPrimario = "#111827";
    private const string TextoSecundario = "#6B7280";
    private const string Borda = "#E5E7EB";
    private const string FundoCabecalho = "#F1F5F9";

    private static readonly System.Globalization.CultureInfo Brasil = new("pt-BR");

    private readonly IClinicaRepositorio _repo;
    private readonly AgendaService _agenda;

    public AgendaPdfService(IClinicaRepositorio repo, AgendaService agenda)
    {
        _repo = repo;
        _agenda = agenda;
    }

    /// <summary>
    /// A folha do dia: quem vem, a que horas, com quem e em qual sala.
    ///
    /// Cancelados e faltas do dia entram MARCADOS em vez de sumirem — quem lê a folha às
    /// 14h precisa saber que o horário das 15h vagou, e uma linha ausente se confunde com
    /// horário que nunca existiu.
    /// </summary>
    public async Task<byte[]> AgendaDoDiaAsync(
        DateOnly dia, int? profissionalId = null, DadosPrestador? prestador = null,
        CancellationToken ct = default)
    {
        var doDia = await _agenda.DoDiaAsync(dia, ct);

        var linhas = doDia
            .Where(a => profissionalId is null || a.ProfissionalId == profissionalId)
            .OrderBy(a => a.DataHora)
            .ToList();

        var titulo = profissionalId is not null && linhas.Count > 0
            ? $"Agenda de {linhas[0].Profissional?.Rotulo ?? "profissional"}"
            : "Agenda do dia";

        return Montar(
            titulo,
            dia.ToString("dddd, dd 'de' MMMM 'de' yyyy", Brasil),
            prestador,
            col =>
            {
                if (linhas.Count == 0)
                {
                    col.Item().PaddingTop(20).Text("Nenhum horário marcado para este dia.")
                        .FontSize(11).FontColor(TextoSecundario);
                    return;
                }

                col.Item().Table(tabela =>
                {
                    tabela.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(50);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });

                    tabela.Header(h =>
                    {
                        Cabecalho(h, "Hora");
                        Cabecalho(h, "Paciente");
                        Cabecalho(h, "Profissional");
                        Cabecalho(h, "Sala");
                        Cabecalho(h, "Situação");
                    });

                    foreach (var a in linhas)
                    {
                        var apagado = a.Status is StatusAgendamento.Cancelado or StatusAgendamento.Faltou;
                        var cor = apagado ? TextoSecundario : TextoPrimario;

                        Celula(tabela, a.DataHora.ToString("HH:mm"), cor);
                        Celula(tabela, a.Paciente?.Nome ?? "—", cor);
                        Celula(tabela, a.Profissional?.Rotulo ?? "—", cor);
                        Celula(tabela, a.Sala?.Nome ?? "—", cor);
                        Celula(tabela, Situacao(a), cor);
                    }
                });

                var ativos = linhas.Count(a => a.OcupaAgenda);
                col.Item().PaddingTop(12).Text(
                        $"{ativos} horário(s) ativo(s) de {linhas.Count} na folha.")
                    .FontSize(9).FontColor(TextoSecundario);
            });
    }

    /// <summary>
    /// O comprovante que o paciente leva. Sem valor e sem diagnóstico: ele existe para o
    /// horário não ser esquecido, e papel avulso não é lugar de dado clínico.
    /// </summary>
    public async Task<byte[]> ComprovanteAsync(
        int agendamentoId, DadosPrestador? prestador = null, CancellationToken ct = default)
    {
        var ag = await _repo.ObterAgendamentoAsync(agendamentoId, ct)
            ?? throw new InvalidOperationException("Agendamento não encontrado.");

        var telefone = prestador?.Telefone;

        return Montar(
            "Comprovante de agendamento",
            ag.DataHora.ToString("dddd, dd 'de' MMMM 'de' yyyy", Brasil),
            prestador,
            col =>
            {
                col.Item().PaddingTop(6).Text(ag.Paciente?.Nome ?? "—")
                    .Bold().FontSize(16).FontColor(AzulEscuro);

                col.Item().PaddingTop(14).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("HORÁRIO").FontSize(8).FontColor(TextoSecundario);
                        c.Item().Text(ag.DataHora.ToString("HH:mm")).Bold().FontSize(22)
                            .FontColor(Azul);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("PROFISSIONAL").FontSize(8).FontColor(TextoSecundario);
                        c.Item().Text(ag.Profissional?.Rotulo ?? "a definir").FontSize(12);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("SALA").FontSize(8).FontColor(TextoSecundario);
                        c.Item().Text(ag.Sala?.Nome ?? "a definir").FontSize(12);
                    });
                });

                col.Item().PaddingTop(16).LineHorizontal(1).LineColor(Borda);

                // O que o paciente precisa saber, escrito no papel que ele leva. É isto
                // que o bloco de anotação não tinha — e é o motivo das ligações no dia
                // seguinte perguntando o horário.
                col.Item().PaddingTop(12).Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(10).FontColor(TextoPrimario));
                    t.Span("Chegue com 10 minutos de antecedência. ");
                    t.Span("Para desmarcar ou remarcar, avise a clínica com pelo menos 24 horas — "
                           + "o horário avisado pode ser oferecido a quem está na lista de espera.");
                });

                if (!string.IsNullOrWhiteSpace(telefone))
                    col.Item().PaddingTop(8)
                        .Text($"Contato da clínica: {telefone}")
                        .FontSize(10).FontColor(TextoSecundario);

                if (!string.IsNullOrWhiteSpace(ag.Observacoes))
                    col.Item().PaddingTop(10).Background(FundoCabecalho).Padding(10)
                        .Text(ag.Observacoes!).FontSize(9.5f).FontColor(TextoPrimario);
            });
    }

    // ==================== Molde comum ====================

    private static byte[] Montar(
        string titulo, string subtitulo, DadosPrestador? prestador,
        Action<ColumnDescriptor> conteudo)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var nomeClinica =
            PrimeiroPreenchido(prestador?.NomeFantasia, prestador?.RazaoSocial) ?? "Clínica";

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
                            if (!string.IsNullOrWhiteSpace(prestador?.Endereco))
                                c.Item().Text(prestador!.Endereco!)
                                    .FontSize(8.5f).FontColor(TextoSecundario);
                        });

                        row.RelativeItem(2).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text(titulo.ToUpperInvariant())
                                .Bold().FontSize(13).FontColor(Azul);
                            c.Item().AlignRight().Text(subtitulo)
                                .FontSize(9).FontColor(TextoSecundario);
                        });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(2).LineColor(Azul);
                });

                page.Content().PaddingVertical(12).Column(conteudo);

                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Borda);
                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text(
                                $"Impresso em {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Brasil)}")
                            .FontSize(8).FontColor(TextoSecundario);
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(8).FontColor(TextoSecundario));
                            t.CurrentPageNumber();
                            t.Span(" / ");
                            t.TotalPages();
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Cabecalho(TableCellDescriptor h, string texto)
        => h.Cell().Background(FundoCabecalho).PaddingVertical(5).PaddingHorizontal(6)
            .Text(texto).Bold().FontSize(9).FontColor(AzulEscuro);

    private static void Celula(TableDescriptor tabela, string texto, string cor)
        => tabela.Cell().BorderBottom(1).BorderColor(Borda)
            .PaddingVertical(5).PaddingHorizontal(6)
            .Text(texto).FontSize(9.5f).FontColor(cor);

    private static string Situacao(Agendamento a) => a.Status switch
    {
        StatusAgendamento.Cancelado => "CANCELADO",
        StatusAgendamento.Faltou => "FALTOU",
        StatusAgendamento.Realizado => "atendido",
        _ => a.Encaixe ? "encaixe" : "marcado"
    };

    private static string? PrimeiroPreenchido(params string?[] valores)
        => valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
