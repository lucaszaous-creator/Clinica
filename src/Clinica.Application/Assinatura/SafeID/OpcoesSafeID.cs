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
/// <param name="RedirectUris">
/// URIs de retorno da autorização, na ordem de preferência. Precisam estar PRÉ-CADASTRADAS
/// na Safeweb — não basta mandar na requisição —, e a Safeweb <b>aceita loopback</b>
/// (confirmado no cadastro da aplicação da clínica), que é o padrão RFC 8252 para
/// aplicativo nativo.
///
/// São VÁRIAS de propósito. A aplicação precisa abrir uma porta na máquina do consultório, e
/// porta é recurso disputado: outro programa pode já estar em 8123. Com três registradas, o
/// sistema cai para a seguinte sozinho — a alternativa seria a médica não conseguir assinar
/// até alguém voltar ao portal da Safeweb, que é o pior momento possível para descobrir isso.
/// </param>
public sealed record OpcoesSafeID(
    string ClientId,
    string ClientSecret,
    IReadOnlyList<Uri>? RedirectUris = null,
    Uri? BaseUrl = null)
{
    /// <summary>Produção.</summary>
    public static readonly Uri BasePadrao =
        new("https://pscsafeweb.safewebpss.com.br/Service/Microservice/OAuth/api/v0/oauth/");

    /// <summary>
    /// Homologação. Não está na coleção Postman — saiu do <c>appsettings.json</c> do projeto
    /// de demonstração da própria Safeweb, e é o ambiente onde se mede o que a doc não
    /// publica (o tamanho do PKCS#7) sem gastar assinatura do plano de produção.
    /// </summary>
    public static readonly Uri BaseHomologacao =
        new("https://pscsafeweb-homologacao.safewebpss.com.br/Service/Microservice/OAuth/api/v0/oauth/");

    public Uri Base => BaseUrl ?? BasePadrao;

    /// <summary>
    /// "homologação" ou "produção", para a mensagem de erro NOMEAR o ambiente.
    ///
    /// O PSC responde credencial errada e ambiente errado com a mesma frase — "não foi
    /// possível encontrar a aplicação com base no client_id" —, e a aplicação cadastrada no
    /// portal de produção realmente não existe em homologação. Sem dizer qual ambiente foi
    /// chamado, a clínica conferiria o client_id letra por letra procurando um erro de
    /// digitação que não existe.
    /// </summary>
    public string NomeAmbiente => Base == BaseHomologacao ? "homologação" : "produção";

    /// <summary>
    /// As URIs de retorno cadastradas no portal da Safeweb para esta aplicação.
    ///
    /// Moram em CÓDIGO, e não em configuração, porque não são escolha de quem instala: são
    /// parte do cadastro da aplicação no PSC, e o PSC recusa qualquer outra. Deixá-las
    /// configuráveis por máquina só criaria a chance de alguém digitar uma quarta porta que
    /// a Safeweb nunca vai aceitar — e o erro apareceria como "autorização recusada", sem
    /// dizer que a causa foi um número trocado num campo.
    ///
    /// São três porque porta é recurso disputado: se 8123 estiver ocupada na máquina do
    /// consultório, a escuta cai para a seguinte sozinha.
    /// </summary>
    public static readonly IReadOnlyList<Uri> RetornosPadrao =
    [
        new("http://127.0.0.1:8123/safeid/retorno"),
        new("http://127.0.0.1:8124/safeid/retorno"),
        new("http://127.0.0.1:8125/safeid/retorno")
    ];

    /// <summary>As de retorno em uso — as configuradas, ou o cadastro padrão.</summary>
    public IReadOnlyList<Uri> Retornos =>
        RedirectUris is { Count: > 0 } configuradas ? configuradas : RetornosPadrao;
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

    /// <summary>
    /// Quanto vale a sessão quando o ato pede mais de uma assinatura. Cinco minutos cobrem
    /// duas selagens seguidas com folga para o banco e a rede, e são uma fração do padrão
    /// do PSC — autorizar uma SEMANA para selar duas folhas abre muito mais do que o ato
    /// pede.
    /// </summary>
    public const int SegundosDaSessaoCurta = 300;

    /// <summary>
    /// O escopo que um ato precisa, pelo número de documentos que ele vai selar com o MESMO
    /// certificado.
    ///
    /// ⚠️ Errar isto só aparece na SEGUNDA assinatura, e sempre: com
    /// <see cref="AssinaturaUnica"/> o token <b>morre no uso</b>, então um ato de duas
    /// selagens teria a primeira aceita e a segunda recusada em toda folha da clínica. Os
    /// testes não pegariam — o assinador de teste não tem token que morra —, e cada
    /// tentativa em produção é COBRADA.
    ///
    /// Mora aqui, e não na tela que escolhe o certificado, pela razão de sempre: decisão
    /// dentro de ViewModel do WPF é decisão que o <c>dotnet test</c> não alcança.
    /// </summary>
    public static (string Escopo, int? DuracaoSegundos) ParaAto(int assinaturas)
        => assinaturas > 1
            ? (Sessao, SegundosDaSessaoCurta)
            : (AssinaturaUnica, null);
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
