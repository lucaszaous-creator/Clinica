namespace Clinica.Application.Assinatura.SafeID;

/// <summary>
/// Uma autorização concluída: o token e os certificados que ele alcança.
///
/// <b>Vida curta, e isso é do desenho.</b> Com <see cref="EscopoSafeID.AssinaturaUnica"/> —
/// o escolhido pela clínica — o token do PSC morre no primeiro uso, então esta sessão vale
/// para UM documento. Guardá-la em campo para reaproveitar produziria o erro mais confuso
/// possível: a segunda assinatura falharia com "token inválido" depois de a médica já ter
/// confirmado no celular.
/// </summary>
public sealed record SessaoSafeID(
    TokenSafeID Token,
    IReadOnlyList<CertificadoDeNuvem> Certificados);

/// <summary>
/// O passo a passo da autorização no SafeID: abre a escuta, manda a médica ao QR Code,
/// espera o retorno, troca o código pelo token e lê os certificados (parcela 44).
///
/// Por que é um serviço e não código de janela
/// -------------------------------------------
/// Porque a sequência tem três coisas que não podem morar num clique: o <c>state</c> que
/// amarra o retorno ao pedido, o <c>code_verifier</c> do PKCE que precisa sobreviver da
/// autorização até a troca, e a URI escolhida em tempo de execução, que tem de ser a MESMA
/// nas duas chamadas. Errar qualquer um dos três produz uma recusa do PSC cuja mensagem não
/// diz qual foi o erro — e um code-behind de janela sobrevive até a segunda tela que
/// precisar autorizar.
/// </summary>
public sealed class AutorizacaoSafeIDService
{
    private readonly Func<CancellationToken, Task<OpcoesSafeID?>> _opcoes;
    private readonly Func<OpcoesSafeID, ClienteSafeID> _fabrica;
    private readonly Action<Uri> _abrirNavegador;

    /// <summary>
    /// Quanto se espera a médica pegar o celular, ler o QR e confirmar.
    ///
    /// Três minutos é generoso de propósito: o custo de esperar demais é uma janela aberta,
    /// e o de esperar de menos é a autorização morrer no instante em que ela desbloqueia o
    /// telefone — e aí a assinatura recomeça do zero, com o paciente na sala.
    /// </summary>
    public static readonly TimeSpan EsperaPadrao = TimeSpan.FromMinutes(3);

    /// <param name="opcoes">
    /// De onde vêm as credenciais. É um DELEGADO, e não o <see cref="ProvedorOpcoesSafeID"/>,
    /// porque este serviço precisa das opções e não de quem sabe buscá-las — o que de quebra
    /// permite testar o passo a passo inteiro sem levantar banco.
    /// </param>
    public AutorizacaoSafeIDService(
        Func<CancellationToken, Task<OpcoesSafeID?>> opcoes,
        Func<OpcoesSafeID, ClienteSafeID> fabrica,
        Action<Uri> abrirNavegador)
    {
        _opcoes = opcoes;
        _fabrica = fabrica;
        _abrirNavegador = abrirNavegador;
    }

    /// <summary>Se a clínica cadastrou o SafeID. Quem chama usa isto para OFERECER a opção.</summary>
    public async Task<bool> ConfiguradoAsync(CancellationToken ct = default)
        => await _opcoes(ct) is not null;

    /// <summary>
    /// Autoriza e devolve os certificados em nuvem da titular.
    /// </summary>
    /// <param name="cpf">
    /// Vai como <c>login_hint</c>. Não é segurança — é para a página do PSC já abrir no CPF
    /// certo em vez de pedir que a médica o digite de novo em cada assinatura.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// SafeID não configurado, autorização recusada, ou nenhum certificado no PSC.
    /// </exception>
    public async Task<SessaoSafeID> AutorizarAsync(
        string? cpf = null,
        string escopo = EscopoSafeID.AssinaturaUnica,
        TimeSpan? espera = null,
        CancellationToken ct = default)
    {
        var opcoes = await _opcoes(ct)
            ?? throw new InvalidOperationException(
                "O SafeID ainda não foi configurado. Cadastre o client_id e o client_secret "
                + "em Configurações → Operação antes de assinar em nuvem.");

        var cliente = _fabrica(opcoes);

        using var escuta = EscutaLoopback.Abrir(opcoes.Retornos);

        var desafio = DesafioPkce.Gerar();
        var estado = Guid.NewGuid().ToString("N");

        _abrirNavegador(cliente.UrlDeAutorizacao(
            desafio, escuta.Endereco, escopo, cpf, estado));

        // O tempo-limite é combinado com o cancelamento de quem chama (o botão Cancelar da
        // janela): o que vier primeiro encerra a espera.
        using var relogio = new CancellationTokenSource(espera ?? EsperaPadrao);
        using var combinado = CancellationTokenSource.CreateLinkedTokenSource(ct, relogio.Token);

        RetornoAutorizacao retorno;
        try
        {
            retorno = await escuta.EsperarAsync(estado, combinado.Token);
        }
        catch (OperationCanceledException) when (relogio.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "A autorização no SafeID expirou sem confirmação. Tente de novo — a página "
                + "do QR Code vale por poucos minutos.");
        }

        if (!retorno.Autorizou)
            throw new InvalidOperationException(
                "A autorização no SafeID não foi concluída"
                + (retorno.Descricao is { Length: > 0 } motivo ? $": {motivo}." : "."));

        var token = await cliente.TokenPorCodigoAsync(
            retorno.Codigo!, desafio, escuta.Endereco, ct);

        // `somenteDaAutorizacao` porque o interesse é no certificado que ESTA autorização
        // alcança: listar todos os do titular ofereceria à médica um que o token não pode
        // usar, e a recusa só apareceria no fim, depois de ela escolher.
        var certificados = await cliente.CertificadosAsync(
            token.AccessToken, somenteDaAutorizacao: true, ct);

        return new SessaoSafeID(token, certificados);
    }
}
