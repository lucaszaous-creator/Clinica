using System.Xml.Linq;

namespace Clinica.Application.Servicos;

/// <summary>
/// Um lote de guias para enviar à operadora pelo web service (transação TISS
/// <c>ENVIO_LOTE_GUIAS</c>, serviço <c>tissLoteGuias</c>). As guias chegam prontas, como
/// elementos do padrão — a guia de consulta conferida sai de
/// <see cref="TissExportService.GuiaConsultaConferida"/>.
/// </summary>
public sealed record PedidoLoteGuiasTiss
{
    public required RemetenteTissWs Remetente { get; init; }

    /// <summary>Sequencial da transação (1 a 12 caracteres), único por mensagem enviada.</summary>
    public required string Sequencial { get; init; }

    /// <summary>Data e hora do registro da transação (hora local da clínica).</summary>
    public required DateTime Momento { get; init; }

    /// <summary>Número do lote no prestador (1 a 12 caracteres).</summary>
    public required string NumeroLote { get; init; }

    /// <summary>
    /// De 1 a 100 guias, todas do MESMO tipo (o padrão não mistura consulta com SP/SADT num
    /// lote), cada uma com o seu <c>numeroGuiaPrestador</c>, sem repetição.
    /// </summary>
    public required IReadOnlyList<XElement> Guias { get; init; }
}

/// <summary>O que a operadora respondeu ao envio de um lote.</summary>
public sealed record RespostaLoteGuiasTiss
{
    /// <summary>
    /// <c>true</c> = a operadora devolveu o protocolo de recebimento; <c>false</c> = ela respondeu
    /// com <c>mensagemErro</c> no lugar do protocolo (lote recusado, com o motivo em
    /// <see cref="GlosasDoLote"/>); <c>null</c> = NÃO SE SABE — falha de login, de schema, de
    /// hash, rede ou resposta ilegível. Três estados de propósito: "não sei" não pode ter a
    /// cara de "recusado", e um lote sem resposta pode ter chegado.
    /// </summary>
    public bool? Recebido { get; init; }

    /// <summary>Número do protocolo de recebimento — a chave da consulta de situação.</summary>
    public string? Protocolo { get; init; }

    public string? NumeroLote { get; init; }

    public DateOnly? DataEnvio { get; init; }

    public decimal? ValorTotal { get; init; }

    /// <summary>Valor glosado do protocolo inteiro, quando a operadora já glosa no recebimento.</summary>
    public decimal? ValorGlosado { get; init; }

    /// <summary>As guias como a operadora as devolveu, com o número dela quando vem.</summary>
    public IReadOnlyList<GuiaNoProtocoloTiss> Guias { get; init; } = [];

    /// <summary>Glosa do lote: a do protocolo recebido ou o motivo da recusa.</summary>
    public IReadOnlyList<(string Codigo, string? Descricao)> GlosasDoLote { get; init; } = [];

    /// <summary>
    /// Falha de transporte do padrão (<c>LoginInvalido</c>, <c>HashInvalido</c>,
    /// <c>SchemaInvalido</c>, <c>VersaoInvalida</c>…), o texto do erro SOAP ou a falha de rede.
    /// </summary>
    public string? Falha { get; init; }

    /// <summary>Uma frase para a tela ou para o console, sem dado de beneficiário.</summary>
    public string Resumo => Recebido switch
    {
        true => $"Lote recebido: protocolo {Protocolo}"
                + (Guias.Count > 0 ? $", {Guias.Count} guia(s)" : string.Empty)
                + (Guias.Any(g => g.Glosada) ? $", {Guias.Count(g => g.Glosada)} com glosa" : string.Empty)
                + (GlosasDoLote.Count > 0 ? "; glosa do lote: " + TissMensagemWs.DescreverMotivos(GlosasDoLote) : string.Empty)
                + ".",
        false => GlosasDoLote.Count == 0
            ? "A operadora recusou o lote (sem informar o motivo)."
            : "A operadora recusou o lote: " + TissMensagemWs.DescreverMotivos(GlosasDoLote),
        null => "Não se sabe se o lote foi recebido: " + (Falha ?? "resposta sem protocolo reconhecível")
                + ". Confira com a operadora antes de reenviar."
    };
}

/// <summary>
/// Monta e lê as mensagens do web service TISS de envio de lote de guias (padrão ANS 4.01.00):
/// <c>loteGuiasWS</c> na ida, <c>protocoloRecebimentoWS</c> na volta. É só texto — quem envia é
/// o <see cref="TissWebserviceCliente"/>.
/// </summary>
public static class TissLoteGuiasWs
{
    /// <summary>O máximo de guias por lote no XSD (<c>maxOccurs="100"</c> em <c>ctm_guiaLote</c>).</summary>
    public const int MaximoDeGuias = 100;

    private static readonly XNamespace Ans = TissMensagemWs.Ans;

    /// <summary>Os cinco tipos de guia que um lote aceita — um só por lote.</summary>
    private static readonly string[] TiposDeGuia = ["guiaSP-SADT", "guiaResumoInternacao", "guiaHonorarios", "guiaConsulta", "guiaOdonto"];

    /// <summary>
    /// O elemento <c>loteGuiasWS</c>, já com o hash. Recusa ANTES do envio o lote que o padrão
    /// recusaria: vazio, com mais de 100 guias, com tipos misturados ou com número de guia repetido.
    /// </summary>
    public static XElement MontarPedido(PedidoLoteGuiasTiss pedido)
    {
        TissMensagemWs.ExigirTamanho(pedido.NumeroLote, 12, "número do lote");
        TissMensagemWs.Exigir(pedido.Guias.Count >= 1, "O lote precisa de pelo menos uma guia.");
        TissMensagemWs.Exigir(pedido.Guias.Count <= MaximoDeGuias,
            $"O lote leva no máximo {MaximoDeGuias} guias (vieram {pedido.Guias.Count}): divida em mais de um lote.");

        var tipos = pedido.Guias.Select(g => g.Name).Distinct().ToList();
        TissMensagemWs.Exigir(tipos.All(t => t.Namespace == Ans && TiposDeGuia.Contains(t.LocalName)),
            "O lote só leva guias do padrão TISS (guiaConsulta, guiaSP-SADT, guiaHonorarios, guiaResumoInternacao ou guiaOdonto).");
        TissMensagemWs.Exigir(tipos.Count == 1,
            "O lote leva um só tipo de guia: separe as de consulta das de SP/SADT em lotes diferentes.");

        var numeros = pedido.Guias.Select(NumeroGuiaPrestadorDa).ToList();
        TissMensagemWs.Exigir(numeros.All(n => !string.IsNullOrWhiteSpace(n) && n.Length <= 20),
            "Toda guia do lote precisa do numeroGuiaPrestador, de 1 a 20 caracteres.");
        TissMensagemWs.Exigir(numeros.Distinct().Count() == numeros.Count,
            "Há número de guia repetido no lote: cada guia tem o seu numeroGuiaPrestador.");

        var corpo = new XElement(Ans + "loteGuias",
            new XElement(Ans + "numeroLote", pedido.NumeroLote),
            // Cópias: o XElement de quem chamou continua dele, fora deste documento.
            new XElement(Ans + "guiasTISS", pedido.Guias.Select(g => new XElement(g))));

        return TissMensagemWs.Fechar("loteGuiasWS",
            TissMensagemWs.Cabecalho("ENVIO_LOTE_GUIAS", pedido.Remetente, pedido.Sequencial, pedido.Momento),
            corpo);
    }

    /// <summary>O envelope SOAP 1.1 pronto para o POST, em ISO-8859-1.</summary>
    public static string MontarEnvelope(PedidoLoteGuiasTiss pedido) => TissMensagemWs.Envelope(MontarPedido(pedido));

    /// <summary>O envelope com a senha mascarada, para log ou chamado (ainda leva as carteiras).</summary>
    public static string MontarEnvelopeParaExibir(PedidoLoteGuiasTiss pedido) => TissMensagemWs.ParaExibir(MontarEnvelope(pedido));

    /// <summary>Lê o que voltou do <see cref="TissWebserviceCliente.EnviarAsync"/>, juntando o status HTTP à falha.</summary>
    public static RespostaLoteGuiasTiss LerResposta(RespostaBrutaTissWs bruta)
    {
        if (bruta.Corpo is null)
            return new RespostaLoteGuiasTiss { Falha = bruta.Falha ?? "sem resposta da operadora" };
        var lida = LerResposta(bruta.Corpo);
        return lida with { Falha = TissMensagemWs.FalhaComHttp(lida.Falha, bruta.StatusHttp) };
    }

    /// <summary>
    /// Lê o <c>protocoloRecebimentoWS</c>: o protocolo com as guias, a recusa do lote ou a falha.
    /// Nunca lança por conteúdo inesperado — devolve <c>Recebido = null</c> com o motivo.
    /// </summary>
    public static RespostaLoteGuiasTiss LerResposta(string xmlDaResposta)
    {
        if (TissMensagemWs.Abrir(xmlDaResposta, out var doc) is { } falha)
            return new RespostaLoteGuiasTiss { Falha = falha };

        var recebimento = doc!.Descendants(Ans + "recebimentoLote").FirstOrDefault();
        if (recebimento is null)
            return new RespostaLoteGuiasTiss { Falha = "resposta sem <recebimentoLote>" };

        var protocolo = recebimento.Element(Ans + "protocoloRecebimento");
        if (protocolo is null)
        {
            // A escolha do XSD é protocolo OU mensagemErro: com o erro, a operadora respondeu
            // que não recebeu — isso é recusa. Sem nenhum dos dois, não se sabe.
            var erro = recebimento.Element(Ans + "mensagemErro");
            return erro is null
                ? new RespostaLoteGuiasTiss { Falha = "resposta sem protocolo e sem mensagem de erro" }
                : new RespostaLoteGuiasTiss { Recebido = false, GlosasDoLote = [TissMensagemWs.LerMotivo(erro)] };
        }

        var detalhe = protocolo.Element(Ans + "detalheProtocolo");
        var numeroProtocolo = TissMensagemWs.TextoOuNulo(detalhe?.Element(Ans + "numeroProtocolo"));
        if (numeroProtocolo is null)
            return new RespostaLoteGuiasTiss { Falha = "protocolo de recebimento sem número" };

        var glosa = detalhe!.Element(Ans + "glosaProtocolo");
        return new RespostaLoteGuiasTiss
        {
            Recebido = true,
            Protocolo = numeroProtocolo,
            NumeroLote = TissMensagemWs.TextoOuNulo(protocolo.Element(Ans + "numeroLote")),
            DataEnvio = TissMensagemWs.LerData(protocolo.Element(Ans + "dataEnvioLote")),
            ValorTotal = TissMensagemWs.LerValor(detalhe.Element(Ans + "valorTotalProtocolo")),
            ValorGlosado = TissMensagemWs.LerValor(glosa?.Element(Ans + "vlGlosaProtocolo")),
            GlosasDoLote = TissMensagemWs.LerMotivos(glosa?.Element(Ans + "motivosGlosa")),
            Guias = detalhe.Element(Ans + "dadosGuiasProtocolo")?.Elements(Ans + "dadosGuias")
                .Select(TissMensagemWs.LerGuia).ToList() ?? []
        };
    }

    /// <summary>
    /// O <c>numeroGuiaPrestador</c> de uma guia do padrão, esteja ele no cabeçalho
    /// (<c>cabecalhoConsulta</c>, <c>cabecalhoGuia</c>) ou onde o tipo da guia o puser.
    /// </summary>
    public static string? NumeroGuiaPrestadorDa(XElement guia)
        => TissMensagemWs.TextoOuNulo(guia.Descendants(Ans + "numeroGuiaPrestador").FirstOrDefault());
}
