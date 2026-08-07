namespace Clinica.Application.Assinatura.SafeID;

/// <summary>
/// O que a aplicação precisa saber para falar com o PSC Safeweb.
///
/// O <see cref="ClientSecret"/> é credencial, e credencial não mora em tabela de
/// configuração junto do prazo de recurso de glosa: quem lê `Configuracoes` no banco passa a
/// poder assinar pela médica. Vem de fora — variável de ambiente ou o mesmo cofre DPAPI que
/// já guarda a connection string (`ConexaoStore`) —, e é por isso que este record não tem um
/// método "carregar do banco".
/// </summary>
/// <param name="RedirectUri">
/// URI de retorno da autorização. Precisa estar PRÉ-CADASTRADA na Safeweb — não basta
/// mandar na requisição. Para aplicativo nativo o padrão (RFC 8252) é um loopback
/// <c>http://127.0.0.1:{porta}</c>, e se a Safeweb aceitar cadastrá-lo o fluxo do QR Code
/// funciona sem a clínica ter endereço público nenhum.
/// </param>
public sealed record OpcoesSafeID(
    string ClientId,
    string ClientSecret,
    Uri? RedirectUri = null,
    Uri? BaseUrl = null)
{
    /// <summary>
    /// Produção. A coleção oficial da Safeweb traz esta URL e só ela — não há host de
    /// homologação documentado, e inventar um subdomínio "hml" produziria erro de DNS
    /// apresentado como falha de credencial.
    /// </summary>
    public static readonly Uri BasePadrao =
        new("https://pscsafeweb.safewebpss.com.br/Service/Microservice/OAuth/api/v0/oauth/");

    public Uri Base => BaseUrl ?? BasePadrao;
}

/// <summary>
/// Escopo do token, que decide QUANTAS vezes a médica confirma no celular.
///
/// Numa consulta que emite receita, atestado e folha de infusão,
/// <see cref="AssinaturaUnica"/> pediria quatro confirmações. <see cref="Sessao"/> pede uma
/// e vale até o token expirar — é o que serve ao consultório.
/// </summary>
public static class EscopoSafeID
{
    /// <summary>Um hash só; o token morre no uso.</summary>
    public const string AssinaturaUnica = "single_signature";

    /// <summary>Vários hashes numa requisição só; o token morre no uso.</summary>
    public const string AssinaturaMultipla = "multi_signature";

    /// <summary>Várias chamadas enquanto o token valer. Máximo de 7 dias para pessoa física.</summary>
    public const string Sessao = "signature_session";
}

/// <summary>Token de acesso devolvido pelo PSC, com o CPF que ele afirma ser do titular.</summary>
/// <param name="CpfAutorizado">
/// O que o servidor DIZ. Não substitui o CPF lido de dentro do certificado (OID
/// 2.16.76.1.3.1) — são coisas diferentes no dia em que divergirem, e é o do certificado que
/// prova quem assinou.
/// </param>
public sealed record TokenSafeID(
    string AccessToken,
    DateTime ExpiraEm,
    string? Escopo = null,
    string? CpfAutorizado = null,
    string? SlotAlias = null)
{
    /// <summary>
    /// Uma folga de 30 s evita o caso em que o token passa na conferência e vence no voo
    /// da requisição — que apareceria para a médica como falha de assinatura sem explicação.
    /// </summary>
    public bool Vigente => DateTime.UtcNow < ExpiraEm.AddSeconds(-30);
}

/// <summary>Um certificado que o titular tem no PSC.</summary>
/// <param name="Alias">
/// Identificador do certificado no PSC. Vai na requisição de assinatura quando o titular tem
/// mais de um — sem ele o PSC escolhe sozinho, e escolher sozinho acerta na maioria das vezes
/// e erra em silêncio nas outras.
/// </param>
public sealed record CertificadoDeNuvem(string Alias, CertificadoAssinatura Certificado);
