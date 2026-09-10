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

    /// <summary>
    /// O TRAÇO que o paciente desenhou no celular, GUARDADO aqui assim que o desktop o lê
    /// (set/2026).
    ///
    /// ⚠️ Ele não morava em lugar nenhum: vivia só no balde, e quem o trazia para dentro era
    /// a janela aberta. Quem enviava o link e fechava a janela perdia a assinatura — a
    /// varredura de 24h cancelava a coleta e apagava o objeto, e o paciente tinha assinado
    /// de verdade. Perder assinatura já dada é o pior desfecho possível deste fluxo, porque
    /// nada falha: o termo continua "pendente" como se ninguém tivesse assinado.
    ///
    /// Reusa o <see cref="TracoAssinatura"/> do balcão — mesma tabela, mesma forma (PNG,
    /// largura, altura). Tabela à parte é o que impede a lista de coletas de arrastar
    /// imagens; aqui a coluna é só o ponteiro.
    ///
    /// Nulo enquanto o paciente não respondeu, e nulo nas coletas anteriores a esta versão.
    /// </summary>
    public int? TracoAssinaturaId { get; set; }
    public TracoAssinatura? TracoAssinatura { get; set; }

    /// <summary>
    /// O que o paciente respondeu em cada declaração, como o celular mandou
    /// (<c>{"1":"Sim","2":"Não"}</c>) — guardado junto do traço e pela mesma razão.
    ///
    /// ⚠️ Guardar o traço sem as respostas seria meia recuperação: o selo do termo cobre
    /// o que o paciente VIU e RESPONDEU, e uma conferência que trouxesse a assinatura com
    /// as declarações em branco obrigaria a técnica a responder POR ELE.
    /// </summary>
    public string? RespostasJson { get; set; }

    /// <summary>A coleta terminou: o traço entrou no documento pelo Confirmar da técnica.</summary>
    public DateTime? ConcluidaEm { get; set; }

    public DateTime? CanceladaEm { get; set; }
    public string? CanceladaPor { get; set; }

    public bool EmAberto => ConcluidaEm is null && CanceladaEm is null;

    public bool Vencida(DateTime agora) => EmAberto && agora > ExpiraEm;

    /// <summary>
    /// O paciente JÁ ASSINOU no celular e a assinatura está guardada aqui, esperando que
    /// alguém confira a identidade e conclua.
    ///
    /// É a fila que a parcela 81 não tinha: sem ela, o circuito só fechava se a janela
    /// daquele termo estivesse aberta na hora em que a resposta chegou. É este estado que
    /// vira o selo "Termo assinado — conferir" na lista do dia.
    ///
    /// ⚠️ Coleta neste estado NUNCA é cancelada pela expiração — o que vence é o LINK, e o
    /// link já cumpriu o papel dele. O que sai do ar é o objeto no balde; a assinatura
    /// fica.
    /// </summary>
    public bool AguardaConferencia => EmAberto && RespondidaEm is not null;

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
