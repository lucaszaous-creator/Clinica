namespace Clinica.Application.Assinatura.SafeID;

/// <summary>
/// De onde saem as credenciais do SafeID.
///
/// Por que do AMBIENTE, e não da tabela de configuração
/// ----------------------------------------------------
/// O <c>client_secret</c> autoriza pedir assinatura em nome da titular. Guardá-lo em
/// <c>Configuracoes</c> daria a quem lê aquela tabela — qualquer usuário do Gerente, qualquer
/// consulta ao banco, qualquer dump de apoio — o poder de assinar pela médica. É a mesma razão
/// pela qual a connection string mora no <c>ConexaoStore</c> criptografado e não no banco que
/// ela mesma abre.
///
/// A convenção de nome segue a que o projeto já usa para
/// <c>ConnectionStrings__Clinica</c>: duplo sublinhado separando a seção da chave.
/// </summary>
public static class ConfiguracaoSafeID
{
    public const string ChaveClientId = "SafeID__ClientId";
    public const string ChaveClientSecret = "SafeID__ClientSecret";
    public const string ChaveRedirectUri = "SafeID__RedirectUri";
    public const string ChaveAmbiente = "SafeID__Ambiente";

    /// <summary>
    /// Lê as opções do ambiente, ou devolve <c>null</c> quando a clínica não configurou o
    /// SafeID.
    ///
    /// <b>Devolver null é o comportamento correto, não uma falha.</b> Este método é chamado
    /// no arranque de TODOS os aplicativos, inclusive o faturamento congelado, que não tem
    /// nada a ver com assinatura em nuvem. Lançar exceção aqui derrubaria a abertura de um
    /// app em produção por causa de uma variável de ambiente que ele nunca precisou.
    /// </summary>
    public static OpcoesSafeID? DoAmbiente(Func<string, string?>? ler = null)
    {
        ler ??= Environment.GetEnvironmentVariable;

        var clientId = Limpar(ler(ChaveClientId));
        var clientSecret = Limpar(ler(ChaveClientSecret));

        // Sem as duas não há integração possível. Meia configuração é pior do que nenhuma:
        // a tela apareceria e falharia no clique.
        if (clientId is null || clientSecret is null) return null;

        return new OpcoesSafeID(
            clientId,
            clientSecret,
            RedirectUris: LerUris(ler(ChaveRedirectUri)),
            BaseUrl: EhHomologacao(ler(ChaveAmbiente))
                ? OpcoesSafeID.BaseHomologacao
                : OpcoesSafeID.BasePadrao);
    }

    /// <summary>
    /// Homologação só quando pedida por extenso. O padrão é PRODUÇÃO porque o engano barato
    /// é apontar para homologação sem perceber e descobrir depois que meses de documentos
    /// foram assinados com certificado de teste — o inverso falha no primeiro clique, que é
    /// o momento certo de falhar.
    /// </summary>
    private static bool EhHomologacao(string? valor)
        => Limpar(valor) is { } texto
           && (texto.Equals("homologacao", StringComparison.OrdinalIgnoreCase)
               || texto.Equals("homologação", StringComparison.OrdinalIgnoreCase)
               || texto.Equals("hml", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// As URIs de retorno, separadas por ponto e vírgula, na ordem de preferência —
    /// as MESMAS cadastradas no portal da Safeweb, e na mesma grafia.
    ///
    /// Cada uma é lida como está, sem normalizar nem completar nada: o OAuth compara
    /// <c>redirect_uri</c> como TEXTO, e uma barra a mais no fim vira recusa com mensagem que
    /// não explica o motivo. "Arrumar" o que a clínica digitou produziria exatamente o erro
    /// que ninguém consegue diagnosticar.
    /// </summary>
    private static IReadOnlyList<Uri>? LerUris(string? valor)
    {
        if (Limpar(valor) is not { } texto) return null;

        var uris = texto
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(parte => Uri.TryCreate(parte, UriKind.Absolute, out var uri) ? uri : null)
            .Where(uri => uri is not null)
            .Select(uri => uri!)
            .ToList();

        return uris.Count > 0 ? uris : null;
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
