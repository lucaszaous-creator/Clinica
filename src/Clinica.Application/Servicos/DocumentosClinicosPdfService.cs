using Clinica.Application.Abstracoes;
using Clinica.Application.Assinatura;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;
using QRCoder;
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
/// Assinatura eletrônica (parcela 43)
/// ----------------------------------
/// Ela é OPCIONAL e muda o papel. Sem assinatura digital, o que sai impresso é o carimbo
/// do profissional, a linha de assinatura à caneta e o CÓDIGO DE CONFERÊNCIA — e o rodapé
/// diz exatamente isso, porque chamar carimbo de assinatura digital seria mentir sobre o
/// que a via garante. Com assinatura (<c>AssinaturaDeDocumentoClinicoService</c>), a folha
/// reserva a faixa do carimbo digital e o rodapé passa a dizer duas coisas que o
/// farmacêutico e o RH precisam saber: onde conferir o arquivo (o validador do ITI, que é
/// público e gratuito) e que <b>a via válida é o ARQUIVO, não esta impressão</b> — imprimir
/// um PDF assinado deixa a assinatura para trás, e é assim que uma receita eletrônica
/// legítima volta recusada do balcão.
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

    // ---- Geometria da folha, e por que ela é CONSTANTE ----
    //
    // O carimbo visível da assinatura digital é colado pelo PdfSharp num retângulo
    // ABSOLUTO da página, depois de o QuestPDF já ter desenhado tudo. Como o conteúdo
    // flui, o único lugar cuja posição se sabe de antemão é o RODAPÉ, que o QuestPDF
    // ancora na mesma altura em todas as páginas. Mexer numa destas constantes sem mexer
    // em AreaDaAssinatura() faz o carimbo cair em cima do texto.
    private const float AlturaPagina = 841.89f;      // A4 em pontos
    private const float MargemPagina = 42.52f;       // 1,5 cm
    private const float AlturaRodape = 104f;
    private const float AlturaFaixaAssinatura = 58f;

    /// <summary>
    /// Altura da área onde o traço do paciente é desenhado, acima da linha (parcela 66).
    ///
    /// 38 pontos, e não os 46 da primeira versão: com o bloco inteiro travado por
    /// `ShowEntire()`, cada ponto a mais aqui é um ponto a mais de chance de o bloco não
    /// caber e a via de uma página virar duas. Uma assinatura manuscrita cabe folgada em
    /// 38 pontos — a régua é o traço, não a área.
    /// </summary>
    private const float AlturaDaAssinatura = 38f;
    // 220 e não 250: o rodapé do documento assinado divide a faixa com o QR e com o
    // texto de conferência, e a 250 as três linhas quebravam no meio da palavra.
    private const float LarguraCarimbo = 220f;

    /// <summary>
    /// O validador OFICIAL de documentos digitais de SAÚDE — não o validador genérico de
    /// assinaturas.
    ///
    /// A diferença decide o que o balcão consegue responder. O genérico
    /// (<c>validar.iti.gov.br</c>) diz se o arquivo está íntegro e quem o assinou. Este,
    /// mantido pelo ITI com apoio dos Conselhos Federais de Medicina e de Farmácia,
    /// responde ainda a pergunta que o farmacêutico realmente precisa fazer: <b>quem
    /// assinou é prescritor com registro ATIVO?</b> — e é onde ele registra a dispensação.
    /// É o endereço que os CRFs mandam usar.
    /// </summary>
    public const string ValidadorOficial = "assinaturadigital.iti.gov.br";

    /// <summary>A página do farmacêutico, para o QR cair direto nela.</summary>
    public const string ValidadorFarmaceutico = "https://assinaturadigital.iti.gov.br/farmaceutico/";

    private readonly IClinicaRepositorio _repo;

    public DocumentosClinicosPdfService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Onde o carimbo da assinatura digital é colado. Sempre na ÚLTIMA página: é lá que
    /// está o fim do documento, e é a página que alguém confere.
    ///
    /// ⚠️ <b>O Y é medido do PÉ DA PÁGINA, não do topo.</b> O retângulo vai para o
    /// <c>/Rect</c> da anotação, e o PDF tem a origem no canto INFERIOR esquerdo — foi
    /// medido: o PDFsharp grava o que recebe, sem converter. A fórmula antiga calculava a
    /// distância a partir do TOPO e o carimbo saía a ~156 pontos do alto da folha, em cima
    /// do título, enquanto o rodapé reservava o espaço dele e ficava vazio. Saiu assim em
    /// TODO documento assinado até 20/08/2026.
    ///
    /// A conta agora é a do próprio rodapé: ele ocupa <c>AlturaRodape</c> logo acima da
    /// margem de baixo, e a faixa da assinatura é o PRIMEIRO item da coluna dele — ou
    /// seja, o topo dessa faixa. Os 2 pontos são o respiro de cada lado (a altura tira 4).
    /// </summary>
    public static AreaAssinatura AreaDaAssinatura(int totalPaginas)
        => new(
            Pagina: Math.Max(0, totalPaginas - 1),
            X: MargemPagina,
            Y: MargemPagina + AlturaRodape - AlturaFaixaAssinatura + 2,
            Largura: LarguraCarimbo,
            Altura: AlturaFaixaAssinatura - 4);

    /// <summary>
    /// O PDF do documento, pronto para imprimir ou enviar.
    ///
    /// <b>Documento assinado devolve os BYTES GUARDADOS</b>, nunca um PDF novo: a
    /// assinatura cobre uma faixa de bytes do arquivo, e um documento "igual" regerado
    /// agora abriria como INVÁLIDO no leitor de quem confere. A regra mora aqui, e não em
    /// cada tela, de propósito — são seis telas chamando este método (ficha do paciente,
    /// central de documentos, prescrições da Recepção, prescrições do Consultório e as
    /// duas do financeiro), e uma que esquecesse produziria uma segunda via inválida sem
    /// nenhum sinal de que algo deu errado.
    /// </summary>
    public async Task<byte[]> GerarAsync(
        int documentoId, DadosPrestador? prestador = null, CancellationToken ct = default)
    {
        var documento = await _repo.ObterDocumentoAsync(documentoId, ct)
            ?? throw new InvalidOperationException($"Documento {documentoId} não encontrado.");

        // Documento gravado por uma versão mais nova: esta não sabe montar o miolo dele. A
        // folha SAIRIA — com cabeçalho, número e assinatura, e sem as declarações no meio —,
        // e é justamente a garantia aparente que este serviço recusa desde a parcela 3.
        // A segunda via guardada continua saindo (o `return` abaixo), porque ali os bytes já
        // existem e não dependem deste build entender o tipo.
        if (documento.Tipo == TipoDocumentoClinico.Desconhecido
            && documento.ArquivoAssinadoId is null)
            throw new InvalidOperationException(
                $"O documento {documento.Numero} é de um tipo que esta versão do sistema não "
                + "conhece, e imprimi-lo produziria uma folha incompleta. Atualize o sistema "
                + "para abri-lo.");

        if (documento.ArquivoAssinadoId is int arquivoId
            && await _repo.ObterArquivoAssinadoAsync(arquivoId, ct) is { } guardado)
            return guardado.Conteudo;

        // O traço do paciente (parcela 66) só sai do banco quando o documento tem um: é a
        // única leitura que traz imagem, e pagá-la em toda receita seria arrastar bytes
        // para desenhar uma folha que não tem assinatura de paciente nenhuma.
        var traco = documento.TracoAssinaturaId is int tracoId
            ? (await _repo.ObterTracoAssinaturaAsync(tracoId, ct))?.Conteudo
            : null;

        return Gerar(documento, prestador, tracoPaciente: traco);
    }

    /// <param name="paraAssinaturaEletronica">
    /// A folha vai ser assinada com certificado logo depois de gerada. Reserva a faixa do
    /// carimbo e troca o rodapé — a via impressa deixa de ser o documento e passa a ser
    /// cópia dele.
    /// </param>
    /// <param name="urlDoArquivo">
    /// Endereço público do arquivo assinado (parcela 53). Quando vem preenchido, o QR passa
    /// a apontar para o DOCUMENTO em vez de para o validador — o farmacêutico escaneia da
    /// tela do celular do paciente e baixa o arquivo, sem ninguém enviar nada.
    ///
    /// Precisa chegar aqui ANTES da assinatura, porque a assinatura sela os bytes e o QR é
    /// um deles. Quem garante a ordem é o <c>PublicacaoDocumentoService.GarantirTokenAsync</c>.
    /// </param>
    /// <param name="tracoPaciente">
    /// O PNG da assinatura do paciente (parcela 66). Quando vem preenchido, ele é desenhado
    /// no lugar da linha em branco, com a frase de evidência embaixo.
    ///
    /// ⚠️ Ele precisa entrar ANTES da assinatura com certificado, e não depois: o selo cobre
    /// uma faixa de BYTES, e acrescentar a imagem em seguida invalidaria a assinatura do
    /// profissional. Quem impede a ordem errada é o
    /// <c>AssinaturaDoPacienteService.ColherAsync</c>, que recusa colher traço em documento
    /// já assinado eletronicamente.
    /// </param>
    public byte[] Gerar(
        DocumentoClinico documento, DadosPrestador? prestador = null,
        bool paraAssinaturaEletronica = false, string? urlDoArquivo = null,
        byte[]? tracoPaciente = null)
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

                    PainelPaciente(
                        col, paciente,
                        comEndereco: documento.Tipo == TipoDocumentoClinico.Receita);

                    if (!string.IsNullOrWhiteSpace(documento.Corpo))
                        Paragrafos(col, documento.Corpo!,
                            // O TERMO é o único documento cujo corpo é um texto LONGO — dez,
                            // doze parágrafos de consentimento. Com o espaçamento de 10 que
                            // serve à receita (duas linhas de orientação), ele empurrava a
                            // assinatura para uma segunda página que só tinha a assinatura.
                            // Compacto, a mesma folha cabe inteira — e a via que o paciente
                            // assina e guarda tem de ser UMA (parcela 66, 4ª rodada).
                            compacto: documento.Tipo == TipoDocumentoClinico.TermoProcedimento);

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

                        case TipoDocumentoClinico.TermoProcedimento:
                            Declaracoes(col, itens);
                            break;
                    }

                    if (!string.IsNullOrWhiteSpace(documento.Observacoes))
                        col.Item().Background(FundoSuave).Border(1).BorderColor(Borda).Padding(10).Text(t =>
                        {
                            t.Span("Observações  ").Bold().FontSize(8.5f).FontColor(TextoSecundario);
                            t.Span(documento.Observacoes!).FontSize(9.5f);
                        });


                    Assinaturas(col, documento, paraAssinaturaEletronica, tracoPaciente);
                });

                Rodape(page, documento, paraAssinaturaEletronica, urlDoArquivo);
            });
        }).GeneratePdf();
    }

    // ==================== Blocos ====================

    /// <summary>
    /// O rodapé, e a faixa de altura fixa onde o carimbo digital é colado depois.
    ///
    /// Duas frases mudam quando há assinatura eletrônica, e nenhuma delas é decoração:
    ///
    /// 1. <b>onde conferir</b> — o validador do ITI é público, gratuito e é o que o
    ///    farmacêutico usa para verificar assinatura, integridade e emissor. Sem essa
    ///    linha, o paciente entrega um PDF e a farmácia não tem como saber o que fazer
    ///    com ele; foi por não existir esse endereço que a primeira leitura do problema
    ///    concluiu que precisaríamos construir um portal de validação;
    ///
    /// 2. <b>que a impressão é cópia</b> — imprimir um PDF assinado deixa a assinatura
    ///    para trás (ela vive nos bytes do arquivo, não na tinta). Quem leva só o papel
    ///    leva um documento sem a garantia que o sistema produziu, e essa é a forma mais
    ///    comum de uma receita eletrônica legítima voltar recusada do balcão.
    /// </summary>
    private static void Rodape(
        PageDescriptor page, DocumentoClinico documento, bool assinaturaEletronica,
        string? urlDoArquivo = null)
    {
        page.Footer().Height(assinaturaEletronica ? AlturaRodape : 34).Column(col =>
        {
            if (assinaturaEletronica)
                col.Item().Height(AlturaFaixaAssinatura).Row(row =>
                {
                    // Esquerda: reservada, e VAZIA de propósito — é aqui que o PdfSharp
                    // cola o carimbo com titular, CPF do certificado e emissor.
                    row.ConstantItem(LarguraCarimbo);
                    row.ConstantItem(16);

                    // O QR leva à PÁGINA DO FARMACÊUTICO do validador, não a um documento
                    // hospedado: nós não hospedamos nada, e prometer que o QR "abre a
                    // receita" seria mentir sobre o que ele faz. Ele poupa a digitação do
                    // endereço no balcão, e o arquivo continua sendo o que se confere lá.
                    var qrDaFolha = urlDoArquivo is null ? QrDoValidador() : Qr(urlDoArquivo);
                    if (qrDaFolha is { } qr)
                    {
                        row.ConstantItem(AlturaFaixaAssinatura - 6).AlignBottom().Image(qr);
                        row.ConstantItem(8);
                    }

                    row.RelativeItem().AlignBottom().Column(c =>
                    {
                        c.Item().Text("Assinado digitalmente — ICP-Brasil")
                            .SemiBold().FontSize(8.5f);
                        // Uma linha só, discreta, como fazem os sistemas do mercado.
                        // O bloco de três passos e o aviso de "impressão é cópia" saíram
                        // na parcela 52: NENHUMA norma os exigia — nem o art. 35 da Lei
                        // 5.991/1973 (que lista o CONTEÚDO da receita), nem a Lei
                        // 14.063/2020 (que trata da assinatura). Eram escolha nossa, e
                        // ocupavam um terço da folha com instrução que o farmacêutico já
                        // recebe do próprio CRF.
                        // ⚠️ A linha diz COM O QUÊ escanear e POR QUAL BOTÃO validar —
                        // porque o validador tem um botão "Ler QR Code" que NÃO é isto:
                        // aquele fluxo é para plataformas cadastradas no ITI (ele chama a
                        // URL pedindo application/validador-iti+json com _secretCode) e
                        // PEDE UM CÓDIGO que esta folha não tem. A clínica descobriu no
                        // balcão (16/08/2026), com a receita na mão: leu o QR lá dentro e
                        // caiu numa caixa de código impossível de preencher — a dois
                        // centímetros do NOSSO código de conferência, que não é dele.
                        c.Item().Text(urlDoArquivo is null
                                ? $"Validar em {ValidadorOficial}, enviando o arquivo do documento"
                                : $"Escaneie com a câmera do celular para baixar o arquivo. "
                                  + $"Valide em {ValidadorOficial} colando o link ou enviando "
                                  + "o arquivo — o \u201CLer QR Code\u201D de lá pede um código "
                                  + "que esta folha não tem.")
                            .FontSize(8).FontColor(TextoSecundario);
                    });
                });

            col.Item().PaddingBottom(4).LineHorizontal(0.75f).LineColor(Borda);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("Conferência  ").FontSize(8).FontColor(TextoSecundario);
                    t.Span(documento.CodigoVerificacao).SemiBold().FontSize(8.5f);
                    t.Span("  ·  código interno da clínica (confere-se na ficha do "
                           + "paciente) — não é o código que o validador gov.br pede.")
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
    }

    /// <param name="comEndereco">
    /// O endereço residencial só sai na RECEITA, onde o art. 35 da Lei 5.991/1973 o exige
    /// para ela poder ser aviada. Num atestado ele iria junto para a mão do RH sem
    /// nenhuma exigência que o justifique, e dado que não precisa sair não sai — é a
    /// mesma economia do CID, que só é impresso com autorização do paciente.
    /// </param>
    /// <summary>
    /// O QR do validador oficial, em PNG. Gerado uma vez por processo — o conteúdo é uma
    /// constante, e recodificar a cada folha seria trabalho puro.
    ///
    /// Devolve null se a codificação falhar: um documento sem QR continua conferível (o
    /// endereço está escrito ao lado, por extenso). Derrubar a emissão de uma receita por
    /// causa de um quadradinho seria trocar o essencial pelo acessório.
    /// </summary>
    public static byte[]? QrDoValidador()
        => _qrValidador ??= Qr(ValidadorFarmaceutico);

    /// <summary>
    /// QR de uma URL qualquer. Sem cache quando é o link do DOCUMENTO (parcela 53): ele é
    /// único por receita, e cachear devolveria o QR de outro paciente — o pior defeito
    /// possível nesta folha.
    /// </summary>
    private static byte[]? Qr(string url)
    {
        try
        {
            using var gerador = new QRCodeGenerator();
            using var dados = gerador.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            return new PngByteQRCode(dados).GetGraphic(6);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("DocumentosClinicosPdfService.Qr", ex);
            return null;
        }
    }

    private static byte[]? _qrValidador;


    private static void PainelPaciente(
        ColumnDescriptor col, Paciente? paciente, bool comEndereco = false)
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

            if (!comEndereco) return;

            c.Item().PaddingTop(4).Text(t =>
            {
                t.Span("Endereço  ").FontSize(8.5f).FontColor(TextoSecundario);
                t.Span(string.IsNullOrWhiteSpace(paciente?.Endereco)
                    // Escrito, e não omitido: quem recebe o papel precisa ver que a
                    // exigência ficou por cumprir, em vez de descobrir na farmácia.
                    ? "(não informado — exigido pelo art. 35 da Lei 5.991/1973)"
                    : paciente!.Endereco!).FontSize(9.5f);
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
    /// As declarações do termo de procedimento (parcela 66), com a resposta do paciente.
    ///
    /// A resposta sai por EXTENSO ("Sim" / "Não") e não como um X marcado: o que se
    /// contesta num termo é justamente o que a pessoa afirmou, e um quadradinho preenchido
    /// admite a leitura de que alguém o marcou depois. A negada sai destacada, porque quem
    /// pega o papel na sala precisa ver "NÃO — jejum" sem procurar.
    ///
    /// Declaração sem resposta sai com um traço, nunca em branco: em branco não se
    /// distingue de linha que a impressora comeu.
    /// </summary>
    private static void Declaracoes(ColumnDescriptor col, IReadOnlyList<ItemDocumento> itens)
    {
        if (itens.Count == 0) return;

        col.Item().PaddingTop(4).Text("Declarações do paciente")
            .Bold().FontSize(11).FontColor(AzulEscuro);

        foreach (var item in itens)
        {
            var negativa = RespostaDeclaracao.EhNegativa(item.Quantidade);
            var respondida = !string.IsNullOrWhiteSpace(item.Quantidade);

            // `ShowEntire()`: a declaração e a resposta dela são UMA linha. Quebrada, o
            // papel diria "Estou em jejum de 8 horas" no fim de uma página e "Não" no
            // começo da outra — que é a pior forma possível de partir este documento.
            col.Item().ShowEntire()
                .BorderBottom(1).BorderColor(Borda).PaddingVertical(6).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(item.Descricao).SemiBold().FontSize(10.5f);
                    if (!string.IsNullOrWhiteSpace(item.Detalhe))
                        c.Item().Text(item.Detalhe!).FontSize(9).FontColor(TextoSecundario);
                });

                row.ConstantItem(70).AlignRight().AlignMiddle()
                    .Text(respondida ? item.Quantidade! : "—")
                    .Bold().FontSize(10)
                    .FontColor(negativa ? VermelhoForte
                        : respondida ? VerdeForte : TextoSecundario);
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

    /// <summary>
    /// Carimbo do profissional e linhas de assinatura.
    ///
    /// Com assinatura eletrônica a linha do PROFISSIONAL some, e o nome dele vai no
    /// carimbo digital do rodapé. Não é economia de espaço: linha em branco embaixo de um
    /// documento já assinado convida alguém a assinar a cópia, e aí passam a existir duas
    /// vias com autoria diferente para o mesmo documento. A do PACIENTE continua, porque
    /// consentimento e anamnese são assinados por ele, na folha, e o certificado da
    /// clínica não assina no lugar dele.
    /// </summary>
    private static void Assinaturas(
        ColumnDescriptor col, DocumentoClinico documento, bool assinaturaEletronica,
        byte[]? tracoPaciente = null)
    {
        var assinaPaciente = documento.Tipo is TipoDocumentoClinico.Consentimento
                                 or TipoDocumentoClinico.Anamnese
                                 or TipoDocumentoClinico.TermoProcedimento;

        // ---- Termo já assinado pelo paciente (parcela 66) ----
        //
        // O traço entra no lugar da linha em branco, e a frase de evidência embaixo dele.
        // ⚠️ A frase diz "assinatura eletrônica simples" e nunca "assinatura digital": o
        // traço não é certificado, e chamá-lo assim seria a garantia aparente que este
        // serviço se recusa a produzir desde a parcela 3 — a mesma razão pela qual o
        // carimbo do profissional não se chama assinatura digital.
        if (documento.PacienteAssinou && tracoPaciente is { Length: > 0 })
        {
            // ⚠️ UM ÚNICO item, e `ShowEntire()` (parcela 66, 4ª rodada — o cliente mandou a
            // foto do PDF em duas páginas).
            //
            // Antes eram TRÊS itens irmãos — a linha de assinaturas, a frase de evidência e
            // o aviso do selo —, e o QuestPDF quebra a página ENTRE itens: as linhas
            // ficaram no fim da página 1 e os nomes embaixo delas foram para a 2, com o
            // cabeçalho repetido no meio. A via saía com a assinatura órfã de quem assinou,
            // que é o pior lugar possível para um documento se partir.
            //
            // `ShowEntire()` faz o bloco andar inteiro: ou cabe onde está, ou desce todo.
            col.Item().PaddingTop(18).ShowEntire().Column(bloco =>
            {
                bloco.Item().Row(row =>
                {
                    if (!assinaturaEletronica)
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Height(AlturaDaAssinatura);
                            c.Item().LineHorizontal(1).LineColor(TextoSecundario);
                            c.Item().PaddingTop(3).AlignCenter()
                                .Text(documento.Profissional?.Nome ?? "Profissional responsável")
                                .SemiBold().FontSize(9.5f);
                            c.Item().AlignCenter()
                                .Text(documento.Profissional?.RegistroConselho is { Length: > 0 } r
                                    ? r : "Assinatura e carimbo")
                                .FontSize(8.5f).FontColor(TextoSecundario);
                        });

                        row.ConstantItem(40);
                    }

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Height(AlturaDaAssinatura).AlignBottom().AlignCenter()
                            .Image(tracoPaciente).FitArea();
                        c.Item().LineHorizontal(1).LineColor(TextoSecundario);
                        c.Item().PaddingTop(3).AlignCenter()
                            .Text(documento.Paciente?.Nome ?? "Paciente")
                            .SemiBold().FontSize(9.5f);
                        c.Item().AlignCenter().Text("Paciente (ou responsável)")
                            .FontSize(8.5f).FontColor(TextoSecundario);
                    });
                });

                bloco.Item().PaddingTop(8).Text(documento.FraseAssinaturaPaciente)
                    .FontSize(7.5f).FontColor(TextoSecundario);

                // O selo RECALCULADO, e não o gravado (parcela 66, 2ª rodada). Guardar um
                // hash que ninguém confere é guardar um número: a garantia só existe quando
                // tem consequência visível, e esta é a consequência — a segunda via de um
                // termo alterado depois da assinatura sai DIZENDO que não prova o que foi
                // assinado.
                if (documento.AvisoDeSeloQuebrado is { Length: > 0 } aviso)
                    bloco.Item().PaddingTop(6)
                        .Background(VermelhoSuave).Border(1).BorderColor(VermelhoForte).Padding(8)
                        .Text(aviso).SemiBold().FontSize(8).FontColor(VermelhoForte);
            });

            return;
        }

        // Recusa: o papel diz que o termo foi apresentado e não foi assinado. Sair com a
        // linha em branco faria a via parecer um termo que ninguém chegou a mostrar.
        if (documento.PacienteRecusou)
        {
            col.Item().PaddingTop(24)
                .Background(VermelhoSuave).Border(1).BorderColor(VermelhoForte).Padding(10)
                .Text(documento.FraseAssinaturaPaciente)
                .SemiBold().FontSize(9).FontColor(VermelhoForte);
            return;
        }

        // Na via ASSINADA não há linha para assinar — mas o prescritor tem de aparecer.
        //
        // O art. 35 da Lei 5.991/1973 lista o NÚMERO DE INSCRIÇÃO NO CONSELHO como
        // conteúdo da receita, não como parte da assinatura. Até aqui o documento assinado
        // saía sem nome e sem CRM em lugar nenhum do corpo: a identidade existia só dentro
        // do certificado, visível no validador do ITI ou no painel de assinatura do leitor
        // de PDF. O farmacêutico que abre o arquivo via uma receita sem prescritor.
        //
        // Descoberto GERANDO a receita assinada, não lendo o código — e é exatamente o
        // documento que a clínica quer que a farmácia atenda.
        if (assinaturaEletronica && !assinaPaciente)
        {
            col.Item().PaddingTop(28).AlignCenter().Column(c =>
            {
                c.Item().AlignCenter()
                    .Text(documento.Profissional?.Nome ?? "Profissional responsável")
                    .SemiBold().FontSize(9.5f);

                var conselho = documento.Profissional?.RegistroConselho;
                if (!string.IsNullOrWhiteSpace(conselho))
                    c.Item().AlignCenter().Text(conselho!)
                        .FontSize(8.5f).FontColor(TextoSecundario);

                c.Item().AlignCenter()
                    .Text("Assinado eletronicamente — não requer assinatura manuscrita")
                    .FontSize(8f).FontColor(TextoSecundario);
            });
            return;
        }

        // `ShowEntire()` pela razão do bloco assinado acima: a linha de assinatura e o nome
        // embaixo dela são UMA coisa, e quebrar entre os dois deixa a via com um traço
        // órfão no fim de uma página e o nome de quem assinou no começo da seguinte.
        col.Item().PaddingTop(30).ShowEntire().Row(row =>
        {
            if (!assinaturaEletronica)
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

    /// <param name="compacto">
    /// Espaçamento e corpo menores, para texto LONGO. O espaçamento de 10 pontos entre
    /// itens vem da coluna da página e serve a documento curto; num termo de doze
    /// parágrafos ele sozinho soma mais de cem pontos — uma folha inteira de respiro num
    /// papel que precisa caber numa página só.
    /// </param>
    private static void Paragrafos(ColumnDescriptor col, string texto, bool compacto = false)
    {
        var paragrafos = texto.Split('\n')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (!compacto)
        {
            foreach (var p in paragrafos) col.Item().Text(p).FontSize(10.5f);
            return;
        }

        // Uma coluna PRÓPRIA, com o espaçamento dela: o `Spacing` da coluna da página vale
        // entre os blocos da folha (paciente, corpo, declarações, assinatura) e não deve
        // mudar por causa do miolo de um tipo só.
        col.Item().Column(c =>
        {
            c.Spacing(5);
            foreach (var p in paragrafos) c.Item().Text(p).FontSize(10f).LineHeight(1.25f);
        });
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
