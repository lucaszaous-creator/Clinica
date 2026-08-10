using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>O que a publicação produziu, já com a frase que a tela mostra.</summary>
/// <param name="Url">Endereço do arquivo. Null quando não foi publicado.</param>
/// <param name="Erro">Por que falhou. Null quando deu certo.</param>
public sealed record ResultadoPublicacao(string? Url, DateOnly? Ate, string? Erro)
{
    public bool Publicou => Url is not null && Erro is null;

    public static ResultadoPublicacao Falhou(string erro) => new(null, null, erro);

    /// <summary>Não se aplica — tipo que não se publica, ou publicação desligada.</summary>
    public static ResultadoPublicacao NaoSeAplica() => new(null, null, null);
}

/// <summary>
/// A PUBLICAÇÃO DO DOCUMENTO ASSINADO (parcela 53).
///
/// O problema que resolve
/// ----------------------
/// A receita assinada é conferida no ITI pelo ENVIO DO ARQUIVO. Na prática isso obriga o
/// paciente a mandar o PDF para a farmácia, e é onde o balcão trava. Publicado, o
/// farmacêutico escaneia o QR da tela do celular do paciente, baixa e confere — sem
/// ninguém enviar nada.
///
/// A ordem das operações NÃO é livre
/// ---------------------------------
/// O QR precisa estar dentro do PDF, e a assinatura sela os bytes. Então:
///
/// <code>
/// 1. GarantirTokenAsync   ← o token nasce AQUI, antes de tudo
/// 2. gera o PDF           ← o QR já aponta para a URL final
/// 3. assina               ← sela os bytes, QR incluído
/// 4. PublicarAsync        ← sobe no caminho que o token determina
/// </code>
///
/// Inverter 1 e 2 é o erro que só aparece na farmácia: o QR apontaria para um endereço que
/// ninguém publicou, ou obrigaria a mexer no arquivo assinado, invalidando a assinatura.
///
/// Três decisões
/// -------------
/// 1. <b>O token é estável.</b> Renovar reusa o mesmo, para o QR do PDF que o paciente já
///    tem continuar valendo. Token novo a cada renovação mataria os arquivos já assinados.
/// 2. <b>Falhar não invalida nada.</b> O documento continua assinado e válido; só o link
///    não sobe. Mas a falha PRECISA aparecer na tela — sucesso silencioso faria a secretária
///    entregar dizendo "é só escanear", e o farmacêutico encontrar um endereço morto.
/// 3. <b>Cancelar o documento tira o link do ar.</b> Receita cancelada que continua baixável
///    é a pior espécie de documento publicado.
/// </summary>
public sealed class PublicacaoDocumentoService
{
    private readonly IClinicaRepositorio _repo;
    private readonly IArmazenamentoPublico _armazenamento;
    private readonly ParametrosService _parametros;
    private readonly Func<DateOnly> _hoje;

    public PublicacaoDocumentoService(
        IClinicaRepositorio repo,
        IArmazenamentoPublico armazenamento,
        ParametrosService parametros,
        Func<DateOnly>? hoje = null)
    {
        _repo = repo;
        _armazenamento = armazenamento;
        _parametros = parametros;
        _hoje = hoje ?? (() => DateOnly.FromDateTime(DateTime.Today));
    }

    /// <summary>
    /// A publicação está configurada (há domínio base cadastrado). Sem isso o sistema
    /// funciona exatamente como antes — o QR volta a apontar para o validador do ITI.
    /// </summary>
    public async Task<bool> LigadaAsync(CancellationToken ct = default)
        => await _parametros.ObterUrlPublicacaoAsync(ct) is not null;

    /// <summary>
    /// Garante que o documento tenha token e devolve a URL que vai no QR. Null quando o
    /// tipo não se publica ou a publicação não está configurada.
    ///
    /// <b>Chamar ANTES de gerar o PDF.</b> Ver o resumo da classe.
    /// </summary>
    public async Task<string?> GarantirTokenAsync(
        DocumentoClinico documento, CancellationToken ct = default)
    {
        if (!PublicacaoDocumento.PodePublicar(documento.Tipo)) return null;

        if (await _parametros.ObterUrlPublicacaoAsync(ct) is not { } baseUrl) return null;

        if (string.IsNullOrWhiteSpace(documento.TokenPublicacao))
        {
            documento.TokenPublicacao = PublicacaoDocumento.GerarToken();
            await _repo.SalvarAsync(ct);
        }

        return PublicacaoDocumento.Url(baseUrl, documento.TokenPublicacao!);
    }

    /// <summary>
    /// Sobe o arquivo assinado e abre a janela de publicação. Chamado DEPOIS da assinatura.
    /// </summary>
    public async Task<ResultadoPublicacao> PublicarAsync(
        DocumentoClinico documento, byte[] pdfAssinado, CancellationToken ct = default)
    {
        if (!PublicacaoDocumento.PodePublicar(documento.Tipo))
            return ResultadoPublicacao.NaoSeAplica();

        if (await _parametros.ObterUrlPublicacaoAsync(ct) is not { } baseUrl)
            return ResultadoPublicacao.NaoSeAplica();

        if (string.IsNullOrWhiteSpace(documento.TokenPublicacao))
            // Só acontece se alguém chamar fora de ordem. Dizer isso é melhor do que
            // cunhar um token agora: o PDF já assinado teria o QR de OUTRO endereço.
            return ResultadoPublicacao.Falhou(
                "O documento foi assinado sem token de publicação — o QR dele aponta para "
                + "o validador, não para o arquivo. Emita outro para publicar.");

        var token = documento.TokenPublicacao!;
        var ate = _hoje().AddDays(await _parametros.ObterDiasPublicacaoAsync(ct));

        try
        {
            await _armazenamento.PublicarAsync(
                PublicacaoDocumento.CaminhoDoObjeto(token), pdfAssinado, "application/pdf", ct);
        }
        catch (Exception ex)
        {
            // Degradação COM rastro: o documento continua assinado e válido.
            Diagnostico.Registrar(
                $"Publicação — {documento.Numero} não pôde ser enviada ao armazenamento", ex);
            return ResultadoPublicacao.Falhou(
                $"O documento foi assinado, mas o link não subiu: {ex.Message}. "
                + "Ele continua válido — o paciente precisa levar o arquivo.");
        }

        documento.PublicadoEm = DateTime.Now;
        documento.PublicadoAte = ate;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = documento.AssinanteNome ?? "?",
            Acao = "DocumentoPublicado",
            PacienteId = documento.PacienteId,
            Detalhe = $"{documento.Numero} · link no ar até {ate:dd/MM/yyyy}"
        }, ct);

        await _repo.SalvarAsync(ct);

        return new ResultadoPublicacao(PublicacaoDocumento.Url(baseUrl, token), ate, null);
    }

    /// <summary>
    /// Caminho do objeto que o teste de conexão grava. Fica FORA do prefixo dos documentos
    /// (<see cref="PublicacaoDocumento.CaminhoDoObjeto"/>) para que um teste jamais possa
    /// colidir com uma receita publicada.
    /// </summary>
    public const string CaminhoDeTeste = "teste-de-conexao.txt";

    /// <summary>
    /// Prova as credenciais AGORA, em Configurações, fazendo o que a publicação real faz:
    /// grava um objeto e apaga.
    /// </summary>
    /// <remarks>
    /// <b>Por que gravar de verdade e não só pedir a lista do balde.</b> Um teste que
    /// apenas lê passaria com credencial de leitura, com balde inexistente e — o caso que
    /// mais importa — com um provedor que não aceita a ACL de leitura pública, que é
    /// exatamente o que o <c>PutObject</c> da publicação usa. O teste tem de exercitar o
    /// mesmo caminho, senão ele atesta uma coisa e a receita falha por outra.
    ///
    /// E apaga em seguida: teste que deixa lixo no balde de produção ensina a clínica a
    /// não testar.
    ///
    /// Devolve <c>null</c> quando deu certo, ou a explicação. Descobrir credencial errada
    /// aqui é barato; descobrir com o paciente esperando a receita é o pior momento
    /// possível.
    /// </remarks>
    public async Task<string?> TestarConexaoAsync(CancellationToken ct = default)
    {
        var carimbo = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        var conteudo = System.Text.Encoding.UTF8.GetBytes(
            $"Teste de conexão do sistema da clínica em {carimbo}. "
            + "Este arquivo é apagado logo em seguida.");

        try
        {
            await _armazenamento.PublicarAsync(CaminhoDeTeste, conteudo, "text/plain", ct);
        }
        catch (Exception ex)
        {
            Diagnostico.Registrar("Publicação — teste de conexão falhou ao gravar", ex);
            return $"Não foi possível gravar no armazenamento: {ex.Message}";
        }

        try
        {
            await _armazenamento.RemoverAsync(CaminhoDeTeste, ct);
        }
        catch (Exception ex)
        {
            // Gravou e não apagou: a publicação FUNCIONA, e o que falha é a expiração. Dizer
            // "deu certo" esconderia que os links não sairiam do ar no vencimento.
            Diagnostico.Registrar("Publicação — teste de conexão falhou ao apagar", ex);
            return "A gravação funcionou, mas o sistema não conseguiu APAGAR o arquivo de "
                + $"teste: {ex.Message}. As credenciais precisam de permissão de exclusão, "
                + "senão os links não saem do ar quando o prazo vence.";
        }

        return null;
    }

    /// <summary>
    /// Republica um documento cujo link venceu, reusando o MESMO token — é o que faz o QR
    /// do PDF que o paciente já tem voltar a funcionar.
    /// </summary>
    public async Task<ResultadoPublicacao> RenovarAsync(
        int documentoId, CancellationToken ct = default)
    {
        var documento = await _repo.ObterDocumentoAsync(documentoId, ct)
            ?? throw new InvalidOperationException("Documento não encontrado.");

        if (documento.Cancelado)
            return ResultadoPublicacao.Falhou(
                $"O documento {documento.Numero} está cancelado e não volta ao ar.");

        if (documento.ArquivoAssinadoId is not int arquivoId
            || await _repo.ObterArquivoAssinadoAsync(arquivoId, ct) is not { } arquivo)
            return ResultadoPublicacao.Falhou(
                "Só documento ASSINADO se publica — não há arquivo guardado para este.");

        return await PublicarAsync(documento, arquivo.Conteudo, ct);
    }

    /// <summary>
    /// Tira o link do ar. Chamado no cancelamento do documento e na expiração.
    ///
    /// Não apaga registro nenhum: os bytes assinados continuam no banco pelos 20 anos da
    /// Lei 13.787/2018. O que sai do ar é a publicação.
    /// </summary>
    public async Task DespublicarAsync(
        DocumentoClinico documento, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(documento.TokenPublicacao)) return;
        if (documento.PublicadoAte is null) return;

        try
        {
            await _armazenamento.RemoverAsync(
                PublicacaoDocumento.CaminhoDoObjeto(documento.TokenPublicacao!), ct);
        }
        catch (Exception ex)
        {
            // Falhar aqui é pior do que falhar ao publicar: o arquivo continua no ar. Vai
            // para o log com o caminho, para alguém conseguir removê-lo à mão.
            Diagnostico.Registrar(
                $"Publicação — {documento.Numero} não pôde ser retirada do ar "
                + $"(caminho {PublicacaoDocumento.CaminhoDoObjeto(documento.TokenPublicacao!)})", ex);
            return;
        }

        documento.PublicadoAte = null;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = "sistema",
            Acao = "DocumentoDespublicado",
            PacienteId = documento.PacienteId,
            Detalhe = $"{documento.Numero} · link retirado do ar"
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Retira do ar os links vencidos. Roda na abertura do Gerente, junto do backup — pelo
    /// mesmo motivo: o sistema é desktop e não tem serviço residente.
    ///
    /// Devolve quantos saíram. Se o provedor já expira por regra de ciclo de vida, isto
    /// vira redundância barata — e redundância no lado que TIRA dado do ar é bem-vinda.
    /// </summary>
    public async Task<int> ExpirarVencidosAsync(CancellationToken ct = default)
    {
        var hoje = _hoje();
        var vencidos = await _repo.DocumentosPublicadosVencidosAsync(hoje, ct);

        var saíram = 0;
        foreach (var documento in vencidos)
        {
            ct.ThrowIfCancellationRequested();
            await DespublicarAsync(documento, ct);
            saíram++;
        }

        return saíram;
    }
}
