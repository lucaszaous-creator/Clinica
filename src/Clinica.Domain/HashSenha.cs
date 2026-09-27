using System.Security.Cryptography;

namespace Clinica.Domain;

/// <summary>
/// Guarda de senha da suíte: PBKDF2-HMAC-SHA256 com sal por usuário.
///
/// Está no domínio, e não no serviço, porque é REGRA — o formato do hash tem que ser o
/// mesmo em quem grava e em quem confere, e uma cópia divergente transformaria "senha
/// errada" num mistério. Nada aqui depende de banco, tela ou DI.
///
/// A comparação é em tempo fixo: comparar string com <c>==</c> sai mais cedo no
/// primeiro byte diferente, e esse tempo é informação sobre o hash.
/// </summary>
public static class HashSenha
{
    /// <summary>O custo novo acompanha o hash; os hashes antigos continuam verificáveis.</summary>
    public const int Iteracoes = 600_000;
    private const int IteracoesLegadas = 210_000;
    private const string PrefixoAtual = "pbkdf2-sha256:600000:";

    private const int TamanhoSal = 16;   // 128 bits
    private const int TamanhoHash = 32;  // 256 bits

    /// <summary>Tamanho mínimo aceito — abaixo disso, iteração nenhuma salva a senha.</summary>
    public const int TamanhoMinimoSenha = 15;

    /// <summary>Gera hash versionado e sal em Base64 para uma senha nova.</summary>
    public static (string Hash, string Sal) Gerar(string senha)
    {
        ArgumentNullException.ThrowIfNull(senha);

        var sal = RandomNumberGenerator.GetBytes(TamanhoSal);
        var hash = Derivar(senha, sal);
        return (PrefixoAtual + Convert.ToBase64String(hash), Convert.ToBase64String(sal));
    }

    /// <summary>
    /// A senha corresponde ao hash gravado? Devolve false (nunca lança) quando o par
    /// gravado está corrompido — usuário com hash ilegível não entra, mas também não
    /// derruba a tela de login.
    /// </summary>
    public static bool Confere(string? senha, string? hash, string? sal)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(sal))
            return false;

        var atual = hash.StartsWith(PrefixoAtual, StringComparison.Ordinal);
        if (!atual && hash.Contains(':')) return false;
        var valorHash = atual ? hash[PrefixoAtual.Length..] : hash;
        byte[] esperado;
        byte[] bytesSal;
        try
        {
            esperado = Convert.FromBase64String(valorHash);
            bytesSal = Convert.FromBase64String(sal);
        }
        catch (FormatException)
        {
            return false;
        }

        if (esperado.Length != TamanhoHash || bytesSal.Length != TamanhoSal) return false;

        var calculado = Derivar(senha, bytesSal, atual ? Iteracoes : IteracoesLegadas);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    public static bool PrecisaRehash(string? hash)
        => hash is not null && !hash.StartsWith(PrefixoAtual, StringComparison.Ordinal);

    /// <summary>
    /// Critica a senha escolhida. Devolve null quando serve, ou a explicação para a
    /// tela — o mesmo texto no cadastro e na troca de senha.
    /// </summary>
    public static string? Criticar(string? senha)
    {
        if (string.IsNullOrWhiteSpace(senha))
            return "Informe a senha.";
        if (senha.Length < TamanhoMinimoSenha)
            return $"A senha precisa de pelo menos {TamanhoMinimoSenha} caracteres.";
        if (senha.Trim().Length != senha.Length)
            return "A senha não pode começar nem terminar com espaço.";
        return null;
    }

    private static byte[] Derivar(string senha, byte[] sal, int iteracoes = Iteracoes)
        => Rfc2898DeriveBytes.Pbkdf2(senha, sal, iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
}
