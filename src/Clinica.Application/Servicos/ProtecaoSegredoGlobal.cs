using System.Security.Cryptography;
using System.Text;

namespace Clinica.Application.Servicos;

/// <summary>Protege credenciais compartilhadas com chave fora do banco clínico.</summary>
public sealed class ProtecaoSegredoGlobal
{
    public const string VariavelChave = "CLINICA_CREDENCIAIS_CHAVE";
    private const string Prefixo = "enc:v1:";
    private readonly byte[] chave;

    public ProtecaoSegredoGlobal(string? chaveBase64)
    {
        try { chave = Convert.FromBase64String(chaveBase64 ?? ""); }
        catch (FormatException) { throw new InvalidOperationException("Configure a chave de proteção das credenciais da clínica."); }
        if (chave.Length != 32)
            throw new InvalidOperationException("Configure uma chave de proteção de credenciais com 32 bytes.");
    }

    public static ProtecaoSegredoGlobal DoAmbiente()
        => new(Environment.GetEnvironmentVariable(VariavelChave));

    public static bool EstaProtegido(string valor) => valor.StartsWith(Prefixo, StringComparison.Ordinal);

    public string Proteger(string nome, string valor)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var entrada = Encoding.UTF8.GetBytes(valor);
        var cifrado = new byte[entrada.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(chave, 16))
            aes.Encrypt(nonce, entrada, cifrado, tag, Encoding.UTF8.GetBytes(nome));
        var pacote = new byte[nonce.Length + tag.Length + cifrado.Length];
        nonce.CopyTo(pacote, 0);
        tag.CopyTo(pacote, nonce.Length);
        cifrado.CopyTo(pacote, nonce.Length + tag.Length);
        return Prefixo + Convert.ToBase64String(pacote);
    }

    public string Revelar(string nome, string valor)
    {
        if (!EstaProtegido(valor))
            throw new InvalidOperationException("A credencial armazenada precisa ser protegida.");
        try
        {
            var pacote = Convert.FromBase64String(valor[Prefixo.Length..]);
            if (pacote.Length < 28) throw new FormatException();
            var claro = new byte[pacote.Length - 28];
            using (var aes = new AesGcm(chave, 16))
                aes.Decrypt(pacote.AsSpan(0, 12), pacote.AsSpan(28), pacote.AsSpan(12, 16),
                    claro, Encoding.UTF8.GetBytes(nome));
            return Encoding.UTF8.GetString(claro);
        }
        catch (Exception e) when (e is FormatException or CryptographicException)
        {
            throw new InvalidOperationException("Não foi possível abrir a credencial protegida da clínica.");
        }
    }
}
