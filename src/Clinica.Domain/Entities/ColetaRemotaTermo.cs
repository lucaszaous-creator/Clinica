namespace Clinica.Domain.Entities;

/// <summary>
/// UM ENVIO do termo para o celular do paciente (o link pelo WhatsApp) — parcela 81.
/// O mapa completo da decisão está em <c>docs/termo-pelo-whatsapp.md</c>.
///
/// Esta linha é a EVIDÊNCIA do canal: para qual telefone foi, quem enviou, quando o
/// paciente respondeu e de onde (IP, aparelho). O traço e as respostas em si não moram
/// aqui — eles entram no documento pelo MESMO <c>ColherAsync</c> da coleta no balcão,
/// porque o selo do termo não muda com o canal.
///
/// ⚠️ A linha NÃO se apaga: coleta cancelada ou vencida fica marcada. "Enviamos um link
/// com o termo para o celular do paciente" é exatamente o tipo de afirmação que uma
/// contestação pede para provar — e o que sai do ar é o OBJETO no balde, nunca o registro.
/// </summary>
public class ColetaRemotaTermo
{
    public int Id { get; set; }

    public int DocumentoClinicoId { get; set; }
    public DocumentoClinico? Documento { get; set; }

    /// <summary>O token da URL — 26 caracteres, 2^127 (<see cref="PublicacaoDocumento"/>).
    /// É a única barreira de acesso ao pedido publicado, como nas receitas.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Para onde o link foi — o telefone DA FICHA no momento do envio, copiado:
    /// a ficha pode mudar amanhã, e a evidência é sobre o envio de hoje.</summary>
    public string TelefoneDestino { get; set; } = string.Empty;

    /// <summary>Quem enviou (o login, nunca o usuário do Windows).</summary>
    public string EnviadaPor { get; set; } = string.Empty;

    public DateTime CriadaEm { get; set; }

    /// <summary>Vence em 24h fixas — o link é para a SALA DE ESPERA, não para a semana.
    /// Quem precisa de mais tempo reenvia, e o reenvio fica registrado.</summary>
    public DateTime ExpiraEm { get; set; }

    /// <summary>Quando o desktop VIU a resposta do celular (não quando o paciente enviou —
    /// essa hora está na evidência, informada pela borda).</summary>
    public DateTime? RespondidaEm { get; set; }

    /// <summary>IP, aparelho e hora informados pela borda ao receber a assinatura.</summary>
    public string? EvidenciaResposta { get; set; }

    /// <summary>A coleta terminou: o traço entrou no documento pelo Confirmar da técnica.</summary>
    public DateTime? ConcluidaEm { get; set; }

    public DateTime? CanceladaEm { get; set; }
    public string? CanceladaPor { get; set; }

    public bool EmAberto => ConcluidaEm is null && CanceladaEm is null;

    public bool Vencida(DateTime agora) => EmAberto && agora > ExpiraEm;

    /// <summary>Onde o PEDIDO (o que o paciente lê) mora no balde. Prefixo `t/` próprio —
    /// as receitas usam `r/`, e o Worker do termo só enxerga o dele.</summary>
    public static string CaminhoPedido(string token) => $"t/{token[..2]}/{token}.json";

    /// <summary>Onde a RESPOSTA (traço + declarações) chega. Write-once: o Worker recusa
    /// segunda gravação — a primeira assinatura é A assinatura.</summary>
    public static string CaminhoResposta(string token) => $"t/{token[..2]}/{token}.resposta.json";

    public static string Url(string baseUrl, string token)
        => $"{baseUrl.TrimEnd('/')}/t/{token}";

    public const int HorasNoAr = 24;
}
