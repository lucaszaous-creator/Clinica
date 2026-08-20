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

        var pdf = await _pdfs.GerarPrescricaoAsync(
            prescricaoId, await PrestadorAsync(ct), paraAssinaturaEletronica: true, ct);

        var assinado = await SelarAsync(
            pdf, certificado,
            motivo: $"Prescrição de execução interna {prescricao.Numero}",
            nomeExibido: prescricao.Profissional?.Nome ?? certificado.Titular,
            registroConselho: prescricao.Profissional?.RegistroConselho,
            ct,
            duasAssinaturas: prescricao.ExigeAssinaturaEletronicaDaExecucao);

        var arquivo = await GuardarAsync(assinado.Pdf, $"{prescricao.Numero}.pdf", ct);

        var assinatura = Montar(
            certificado, assinado, arquivo, usuarioId,
            prescricao.Profissional?.Nome ?? certificado.Titular,
            prescricao.Profissional?.RegistroConselho);

        return await _prescricoes.AssinarAsync(
            prescricaoId, assinatura, confirmouAlergia, operador, ct);
    }

    /// <summary>
    /// A 2ª ASSINATURA — a da enfermagem, sobre o REGISTRO DE EXECUÇÃO (decisão da direção,
    /// 14/08/2026). Método NOVO ao lado do fluxo do prescritor, que não muda: o motor
    /// congelado (<c>AssinaturaDigitalService</c>, SafeID) é reusado tal e qual.
    ///
    /// A regra que faz esta assinatura valer é a MESMA do prescritor, apontada para outra
    /// pessoa: <b>o CPF do certificado tem de bater com o CPF de quem está ASSINANDO</b> —
    /// a enfermeira logada, resolvida pelo vínculo <c>UsuarioSistema → Profissional</c>.
    /// Sem essa conferência, o e-CPF da médica assinaria a execução da técnica e o
    /// documento diria que quem executou foi quem não executou.
    ///
    /// ⚠️ <b>É a MESMA prescrição, e isso mudou na 2ª rodada (16/08/2026).</b> A primeira
    /// versão fazia a enfermagem selar o Registro de execução — outro arquivo —, porque a
    /// premissa da parcela 42 dizia que duas assinaturas no mesmo PDF não existiam. A
    /// premissa foi medida e derrubada: o que não faz atualização incremental é o PDFsharp,
    /// não o PDF. A clínica descreveu o fluxo real — *"a infusão vai pra enfermagem e ela
    /// também assina a prescrição que já foi assinada pelo médico"* —, e é o que a
    /// legalidade pede: <b>uma folha com as duas assinaturas</b>, não duas folhas com uma
    /// cada.
    ///
    /// Por isso aqui não se gera PDF nenhum: parte-se dos BYTES que a médica assinou e
    /// anexa-se uma revisão. Regerar a folha produziria um arquivo diferente do que ela
    /// selou, e a assinatura dela abriria como inválida.
    /// </summary>
    /// <param name="usuarioId">
    /// Quem está assinando — obrigatório aqui, ao contrário do prescritor: lá o signatário
    /// é o profissional DA FOLHA; aqui é quem fez login, e sem ele não há CPF contra o qual
    /// conferir o certificado.
    /// </param>
    public async Task<PrescricaoInterna> AssinarExecucaoAsync(
        int prescricaoId, CertificadoAssinatura certificado, int usuarioId,
        string? operador = null, CancellationToken ct = default)
    {
        var prescricao = await _repo.ObterPrescricaoInternaAsync(prescricaoId, ct)
            ?? throw new InvalidOperationException("Prescrição não encontrada.");

        var usuario = await _repo.ObterUsuarioAsync(usuarioId, ct)
            ?? throw new InvalidOperationException("Usuário não encontrado.");

        var executante = usuario.Profissional
            ?? throw new InvalidOperationException(
                $"O usuário {usuario.Nome} não está vinculado a um profissional no cadastro "
                + "de Equipe. Sem o vínculo não há CPF contra o qual conferir o certificado "
                + "— e assinar sem conferir provaria só que alguém com algum token assinou.");

        TitularDoCertificado.Exigir(certificado, executante.Cpf, executante.Nome);

        // Os bytes que a MÉDICA assinou — nunca uma folha regerada. É sobre eles que a
        // assinatura dela foi calculada, e é sobre eles que a da enfermagem se apoia.
        //
        // ⚠️ Buscados pelo ArquivoId, NUNCA pela navegação `AssinaturaDoPrescritor.Arquivo`:
        // `ObterPrescricaoInternaAsync` faz `.Include(p => p.Assinaturas)` e NÃO faz
        // `.ThenInclude(a => a.Arquivo)`, e o projeto não usa lazy loading. Em produção,
        // onde cada operação abre escopo próprio, a navegação chega NULA e a assinatura
        // falhava sempre — enquanto os testes passavam, porque compartilham um DbContext e
        // o relationship fixup do EF preenche a navegação pelo change tracker.
        // É a forma mais discreta do "verde não quer dizer funciona" desta área: nada
        // falha no build, nada falha no teste, e a clínica leva a recusa na primeira
        // tentativa — que aqui é COBRADA pelo PSC.
        var idDoArquivo = prescricao.AssinaturaDoPrescritor?.ArquivoId
            ?? throw new InvalidOperationException(
                "A prescrição não tem o arquivo assinado pelo prescritor guardado, então "
                + "não há sobre o que anexar a 2ª assinatura. A folha NÃO foi assinada — "
                + "a via em papel continua valendo.");

        var doPrescritor = (await _repo.ObterArquivoAssinadoAsync(idDoArquivo, ct))?.Conteudo
            ?? throw new InvalidOperationException(
                "O arquivo assinado pelo prescritor não foi encontrado no banco "
                + $"(id {idDoArquivo}). A folha NÃO foi assinada — a via em papel continua "
                + "valendo.");

        // ⚠️ O REGISTRO é gerado ANTES de a assinatura ser registrada, e a ordem é a regra:
        // depois dela, o rodapé passaria a escrever "este arquivo NÃO é assinado — a
        // assinatura está na folha X", que é o texto da via MONTADA NA HORA. Selar esse
        // texto produziria um arquivo assinado dizendo que não é assinado.
        var registro = await _pdfs.GerarRegistroExecucaoAsync(
            prescricaoId, await PrestadorAsync(ct), paraAssinaturaEletronica: true, ct: ct);

        var assinado = await AnexarAsync(
            doPrescritor, certificado,
            motivo: $"Execução da prescrição {prescricao.Numero}",
            nomeExibido: executante.Nome,
            registroConselho: executante.RegistroConselho,
            ct);

        // Arquivo NOVO em vez de sobrescrever o da médica: o dela continua sendo o registro
        // do que ela selou, e o novo o contém inteiro como PREFIXO — nada se perde, e a
        // regra do prontuário que não se apaga vale para o arquivo também.
        var arquivo = await GuardarAsync(
            assinado.Pdf, $"{prescricao.Numero.Replace('/', '-')} assinada.pdf", ct);

        // ---- O SEGUNDO arquivo do MESMO ato ----
        //
        // A folha da prescrição foi selada pela médica ANTES da execução: ela nunca poderá
        // mostrar o ✓, a rodela e o suspenso, e acrescentar-lhe uma página faz o validador
        // de fora acusar modificação ILEGAL na assinatura DELA (medido no pyhanko). Quem
        // mostra a execução é o registro — e para ele valer como prova, é selado aqui, com
        // o MESMO certificado que a enfermeira acabou de escolher.
        //
        // ⚠️ Falhar aqui NÃO desfaz a assinatura da prescrição. É a hierarquia da parcela
        // 65 aplicada a esta área: o ato irreversível não pode depender do passo que veio
        // depois dele. Sem o registro selado, a coluna fica nula e a folha montada na hora
        // DIZ que não é assinada, apontando a que é — o que é a verdade.
        ArquivoAssinado? arquivoDoRegistro = null;
        try
        {
            var registroSelado = await SelarAsync(
                registro, certificado,
                motivo: $"Registro de execução da prescrição {prescricao.Numero}",
                nomeExibido: executante.Nome,
                registroConselho: executante.RegistroConselho,
                ct);

            arquivoDoRegistro = await GuardarAsync(
                registroSelado.Pdf,
                $"{prescricao.Numero.Replace('/', '-')} execucao assinada.pdf", ct);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar(
                "Registro de execução não pôde ser selado (a prescrição FOI assinada)", ex);
        }

        var assinatura = Montar(
            certificado, assinado, arquivo, usuarioId,
            executante.Nome, executante.RegistroConselho);

        assinatura.ArquivoRegistroId = arquivoDoRegistro?.Id;

        // Quem valida o ESTADO da folha (encerrada, campo marcado, ainda sem assinatura) é
        // o serviço de domínio — a mesma divisão de trabalho do fluxo do prescritor.
        return await _prescricoes.AssinarExecucaoAsync(prescricaoId, assinatura, operador, ct);
    }

    /// <summary>
    /// A folha para ler ou reimprimir.
    ///
    /// A <b>Prescrição</b>, quando assinada, devolve os <b>bytes GUARDADOS</b> — nunca um
    /// PDF novo: a assinatura cobre uma faixa de bytes do arquivo, e um documento "igual"
    /// regerado agora teria outra, então a segunda via sairia inválida.
    ///
    /// O <b>Registro de execução</b> tem dois regimes: enquanto NÃO é assinado, é montado
    /// na hora — ele muda a cada item checado, e congelá-lo mostraria um estado que já
    /// passou. Assinado pela enfermagem (folha com a 2ª assinatura, 14/08/2026), vale a
    /// mesma regra da Prescrição: devolvem-se os bytes GUARDADOS, porque a assinatura
    /// cobre uma faixa de bytes e um arquivo regerado sairia inválido.
    /// </summary>
    public async Task<FolhaAssinada> FolhaAsync(
        int prescricaoId, FolhaPrescricao folhaPedida, CancellationToken ct = default)
    {
        var prescricao = await _repo.ObterPrescricaoInternaAsync(prescricaoId, ct)
            ?? throw new InvalidOperationException("Prescrição não encontrada.");

        var ehPrescricao = folhaPedida == FolhaPrescricao.Prescricao;

        // ⚠️ A PRESCRIÇÃO devolve a assinatura MAIS RECENTE dela: quando a enfermagem
        // assinou, o arquivo com as DUAS é o que vale — e ele contém o da médica inteiro
        // como prefixo, então nada se perde. Devolver o da prescritora aqui entregaria uma
        // segunda via sem a assinatura de quem executou, que é justamente a metade que a
        // clínica precisa provar.
        //
        // O Registro de execução continua montado NA HORA: ele muda a cada item checado, e
        // congelá-lo mostraria um estado que já passou.
        var assinatura = ehPrescricao
            ? prescricao.AssinaturaDaExecucao ?? prescricao.AssinaturaDoPrescritor
            : prescricao.AssinaturaDaExecucao;

        var sufixo = ehPrescricao ? string.Empty : " execucao";
        var nome = $"{prescricao.Numero.Replace('/', '-')}{sufixo}.pdf";

        // Cada folha devolve o SEU arquivo selado: a prescrição, a que leva os dois
        // carimbos; o registro, o que foi selado no mesmo ato (20/08/2026). Trocar um pelo
        // outro entregaria a segunda via de um documento no lugar do outro.
        var idDoArquivo = ehPrescricao ? assinatura?.ArquivoId : assinatura?.ArquivoRegistroId;

        if (idDoArquivo is int arquivoId
            && await _repo.ObterArquivoAssinadoAsync(arquivoId, ct) is { } guardado)
        {
            return new FolhaAssinada(
                guardado.Conteudo, guardado.NomeArquivo,
                assinatura, _assinador.Conferir(guardado.Conteudo));
        }

        // Registro ainda NÃO selado (execução em curso, regime do papel, ou a selagem
        // falhou): ele é montado na hora e não carrega assinatura nenhuma — dizer que
        // carrega seria a garantia aparente que o rodapé passou a recusar.
        if (!ehPrescricao) assinatura = null;

        var prestador = await PrestadorAsync(ct);
        var pdf = ehPrescricao
            ? await _pdfs.GerarPrescricaoAsync(prescricaoId, prestador, ct: ct)
            : await _pdfs.GerarRegistroExecucaoAsync(prescricaoId, prestador, ct: ct);

        return new FolhaAssinada(pdf, nome, assinatura, null);
    }

    // ---- Apoio ----

    private async Task<ResultadoAssinatura> SelarAsync(
        byte[] pdf, CertificadoAssinatura certificado, string motivo,
        string nomeExibido, string? registroConselho, CancellationToken ct,
        bool duasAssinaturas = false)
    {
        // A largura do carimbo depende de a folha reservar UM ou DOIS espaços — é a mesma
        // conta do rodapé, e desencontrá-las faz um carimbo cobrir o outro.
        var area = PrescricaoInternaPdfService.AreaDaAssinatura(
            ContarPaginas(pdf), duasAssinaturas);

        return await _assinador.AssinarAsync(pdf, certificado, new PedidoAssinatura(
            Motivo: motivo,
            NomeExibido: nomeExibido,
            RegistroConselho: registroConselho,
            Area: area,
            CarimbadoraDeTempo: await _parametros.ObterCarimbadoraDeTempoAsync(ct)));
    }

    /// <summary>
    /// A 2ª assinatura sobre a MESMA folha: revisão incremental, com o carimbo ao LADO do
    /// da médica em vez de por cima dele.
    /// </summary>
    private async Task<ResultadoAssinatura> AnexarAsync(
        byte[] pdfAssinado, CertificadoAssinatura certificado, string motivo,
        string nomeExibido, string? registroConselho, CancellationToken ct)
    {
        var area = PrescricaoInternaPdfService.AreaDaSegundaAssinatura(
            ContarPaginas(pdfAssinado));

        return await _assinador.AnexarAssinaturaAsync(
            pdfAssinado, certificado,
            new PedidoAssinatura(
                Motivo: motivo,
                NomeExibido: nomeExibido,
                RegistroConselho: registroConselho,
                Area: area,
                CarimbadoraDeTempo: await _parametros.ObterCarimbadoraDeTempoAsync(ct)),
            nomeCampo: "AssinaturaExecucao");
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
