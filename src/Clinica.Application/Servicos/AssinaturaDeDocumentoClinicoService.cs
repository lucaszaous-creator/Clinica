using Clinica.Application.Abstracoes;
using Clinica.Application.Assinatura;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O documento assinado, pronto para entregar ao paciente.</summary>
/// <param name="Pdf">Os bytes que valem — os mesmos que ficaram guardados.</param>
/// <param name="NomeArquivo">Nome sugerido do arquivo; é ele que o paciente recebe.</param>
/// <param name="Conferencia">
/// O resultado de reconferir a assinatura logo depois de produzi-la. Nunca se assume que
/// deu certo: se a conferência não fecha, o arquivo não deveria sair daqui.
/// </param>
public sealed record DocumentoAssinado(
    byte[] Pdf, string NomeArquivo, DocumentoClinico Documento, ConferenciaAssinatura Conferencia)
{
    /// <summary>
    /// O que aconteceu com o link público (parcela 53). Null = publicação desligada ou
    /// tipo que não se publica. Falha aqui NÃO invalida a assinatura — mas a tela tem de
    /// mostrar, senão a secretária entrega dizendo "é só escanear" e o QR está morto.
    /// </summary>
    public ResultadoPublicacao? Publicacao { get; init; }
}

/// <summary>
/// Assinatura ICP-Brasil dos documentos da feature 07 — receita, atestado, declaração de
/// comparecimento e pedido de exame (parcela 43).
///
/// Por que a parcela existe
/// ------------------------
/// A parcela 42 pôs assinatura qualificada na folha de infusão, que é um documento
/// INTERNO — o único que a lei dispensa (art. 13, parágrafo único, da Lei 14.063/2020:
/// "não se aplica aos atos internos do ambiente hospitalar"). Os quatro documentos que
/// SAEM da clínica, que são os que a lei realmente disciplina, continuavam com carimbo
/// impresso e código de conferência. O motor já estava pronto e testado; faltava a porta.
///
/// O que a lei exige, e o que isto entrega
/// ---------------------------------------
/// <list type="bullet">
/// <item><b>Atestado em meio eletrônico</b> — art. 13 da Lei 14.063/2020: só vale com
/// assinatura eletrônica QUALIFICADA. É o único caso em que o arquivo sem certificado
/// não é um documento com defeito, e sim um arquivo sem valor.</item>
/// <item><b>Receita simples, pedido de exame, declaração, relatório</b> — art. 14 da mesma
/// lei: valem com assinatura avançada OU qualificada. Assinamos com a qualificada porque
/// é a que a farmácia consegue conferir sozinha, sem cadastro em plataforma nenhuma.</item>
/// <item><b>Conteúdo da receita</b> — art. 35 da Lei 5.991/1973. Ver
/// <see cref="ConformidadeDocumentoClinico"/>.</item>
/// <item><b>Receituário de controle especial</b> — fora de escopo, e de propósito: depende
/// do SNCR da ANVISA. Ver <c>docs/prescricao-eletronica-conformidade.md</c>.</item>
/// </list>
///
/// A única recusa deste serviço, e por que ela é recusa
/// ----------------------------------------------------
/// A emissão em papel apenas AVISA o que falta — a regra da casa. Aqui a falta IMPEDE, e
/// o motivo é que a assinatura é irreversível: ela sela os bytes, e corrigir o endereço
/// do paciente depois exige cancelar o documento e emitir outro. Pior, um documento
/// assinado é o que mais PARECE oficial; assinar uma receita que a farmácia não pode
/// aviar produz exatamente o objeto que este projeto se recusa a produzir desde a parcela
/// 3 — uma garantia aparente. Vale a mesma lógica da divergência do fechamento de caixa e
/// da rodela sem justificativa.
/// </summary>
public sealed class AssinaturaDeDocumentoClinicoService
{
    private readonly IClinicaRepositorio _repo;
    private readonly DocumentosClinicosPdfService _pdfs;
    private readonly AssinaturaDigitalService _assinador;
    private readonly ParametrosService _parametros;

    /// <summary>
    /// Publicação do arquivo assinado (parcela 53). OPCIONAL: nulo mantém o comportamento
    /// anterior por inteiro — QR para o validador, nada sobe para lugar nenhum. É o que
    /// permite a clínica rodar sem contratar armazenamento.
    /// </summary>
    private readonly PublicacaoDocumentoService? _publicacao;

    public AssinaturaDeDocumentoClinicoService(
        IClinicaRepositorio repo,
        DocumentosClinicosPdfService pdfs,
        AssinaturaDigitalService assinador,
        ParametrosService parametros,
        PublicacaoDocumentoService? publicacao = null)
    {
        _repo = repo;
        _pdfs = pdfs;
        _assinador = assinador;
        _parametros = parametros;
        _publicacao = publicacao;
    }

    /// <summary>
    /// Assina o documento com o certificado escolhido e guarda os bytes assinados.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Documento inexistente, cancelado, já assinado, certificado de outra pessoa, ou
    /// exigência legal por cumprir.
    /// </exception>
    public async Task<DocumentoAssinado> AssinarAsync(
        int documentoId, CertificadoAssinatura certificado,
        int? usuarioId = null, string? operador = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(certificado);

        var documento = await _repo.ObterDocumentoAsync(documentoId, ct)
            ?? throw new InvalidOperationException("Documento não encontrado.");

        if (documento.Cancelado)
            throw new InvalidOperationException(
                $"O documento {documento.Numero} está cancelado e não pode ser assinado. "
                + "Emita outro no lugar dele.");

        // Assinar de novo produziria um segundo arquivo válido do MESMO documento, com
        // data diferente — e nada diria qual dos dois o paciente levou. Documento emitido
        // é fato: corrige-se cancelando e emitindo outro.
        if (documento.AssinadoEletronicamente)
            throw new InvalidOperationException(
                $"O documento {documento.Numero} já foi assinado em "
                + $"{documento.AssinadoEm:dd/MM/yyyy HH:mm} por {documento.AssinanteNome}. "
                + "Para trocar o conteúdo, cancele e emita outro.");

        TitularDoCertificado.Exigir(
            certificado, documento.Profissional?.Cpf, documento.Profissional?.Nome);

        ExigirConformidade(documento);

        var prestador = await _parametros.ObterPrestadorAsync(ct);

        // O TOKEN NASCE AQUI, antes do PDF. A URL vai no QR e o QR é selado pela
        // assinatura — inverter esta ordem obrigaria a mexer no arquivo já assinado, o que
        // quebraria a própria assinatura. Null quando a publicação está desligada ou o
        // tipo não se publica, e aí o QR volta a apontar para o validador do ITI.
        var urlDoArquivo = _publicacao is null
            ? null
            : await _publicacao.GarantirTokenAsync(documento, ct);

        var pdf = _pdfs.Gerar(documento, prestador,
            paraAssinaturaEletronica: true, urlDoArquivo: urlDoArquivo);

        var assinado = await _assinador.AssinarAsync(pdf, certificado, new PedidoAssinatura(
            Motivo: $"{TipoDocumentoInfo.Rotular(documento.Tipo)} {documento.Numero}",
            NomeExibido: documento.Profissional?.Nome ?? certificado.Titular,
            RegistroConselho: documento.Profissional?.RegistroConselho,
            Area: DocumentosClinicosPdfService.AreaDaAssinatura(ContarPaginas(pdf)),
            CarimbadoraDeTempo: await _parametros.ObterCarimbadoraDeTempoAsync(ct)));

        // Conferir ANTES de gravar: assinatura que não fecha na própria máquina que a
        // produziu não vai fechar na farmácia, e gravá-la deixaria o sistema afirmando
        // uma garantia que o arquivo não tem.
        var conferencia = _assinador.Conferir(assinado.Pdf);
        if (!conferencia.Integra)
            throw new InvalidOperationException(
                $"A assinatura foi produzida mas não conferiu: {conferencia.Frase} "
                + "O documento NÃO foi marcado como assinado.");

        var nomeArquivo = NomeDoArquivo(documento);

        var arquivo = new ArquivoAssinado
        {
            Conteudo = assinado.Pdf,
            NomeArquivo = nomeArquivo,
            GeradoEm = DateTime.Now
        };

        await _repo.AdicionarArquivoAssinadoAsync(arquivo, ct);

        // Gravado antes para o Id existir: o documento aponta para o arquivo, e sem o Id
        // a referência ficaria nula e o PDF viraria órfão — dado guardado sem leitor, a
        // variante do defeito recorrente que este projeto já cometeu seis vezes.
        await _repo.SalvarAsync(ct);

        Carimbar(documento, certificado, assinado, arquivo.Id, usuarioId);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = operador ?? "?",
            Acao = "DocumentoClinicoAssinado",
            PacienteId = documento.PacienteId,
            Detalhe = $"{documento.Numero} · {TipoDocumentoInfo.Rotular(documento.Tipo)} · "
                    + $"titular {certificado.Titular} · CPF {Cpf.Formatar(certificado.Cpf)} · "
                    + $"hash {assinado.Hash[..12]}"
                    + (assinado.CarimboTempoEm is { } carimbo
                        ? $" · carimbo do tempo {carimbo:dd/MM/yyyy HH:mm}"
                        : " · sem carimbo do tempo (data declarada)")
        }, ct);

        await _repo.SalvarAsync(ct);

        // Publica DEPOIS de gravar: o documento já está assinado e válido, e a publicação
        // é efeito colateral. Falhar aqui não desfaz nada — vira frase no resultado, e a
        // tela mostra. Sucesso silencioso faria a secretária entregar dizendo "é só
        // escanear" com o QR apontando para um endereço que não existe.
        var publicacao = _publicacao is null
            ? null
            : await _publicacao.PublicarAsync(documento, assinado.Pdf, ct);

        return new DocumentoAssinado(assinado.Pdf, nomeArquivo, documento, conferencia)
        {
            Publicacao = publicacao
        };
    }

    /// <summary>
    /// Reconfere a assinatura de um documento já guardado — a pergunta da tela de
    /// conferência: <i>este arquivo continua sendo o que foi assinado?</i>
    ///
    /// Devolve o terceiro estado ("não foi possível conferir") em vez de <c>false</c>
    /// quando o documento não tem assinatura ou o arquivo sumiu: falha de conferência
    /// nunca pode ser exibida como sucesso, e "não tem assinatura" não é "foi adulterado".
    /// </summary>
    public async Task<ConferenciaAssinatura> ConferirAsync(
        int documentoId, CancellationToken ct = default)
    {
        var documento = await _repo.ObterDocumentoAsync(documentoId, ct);

        if (documento is null)
            return ConferenciaAssinatura.NaoConferida("documento não encontrado");

        if (!documento.AssinadoEletronicamente)
            return ConferenciaAssinatura.NaoConferida(
                "este documento não foi assinado eletronicamente");

        if (documento.ArquivoAssinadoId is not int arquivoId
            || await _repo.ObterArquivoAssinadoAsync(arquivoId, ct) is not { } arquivo)
            return ConferenciaAssinatura.NaoConferida(
                "o arquivo assinado não está mais guardado no banco");

        return _assinador.Conferir(arquivo.Conteudo);
    }

    /// <summary>
    /// O que falta para o documento cumprir a lei — a mesma leitura que a tela de emissão
    /// mostra, exposta aqui para quem só tem o Id em mãos.
    /// </summary>
    public async Task<IReadOnlyList<ExigenciaLegal>> ConferirConformidadeAsync(
        int documentoId, bool assinaturaEletronica = true, CancellationToken ct = default)
    {
        var documento = await _repo.ObterDocumentoAsync(documentoId, ct)
            ?? throw new InvalidOperationException("Documento não encontrado.");

        return ConformidadeDocumentoClinico.Conferir(documento, assinaturaEletronica);
    }

    // ---- Apoio ----

    private static void ExigirConformidade(DocumentoClinico documento)
    {
        var faltas = ConformidadeDocumentoClinico
            .Conferir(documento, assinaturaEletronica: true)
            .Where(f => f.Impeditiva)
            .ToList();

        if (faltas.Count == 0) return;

        // A recusa DIZ o que falta, item a item. Guarda que volta em silêncio é botão que
        // não faz nada — e aqui seria pior: o profissional concluiria que o certificado
        // está com problema e iria mexer no token.
        throw new InvalidOperationException(
            $"Este documento ainda não cumpre o que a lei exige, e assinar assim produziria "
            + $"um arquivo que parece oficial e não serve:{Environment.NewLine}"
            + string.Join(
                Environment.NewLine, faltas.Select(f => $"  · {f.Frase}")));
    }

    private static void Carimbar(
        DocumentoClinico documento, CertificadoAssinatura certificado,
        ResultadoAssinatura assinado, int arquivoId, int? usuarioId)
    {
        documento.AssinaturaTipo = TipoAssinatura.IcpBrasil;
        documento.AssinaturaHash = assinado.Hash;
        documento.AssinaturaAlgoritmo = "SHA-256";
        documento.AssinadoEm = DateTime.Now;
        documento.AssinadoPorUsuarioId = usuarioId;

        // Nome e conselho são COPIADOS: o cadastro da equipe muda, e o documento que o
        // paciente levou não pode mudar junto.
        documento.AssinanteNome = documento.Profissional?.Nome ?? certificado.Titular;
        documento.AssinanteRegistroConselho = documento.Profissional?.RegistroConselho;
        documento.AssinanteCpf = certificado.Cpf;

        documento.CertificadoTitular = certificado.Titular;
        documento.CertificadoEmissor = certificado.Emissor;
        documento.CertificadoSerie = certificado.NumeroSerie;
        documento.CertificadoValidoDe = certificado.ValidoDe;
        documento.CertificadoValidoAte = certificado.ValidoAte;

        documento.CarimboTempoEm = assinado.CarimboTempoEm;
        documento.CarimboTempoAutoridade = assinado.CarimboTempoAutoridade;

        documento.ArquivoAssinadoId = arquivoId;
    }

    private static string NomeDoArquivo(DocumentoClinico documento)
        => $"{TipoDocumentoInfo.Rotular(documento.Tipo)}-"
           + $"{documento.Numero.Replace('/', '-')}-assinado.pdf";

    private static int ContarPaginas(byte[] pdf)
    {
        using var fluxo = new MemoryStream(pdf, writable: false);
        using var documento = PdfSharp.Pdf.IO.PdfReader.Open(
            fluxo, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        return documento.PageCount;
    }
}
