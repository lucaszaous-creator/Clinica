using System.Net;

namespace Clinica.Application.Abstracoes;

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
            || !EndpointPermitido(uri))
            return null;

        return new OpcoesArmazenamento(
            endpoint, Limpar(regiao) ?? RegiaoPadrao, bucket, chave, segredo);
    }

    private static bool EndpointPermitido(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort
            || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return false;

        var host = uri.IdnHost;
        return host.Contains('.') && !host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            && !IPAddress.TryParse(host, out _);
    }

    public static OpcoesArmazenamento? DoAmbiente()
        => De(Var("ENDPOINT"), Var("REGIAO"), Var("BUCKET"), Var("CHAVE"), Var("SEGREDO"));

    private static string? Var(string nome)
        => Environment.GetEnvironmentVariable(PrefixoAmbiente + nome);

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
