namespace Clinica.Application.Abstracoes;

/// <summary>
/// As credenciais do armazenamento S3-compatível em vigor (parcela 53).
///
/// De onde elas saem
/// -----------------
/// <b>Do banco, com o ambiente podendo sobrepor</b> — o precedente do
/// <c>ProvedorOpcoesSafeID</c> (parcela 44). Cadastrar em Configurações uma vez alcança
/// todas as máquinas; exigir variável de ambiente obrigaria a abrir o Prompt de Comando em
/// cada consultório, e a publicação falharia CALADA justamente na máquina que ninguém
/// configurou.
///
/// O ambiente vence porque é assim que se testa um build sem escrever na base da clínica —
/// a mesma regra de <c>ConnectionStrings__Clinica</c> (ver <c>docs/testar-sem-publicar.md</c>).
/// </summary>
/// <param name="Endpoint">
/// Endereço do PROVEDOR (ex.: <c>https://br-se1.magaluobjects.com</c>). Não confundir com o
/// domínio da clínica: aquele é o nome público que vai no QR, este é para onde o sistema
/// escreve. São dois campos porque são duas coisas — e é justamente essa separação que
/// permite trocar de provedor sem matar os QR já assinados.
/// </param>
/// <param name="Regiao">
/// Região do provedor. Vários S3-compatíveis ignoram, mas o SDK exige um valor para assinar
/// a requisição; por isso há padrão em vez de campo obrigatório.
/// </param>
/// <param name="Bucket">Balde onde os objetos são gravados.</param>
public sealed record OpcoesArmazenamento(
    string Endpoint,
    string Regiao,
    string Bucket,
    string Chave,
    string Segredo)
{
    /// <summary>
    /// Região usada quando a clínica não informou nenhuma. <c>us-east-1</c> é a que os
    /// provedores S3-compatíveis aceitam por convenção quando não têm região própria.
    /// </summary>
    public const string RegiaoPadrao = "us-east-1";

    private const string PrefixoAmbiente = "CLINICA_ARMAZENAMENTO_";

    /// <summary>
    /// Monta as opções, ou devolve <c>null</c> quando falta o que quer que seja.
    ///
    /// <b>Meia credencial é o mesmo que credencial nenhuma</b>, e é aqui — num lugar só —
    /// que isso é decidido. Aceitar um conjunto incompleto faria a publicação parecer
    /// ligada e estourar no clique de quem está assinando, com o paciente esperando.
    /// </summary>
    public static OpcoesArmazenamento? De(
        string? endpoint, string? regiao, string? bucket, string? chave, string? segredo)
    {
        endpoint = Limpar(endpoint);
        bucket = Limpar(bucket);
        chave = Limpar(chave);
        segredo = Limpar(segredo);

        if (endpoint is null || bucket is null || chave is null || segredo is null) return null;

        // Endereço escrito errado é recusado AQUI, e não na hora de publicar: o erro do SDK
        // para uma URL malformada não diz nada sobre esta tela.
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return null;

        return new OpcoesArmazenamento(
            endpoint, Limpar(regiao) ?? RegiaoPadrao, bucket, chave, segredo);
    }

    /// <summary>
    /// As opções vindas do ambiente, ou <c>null</c>. Responde INTEIRO ou não responde:
    /// misturar a chave do ambiente com o segredo do banco produziria um par que não existe
    /// em lugar nenhum — a mesma decisão do SafeID.
    /// </summary>
    public static OpcoesArmazenamento? DoAmbiente()
        => De(Var("ENDPOINT"), Var("REGIAO"), Var("BUCKET"), Var("CHAVE"), Var("SEGREDO"));

    private static string? Var(string nome)
        => Environment.GetEnvironmentVariable(PrefixoAmbiente + nome);

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
