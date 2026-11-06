using System.Security.Cryptography;
using System.Text;

namespace Clinica.Application.Tablet;

/// <summary>
/// TOTP (RFC 6238 sobre HOTP, RFC 4226): HMAC-SHA1, 6 dígitos, passo de 30 s — o padrão que o
/// Google Authenticator e o Microsoft Authenticator leem do QR. Aceita o passo anterior e o
/// seguinte (relógio do celular adiantado ou atrasado até 30 s) e devolve QUAL passo casou,
/// para quem chama recusar a reutilização.
/// </summary>
public static class Totp
{
    public const int Digitos = 6;
    public const int PassoSegundos = 30;
    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Segredo novo de 160 bits (o tamanho do HMAC-SHA1).</summary>
    public static byte[] NovoSegredo() => RandomNumberGenerator.GetBytes(20);

    public static long Passo(DateTimeOffset instante) => instante.ToUnixTimeSeconds() / PassoSegundos;

    public static string Codigo(byte[] segredo, long passo)
    {
        Span<byte> contador = stackalloc byte[8];
        for (var i = 7; i >= 0; i--) { contador[i] = (byte)(passo & 0xFF); passo >>= 8; }
        Span<byte> hmac = stackalloc byte[20];
        HMACSHA1.HashData(segredo, contador, hmac);
        var deslocamento = hmac[^1] & 0x0F;
        var binario = ((hmac[deslocamento] & 0x7F) << 24) | (hmac[deslocamento + 1] << 16)
                      | (hmac[deslocamento + 2] << 8) | hmac[deslocamento + 3];
        return (binario % 1_000_000).ToString("D6");
    }

    /// <summary>
    /// O passo que casou com <paramref name="codigo"/>, ou nulo. Só aceita passo MAIOR que
    /// <paramref name="ultimoPassoAceito"/> — o mesmo código não entra duas vezes.
    /// A comparação é de tempo constante.
    /// </summary>
    public static long? Verificar(byte[] segredo, string? codigo, DateTimeOffset agora, long ultimoPassoAceito)
    {
        var limpo = Normalizar(codigo);
        if (limpo is null) return null;
        var atual = Passo(agora);
        long? casou = null;
        for (var p = atual - 1; p <= atual + 1; p++)
        {
            var esperado = Encoding.ASCII.GetBytes(Codigo(segredo, p));
            if (CryptographicOperations.FixedTimeEquals(esperado, Encoding.ASCII.GetBytes(limpo)) && p > ultimoPassoAceito)
                casou ??= p;
        }
        return casou;
    }

    /// <summary>"123 456" ou "123-456" viram "123456"; qualquer outra coisa é nulo.</summary>
    public static string? Normalizar(string? codigo)
    {
        if (codigo is null || codigo.Length > 12) return null;
        var digitos = new string(codigo.Where(c => c is not (' ' or '-')).ToArray());
        return digitos.Length == Digitos && digitos.All(char.IsAsciiDigit) ? digitos : null;
    }

    public static string Base32(byte[] dados)
    {
        var sb = new StringBuilder((dados.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in dados)
        {
            buffer = (buffer << 8) | b; bits += 8;
            while (bits >= 5) { sb.Append(Alfabeto[(buffer >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(Alfabeto[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    /// <summary>A chave em grupos de 4 ("JBSW Y3DP …"), para digitar quando a câmera não lê o QR.</summary>
    public static string ChaveLegivel(byte[] segredo)
        => string.Join(' ', Base32(segredo).Chunk(4).Select(c => new string(c)));

    /// <summary>O endereço otpauth:// que o QR carrega.</summary>
    public static string Endereco(string emissor, string conta, byte[] segredo)
        => $"otpauth://totp/{Uri.EscapeDataString(emissor)}:{Uri.EscapeDataString(conta)}"
           + $"?secret={Base32(segredo)}&issuer={Uri.EscapeDataString(emissor)}&algorithm=SHA1&digits={Digitos}&period={PassoSegundos}";

    /// <summary>Dez códigos de recuperação de 10 caracteres (50 bits cada), no formato "abcde-fghij".</summary>
    public static IReadOnlyList<string> NovosCodigosDeRecuperacao()
        => Enumerable.Range(0, 10).Select(_ =>
        {
            var c = Base32(RandomNumberGenerator.GetBytes(7))[..10].ToLowerInvariant();
            return $"{c[..5]}-{c[5..]}";
        }).ToList();

    /// <summary>O resumo guardado de um código de recuperação, amarrado ao usuário.</summary>
    public static string ResumoRecuperacao(int usuarioId, string codigo)
    {
        var limpo = new string(codigo.Trim().ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"recuperacao-portal:{usuarioId}:{limpo}")));
    }
}
