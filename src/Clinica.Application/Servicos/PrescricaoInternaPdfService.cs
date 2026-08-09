using Clinica.Application.Abstracoes;
using Clinica.Application.Assinatura;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clinica.Application.Servicos;

/// <summary>
/// As DUAS folhas da prescrição de execução interna (parcela 42).
///
/// A <b>Prescrição</b> é o papel que sai da impressora e vai para a sala. Ela leva a
/// assinatura ICP-Brasil de quem prescreveu e as <b>colunas de checagem EM BRANCO</b>, que a
/// enfermeira preenche à caneta — hora em que administrou, visto, e o motivo quando não
/// realizou. É nela que está a assinatura manuscrita da enfermagem, e é ela o documento.
///
/// O <b>Registro de execução</b> é o espelho eletrônico: o que a técnica digitou na Sala de
/// infusão, com ✓ e "rodela". Serve ao prontuário e à conferência do fim do dia, NÃO é
/// assinado eletronicamente, e o rodapé dele diz isso — a autoria da execução está no papel.
///
/// Por que a prescritora pode assinar um PDF com campos em branco
/// --------------------------------------------------------------
/// Porque eles são pré-impressos do formulário, exatamente como no talão de papel. Ela
/// atesta o que MANDOU fazer; o que foi feito é escrito em cima da folha, depois, por outra
/// pessoa e com outra caneta. A assinatura dela cobre o PDF — não o que a caneta escreve
/// sobre a impressão dele.
///
/// O que o PDF diz sobre a própria assinatura
/// ------------------------------------------
/// O rodapé escreve o NÍVEL (ICP-Brasil, eletrônica no sistema, ou à mão na via impressa)
/// e, quando não há carimbo do tempo de uma ACT, escreve que a data é declarada pelo
/// relógio de quem assinou. É a mesma disciplina do
/// <see cref="DocumentosClinicosPdfService"/>, que se recusa a chamar carimbo impresso de
/// assinatura digital: <b>a folha nunca promete mais do que garante.</b>
/// </summary>
public sealed class PrescricaoInternaPdfService
{
    private const string Azul = MarcaSemDor.Azul;
    private const string AzulEscuro = MarcaSemDor.Navy;
    private const string TextoPrimario = "#111827";
    private const string TextoSecundario = "#6B7280";
    private const string Borda = "#E5E7EB";
    private const string FundoSuave = "#F8FAFC";
    private const string FundoCabecalhoTabela = "#F1F5F9";
    private const string VerdeForte = "#15803D";
    private const string VerdeSuave = "#DCFCE7";
    private const string VermelhoForte = "#B91C1C";
    private const string VermelhoSuave = "#FEE2E2";
    private const string AmareloForte = "#92400E";
    private const string AmareloSuave = "#FEF3C7";

    // ---- Geometria da folha, e por que ela é CONSTANTE ----
    //
    // O carimbo visível da assinatura digital é colado pelo PdfSharp num retângulo
    // ABSOLUTO da página, depois de o QuestPDF já ter desenhado tudo. Como o conteúdo
    // flui (uma folha com 12 itens ocupa mais que uma com 2), o único lugar cuja posição
    // se sabe de antemão é o RODAPÉ — que o QuestPDF ancora na mesma altura em todas as
    // páginas. Por isso a faixa da assinatura mora dentro do rodapé, com altura fixa, e
    // AreaDaAssinatura() calcula o retângulo a partir destas mesmas constantes. Trocar uma
    // sem a outra faria o carimbo cair em cima do texto.
    private const float AlturaPagina = 841.89f;      // A4 em pontos
    private const float MargemPagina = 42.52f;       // 1,5 cm
    private const float AlturaRodape = 116f;
    private const float AlturaFaixaAssinatura = 58f;
    private const float LarguraCarimbo = 250f;

    private readonly IClinicaRepositorio _repo;
    private readonly PrescricaoService _conferenciaClinica;

    public PrescricaoInternaPdfService(IClinicaRepositorio repo, PrescricaoService conferenciaClinica)
    {
        _repo = repo;
        _conferenciaClinica = conferenciaClinica;
    }

    /// <summary>
    /// Onde o carimbo da assinatura digital é colado. Sempre na ÚLTIMA página: é lá que
    /// está o fim do documento, e é a página que alguém confere.
    /// </summary>
    public static AreaAssinatura AreaDaAssinatura(int totalPaginas)
        => new(
            Pagina: Math.Max(0, totalPaginas - 1),
            X: MargemPagina,
            Y: AlturaPagina - MargemPagina - AlturaRodape + 2,
            Largura: LarguraCarimbo,
            Altura: AlturaFaixaAssinatura - 4);

    // ==================== Folha 1 · a prescrição ====================

    public async Task<byte[]> GerarPrescricaoAsync(
        int prescricaoId, DadosPrestador? prestador = null, CancellationToken ct = default)
    {
        var prescricao = await _repo.ObterPrescricaoInternaAsync(prescricaoId, ct)
            ?? throw new InvalidOperationException($"Prescrição {prescricaoId} não encontrada.");

        var alergias = await AlergiasParaAFolhaAsync(prescricao, ct);
        return GerarPrescricao(prescricao, alergias, prestador);
    }

    public byte[] GerarPrescricao(
        PrescricaoInterna prescricao, IReadOnlyList<string> alergias,
        DadosPrestador? prestador = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var itens = prescricao.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id).ToList();
        var assinatura = prescricao.AssinaturaDoPrescritor;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                Moldura(page);

                Cabecalho(page, prestador, "Prescrição de execução interna", prescricao,
                    prescricao.Cancelada ? "CANCELADA" : null);

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    AvisoDeUsoInterno(col);

                    if (prescricao.Cancelada)
                        Faixa(col, VermelhoSuave, VermelhoForte, "Prescrição cancelada",
                            prescricao.MotivoCancelamento);

                    IdentificacaoDoPaciente(col, prescricao);
                    BlocoDeAlergias(col, alergias);

                    if (!string.IsNullOrWhiteSpace(prescricao.Indicacao))
                        Campo(col, "Indicação", prescricao.Indicacao!);

                    TabelaDaPrescricao(col, itens);

                    if (!string.IsNullOrWhiteSpace(prescricao.Observacoes))
                        Campo(col, "Orientações gerais", prescricao.Observacoes!);
                });

                Rodape(page, prescricao, assinatura,
                    nomeLinha: prescricao.Profissional?.Nome ?? "Profissional responsável",
                    registroLinha: prescricao.Profissional?.RegistroConselho,
                    papel: "Prescritor");
            });
        }).GeneratePdf();
    }

    // ==================== Folha 2 · o registro de execução ====================

    public async Task<byte[]> GerarRegistroExecucaoAsync(
        int prescricaoId, DadosPrestador? prestador = null, CancellationToken ct = default)
    {
        var prescricao = await _repo.ObterPrescricaoInternaAsync(prescricaoId, ct)
            ?? throw new InvalidOperationException($"Prescrição {prescricaoId} não encontrada.");

        var alergias = await AlergiasParaAFolhaAsync(prescricao, ct);
        return GerarRegistroExecucao(prescricao, alergias, prestador);
    }

    public byte[] GerarRegistroExecucao(
        PrescricaoInterna prescricao, IReadOnlyList<string> alergias,
        DadosPrestador? prestador = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var itens = prescricao.Itens.OrderBy(i => i.Ordem).ThenBy(i => i.Id).ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                Moldura(page);

                Cabecalho(page, prestador, "Registro de execução — enfermagem", prescricao, null);

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(10);

                    VinculoComAPrescricao(col, prescricao);
                    IdentificacaoDoPaciente(col, prescricao);
                    BlocoDeAlergias(col, alergias);

                    TabelaDaExecucao(col, itens);
                    Justificativas(col, itens);
                    Retificacoes(col, itens);
                });

                // Assinatura NULA de propósito: o registro eletrônico não é assinado, e o
                // rodapé imprime a linha para a enfermeira assinar à mão.
                Rodape(page, prescricao, assinatura: null,
                    nomeLinha: NomeDoExecutante(prescricao) ?? "Enfermagem responsável",
                    registroLinha: ConselhoDoExecutante(prescricao),
                    papel: "Enfermagem");
            });
        }).GeneratePdf();
    }

    // ==================== Blocos ====================

    private static void Moldura(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(MargemPagina);
        page.MarginTop(MargemPagina);
        page.MarginBottom(MargemPagina);
        page.DefaultTextStyle(t => t.FontSize(10).FontColor(TextoPrimario));
    }

    private static void Cabecalho(
        PageDescriptor page, DadosPrestador? prestador, string titulo,
        PrescricaoInterna prescricao, string? selo)
    {
        var nomeClinica = PrimeiroPreenchido(prestador?.NomeFantasia, prestador?.RazaoSocial) ?? "Clínica";

        page.Header().Column(col =>
        {
            if (MarcaSemDor.Logo is { } logo)
                col.Item().PaddingBottom(8).Width(130).Image(logo);

            col.Item().Row(row =>
            {
                row.RelativeItem(3).Column(c =>
                {
                    c.Item().Text(nomeClinica).Bold().FontSize(15).FontColor(AzulEscuro);

                    var registro = Juntar(
                        Prefixado("CNPJ ", prestador?.Cnpj), Prefixado("CNES ", prestador?.Cnes));
                    if (registro is not null)
                        c.Item().Text(registro).FontSize(8.5f).FontColor(TextoSecundario);

                    if (!string.IsNullOrWhiteSpace(prestador?.Endereco))
                        c.Item().Text(prestador!.Endereco!).FontSize(8.5f).FontColor(TextoSecundario);
                });

                row.RelativeItem(2).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text(titulo.ToUpperInvariant())
                        .Bold().FontSize(12).FontColor(Azul);

                    if (selo is not null)
                        c.Item().AlignRight().PaddingTop(4)
                            .Background(VermelhoSuave).PaddingVertical(3).PaddingHorizontal(10)
                            .Text(selo).Bold().FontSize(9).FontColor(VermelhoForte);

                    c.Item().AlignRight().PaddingTop(6)
                        .Text($"Nº {prescricao.Numero}").Bold().FontSize(11);
                    c.Item().AlignRight()
                        .Text($"{prescricao.Data:dd/MM/yyyy} às {prescricao.Hora:HH\\:mm}")
                        .FontSize(8.5f).FontColor(TextoSecundario);
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(2).LineColor(Azul);
        });
    }

    /// <summary>
    /// O aviso que impede o mal-entendido mais provável desta folha.
    ///
    /// Ela tem cara de receita e NÃO é. Não é escolha de leiaute: esta folha não cumpre o
    /// art. 35 da Lei 5.991/1973 — não traz o endereço residencial do paciente nem passa
    /// pela conferência de conteúdo que a receita passa —, então a farmácia não pode
    /// aviá-la. Sem a linha, o paciente que a leva embora tenta comprar com ela, a
    /// farmácia recusa, e o que fica parecendo defeito é a clínica.
    ///
    /// A frase DIZ O QUE FAZER no lugar (parcela 52). A versão anterior só dizia o que a
    /// folha não era, e isso deixava sem resposta o caso legítimo: a médica faz a infusão
    /// aqui E quer que o paciente compre algo para casa. Nesse caso são dois documentos, e
    /// o segundo já existe a um clique — Prescrições → Receita, que sai com o endereço, a
    /// posologia conferida e o QR do validador do ITI para o farmacêutico.
    ///
    /// É a mesma regra do <c>FolhaCatalogo.Exigencia</c>: descobrir o requisito errando é
    /// o que faz a pessoa desistir do sistema.
    /// </summary>
    private static void AvisoDeUsoInterno(ColumnDescriptor col)
        => col.Item().Background(FundoCabecalhoTabela).Border(1).BorderColor(Borda)
            .Padding(8).Text(t =>
            {
                t.Span("Uso interno  ").Bold().FontSize(8.5f).FontColor(AzulEscuro);
                t.Span("Medicação administrada no próprio consultório, pela equipe. "
                     + "Para o paciente comprar medicamento na farmácia, emita uma "
                     + "Receita — ela sai com o endereço do paciente e o código de "
                     + "conferência que o farmacêutico precisa.")
                    .FontSize(8.5f).FontColor(TextoSecundario);
            });

    private static void IdentificacaoDoPaciente(ColumnDescriptor col, PrescricaoInterna prescricao)
    {
        var paciente = prescricao.Paciente;

        col.Item().Background(FundoSuave).Border(1).BorderColor(Borda).Padding(10).Row(row =>
        {
            row.RelativeItem(3).Column(c =>
            {
                c.Item().Text("Paciente").FontSize(8).FontColor(TextoSecundario);
                c.Item().Text(paciente?.Nome ?? "—").SemiBold().FontSize(11);
            });

            row.RelativeItem(2).Column(c =>
            {
                c.Item().Text("Nascimento").FontSize(8).FontColor(TextoSecundario);
                c.Item().Text(paciente?.DataNascimento is { } d
                    ? $"{d:dd/MM/yyyy}" : "—").FontSize(10);
            });

            row.RelativeItem(2).Column(c =>
            {
                c.Item().Text("Prescritor").FontSize(8).FontColor(TextoSecundario);
                c.Item().Text(prescricao.Profissional?.Nome ?? "—").FontSize(10);
                if (!string.IsNullOrWhiteSpace(prescricao.Profissional?.RegistroConselho))
                    c.Item().Text(prescricao.Profissional!.RegistroConselho!)
                        .FontSize(8).FontColor(TextoSecundario);
            });
        });
    }

    /// <summary>
    /// As alergias, em vermelho, ANTES da tabela.
    ///
    /// Numa folha de infusão isto não é enfeite: é a informação que muda a conduta de quem
    /// está com a seringa na mão, e é a razão de o serviço de conferência da parcela 40
    /// existir. Quando não há alergia registrada, a folha DIZ isso — "nenhuma registrada"
    /// e um espaço em branco são coisas diferentes, e a segunda faz a equipe supor que
    /// ninguém perguntou.
    /// </summary>
    private static void BlocoDeAlergias(ColumnDescriptor col, IReadOnlyList<string> alergias)
    {
        if (alergias.Count == 0)
        {
            col.Item().Text(t =>
            {
                t.Span("Alergias  ").Bold().FontSize(8.5f).FontColor(TextoSecundario);
                t.Span("nenhuma registrada no prontuário até esta data.")
                    .FontSize(8.5f).FontColor(TextoSecundario);
            });
            return;
        }

        col.Item().Background(VermelhoSuave).Border(1).BorderColor(VermelhoForte)
            .Padding(10).Column(c =>
            {
                c.Item().Text("ALERGIAS REGISTRADAS").Bold().FontSize(9).FontColor(VermelhoForte);
                foreach (var alergia in alergias)
                    c.Item().PaddingTop(2).Text("• " + alergia).FontSize(10).FontColor(VermelhoForte);
            });
    }

    /// <summary>
    /// A tabela da prescrição — e as COLUNAS EM BRANCO que a enfermagem preenche à caneta.
    ///
    /// Elas são o coração desta folha impressa. Quem confere e assina a execução é a
    /// enfermeira, no papel, depois que a folha sai da impressora: sem as colunas ela
    /// escreveria na margem, e uma folha anotada na margem não se lê como registro.
    ///
    /// A prescritora assinar digitalmente um PDF com esses campos em branco é correto —
    /// eles são pré-impressos do formulário, como no talão de papel. Ela atesta o que
    /// mandou fazer; o que foi feito entra por cima, à mão.
    /// </summary>
    private static void TabelaDaPrescricao(ColumnDescriptor col, List<ItemPrescricaoInterna> itens)
    {
        col.Item().Table(tabela =>
        {
            tabela.ColumnsDefinition(c =>
            {
                c.ConstantColumn(22);    // nº
                c.RelativeColumn(5);     // medicamento
                c.ConstantColumn(56);    // via
                c.ConstantColumn(58);    // tempo
                c.ConstantColumn(40);    // previsto
                c.ConstantColumn(52);    // (em branco) hora realizada
                c.ConstantColumn(34);    // (em branco) visto
            });

            tabela.Header(h =>
            {
                CabecalhoCelula(h, "#");
                CabecalhoCelula(h, "Medicamento · dose · diluente · volume");
                CabecalhoCelula(h, "Via");
                CabecalhoCelula(h, "Tempo");
                CabecalhoCelula(h, "Prev.");
                CabecalhoCelula(h, "Feito às");
                CabecalhoCelula(h, "Visto");
            });

            foreach (var item in itens)
            {
                var apagado = item.Suspenso;

                Celula(tabela).Text(item.Ordem.ToString()).FontSize(9)
                    .FontColor(apagado ? TextoSecundario : TextoPrimario);

                Celula(tabela).Column(c =>
                {
                    c.Item().Text(item.TextoCompleto).FontSize(9.5f)
                        .SemiBold().FontColor(apagado ? TextoSecundario : TextoPrimario)
                        .Strikethrough(apagado);

                    if (item.SeNecessario)
                        c.Item().Text("se necessário (SOS)").FontSize(8).FontColor(AmareloForte);

                    if (!string.IsNullOrWhiteSpace(item.Observacoes))
                        c.Item().Text(item.Observacoes!).FontSize(8).FontColor(TextoSecundario);

                    if (apagado)
                        c.Item().Text($"SUSPENSO — {item.MotivoSuspensao}")
                            .FontSize(8).Bold().FontColor(VermelhoForte);
                });

                Celula(tabela).Text(RotulosEnum.De(item.Via)).FontSize(8.5f);
                Celula(tabela).Text(item.TempoInfusao ?? "—").FontSize(8.5f);
                Celula(tabela).Text(item.HoraPrevista is { } h ? $"{h:HH\\:mm}" : "—").FontSize(8.5f);

                // Em branco DE PROPÓSITO: é aqui que a enfermeira escreve a hora e faz o
                // "chequezinho" (ou circula a hora, quando não foi feito). Item suspenso não
                // ganha campo — dar espaço para checar o que foi suspenso é convidar o erro.
                Celula(tabela).Element(c => CampoParaCaneta(c, apagado));
                Celula(tabela).Element(c => CampoParaCaneta(c, apagado));
            }
        });

        col.Item().PaddingTop(4).Text(
                "A enfermagem anota nas duas últimas colunas: a hora em que administrou e o "
                + "visto. Quando NÃO foi realizado, circule a hora e escreva o motivo abaixo.")
            .FontSize(7.5f).FontColor(TextoSecundario);

        col.Item().PaddingTop(6).Column(c =>
        {
            c.Item().Text("Itens não realizados — motivo").Bold().FontSize(8)
                .FontColor(TextoSecundario);
            // Três linhas: mais que isso vira formulário, menos não cabe uma frase inteira.
            for (var i = 0; i < 3; i++)
                c.Item().PaddingTop(13).LineHorizontal(0.5f).LineColor(Borda);
        });
    }

    /// <summary>Uma célula vazia com linha de apoio — o campo que se preenche à caneta.</summary>
    private static void CampoParaCaneta(IContainer celula, bool suspenso)
    {
        if (suspenso)
        {
            celula.AlignCenter().Text("—").FontSize(8.5f).FontColor(TextoSecundario);
            return;
        }

        celula.PaddingTop(9).PaddingHorizontal(2).LineHorizontal(0.5f).LineColor(Borda);
    }

    /// <summary>
    /// A tabela da execução — onde vivem o "chequezinho" e a "rodela".
    ///
    /// A linguagem visual é a da enfermagem, de propósito: quem lê esta folha lê centenas
    /// de folhas de hospital, e ✓ ao lado do horário significa feito, horário CIRCULADO
    /// significa não feito. Trocar isso por um rótulo de texto obrigaria a equipe a
    /// aprender a convenção deste sistema em cima da que ela já tem — e a folha impressa
    /// vai circular ao lado de folhas de hospital de verdade.
    /// </summary>
    private static void TabelaDaExecucao(ColumnDescriptor col, List<ItemPrescricaoInterna> itens)
    {
        col.Item().Table(tabela =>
        {
            tabela.ColumnsDefinition(c =>
            {
                c.ConstantColumn(22);    // nº
                c.RelativeColumn(5);     // medicamento
                c.ConstantColumn(68);    // via — 52 quebrava "Endovenosa (EV)" no meio da palavra
                c.ConstantColumn(74);    // checagem
                c.RelativeColumn(2);     // executante
            });

            tabela.Header(h =>
            {
                CabecalhoCelula(h, "#");
                CabecalhoCelula(h, "Medicamento · dose · diluente · volume");
                CabecalhoCelula(h, "Via");
                CabecalhoCelula(h, "Checagem");
                CabecalhoCelula(h, "Executante");
            });

            foreach (var item in itens)
            {
                var checagem = item.ChecagemVigente;

                Celula(tabela).Text(item.Ordem.ToString()).FontSize(9);

                Celula(tabela).Column(c =>
                {
                    c.Item().Text(item.TextoCompleto).FontSize(9.5f).SemiBold()
                        .Strikethrough(item.Suspenso);
                    if (item.SeNecessario)
                        c.Item().Text("se necessário (SOS)").FontSize(8).FontColor(AmareloForte);
                    if (item.Suspenso)
                        c.Item().Text($"SUSPENSO pelo prescritor — {item.MotivoSuspensao}")
                            .FontSize(8).Bold().FontColor(VermelhoForte);
                });

                Celula(tabela).Text(RotulosEnum.De(item.Via)).FontSize(8.5f);

                Celula(tabela).Element(celula => MarcaDeChecagem(celula, item, checagem));

                Celula(tabela).Column(c =>
                {
                    if (checagem is null) { c.Item().Text("—").FontSize(8.5f); return; }

                    c.Item().Text(checagem.ExecutanteNome).FontSize(8.5f);
                    if (!string.IsNullOrWhiteSpace(checagem.ExecutanteConselho))
                        c.Item().Text(checagem.ExecutanteConselho!)
                            .FontSize(7.5f).FontColor(TextoSecundario);
                });
            }
        });
    }

    /// <summary>
    /// Famílias tentadas para desenhar o ✓ (U+2713), em ordem.
    ///
    /// A fonte embutida do QuestPDF é a Lato, e ela NÃO tem este glifo — sem a cadeia
    /// abaixo, o "chequezinho" sai como um quadrado vazio (tofu) em toda linha executada.
    /// Isso foi descoberto gerando a folha de verdade, não lendo o código: nenhum teste
    /// falhava, porque nenhum deles OLHA o PDF.
    ///
    /// É o símbolo central da checagem de enfermagem — a folha inteira existe para dizer
    /// "foi feito" —, então ele não podia depender de o computador ter a fonte certa por
    /// acaso. A ordem cobre Windows (onde os cinco apps rodam) e Linux (onde o CI e os
    /// testes rodam); a última é a Lato, que falha desenhando o quadrado, mas só é
    /// alcançada se nenhuma das anteriores existir.
    /// </summary>
    private static readonly string[] FamiliasDoVisto =
        ["Segoe UI Symbol", "Segoe UI", "DejaVu Sans", "FreeSerif", "Lato"];

    /// <summary>
    /// O ✓ com o horário, ou o horário dentro de uma "rodela".
    ///
    /// A rodela é uma cápsula de canto totalmente arredondado com contorno grosso —
    /// visualmente é o círculo que a técnica faz à caneta em volta do horário.
    /// </summary>
    private static void MarcaDeChecagem(
        IContainer celula, ItemPrescricaoInterna item, ChecagemPrescricao? checagem)
    {
        if (item.Suspenso)
        {
            celula.Text("suspenso").FontSize(8.5f).Italic().FontColor(TextoSecundario);
            return;
        }

        if (checagem is null)
        {
            celula.Text(item.SeNecessario ? "não indicado" : "aguardando")
                .FontSize(8.5f).Italic().FontColor(TextoSecundario);
            return;
        }

        if (checagem.Situacao == SituacaoChecagem.Realizado)
        {
            celula.Row(row =>
            {
                row.ConstantItem(14).Text("✓").Bold().FontSize(12).FontColor(VerdeForte)
                    .FontFamily(FamiliasDoVisto);
                row.RelativeItem().PaddingTop(1)
                    .Text($"{checagem.HoraRealizacao:HH\\:mm}").SemiBold().FontSize(10);
            });
            return;
        }

        celula.Column(c =>
        {
            c.Item().Width(48)
                .Border(1.4f).BorderColor(VermelhoForte).CornerRadius(20)
                .PaddingVertical(2).AlignCenter()
                .Text($"{checagem.HoraRealizacao:HH\\:mm}")
                .SemiBold().FontSize(10).FontColor(VermelhoForte);

            c.Item().PaddingTop(2).Text("não realizado")
                .FontSize(7.5f).Bold().FontColor(VermelhoForte);
        });
    }

    private static void Justificativas(ColumnDescriptor col, List<ItemPrescricaoInterna> itens)
    {
        var comJustificativa = itens
            .Select(i => (Item: i, Checagem: i.ChecagemVigente))
            .Where(p => p.Checagem?.Justificativa is not null)
            .ToList();

        if (comJustificativa.Count == 0) return;

        col.Item().Background(FundoSuave).Border(1).BorderColor(Borda).Padding(10).Column(c =>
        {
            c.Item().Text("Justificativas").Bold().FontSize(8.5f).FontColor(TextoSecundario);

            foreach (var (item, checagem) in comJustificativa)
                c.Item().PaddingTop(3).Text(t =>
                {
                    t.Span($"Item {item.Ordem} · {checagem!.HoraRealizacao:HH\\:mm}  ")
                        .SemiBold().FontSize(9);
                    t.Span(checagem.Justificativa!).FontSize(9);
                });
        });
    }

    /// <summary>
    /// As correções, escritas na folha.
    ///
    /// Uma checagem retificada não some do documento — imprimir só o valor final faria a
    /// via em papel esconder exatamente o que a trilha guarda. Quem lê a folha tem de ver
    /// que houve correção, quando, e por quê.
    /// </summary>
    private static void Retificacoes(ColumnDescriptor col, List<ItemPrescricaoInterna> itens)
    {
        var retificacoes = itens
            .SelectMany(i => i.Checagens.Where(c => c.EhRetificacao).Select(c => (Item: i, Checagem: c)))
            .OrderBy(p => p.Item.Ordem).ThenBy(p => p.Checagem.RegistradoEm)
            .ToList();

        if (retificacoes.Count == 0) return;

        col.Item().Background(AmareloSuave).Border(1).BorderColor(AmareloForte)
            .Padding(10).Column(c =>
            {
                c.Item().Text("Checagens retificadas").Bold().FontSize(8.5f).FontColor(AmareloForte);

                foreach (var (item, checagem) in retificacoes)
                    c.Item().PaddingTop(3).Text(t =>
                    {
                        t.Span($"Item {item.Ordem}  ").SemiBold().FontSize(9).FontColor(AmareloForte);
                        t.Span($"corrigida em {checagem.RegistradoEm:dd/MM/yyyy HH\\:mm} por "
                             + $"{checagem.ExecutanteNome}: {checagem.MotivoRetificacao}")
                            .FontSize(9).FontColor(AmareloForte);
                    });
            });
    }

    /// <summary>
    /// O elo entre as duas folhas: o registro de execução aponta para a prescrição que
    /// executa, com o hash da assinatura dela.
    ///
    /// É esta caixa que substitui os dois carimbos na mesma página, e ela prova mais: o
    /// hash só existe se a prescrição foi assinada ANTES, e com aquele conteúdo exato.
    /// </summary>
    private static void VinculoComAPrescricao(ColumnDescriptor col, PrescricaoInterna prescricao)
    {
        var assinatura = prescricao.AssinaturaDoPrescritor;

        col.Item().Background(FundoCabecalhoTabela).Border(1).BorderColor(Borda)
            .Padding(10).Column(c =>
            {
                c.Item().Text($"Executa a prescrição nº {prescricao.Numero}")
                    .SemiBold().FontSize(9.5f).FontColor(AzulEscuro);

                if (assinatura is null)
                {
                    // Sem assinatura eletrônica a folha não mente: ela diz que a prescrição
                    // foi assinada na via impressa, que é o outro caminho legítimo.
                    c.Item().Text("Prescrição assinada na via impressa (sem assinatura eletrônica no sistema).")
                        .FontSize(8.5f).FontColor(TextoSecundario);
                    return;
                }

                c.Item().Text($"Assinada por {assinatura.NomeAssinante}"
                            + Prefixado(" · ", assinatura.RegistroConselho)
                            + $" em {assinatura.AssinadoEm:dd/MM/yyyy HH\\:mm}")
                    .FontSize(8.5f).FontColor(TextoSecundario);

                c.Item().Text($"{assinatura.RotuloDoNivel} · SHA-256 {assinatura.HashCurto}")
                    .FontSize(8.5f).FontColor(TextoSecundario);
            });
    }

    /// <summary>
    /// O rodapé, e a faixa de altura fixa onde o carimbo digital é colado depois.
    ///
    /// A linha de assinatura à mão fica SEMPRE impressa, mesmo quando há assinatura
    /// digital: a via em papel circula pela sala, e quem a pega precisa poder assinar
    /// nela. Não são caminhos concorrentes — são o mesmo documento em dois suportes.
    /// </summary>
    private static void Rodape(
        PageDescriptor page, PrescricaoInterna prescricao, AssinaturaDocumento? assinatura,
        string nomeLinha, string? registroLinha, string papel)
    {
        page.Footer().Height(AlturaRodape).Column(col =>
        {
            col.Item().Height(AlturaFaixaAssinatura).Row(row =>
            {
                // Metade esquerda: reservada para o carimbo da assinatura digital. Quando
                // não há assinatura eletrônica ela não fica em branco — diz o que fazer.
                row.ConstantItem(LarguraCarimbo).Column(c =>
                {
                    if (assinatura is not null) return;

                    c.Item().PaddingTop(30).LineHorizontal(1).LineColor(TextoSecundario);
                    c.Item().PaddingTop(3).Text("Assinatura e carimbo")
                        .FontSize(8).FontColor(TextoSecundario);
                });

                row.ConstantItem(24);

                row.RelativeItem().AlignBottom().Column(c =>
                {
                    c.Item().Text(nomeLinha).SemiBold().FontSize(9.5f);
                    c.Item().Text(Juntar(registroLinha, papel) ?? papel)
                        .FontSize(8).FontColor(TextoSecundario);
                });
            });

            col.Item().PaddingTop(4).LineHorizontal(0.75f).LineColor(Borda);

            col.Item().PaddingTop(3).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(t =>
                    {
                        t.Span("Conferência  ").FontSize(8).FontColor(TextoSecundario);
                        t.Span(prescricao.CodigoVerificacao).SemiBold().FontSize(8.5f);
                        t.Span("  ·  confira este código no sistema.")
                            .FontSize(8).FontColor(TextoSecundario);
                    });

                    c.Item().Text(FraseDoNivel(assinatura)).FontSize(7.5f).FontColor(TextoSecundario);
                });

                row.ConstantItem(90).AlignRight().AlignBottom().Text(t =>
                {
                    t.Span("Página ").FontSize(8).FontColor(TextoSecundario);
                    t.CurrentPageNumber().FontSize(8).FontColor(TextoSecundario);
                    t.Span(" de ").FontSize(8).FontColor(TextoSecundario);
                    t.TotalPages().FontSize(8).FontColor(TextoSecundario);
                });
            });
        });
    }

    /// <summary>
    /// A frase que diz exatamente o que a assinatura desta via garante — nem mais.
    ///
    /// Sem carimbo do tempo de uma ACT credenciada a data é DECLARADA pelo relógio de quem
    /// assinou, e escrever isso é a diferença entre um documento honesto e um que promete
    /// precisão que não tem.
    /// </summary>
    public static string FraseDoNivel(AssinaturaDocumento? assinatura)
    {
        if (assinatura is null)
            return "Sem assinatura eletrônica: vale a assinatura manuscrita na via impressa.";

        var frase = assinatura.RotuloDoNivel;

        if (assinatura.Tipo == TipoAssinatura.IcpBrasil && !string.IsNullOrWhiteSpace(assinatura.CertificadoTitular))
            frase += $" · titular {assinatura.CertificadoTitular}";

        frase += assinatura.CarimboTempoEm is { } carimbo
            ? $" · carimbo do tempo {carimbo:dd/MM/yyyy HH\\:mm} ({assinatura.CarimboTempoAutoridade})"
            : " · data declarada pelo relógio de quem assinou (sem carimbo do tempo)";

        return frase;
    }

    // ==================== Apoio ====================

    /// <summary>
    /// As alergias que vão impressas no topo, na redação da lista de problemas.
    ///
    /// Reusa o <see cref="PrescricaoService"/> em vez de consultar a tabela: a regra de
    /// qual alergia alerta (resolvida alerta, descartada não) mora lá, e duplicá-la aqui
    /// faria a folha impressa e a tela discordarem sobre o mesmo paciente.
    /// </summary>
    public async Task<IReadOnlyList<string>> AlergiasParaAFolhaAsync(
        PrescricaoInterna prescricao, CancellationToken ct = default)
    {
        var conferencia = await _conferenciaClinica.ContextoAsync(prescricao.PacienteId, ct);
        return conferencia.Alergias.Select(a => a.Rotulo).ToList();
    }

    private static string? NomeDoExecutante(PrescricaoInterna prescricao)
        => prescricao.Itens
               .Select(i => i.ChecagemVigente?.ExecutanteNome)
               .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

    private static string? ConselhoDoExecutante(PrescricaoInterna prescricao)
        => prescricao.Itens
               .Select(i => i.ChecagemVigente?.ExecutanteConselho)
               .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

    private static void Campo(ColumnDescriptor col, string rotulo, string texto)
        => col.Item().Column(c =>
        {
            c.Item().Text(rotulo).Bold().FontSize(8.5f).FontColor(TextoSecundario);
            c.Item().PaddingTop(2).Text(texto).FontSize(10);
        });

    private static void Faixa(
        ColumnDescriptor col, string fundo, string tinta, string titulo, string? detalhe)
        => col.Item().Background(fundo).Border(1).BorderColor(tinta).Padding(10).Column(c =>
        {
            c.Item().Text(titulo).Bold().FontSize(9.5f).FontColor(tinta);
            if (!string.IsNullOrWhiteSpace(detalhe))
                c.Item().Text(detalhe!).FontSize(9).FontColor(tinta);
        });

    private static void CabecalhoCelula(TableCellDescriptor h, string texto)
        => h.Cell().Background(FundoCabecalhoTabela)
            .BorderBottom(1).BorderColor(Borda).Padding(5)
            .Text(texto).Bold().FontSize(8).FontColor(TextoSecundario);

    private static IContainer Celula(TableDescriptor tabela)
        => tabela.Cell().BorderBottom(0.5f).BorderColor(Borda).PaddingVertical(5).PaddingHorizontal(4);

    private static string? PrimeiroPreenchido(params string?[] valores)
        => valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? Juntar(params string?[] partes)
    {
        var texto = string.Join("  ·  ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
        return texto.Length == 0 ? null : texto;
    }

    private static string Prefixado(string prefixo, string? valor)
        => string.IsNullOrWhiteSpace(valor) ? string.Empty : prefixo + valor.Trim();
}
