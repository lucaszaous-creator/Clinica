using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O login e a senha do web service de uma operadora, ABERTOS, para montar a mensagem.
/// É classe e não <c>record</c> de propósito: um record imprime as propriedades no
/// <c>ToString()</c>, e bastaria um log com o objeto para a senha vazar.
/// </summary>
public sealed class CredencialTiss
{
    public CredencialTiss(string login, string senhaMd5)
    {
        Login = login;
        SenhaMd5 = senhaMd5;
    }

    public string Login { get; }

    /// <summary>
    /// A senha já em MD5, como vai na mensagem. Vale tanto quanto a senha para o web service:
    /// nunca vai para log, exceção, auditoria nem XML guardado.
    /// </summary>
    public string SenhaMd5 { get; }

    public override string ToString() => "[credencial TISS: conteúdo oculto]";
}

/// <summary>O que a tela pode saber da credencial: o login e SE há senha. Nunca a senha.</summary>
public sealed record CredencialTissNaTela(string? Login, bool TemSenha);

/// <summary>
/// A credencial do web service TISS por convênio e a chave "envio pelo web service" (nov/2026).
///
/// Onde mora: na configuração global do banco (a mesma tabela do <see cref="ParametrosService"/>),
/// cifrada com o <see cref="ProtecaoSegredoGlobal"/> — a chave de cifra fica numa variável de
/// ambiente, FORA do banco.
///
/// O que é guardado: o login em claro (a tela precisa mostrá-lo) e o <b>MD5 da senha</b>, cifrado.
/// A senha em claro não é guardada em lugar nenhum: a mensagem TISS só leva o MD5, e quem tem só
/// o MD5 não entra no portal da operadora pela tela de login.
///
/// O que este serviço NUNCA faz:
/// <list type="bullet">
/// <item>gravar a senha sem cifra (sem chave configurada, o cadastro é recusado);</item>
/// <item>devolver a senha a quem cadastra (<see cref="ParaATelaAsync"/> diz só se ela existe);</item>
/// <item>pôr senha, MD5 ou login em exceção ou em auditoria.</item>
/// </list>
/// </summary>
public sealed class CredenciaisTissService
{
    private readonly IClinicaRepositorio _repo;
    private readonly ProtecaoSegredoGlobal? _protecao;

    public CredenciaisTissService(IClinicaRepositorio repo, ProtecaoSegredoGlobal? protecao = null)
    {
        _repo = repo;
        _protecao = protecao;
    }

    public static string ChaveLogin(string convenioCodigo) => Chave("TissLogin:", convenioCodigo);
    public static string ChaveSenha(string convenioCodigo) => Chave(PrefixoSenha, convenioCodigo);

    /// <summary>O prefixo da senha do web service: a única chave que este serviço CIFRA.</summary>
    public const string PrefixoSenha = "TissSenha:";

    /// <summary>
    /// As chaves que este serviço grava cifradas ("enc:") e que só o CONECTOR abre (com a chave dele).
    /// A publicação do portal (<c>deploy/tablet/regras_publicacao.py</c>, PREFIXOS_CIFRADOS_DO_CONECTOR_TISS)
    /// recusa qualquer credencial cifrada na base, EXCETO estas: o portal e a /gestao nunca as abrem
    /// (<see cref="ParaATelaAsync"/> só pergunta se existe). As duas listas são a mesma — um teste confere — e
    /// cifrar uma chave nova aqui sem pô-la nas duas faz o teste falhar.
    /// </summary>
    public static readonly IReadOnlyList<string> PrefixosCifradosForaDoPortal = [PrefixoSenha];
    public static string ChaveEnvio(string convenioCodigo) => Chave("TissEnvio:", convenioCodigo);

    /// <summary>
    /// Grava (ou troca) a credencial do convênio. A senha chega em claro, vira MD5 aqui e é
    /// cifrada antes de tocar o banco. A trilha registra QUE a credencial mudou e quem mudou —
    /// nunca o conteúdo.
    /// </summary>
    public async Task SalvarAsync(string convenioCodigo, string login, string senha, string operador, CancellationToken ct = default)
    {
        var quem = Operador(operador);
        var usuario = (login ?? string.Empty).Trim();
        if (usuario.Length is 0 or > 20)
            throw new InvalidOperationException("Informe o login do web service (até 20 caracteres).");
        if (string.IsNullOrEmpty(senha))
            throw new InvalidOperationException("Informe a senha do web service.");

        var protetor = Protetor();
        var chaveSenha = ChaveSenha(convenioCodigo);
        await _repo.SalvarConfiguracaoAsync(ChaveLogin(convenioCodigo), usuario, ct);
        await _repo.SalvarConfiguracaoAsync(chaveSenha, protetor.Proteger(chaveSenha, TissElegibilidadeWs.SenhaEmMd5(senha)), ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = quem, Acao = "CredencialTissAlterada",
            Detalhe = $"Credencial do web service TISS alterada ({convenioCodigo.Trim()})"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Apaga a credencial do convênio (o conector passa a recusar as transações dele).</summary>
    public async Task RemoverAsync(string convenioCodigo, string operador, CancellationToken ct = default)
    {
        var quem = Operador(operador);
        await _repo.SalvarConfiguracaoAsync(ChaveLogin(convenioCodigo), string.Empty, ct);
        await _repo.SalvarConfiguracaoAsync(ChaveSenha(convenioCodigo), string.Empty, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = quem, Acao = "CredencialTissRemovida",
            Detalhe = $"Credencial do web service TISS removida ({convenioCodigo.Trim()})"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>O que a tela mostra: o login e se há senha gravada. A senha não volta.</summary>
    public async Task<CredencialTissNaTela> ParaATelaAsync(string convenioCodigo, CancellationToken ct = default)
    {
        var login = await _repo.ObterConfiguracaoAsync(ChaveLogin(convenioCodigo), ct);
        var senha = await _repo.ObterConfiguracaoAsync(ChaveSenha(convenioCodigo), ct);
        return new CredencialTissNaTela(string.IsNullOrWhiteSpace(login) ? null : login, !string.IsNullOrEmpty(senha));
    }

    /// <summary>
    /// A credencial ABERTA, para o conector montar a mensagem. Nulo quando o convênio não tem
    /// credencial. Só funciona onde a chave de cifra está configurada (o servidor do conector).
    /// </summary>
    public async Task<CredencialTiss?> AbrirParaEnvioAsync(string convenioCodigo, CancellationToken ct = default)
    {
        var chaveSenha = ChaveSenha(convenioCodigo);
        var login = await _repo.ObterConfiguracaoAsync(ChaveLogin(convenioCodigo), ct);
        var cifrada = await _repo.ObterConfiguracaoAsync(chaveSenha, ct);
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(cifrada)) return null;
        if (!ProtecaoSegredoGlobal.EstaProtegido(cifrada))
            // Alguém gravou a linha por fora, sem cifra: não se usa nem se repete o valor.
            throw new InvalidOperationException("A senha do web service TISS está gravada sem proteção. Cadastre a credencial de novo.");
        return new CredencialTiss(login.Trim(), Protetor().Revelar(chaveSenha, cifrada));
    }

    /// <summary>O envio de guias pelo web service está ligado para este convênio? Desligado por padrão.</summary>
    public async Task<bool> EnvioLigadoAsync(string convenioCodigo, CancellationToken ct = default)
        => string.Equals(await _repo.ObterConfiguracaoAsync(ChaveEnvio(convenioCodigo), ct), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Liga ou desliga o envio de guias pelo web service para o convênio, com trilha.</summary>
    public async Task DefinirEnvioAsync(string convenioCodigo, bool ligado, string operador, CancellationToken ct = default)
    {
        var quem = Operador(operador);
        await _repo.SalvarConfiguracaoAsync(ChaveEnvio(convenioCodigo), ligado ? "true" : "false", ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = quem, Acao = ligado ? "EnvioTissLigado" : "EnvioTissDesligado",
            Detalhe = $"Envio de guias pelo web service {(ligado ? "ligado" : "desligado")} ({convenioCodigo.Trim()})"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// O endereço do web service deste tipo de transação para o convênio: o que foi cadastrado
    /// ou, quando a família do convênio está no catálogo (<see cref="OperadorasTiss"/>), o de
    /// produção da operadora — ligar o envio não exige digitar URL. Nulo quando não há nenhum dos
    /// dois. Só HTTPS é aceito ao gravar; quem envia confere de novo antes de usar.
    /// </summary>
    public async Task<Uri?> EnderecoAsync(string convenioCodigo, TipoTransacaoTiss tipo, CancellationToken ct = default)
    {
        // Em homologação (comando "ambiente"), TODO serviço vai ao ponto único de homologação da
        // operadora — por cima até do endereço cadastrado, para nenhum teste cair em produção.
        if (await EmHomologacaoAsync(convenioCodigo, ct) && (await OperadoraAsync(convenioCodigo, ct))?.EnderecoHomologacao is { } homologacao)
            return homologacao;
        return await EnderecoCadastradoAsync(convenioCodigo, tipo, ct)
               ?? OperadorasTiss.EnderecoPadrao(await FamiliaAsync(convenioCodigo, ct), tipo);
    }

    // ==================== o pedido de autorização: chave própria por convênio ====================

    public static string ChaveAutorizacao(string convenioCodigo) => Chave("TissAutorizacao:", convenioCodigo);

    /// <summary>
    /// O sistema pede a autorização à operadora sozinho (no "Chegou") e pelo botão? Desligado por padrão,
    /// e SEPARADO do envio de guias: pedir senha não é cobrança, e as duas coisas se ligam em dias diferentes.
    /// </summary>
    public async Task<bool> AutorizacaoLigadaAsync(string convenioCodigo, CancellationToken ct = default)
        => string.Equals(await _repo.ObterConfiguracaoAsync(ChaveAutorizacao(convenioCodigo), ct), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Liga ou desliga o pedido de autorização pelo web service para o convênio, com trilha.</summary>
    public async Task DefinirAutorizacaoAsync(string convenioCodigo, bool ligada, string operador, CancellationToken ct = default)
    {
        var quem = Operador(operador);
        if (ligada && !OperadorasTiss.TemAutorizacao(await FamiliaAsync(convenioCodigo, ct)))
            throw new InvalidOperationException($"O sistema não conhece o pedido de autorização de {convenioCodigo.Trim()}.");
        await _repo.SalvarConfiguracaoAsync(ChaveAutorizacao(convenioCodigo), ligada ? "true" : "false", ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = quem, Acao = ligada ? "AutorizacaoTissLigada" : "AutorizacaoTissDesligada",
            Detalhe = $"Pedido de autorização pelo web service {(ligada ? "ligado" : "desligado")} ({convenioCodigo.Trim()})"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    // ==================== o ambiente: produção ou homologação ====================

    public static string ChaveAmbiente(string convenioCodigo) => Chave("TissAmbiente:", convenioCodigo);

    /// <summary>O valor gravado que quer dizer "homologação". Qualquer outro (ou nada) é produção.</summary>
    public const string Homologacao = "homologacao";

    /// <summary>O convênio está apontado para a homologação da operadora?</summary>
    public async Task<bool> EmHomologacaoAsync(string convenioCodigo, CancellationToken ct = default)
        => string.Equals((await _repo.ObterConfiguracaoAsync(ChaveAmbiente(convenioCodigo), ct))?.Trim(), Homologacao, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Aponta o convênio para a homologação ou de volta para a produção, com trilha. Homologação só
    /// existe para operadora que tem o endereço dela no catálogo (<see cref="OperadoraTiss.EnderecoHomologacao"/>).
    /// </summary>
    public async Task DefinirAmbienteAsync(string convenioCodigo, bool homologacao, string operador, CancellationToken ct = default)
    {
        var quem = Operador(operador);
        if (homologacao && (await OperadoraAsync(convenioCodigo, ct))?.EnderecoHomologacao is null)
            throw new InvalidOperationException($"O sistema não conhece o endereço de homologação de {convenioCodigo.Trim()}.");
        await _repo.SalvarConfiguracaoAsync(ChaveAmbiente(convenioCodigo), homologacao ? Homologacao : "producao", ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = quem, Acao = homologacao ? "AmbienteTissHomologacao" : "AmbienteTissProducao",
            Detalhe = $"Web service de {convenioCodigo.Trim()} apontado para a {(homologacao ? "HOMOLOGAÇÃO" : "produção")}"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>O endereço cadastrado para o convênio (nulo = vale o do catálogo).</summary>
    public async Task<Uri?> EnderecoCadastradoAsync(string convenioCodigo, TipoTransacaoTiss tipo, CancellationToken ct = default)
        => Uri.TryCreate(await _repo.ObterConfiguracaoAsync(ChaveEndereco(convenioCodigo, tipo), ct), UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps ? uri : null;

    /// <summary>O endereço de produção que o sistema conhece sozinho (<see cref="OperadorasTiss"/>).</summary>
    public static Uri? EnderecoPadrao(Convenio familia, TipoTransacaoTiss tipo) => OperadorasTiss.EnderecoPadrao(familia, tipo);

    /// <summary>
    /// A operadora do convênio no catálogo (nulo = o sistema não conhece web service dela). A família
    /// vem do CADASTRO do convênio lido do banco, e não do catálogo em memória: o conector é outro
    /// processo, e o catálogo dele pode nunca ter sido carregado.
    /// </summary>
    public async Task<OperadoraTiss?> OperadoraAsync(string convenioCodigo, CancellationToken ct = default)
        => OperadorasTiss.Da(await FamiliaAsync(convenioCodigo, ct));

    private async Task<Convenio> FamiliaAsync(string convenioCodigo, CancellationToken ct)
    {
        var codigo = (convenioCodigo ?? string.Empty).Trim();
        var cadastro = (await _repo.ConveniosAsync(ct))
            .FirstOrDefault(c => string.Equals(c.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        return cadastro?.Familia
               ?? (Enum.TryParse<Convenio>(codigo, ignoreCase: true, out var embutida) ? embutida : Convenio.Personalizado);
    }

    // ==================== o código do prestador na operadora ====================

    /// <summary>Valor gravado que quer dizer "o CNPJ da clínica".</summary>
    public const string CodigoPeloCnpj = "cnpj";

    /// <summary>Valor gravado que quer dizer "o login do web service".</summary>
    public const string CodigoPeloLogin = "login";

    public static string ChaveCodigoPrestador(string convenioCodigo) => Chave("TissCodigo:", convenioCodigo);

    /// <summary>
    /// A regra do código do prestador para o convênio: a cadastrada (<see cref="DefinirCodigoDoPrestadorAsync"/>)
    /// ou a da operadora no catálogo. Sem nenhuma das duas, o CNPJ da clínica — como foi sempre.
    /// O código literal, quando a regra é <see cref="CodigoDoPrestadorTiss.Cadastrado"/>, vem junto.
    /// </summary>
    public async Task<(CodigoDoPrestadorTiss Regra, string? Codigo)> CodigoDoPrestadorAsync(string convenioCodigo, CancellationToken ct = default)
    {
        var gravado = (await _repo.ObterConfiguracaoAsync(ChaveCodigoPrestador(convenioCodigo), ct))?.Trim();
        if (string.Equals(gravado, CodigoPeloCnpj, StringComparison.OrdinalIgnoreCase)) return (CodigoDoPrestadorTiss.CnpjDaClinica, null);
        if (string.Equals(gravado, CodigoPeloLogin, StringComparison.OrdinalIgnoreCase)) return (CodigoDoPrestadorTiss.LoginDoWebService, null);
        if (!string.IsNullOrEmpty(gravado)) return (CodigoDoPrestadorTiss.Cadastrado, gravado);
        return ((await OperadoraAsync(convenioCodigo, ct))?.CodigoDoPrestador ?? CodigoDoPrestadorTiss.CnpjDaClinica, null);
    }

    /// <summary>
    /// Grava a regra do código do prestador para o convênio: <c>cnpj</c>, <c>login</c> ou o código
    /// em si (1 a 14 caracteres, o tamanho de <c>codigoPrestadorNaOperadora</c> no padrão).
    /// Vazio volta à regra da operadora no catálogo. O código vai para a trilha só como "alterado".
    /// </summary>
    public async Task DefinirCodigoDoPrestadorAsync(string convenioCodigo, string? valor, string operador, CancellationToken ct = default)
    {
        var quem = Operador(operador);
        var texto = (valor ?? string.Empty).Trim();
        if (texto.Length > 14)
            throw new InvalidOperationException("O código do prestador na operadora tem até 14 caracteres (padrão TISS).");
        await _repo.SalvarConfiguracaoAsync(ChaveCodigoPrestador(convenioCodigo), texto, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = quem, Acao = "CodigoPrestadorTissAlterado",
            Detalhe = $"Código do prestador no web service alterado ({convenioCodigo.Trim()}): "
                      + (texto.Length == 0 ? "regra da operadora" : texto.ToLowerInvariant() is CodigoPeloCnpj or CodigoPeloLogin ? texto.ToLowerInvariant() : "código cadastrado")
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Grava o endereço do web service (HTTPS) deste tipo de transação para o convênio, com trilha.</summary>
    /// <summary>O valor do comando <c>endereco</c> que apaga o cadastrado e volta ao catálogo.</summary>
    public const string EnderecoPadraoDoCatalogo = "padrao";

    public async Task DefinirEnderecoAsync(
        string convenioCodigo, TipoTransacaoTiss tipo, string endereco, string operador, CancellationToken ct = default)
    {
        var quem = Operador(operador);
        // "padrao": apaga o endereço cadastrado e volta ao do catálogo (OperadorasTiss) — é como um
        // endereço antigo (ex.: o 4.01.00 da Petrobras) deixa de vencer o catálogo novo.
        if (string.Equals((endereco ?? string.Empty).Trim(), EnderecoPadraoDoCatalogo, StringComparison.OrdinalIgnoreCase))
        {
            await _repo.SalvarConfiguracaoAsync(ChaveEndereco(convenioCodigo, tipo), string.Empty, ct);
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = quem, Acao = "EnderecoTissAlterado",
                Detalhe = $"Endereço do web service de {tipo} de volta ao padrão do catálogo ({convenioCodigo.Trim()})"
            }, ct);
            await _repo.SalvarAsync(ct);
            return;
        }
        if (!Uri.TryCreate((endereco ?? string.Empty).Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || uri.UserInfo.Length > 0)
            throw new InvalidOperationException("O endereço do web service TISS é https://… e não leva usuário nem senha.");
        await _repo.SalvarConfiguracaoAsync(ChaveEndereco(convenioCodigo, tipo), uri.ToString(), ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = quem, Acao = "EnderecoTissAlterado",
            Detalhe = $"Endereço do web service de {tipo} alterado ({convenioCodigo.Trim()}): {uri.Host}"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Uma letra por tipo: a chave da configuração tem 60 caracteres e o convênio, até 40.</summary>
    public static string ChaveEndereco(string convenioCodigo, TipoTransacaoTiss tipo) => Chave(tipo switch
    {
        TipoTransacaoTiss.Elegibilidade => "TissUrl:E:",
        TipoTransacaoTiss.EnvioGuia => "TissUrl:G:",
        TipoTransacaoTiss.StatusProtocolo => "TissUrl:S:",
        TipoTransacaoTiss.CancelamentoGuia or TipoTransacaoTiss.CancelaAutorizacao => "TissUrl:C:",
        TipoTransacaoTiss.Autorizacao => "TissUrl:A:",
        TipoTransacaoTiss.StatusAutorizacao => "TissUrl:T:",
        _ => throw new InvalidOperationException("Tipo de transação TISS sem endereço previsto.")
    }, convenioCodigo);

    private ProtecaoSegredoGlobal Protetor()
    {
        if (_protecao is not null) return _protecao;
        try
        {
            return ProtecaoSegredoGlobal.DoAmbiente();
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"A credencial do web service TISS só é gravada e aberta onde a chave de proteção está configurada ({ProtecaoSegredoGlobal.VariavelChave}). "
                + "Cadastre-a no servidor do conector (docs/tiss-conector.md).");
        }
    }

    private static string Chave(string prefixo, string convenioCodigo)
    {
        var codigo = (convenioCodigo ?? string.Empty).Trim();
        // A chave da configuração tem 60 caracteres; o código do convênio, até 40.
        if (codigo.Length is 0 or > 40)
            throw new InvalidOperationException("Informe o convênio da credencial.");
        return prefixo + codigo;
    }

    private static string Operador(string? operador)
        => string.IsNullOrWhiteSpace(operador)
            ? throw new InvalidOperationException("A credencial da operadora leva quem a alterou: faça login antes.")
            : operador.Trim();
}
