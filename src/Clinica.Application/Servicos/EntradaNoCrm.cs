using System.Security.Cryptography;
using System.Text;

namespace Clinica.Application.Servicos;

/// <summary>
/// ENTRAR NO CRM PELA RECEPÇÃO (out/2026, fase 1 da unificação Recepção × CRM —
/// docs/crm-na-recepcao.md). A recepcionista já entrou no app; a aba "WhatsApp" abre o CRM
/// dentro dele sem pedir a senha de novo. O app EMITE um bilhete; a ponte o CONFERE e diz ao
/// CRM quem é a pessoa — o CRM nunca vê senha, e quem pode entrar continua sendo decidido pela
/// ponte (<c>ClinicTeamBridge.PodeEntrar</c>).
///
/// O bilhete é aleatório (32 bytes) e o banco guarda só o SHA-256 dele
/// (<c>BilhetesEntradaCrm</c>): quem só LÊ o banco — a própria ponte, a web de leitura, um backup
/// — não consegue entrar no CRM com o que leu. Ele vale <see cref="Validade"/> e uma vez só.
///
/// ⚠️ O relógio do PC do balcão (que grava o vencimento) e o da VPS da ponte (que confere) podem
/// discordar: a conferência tolera <see cref="Folga"/>, e é o uso único que impede o reuso dentro
/// dela. PC atrasado além da folga só faz a pessoa cair no login normal.
/// </summary>
public static class EntradaNoCrm
{
    public static readonly TimeSpan Validade = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan Folga = TimeSpan.FromMinutes(2);

    /// <summary>Bilhete mais velho que isso (usado ou não) não serve para nada e pode sair da tabela.</summary>
    public static readonly TimeSpan Descartar = TimeSpan.FromDays(1);

    public const string Recusa = "Não foi possível entrar pela Recepção. Entre com seu usuário e senha.";

    /// <summary>Um bilhete novo e o hash que vai ao banco.</summary>
    public static (string Bilhete, string Hash) Novo()
    {
        var bilhete = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (bilhete, Hash(bilhete));
    }

    public static string Hash(string bilhete) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bilhete))).ToLowerInvariant();

    /// <summary>Formato do bilhete: 43 caracteres base64url. O resto nem chega a ir ao banco.</summary>
    public static bool FormatoValido(string? bilhete)
        => bilhete is { Length: 43 } && bilhete.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    /// <summary>A regra de validade, num lugar só: não usado, não vencido (com a folga) e não emitido "no futuro".</summary>
    public static bool Vale(DateTime criadoEm, DateTime venceEm, DateTime? usadoEm, DateTime agora)
        => usadoEm is null && agora <= venceEm + Folga && criadoEm <= agora + Folga;

    /// <summary>O endereço que a aba abre: o CRM no modo Recepção, com o bilhete no fragmento (não vai ao servidor nem ao log).</summary>
    public static string Endereco(string enderecoDoCrm, string bilhete)
        => enderecoDoCrm.TrimEnd('/') + "/?modo=recepcao#entrada=" + Uri.EscapeDataString(bilhete);

    /// <summary>
    /// O endereço do CRM que a Gerência digita: só https, sem caminho, consulta ou fragmento —
    /// é a origem do CRM. Devolve a forma limpa, ou <c>null</c> quando não serve.
    /// </summary>
    public static string? EnderecoValido(string? digitado)
    {
        var t = digitado?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(t) || !Uri.TryCreate(t, UriKind.Absolute, out var u)) return null;
        if (u.Scheme != Uri.UriSchemeHttps || u.PathAndQuery != "/" || u.Fragment.Length > 0 || u.UserInfo.Length > 0) return null;
        return u.GetLeftPart(UriPartial.Authority);
    }
}
