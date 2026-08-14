using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Clinica.Application.Assinatura.SafeID;

/// <summary>
/// Cliente HTTP do PSC Safeweb (SafeID).
///
/// O que este cliente NÃO faz, e por quê
/// -------------------------------------
/// Ele não manda o documento para lugar nenhum. A API oferece três modos de assinar, e dois
/// deles (<c>signature-icp</c> e <c>pades-signature/*</c>) sobem o <b>PDF inteiro</b> em
/// base64 — nome do paciente, CPF, CID, medicação. Numa clínica que imprime o CID só com
/// autorização expressa do paciente e que mantém <c>TitularDadosService</c> para atender
/// pedido de eliminação, subir a receita para um terceiro contradiria, numa chamada HTTP, a
/// regra que o sistema defende em toda tela.
///
/// Usamos <c>signature</c> com <c>signature_format: CMS</c>: sobem <b>32 bytes de hash</b>,
/// dos quais não se extrai nada, e desce um PKCS#7 destacado — que é exatamente o que
/// <see cref="AssinaturaDigitalService.Conferir"/> já sabe decodificar.
///
/// O preço dessa escolha, dito por inteiro: este método assina "sem políticas […] e o carimbo
/// de tempo" (texto da doc), então não sai com política AD-RT. Não é regressão — a assinatura
/// de hoje já é PAdES-B —, e o carimbo continua vindo da ACT RFC 3161 configurada pela clínica.
/// </summary>
public sealed partial class ClienteSafeID
{
    private readonly HttpClient _http;
    private readonly OpcoesSafeID _opcoes;

    /// <summary>OID do SHA-256, como a API exige em <c>hash_algorithm</c>.</summary>
    public const string OidSha256 = "2.16.840.1.101.3.4.2.1";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ClienteSafeID(HttpClient http, OpcoesSafeID opcoes)
    {
        _http = http;
        _opcoes = opcoes;
    }

    // ---- Autorização ----

    /// <summary>
    /// Token da APLICAÇÃO (<c>client_credentials</c>). Ele não assina nada: serve para
    /// chamar <c>authorize-ca</c> e <c>user-discovery</c>. Confundir os dois é o erro que
    /// produz "401 no meio da assinatura" depois de tudo parecer certo.
    /// </summary>
    public async Task<TokenSafeID> TokenDaAplicacaoAsync(CancellationToken ct = default)
        => await PostFormAsync("client_token", new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _opcoes.ClientId,
            ["client_secret"] = _opcoes.ClientSecret
        }, ct);

    /// <summary>
    /// A URL da página com QR Code que a médica abre no navegador e lê pelo app SafeID.
    ///
    /// O <paramref name="desafio"/> vem de <see cref="DesafioPkce.Gerar"/> e o
    /// <c>code_verifier</c> correspondente tem de sobreviver até
    /// <see cref="TokenPorCodigoAsync"/> — é ele que prova que quem troca o código é quem o
    /// pediu (RFC 7636).
    /// </summary>
    /// <param name="redirectUri">
    /// A URI onde esta execução está de fato escutando. Vem de fora porque a porta é
    /// escolhida em tempo de execução (ver <c>OpcoesSafeID.RedirectUris</c>), e porque o
    /// mesmo valor tem de ir na troca do código — o OAuth compara os dois como texto exato.
    /// </param>
    public Uri UrlDeAutorizacao(
        DesafioPkce desafio, Uri redirectUri, string escopo = EscopoSafeID.Sessao,
        string? cpf = null, string? estado = null, int? duracaoSegundos = null)
    {
        ArgumentNullException.ThrowIfNull(redirectUri);

        var parametros = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _opcoes.ClientId,
            ["redirect_uri"] = redirectUri.ToString(),
            ["state"] = estado,
            ["scope"] = escopo,
            ["code_challenge"] = desafio.Desafio,
            ["code_challenge_method"] = "S256",
            ["login_hint"] = cpf,
            ["lifetime"] = duracaoSegundos?.ToString()
        };

        var query = string.Join('&', parametros
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));

        return new Uri(_opcoes.Base, "authorize?" + query);
    }

    /// <summary>Troca o <c>code</c> devolvido na URI de retorno pelo token que assina.</summary>
    /// <param name="redirectUri">
    /// <b>Exatamente</b> a mesma passada a <see cref="UrlDeAutorizacao"/>. O PSC recusa a
    /// troca se divergir num único caractere, e a mensagem de erro não diz que o problema é
    /// esse — daí ela vir por parâmetro em vez de ser relida da configuração.
    /// </param>
    public async Task<TokenSafeID> TokenPorCodigoAsync(
        string codigo, DesafioPkce desafio, Uri redirectUri, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(redirectUri);

        var campos = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _opcoes.ClientId,
            ["client_secret"] = _opcoes.ClientSecret,
            ["code"] = codigo,
            ["code_verifier"] = desafio.Verificador,
            ["redirect_uri"] = redirectUri.ToString()
        };

        return await PostFormAsync("token", campos, ct);
    }

    /// <summary>
    /// Token pelo caminho que NÃO precisa de URI de retorno: CPF mais a senha composta
    /// (<c>identifierCA</c> obtido uma vez na implantação + PIN do SafeID).
    ///
    /// É a saída para o desktop quando a Safeweb não cadastra loopback: o callback é
    /// necessário uma única vez, para obter o <c>identifierCA</c>, e o dia a dia passa por aqui.
    /// </summary>
    public async Task<TokenSafeID> TokenPorCredenciaisAsync(
        string cpf, string identificadorCaMaisSenha,
        string escopo = EscopoSafeID.Sessao, int? duracaoSegundos = null,
        string? slotAlias = null, CancellationToken ct = default)
    {
        var corpo = new
        {
            client_id = _opcoes.ClientId,
            client_secret = _opcoes.ClientSecret,
            username = cpf,
            password = identificadorCaMaisSenha,
            grant_type = "password",
            scope = escopo,
            lifetime = duracaoSegundos,
            slot_alias = slotAlias
        };

        return LerToken(await PostJsonAsync("pwd_authorize", corpo, token: null, ct));
    }

    // ---- Certificados ----

    /// <summary>
    /// Os certificados do titular autenticado, já lidos.
    ///
    /// É daqui que sai o certificado PÚBLICO, e é por isso que a regra do CPF continua
    /// valendo tal e qual em nuvem: <see cref="CertificadoIcpBrasil.Ler"/> tira o CPF do OID
    /// 2.16.76.1.3.1 de um X.509 comum, e o PEM do PSC é um X.509 comum.
    /// </summary>
    public async Task<IReadOnlyList<CertificadoDeNuvem>> CertificadosAsync(
        string token, bool somenteDaAutorizacao = false, CancellationToken ct = default)
    {
        var caminho = "certificate-discovery"
            + (somenteDaAutorizacao ? "?from_authorization=true" : string.Empty);

        var resposta = await GetAsync(caminho, token, ct);
        var lido = JsonSerializer.Deserialize<RespostaCertificados>(resposta, Json);

        if (lido?.Certificates is null || lido.Certificates.Count == 0)
            throw new InvalidOperationException(
                "O SafeID não devolveu nenhum certificado para este titular. Confira se o "
                + "certificado está vinculado no aplicativo SafeID do celular.");

        var certificados = new List<CertificadoDeNuvem>();

        foreach (var item in lido.Certificates)
        {
            if (string.IsNullOrWhiteSpace(item.Certificate)) continue;

            try
            {
                var x509 = X509Certificate2.CreateFromPem(item.Certificate);
                certificados.Add(new CertificadoDeNuvem(
                    item.Alias ?? string.Empty, CertificadoIcpBrasil.Ler(x509)));
            }
            catch (Exception ex)
            {
                // Um certificado ilegível não pode derrubar a lista inteira — o titular pode
                // ter outro que serve. Mas some da lista, e sumir calado faria a médica
                // procurar no celular um certificado que o sistema descartou aqui.
                Diagnostico.Registrar($"ClienteSafeID.Certificados({item.Alias})", ex);
            }
        }

        return certificados;
    }

    // ---- Assinatura ----

    /// <summary>
    /// Manda o PSC assinar o hash e devolve o <b>PKCS#7 destacado</b>.
    ///
    /// O que sobe é o hash em base64 e mais nada. O que desce, com
    /// <c>signature_format: CMS</c>, traz <c>contentType</c>, <c>signingTime</c> (hora do
    /// PSC), <c>messageDigest</c> e <c>signingCertificateV2</c> — o mesmo formato que o
    /// <c>SignedCms</c> destacado do <see cref="AssinaturaDigitalService"/> confere.
    /// </summary>
    /// <param name="hash">Os 32 bytes crus do SHA-256. A codificação em base64 é feita aqui.</param>
    public async Task<byte[]> AssinarHashAsync(
        string token, byte[] hash, string identificador, string rotulo,
        string? certificadoAlias = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(hash);

        var corpo = new
        {
            certificate_alias = certificadoAlias,
            hashes = new[]
            {
                new
                {
                    id = identificador,
                    alias = rotulo,
                    hash = Convert.ToBase64String(hash),
                    hash_algorithm = OidSha256,
                    signature_format = "CMS"
                }
            }
        };

        var resposta = await PostJsonAsync("signature", corpo, token, ct);
        var lido = JsonSerializer.Deserialize<RespostaAssinatura>(resposta, Json);

        var assinatura = lido?.Signatures?.FirstOrDefault(s => s.Id == identificador)
            ?? lido?.Signatures?.FirstOrDefault();

        if (assinatura?.RawSignature is not { Length: > 0 } bruto)
            throw new InvalidOperationException(
                "O SafeID não devolveu a assinatura do documento. A folha NÃO foi assinada."
                + DetalheDoPsc(lido));

        return DecodificarAssinatura(bruto);
    }

    /// <summary>
    /// Lê o <c>raw_signature</c>, que a doc do PSC chama de "base64" e que nem sempre chega
    /// no base64 <b>padrão</b>.
    ///
    /// Por que não é só <c>Convert.FromBase64String</c>
    /// ------------------------------------------------
    /// Porque ele é estrito nas três coisas em que um serviço REST costuma ser frouxo, e
    /// qualquer uma delas produz a mesma <see cref="FormatException"/> sem dizer qual foi:
    ///
    /// 1. <b>Alfabeto</b> — base64url troca <c>+</c> e <c>/</c> por <c>-</c> e <c>_</c>. É a
    ///    forma que todo o resto desta integração já usa (o PKCE desta mesma classe a
    ///    produz), e é a que um gateway HTTP devolve quando o valor passou por uma camada
    ///    pensada para URL.
    /// 2. <b>Enchimento</b> — sem os <c>=</c> do fim, o comprimento não fecha em múltiplo de
    ///    4 e ele recusa um valor que é perfeitamente decodificável.
    /// 3. <b>Armadura PEM</b> — <c>-----BEGIN PKCS7-----</c> em volta do mesmo conteúdo.
    ///
    /// O hexadecimal — que alguns PSCs devolvem para o resultado cru — é testado <b>ANTES</b>
    /// do base64, e isso não é preferência: o alfabeto do hexa cabe inteiro dentro do do
    /// base64, então <c>Convert.FromBase64String</c> ACEITA qualquer hexa e devolve lixo.
    /// Deixá-lo por último não o tornaria só inalcançável: manteria a resposta em hexa
    /// entrando calada no <c>/Contents</c> do PDF, que é a garantia aparente que este projeto
    /// recusa desde a parcela 3. Por isso ele só ganha quando o resultado começa em
    /// <c>0x30</c> — o SEQUENCE que abre todo DER, e portanto todo PKCS#7. Um base64 de
    /// verdade lido como hexa não cai aí (um DER em base64 começa em "MII", não em "30").
    ///
    /// <b>Recusar continua sendo o desfecho certo quando nada serve</b>, e a mensagem carrega
    /// a evidência: sem ela a próxima ocorrência recomeça do zero, porque a frase do .NET não
    /// diz qual caractere ofendeu nem com o que o valor se parecia.
    /// </summary>
    private static byte[] DecodificarAssinatura(string bruto)
    {
        var limpo = SemArmaduraNemEspaco(bruto);

        if (TentarHexadecimalDer(limpo, out var bytes)) return bytes;
        if (TentarBase64(limpo, out bytes)) return bytes;
        if (TentarBase64(limpo.Replace('-', '+').Replace('_', '/'), out bytes)) return bytes;

        throw new InvalidOperationException(
            "O SafeID devolveu uma assinatura que não pôde ser decodificada. A folha NÃO foi "
            + "assinada. " + Evidencia(bruto));
    }

    /// <summary>Tira a armadura PEM e todo espaço em branco, inclusive quebras de linha.</summary>
    private static string SemArmaduraNemEspaco(string valor)
    {
        var semArmadura = ArmaduraPem().Replace(valor, string.Empty);
        return new string(semArmadura.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    [GeneratedRegex("-----[A-Z0-9 ]*-----")]
    private static partial Regex ArmaduraPem();

    private static bool TentarBase64(string valor, out byte[] bytes)
    {
        bytes = [];
        if (valor.Length == 0) return false;

        // O enchimento que falta é acrescentado aqui; o que sobra continua sendo recusado,
        // porque aí o valor está mesmo corrompido e não apenas encurtado.
        var resto = valor.Length % 4;
        var completo = resto == 0 ? valor : valor + new string('=', 4 - resto);

        try
        {
            bytes = Convert.FromBase64String(completo);
            return bytes.Length > 0;
        }
        catch (FormatException) { return false; }
    }

    /// <summary>
    /// Hexadecimal, e só quando o que sai dele é DER (ver o porquê da ordem em
    /// <see cref="DecodificarAssinatura"/>).
    /// </summary>
    private static bool TentarHexadecimalDer(string valor, out byte[] bytes)
    {
        bytes = [];
        if (valor.Length < 4 || valor.Length % 2 != 0 || !valor.All(Uri.IsHexDigit)) return false;

        try
        {
            var lido = Convert.FromHexString(valor);
            if (lido[0] != 0x30) return false;      // não é SEQUENCE: não é PKCS#7

            bytes = lido;
            return true;
        }
        catch (FormatException) { return false; }
    }

    /// <summary>
    /// O que a próxima ocorrência precisa ter na mão: o tamanho, o começo do valor e os
    /// caracteres que não pertencem ao base64 — que é o que nomeia a causa de uma vez.
    ///
    /// Mostrar o começo é seguro e é decisão: a assinatura destacada cobre um <b>hash</b>,
    /// não o documento, então não há nome de paciente, CID nem medicação dentro dela. E o
    /// que o PSC devolveu aqui já não é assinatura de nada — é o texto que veio no lugar.
    /// </summary>
    private static string Evidencia(string bruto)
    {
        var invalidos = bruto
            .Where(c => !char.IsWhiteSpace(c) && !EhCaractereBase64(c))
            .Distinct().Take(6).ToArray();

        var inicio = bruto.Length <= 24 ? bruto : bruto[..24] + "…";

        return $"O PSC devolveu {bruto.Length} caractere(s), começando por \"{inicio}\""
            + (invalidos.Length > 0
                ? $", com caractere(s) fora do base64: {string.Join(' ', invalidos)}."
                : " — e o comprimento não fecha em base64.");
    }

    private static bool EhCaractereBase64(char c)
        => char.IsAsciiLetterOrDigit(c) || c is '+' or '/' or '=' or '-' or '_';

    /// <summary>
    /// O que o PSC disse ao lado das assinaturas. Ele responde 200 com o corpo explicando o
    /// motivo em alguns casos, e "não devolveu a assinatura" sozinho manda procurar defeito
    /// no lugar errado.
    /// </summary>
    private static string DetalheDoPsc(RespostaAssinatura? lido)
    {
        var detalhe = string.Join(" ", new[] { lido?.Status, lido?.Message }
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        return detalhe.Length == 0 ? string.Empty : $" O PSC respondeu: {detalhe}";
    }

    /// <summary>Diz se um CPF tem certificado no PSC, e quais slots.</summary>
    public async Task<IReadOnlyList<string>> SlotsDoCpfAsync(
        string token, string cpf, CancellationToken ct = default)
    {
        var corpo = new
        {
            client_id = _opcoes.ClientId,
            client_secret = _opcoes.ClientSecret,
            user_cpf_cnpj = "CPF",
            val_cpf_cnpj = cpf
        };

        var resposta = await PostJsonAsync("user-discovery", corpo, token, ct);
        var lido = JsonSerializer.Deserialize<RespostaSlots>(resposta, Json);

        return lido?.Slots?.Select(s => s.SlotAlias ?? string.Empty)
            .Where(s => s.Length > 0).ToList() ?? [];
    }

    // ---- Transporte ----

    private async Task<TokenSafeID> PostFormAsync(
        string caminho, Dictionary<string, string> campos, CancellationToken ct)
    {
        using var conteudo = new FormUrlEncodedContent(campos);
        using var resposta = await _http.PostAsync(new Uri(_opcoes.Base, caminho), conteudo, ct);
        return LerToken(await GarantirAsync(resposta, caminho, ct));
    }

    private async Task<string> PostJsonAsync(
        string caminho, object corpo, string? token, CancellationToken ct)
    {
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Post, new Uri(_opcoes.Base, caminho))
        {
            Content = JsonContent.Create(corpo, options: Json)
        };

        Autorizar(requisicao, token);

        using var resposta = await _http.SendAsync(requisicao, ct);
        return await GarantirAsync(resposta, caminho, ct);
    }

    private async Task<string> GetAsync(string caminho, string? token, CancellationToken ct)
    {
        using var requisicao = new HttpRequestMessage(
            HttpMethod.Get, new Uri(_opcoes.Base, caminho));

        Autorizar(requisicao, token);

        using var resposta = await _http.SendAsync(requisicao, ct);
        return await GarantirAsync(resposta, caminho, ct);
    }

    private static void Autorizar(HttpRequestMessage requisicao, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
            requisicao.Headers.Authorization = new("Bearer", token);
    }

    /// <summary>
    /// Falha de assinatura nunca degrada em silêncio: quem clicou precisa saber que a folha
    /// NÃO foi assinada. O corpo da resposta entra na mensagem porque é onde o PSC explica
    /// o motivo — "401" sozinho manda a médica reinstalar o aplicativo à toa.
    /// </summary>
    private static async Task<string> GarantirAsync(
        HttpResponseMessage resposta, string caminho, CancellationToken ct)
    {
        var corpo = await resposta.Content.ReadAsStringAsync(ct);

        if (resposta.IsSuccessStatusCode) return corpo;

        var detalhe = string.IsNullOrWhiteSpace(corpo) ? "sem detalhe" : corpo.Trim();
        if (detalhe.Length > 400) detalhe = detalhe[..400] + "…";

        throw new InvalidOperationException(
            $"O SafeID recusou a chamada a '{caminho}' ({(int)resposta.StatusCode} "
            + $"{resposta.ReasonPhrase}). Detalhe: {detalhe}");
    }

    private static TokenSafeID LerToken(string json)
    {
        var lido = JsonSerializer.Deserialize<RespostaToken>(json, Json)
            ?? throw new InvalidOperationException("O SafeID devolveu um token ilegível.");

        if (string.IsNullOrWhiteSpace(lido.AccessToken))
            throw new InvalidOperationException("O SafeID não devolveu token de acesso.");

        return new TokenSafeID(
            lido.AccessToken,
            DateTime.UtcNow.AddSeconds(lido.ExpiresIn > 0 ? lido.ExpiresIn : 300),
            lido.Scope,
            lido.AuthorizedIdentification,
            lido.SlotAlias);
    }

    // ---- Contratos de resposta ----

    private sealed record RespostaToken(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("scope")] string? Scope,
        [property: JsonPropertyName("authorized_identification")] string? AuthorizedIdentification,
        [property: JsonPropertyName("slot_alias")] string? SlotAlias);

    private sealed record RespostaCertificados(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("certificates")] List<ItemCertificado>? Certificates);

    private sealed record ItemCertificado(
        [property: JsonPropertyName("alias")] string? Alias,
        [property: JsonPropertyName("certificate")] string? Certificate);

    private sealed record RespostaAssinatura(
        [property: JsonPropertyName("certificate_alias")] string? CertificateAlias,
        [property: JsonPropertyName("signatures")] List<ItemAssinatura>? Signatures,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("message")] string? Message);

    private sealed record ItemAssinatura(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("raw_signature")] string? RawSignature);

    private sealed record RespostaSlots(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("slots")] List<ItemSlot>? Slots);

    private sealed record ItemSlot(
        [property: JsonPropertyName("slot_alias")] string? SlotAlias,
        [property: JsonPropertyName("label")] string? Label);
}

/// <summary>
/// O par do PKCE (RFC 7636): um segredo sorteado agora (<see cref="Verificador"/>) e o
/// SHA-256 dele em base64url (<see cref="Desafio"/>).
///
/// Sem ele, quem interceptasse o <c>code</c> na URI de retorno trocaria por um token e
/// assinaria pela médica. Num app desktop, onde a URI de retorno é um loopback que qualquer
/// processo da máquina pode tentar escutar, isto não é formalidade.
/// </summary>
public sealed record DesafioPkce(string Verificador, string Desafio)
{
    /// <summary>Sorteia um par novo. O verificador tem 43–128 caracteres, como a RFC exige.</summary>
    public static DesafioPkce Gerar()
    {
        var bruto = RandomNumberGenerator.GetBytes(32);
        var verificador = Base64Url(bruto);
        var desafio = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verificador)));
        return new DesafioPkce(verificador, desafio);
    }

    private static string Base64Url(byte[] dados)
        => Convert.ToBase64String(dados).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
