using System.Net;
using System.Net.Sockets;

namespace Clinica.Crm.Bridge;

/// <summary>
/// ONDE a ponte escuta e QUEM pode chamá-la (out/2026, a decisão das duas VPS).
///
/// A ponte roda ao lado do banco da clínica (VPS 1) e o CRM roda noutra máquina (VPS 2),
/// ligada por um túnel WireGuard. Por isso a ponte deixa de estar presa ao 127.0.0.1 — mas
/// só pode sair dele para um endereço de REDE PRIVADA. As duas recusas são o ponto:
/// <list type="bullet">
/// <item><c>0.0.0.0</c> e qualquer IP público são recusados na partida: a ponte escutando
/// na internet é exatamente o "API HTTPS no meio" que <c>docs/banco-na-vps.md</c> recusou,
/// e um erro de digitação no arquivo de configuração não pode fazer isso em silêncio.</item>
/// <item>Nome de máquina também é recusado: ele resolve para o que o DNS quiser, e a
/// conferência de "é privado?" tem de ser sobre o endereço que de fato vai escutar.</item>
/// </list>
/// A chave da ponte (<c>CRM_BRIDGE_KEY</c>) continua exigida em toda chamada: o túnel é a
/// primeira tranca, a chave é a segunda, e o firewall da VPS é a terceira.
/// </summary>
public static class RedeDaPonte
{
    public const string EscutaPadrao = "http://127.0.0.1:5190";
    public const int MaximoDeClientes = 8;

    /// <summary>Loopback, as três faixas da RFC 1918, a 100.64/10 (CGNAT, usada por túneis
    /// como Tailscale) e IPv6 de uso local (fc00::/7).</summary>
    public static bool Privado(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6) return (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
        var b = ip.GetAddressBytes();
        return b[0] == 10
            || b[0] == 172 && b[1] is >= 16 and <= 31
            || b[0] == 192 && b[1] == 168
            || b[0] == 100 && b[1] is >= 64 and <= 127;
    }

    /// <summary>O endereço de escuta (<c>CRM_BRIDGE_URL</c>), conferido. Vazio = o padrão de sempre.</summary>
    public static Uri Escuta(string? valor)
    {
        var texto = string.IsNullOrWhiteSpace(valor) ? EscutaPadrao : valor.Trim();
        if (!Uri.TryCreate(texto, UriKind.Absolute, out var uri) || uri.Scheme != "http"
            || uri.UserInfo != "" || uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "" || uri.IsDefaultPort)
            throw new InvalidOperationException("CRM_BRIDGE_URL deve ser http://IP:PORTA, sem caminho (ex.: http://10.8.0.1:5190).");
        if (!IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip))
            throw new InvalidOperationException("CRM_BRIDGE_URL aceita só um endereço IP, nunca um nome de máquina.");
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || !Privado(ip))
            throw new InvalidOperationException($"A ponte só escuta em endereço privado (127.0.0.1 ou o IP do túnel). {ip} não é privado: nunca abra a ponte para a internet.");
        return uri;
    }

    /// <summary>Os IPs que podem chamar a ponte além do próprio servidor
    /// (<c>CRM_BRIDGE_ALLOWED_CLIENTS</c>, separados por <c>;</c> ou vírgula).</summary>
    public static IReadOnlySet<IPAddress> Clientes(string? valor)
    {
        var lista = new HashSet<IPAddress>();
        foreach (var parte in (valor ?? "").Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!IPAddress.TryParse(parte, out var ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
                throw new InvalidOperationException($"CRM_BRIDGE_ALLOWED_CLIENTS aceita só endereços IP explícitos ({parte}).");
            if (!Privado(ip))
                throw new InvalidOperationException($"CRM_BRIDGE_ALLOWED_CLIENTS aceita só endereços privados (o IP do CRM dentro do túnel). {ip} não é.");
            lista.Add(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);
        }
        if (lista.Count > MaximoDeClientes) throw new InvalidOperationException($"Configure até {MaximoDeClientes} clientes da ponte.");
        return lista;
    }

    /// <summary>Quem chamou pode passar da porta? O próprio servidor sempre; o resto só se listado.</summary>
    public static bool Permitido(IPAddress? origem, IReadOnlySet<IPAddress> clientes)
    {
        if (origem is null) return false;
        if (origem.IsIPv4MappedToIPv6) origem = origem.MapToIPv4();
        return IPAddress.IsLoopback(origem) || clientes.Contains(origem);
    }
}
