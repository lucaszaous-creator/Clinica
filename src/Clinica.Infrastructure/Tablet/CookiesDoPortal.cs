namespace Clinica.Infrastructure.Tablet;

/// <summary>
/// Os cookies do portal num lugar só (nov/2026): a API os grava e a gestão web os lê. Os dois
/// processos compartilham o anel de chaves da proteção de dados (o mesmo diretório e o mesmo nome
/// de aplicação), então o propósito e o formato do cookie do aparelho precisam ser UM.
/// </summary>
public static class CookiesDoPortal
{
    /// <summary>O nome de aplicação da proteção de dados — a API e a gestão leem as mesmas chaves.</summary>
    public const string AplicacaoDaProtecao = "Clinica.Assinaturas.Tablet.v1";

    public const string Sessao = "__Host-clinica.tablet";
    public const string Dispositivo = "__Host-clinica.tablet.device";

    /// <summary>O propósito do cookie do aparelho. Mudou com a saída do código do tablet: cookie antigo não vale mais.</summary>
    public const string PropositoDispositivo = "DispositivoPortal.v2";
    public const string MarcaDispositivo = "portal-v2";

    /// <summary>"portal-v2|&lt;aparelho&gt;|&lt;expira em ms&gt;" já aberto pela proteção → o aparelho, se ainda vale.</summary>
    public static string? LerDispositivo(string? aberto, long agoraMs)
    {
        var valor = (aberto ?? "").Split('|');
        return valor.Length == 3 && valor[0] == MarcaDispositivo && valor[1].Length is > 0 and <= 64
               && long.TryParse(valor[2], out var expira) && expira > agoraMs
            ? valor[1]
            : null;
    }

    public static string EscreverDispositivo(string dispositivo, long expiraMs) => $"{MarcaDispositivo}|{dispositivo}|{expiraMs}";
}
