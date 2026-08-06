using Clinica.Application.Abstracoes;
using Clinica.Application.Assinatura;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Uma das duas folhas, pronta para ler ou reimprimir.</summary>
/// <param name="Conferencia">
/// Só vem preenchida quando a folha tem assinatura eletrônica. É ela que diz se o arquivo
/// continua íntegro — nunca se assume que sim.
/// </param>
public sealed record FolhaAssinada(
    byte[] Pdf,
    string NomeArquivo,
    AssinaturaDocumento? Assinatura,
    ConferenciaAssinatura? Conferencia);

/// <summary>
/// A ORQUESTRAÇÃO da assinatura: gerar o PDF, assinar com o certificado, guardar os bytes
/// e gravar o fato (parcela 42).
///
/// Por que é um serviço, e não código de tela
/// ------------------------------------------
/// Porque a sequência tem uma regra que não pode morar num clique: <b>o CPF do certificado
/// tem de bater com o CPF do profissional</b>. Sem ela, "assinatura qualificada" provaria
/// apenas que alguém com algum token assinou — e o e-CPF da recepcionista assinaria a
/// prescrição da médica sem um único alerta. É a metade que faz a garantia valer, e uma
/// regra dessas escrita no code-behind de uma janela sobrevive até a segunda tela que
/// precisar assinar.
///
/// A divisão de trabalho com os outros três
/// ----------------------------------------
/// <see cref="PrescricaoInternaPdfService"/> desenha, <see cref="AssinaturaDigitalService"/>
/// assina e confere, <see cref="PrescricaoInternaService"/> e
/// <see cref="ChecagemPrescricaoService"/> guardam as regras de negócio. Este aqui só os
/// põe na ordem certa — e é por isso que os testes daqueles três não precisam de
/// certificado nenhum.
/// </summary>
public sealed class AssinaturaDePrescricaoService
{
    private readonly IClinicaRepositorio _repo;
    private readonly PrescricaoInternaService _prescricoes;
    private readonly PrescricaoInternaPdfService _pdfs;
    private readonly AssinaturaDigitalService _assinador;
    private readonly ParametrosService _parametros;

    public AssinaturaDePrescricaoService(
        IClinicaRepositorio repo,
        PrescricaoInternaService prescricoes,
        PrescricaoInternaPdfService pdfs,
        AssinaturaDigitalService assinador,
        ParametrosService parametros)
    {
        _repo = repo;
        _prescricoes = prescricoes;
        _pdfs = pdfs;
        _assinador = assinador;
        _parametros = parametros;
    }

    /// <summary>
    /// Assina a prescrição: gera a folha, sela com o certificado do prescritor e grava.
    /// </summary>
    public async Task<ResultadoAssinaturaPrescricao> AssinarPrescricaoAsync(
        int prescricaoId, CertificadoAssinatura certificado,
        bool confirmouAlergia = false, int? usuarioId = null, string? operador = null,
        CancellationToken ct = default)
    {
        var prescricao = await _repo.ObterPrescricaoInternaAsync(prescricaoId, ct)
            ?? throw new InvalidOperationException("Prescrição não encontrada.");

        TitularDoCertificado.Exigir(
            certificado, prescricao.Profissional?.Cpf, prescricao.Profissional?.Nome);

        var pdf = await _pdfs.GerarPrescricaoAsync(prescricaoId, await PrestadorAsync(ct), ct);

        var assinado = await SelarAsync(
            pdf, certificado,
            motivo: $"Prescrição de execução interna {prescricao.Numero}",
            nomeExibido: prescricao.Profissional?.Nome ?? certificado.Titular,
            registroConselho: prescricao.Profissional?.RegistroConselho,
            ct);

        var arquivo = await GuardarAsync(assinado.Pdf, $"{prescricao.Numero}.pdf", ct);

        var assinatura = Montar(
            certificado, assinado, arquivo, usuarioId,
            prescricao.Profissional?.Nome ?? certificado.Titular,
            prescricao.Profissional?.RegistroConselho);

        return await _prescricoes.AssinarAsync(
            prescricaoId, assinatura, confirmouAlergia, operador, ct);
    }

    /// <summary>
    /// A folha para ler ou reimprimir.
    ///
    /// A <b>Prescrição</b>, quando assinada, devolve os <b>bytes GUARDADOS</b> — nunca um
    /// PDF novo: a assinatura cobre uma faixa de bytes do arquivo, e um documento "igual"
    /// regerado agora teria outra, então a segunda via sairia inválida.
    ///
    /// O <b>Registro de execução</b> é sempre montado na hora, e isso não é descuido: ele
    /// não é assinado eletronicamente (quem assina a execução é a enfermeira, na via
    /// impressa) e ele MUDA enquanto a folha está aberta, a cada item checado. Congelá-lo
    /// faria a reimpressão mostrar um estado que já passou.
    /// </summary>
    public async Task<FolhaAssinada> FolhaAsync(
        int prescricaoId, FolhaPrescricao folhaPedida, CancellationToken ct = default)
    {
        var prescricao = await _repo.ObterPrescricaoInternaAsync(prescricaoId, ct)
            ?? throw new InvalidOperationException("Prescrição não encontrada.");

        var ehPrescricao = folhaPedida == FolhaPrescricao.Prescricao;
        var assinatura = ehPrescricao ? prescricao.AssinaturaDoPrescritor : null;
        var sufixo = ehPrescricao ? string.Empty : " execucao";
        var nome = $"{prescricao.Numero.Replace('/', '-')}{sufixo}.pdf";

        if (assinatura?.ArquivoId is int arquivoId
            && await _repo.ObterArquivoAssinadoAsync(arquivoId, ct) is { } guardado)
        {
            return new FolhaAssinada(
                guardado.Conteudo, guardado.NomeArquivo,
                assinatura, _assinador.Conferir(guardado.Conteudo));
        }

        var prestador = await PrestadorAsync(ct);
        var pdf = ehPrescricao
            ? await _pdfs.GerarPrescricaoAsync(prescricaoId, prestador, ct)
            : await _pdfs.GerarRegistroExecucaoAsync(prescricaoId, prestador, ct);

        return new FolhaAssinada(pdf, nome, assinatura, null);
    }

    // ---- Apoio ----

    private async Task<ResultadoAssinatura> SelarAsync(
        byte[] pdf, CertificadoAssinatura certificado, string motivo,
        string nomeExibido, string? registroConselho, CancellationToken ct)
    {
        var area = PrescricaoInternaPdfService.AreaDaAssinatura(ContarPaginas(pdf));

        return _assinador.Assinar(pdf, certificado, new PedidoAssinatura(
            Motivo: motivo,
            NomeExibido: nomeExibido,
            RegistroConselho: registroConselho,
            Area: area,
            CarimbadoraDeTempo: await _parametros.ObterCarimbadoraDeTempoAsync(ct)));
    }

    private async Task<ArquivoAssinado> GuardarAsync(
        byte[] pdf, string nome, CancellationToken ct)
    {
        var arquivo = new ArquivoAssinado
        {
            Conteudo = pdf,
            NomeArquivo = nome,
            GeradoEm = DateTime.Now
        };

        await _repo.AdicionarArquivoAssinadoAsync(arquivo, ct);

        // Gravado ANTES da assinatura porque a AssinaturaDocumento aponta para ele: sem o
        // Id, a chave estrangeira ficaria nula e o PDF viraria órfão — a quinta variante
        // do "dado gravado sem leitor", desta vez com o leitor existindo e o dado perdido.
        await _repo.SalvarAsync(ct);
        return arquivo;
    }

    private static AssinaturaDocumento Montar(
        CertificadoAssinatura certificado, ResultadoAssinatura assinado,
        ArquivoAssinado arquivo, int? usuarioId, string nome, string? conselho)
        => new()
        {
            Tipo = TipoAssinatura.IcpBrasil,
            HashConteudo = assinado.Hash,
            AlgoritmoHash = "SHA-256",
            AssinadoEm = DateTime.Now,
            UsuarioId = usuarioId,
            NomeAssinante = nome,
            RegistroConselho = conselho,
            CpfAssinante = certificado.Cpf,
            CertificadoTitular = certificado.Titular,
            CertificadoEmissor = certificado.Emissor,
            CertificadoSerie = certificado.NumeroSerie,
            CertificadoValidoDe = certificado.ValidoDe,
            CertificadoValidoAte = certificado.ValidoAte,
            CarimboTempoEm = assinado.CarimboTempoEm,
            CarimboTempoAutoridade = assinado.CarimboTempoAutoridade,
            ArquivoId = arquivo.Id
        };

    private Task<Modelos.DadosPrestador> PrestadorAsync(CancellationToken ct)
        => _parametros.ObterPrestadorAsync(ct);

    private static int ContarPaginas(byte[] pdf)
    {
        using var fluxo = new MemoryStream(pdf, writable: false);
        using var documento = PdfSharp.Pdf.IO.PdfReader.Open(
            fluxo, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        return documento.PageCount;
    }
}
