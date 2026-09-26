using System.Net;

namespace Clinica.Assinaturas.Api;

/// <summary>Origem do visitante para limitar requisições anônimas no túnel privado.</summary>
public static class OrigemRateLimitTablet
{
    public static string? Obter(HttpContext contexto, bool socketPrivado)
    {
        if (!socketPrivado)
            return contexto.Connection.RemoteIpAddress?.ToString() ?? "local";

        // Unix Domain Socket não fornece RemoteIpAddress. Somente cloudflared tem
        // acesso ao socket, e a borda sobrescreve CF-Connecting-IP com o IP real.
        if (contexto.Connection.RemoteIpAddress is not null)
            return null;
        var valores = contexto.Request.Headers["CF-Connecting-IP"];
        if (valores.Count != 1 || !IPAddress.TryParse(valores[0], out var endereco))
            return null;
        return endereco.ToString();
    }
}
