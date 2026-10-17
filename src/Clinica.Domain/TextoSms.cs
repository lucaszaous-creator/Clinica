using System.Globalization;
using System.Text;

namespace Clinica.Domain;

/// <summary>
/// As regras do TEXTO de um SMS (out/2026). Moram no domínio porque decidem DINHEIRO:
/// cada SMS é cobrado por SEGMENTO, e o tamanho de um segmento depende do alfabeto.
///
/// ⚠️ <b>Um acento só transforma 160 caracteres em 70.</b> O SMS padrão usa o alfabeto
/// GSM-7 (sem "ã", "ç", "é"…); um caractere fora dele obriga a mensagem inteira a ir em
/// UCS-2, onde o segmento cai para 70. "Sessão amanhã às 14h" custaria TRÊS segmentos
/// onde "Sessao amanha as 14h" custa um. Por isso o texto que sai pelo provedor passa por
/// <see cref="ParaGsm"/>: tirar o acento custa estética, deixá-lo custa o triplo, todo dia,
/// em toda sessão da clínica.
/// </summary>
public static class TextoSms
{
    public const int CaracteresPorSegmentoGsm = 160;
    public const int CaracteresPorSegmentoMultiplo = 153;

    /// <summary>
    /// Teto de segmentos de UMA mensagem. Texto de campanha maior que isto é recusado na
    /// tela — mensagem de quatro segmentos é carta, custa quatro vezes, e metade das
    /// operadoras a entrega picada.
    /// </summary>
    public const int SegmentosMaximos = 3;

    // O conjunto básico do GSM 03.38 que importa a um texto em português sem acento.
    private const string Gsm =
        "@£$¥\n\r !\"#%&'()*+,-./0123456789:;<=>?ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// O texto no alfabeto GSM-7: acentos removidos ("ç" vira "c"), aspas tipográficas
    /// viram retas, travessão vira hífen, e o que ainda não couber vira espaço. Nunca
    /// devolve caractere fora do alfabeto — é o que garante o segmento de 160.
    /// </summary>
    public static string ParaGsm(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        var decomposto = texto
            .Replace('“', '"').Replace('”', '"').Replace('‘', '\'').Replace('’', '\'')
            .Replace('—', '-').Replace('–', '-').Replace('…', '.').Replace('·', '-')
            .Replace('º', 'o').Replace('ª', 'a')
            .Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(Gsm.Contains(c) ? c : ' ');
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Quantos segmentos o texto custa, já no alfabeto que vai sair.</summary>
    public static int Segmentos(string? texto)
    {
        var t = ParaGsm(texto);
        if (t.Length == 0) return 0;
        return t.Length <= CaracteresPorSegmentoGsm
            ? 1
            : (int)Math.Ceiling(t.Length / (double)CaracteresPorSegmentoMultiplo);
    }

    /// <summary>
    /// O celular em E.164 (<c>+5511987654321</c>), ou <c>null</c> quando o número da ficha
    /// não é de celular brasileiro.
    ///
    /// ⚠️ <b>Fixo não recebe SMS</b>, e mandar para ele é pagar por mensagem que some: só
    /// passa número de 11 dígitos (DDD + 9 + 8) cujo terceiro dígito é 9 — com ou sem o 55
    /// e o zero de longa distância na frente. O resto devolve nulo, e a rodada CONTA o
    /// paciente como "sem celular" em vez de tentar.
    /// </summary>
    public static string? CelularE164(string? telefone)
    {
        var d = Telefone.Normalizar(telefone);
        if (d.StartsWith('0')) d = d.TrimStart('0');
        if (d.Length == 13 && d.StartsWith("55")) d = d[2..];
        if (d.Length != 11 || d[2] != '9' || d[0] == '0') return null;
        return "+55" + d;
    }
}
