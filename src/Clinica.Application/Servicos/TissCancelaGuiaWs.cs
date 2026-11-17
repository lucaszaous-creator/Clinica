using System.Xml.Linq;

namespace Clinica.Application.Servicos;

/// <summary>Uma guia de faturamento a cancelar na operadora.</summary>
public sealed record GuiaACancelarTiss
{
    /// <summary>O número que a clínica mandou no lote (1 a 20 caracteres).</summary>
    public required string NumeroGuiaPrestador { get; init; }

    /// <summary>O número da guia na operadora, quando ela o devolveu.</summary>
    public string? NumeroGuiaOperadora { get; init; }

    /// <summary>O protocolo em que a guia foi recebida, quando se sabe.</summary>
    public string? NumeroProtocolo { get; init; }
}

/// <summary>
/// O pedido para desfazer o que foi enviado por engano (transação TISS <c>CANCELA_GUIA</c>,
/// serviço <c>tissCancelaGuia</c>): guias avulsas OU um lote inteiro — o padrão não mistura.
/// </summary>
public sealed record PedidoCancelaGuiaTiss
{
    public required RemetenteTissWs Remetente { get; init; }

    /// <summary>Sequencial da transação (1 a 12 caracteres), único por mensagem enviada.</summary>
    public required string Sequencial { get; init; }

    /// <summary>Data e hora do registro da transação (hora local da clínica).</summary>
    public required DateTime Momento { get; init; }

    /// <summary>As guias a cancelar. Vazio quando o pedido é do lote inteiro.</summary>
    public IReadOnlyList<GuiaACancelarTiss> Guias { get; init; } = [];

    /// <summary>O número do lote (1 a 12), para cancelar o lote INTEIRO. <c>null</c> quando o pedido é por guia.</summary>
    public string? NumeroLote { get; init; }

    /// <summary>O protocolo do lote a cancelar; opcional, e só vale com <see cref="NumeroLote"/>.</summary>
    public string? NumeroProtocoloDoLote { get; init; }
}

/// <summary>O que a operadora respondeu sobre UMA guia do pedido de cancelamento.</summary>
public sealed record GuiaCanceladaTiss
{
    public string NumeroGuiaPrestador { get; init; } = string.Empty;

    public string? NumeroGuiaOperadora { get; init; }

    public string? NumeroProtocolo { get; init; }

    /// <summary>Código do <c>dm_statusCancelamento</c> (1 a 6).</summary>
    public string Status { get; init; } = string.Empty;

    public string? StatusDescricao => TissCancelaGuiaWs.DescreverStatus(Status);

    /// <summary>Só o status 1 é cancelamento feito; "em processamento" (4) ainda não é.</summary>
    public bool Cancelada => Status == TissCancelaGuiaWs.StatusCancelado;
}

/// <summary>O recibo da operadora para um pedido de cancelamento.</summary>
public sealed record RespostaCancelaGuiaTiss
{
    /// <summary>
    /// <c>true</c> = TUDO o que se pediu foi cancelado; <c>false</c> = a operadora respondeu e
    /// algo NÃO foi cancelado (não cancelado, inexistente, ou <c>mensagemErro</c>); <c>null</c> =
    /// NÃO SE SABE — ainda em processamento, falha de login, schema, hash, rede ou resposta
    /// ilegível. Guia que não se sabe se foi cancelada continua valendo como enviada.
    /// </summary>
    public bool? Cancelado { get; init; }

    /// <summary>Uma linha por guia do recibo (cancelamento por guia).</summary>
    public IReadOnlyList<GuiaCanceladaTiss> Guias { get; init; } = [];

    /// <summary>Número do lote do recibo (cancelamento do lote inteiro).</summary>
    public string? NumeroLote { get; init; }

    public string? NumeroProtocoloDoLote { get; init; }

    /// <summary>Status do lote inteiro (<c>dm_statusCancelamento</c>), no cancelamento por lote.</summary>
    public string? StatusDoLote { get; init; }

    /// <summary>O motivo da operadora quando ela recusa o pedido.</summary>
    public IReadOnlyList<(string Codigo, string? Descricao)> Motivos { get; init; } = [];

    /// <summary>Falha de transporte do padrão, erro SOAP ou falha de rede.</summary>
    public string? Falha { get; init; }

    /// <summary>Uma frase para a tela ou para o console, sem dado de beneficiário.</summary>
    public string Resumo => Cancelado switch
    {
        true => StatusDoLote is not null
            ? $"Lote {NumeroLote} cancelado."
            : $"{Guias.Count} guia(s) cancelada(s).",
        false => Motivos.Count > 0
            ? "A operadora recusou o cancelamento: " + TissMensagemWs.DescreverMotivos(Motivos)
            : StatusDoLote is not null
                ? $"Lote {NumeroLote} não cancelado: {TissCancelaGuiaWs.DescreverStatus(StatusDoLote) ?? "status " + StatusDoLote}."
                : "Cancelamento incompleto: " + string.Join("; ",
                    Guias.Where(g => !g.Cancelada).Select(g => $"{g.NumeroGuiaPrestador} - {g.StatusDescricao ?? "status " + g.Status}")) + ".",
        null => "Não se sabe se o cancelamento foi feito: "
                + (Falha ?? "a operadora ainda está processando o pedido") + "."
    };
}

/// <summary>
/// Monta e lê as mensagens do web service TISS de cancelamento de guia (padrão ANS 4.01.00):
/// <c>cancelaGuiaWS</c> na ida, <c>reciboCancelaGuiaWS</c> na volta.
/// </summary>
public static class TissCancelaGuiaWs
{
    private static readonly XNamespace Ans = TissMensagemWs.Ans;

    /// <summary><c>dm_tipoGuia</c> 2 = guia de faturamento (1 é solicitação, 3 é recurso de glosa).</summary>
    public const string TipoGuiaFaturamento = "2";

    /// <summary><c>dm_statusCancelamento</c> 1 = cancelado com sucesso.</summary>
    public const string StatusCancelado = "1";

    /// <summary><c>dm_statusCancelamento</c> 4 = em processamento: ainda não é resposta.</summary>
    public const string StatusEmProcessamento = "4";

    /// <summary>
    /// O elemento <c>cancelaGuiaWS</c>, já com o hash. O pedido é por guia OU por lote: os dois
    /// juntos, ou nenhum, são recusados antes do envio.
    /// </summary>
    public static XElement MontarPedido(PedidoCancelaGuiaTiss pedido)
    {
        var porLote = pedido.NumeroLote is not null;
        TissMensagemWs.Exigir(porLote != (pedido.Guias.Count > 0),
            "O cancelamento é por guia OU do lote inteiro: informe as guias ou o número do lote, nunca os dois nem nenhum.");
        TissMensagemWs.Exigir(porLote || pedido.NumeroProtocoloDoLote is null,
            "O protocolo do lote só vale no cancelamento do lote inteiro.");

        XElement tipo;
        if (porLote)
        {
            TissMensagemWs.ExigirTamanho(pedido.NumeroLote, 12, "número do lote");
            TissMensagemWs.ExigirTamanhoOpcional(pedido.NumeroProtocoloDoLote, 12, "protocolo do lote");
            tipo = new XElement(Ans + "tipoCancelamento",
                new XElement(Ans + "tipoCancelamentoLote",
                    new XElement(Ans + "numeroLote", pedido.NumeroLote),
                    Opcional("numeroProtocolo", pedido.NumeroProtocoloDoLote)));
        }
        else
        {
            foreach (var guia in pedido.Guias)
            {
                TissMensagemWs.ExigirTamanho(guia.NumeroGuiaPrestador, 20, "número da guia no prestador");
                TissMensagemWs.ExigirTamanhoOpcional(guia.NumeroGuiaOperadora, 20, "número da guia na operadora");
                TissMensagemWs.ExigirTamanhoOpcional(guia.NumeroProtocolo, 12, "protocolo da guia");
            }
            TissMensagemWs.Exigir(pedido.Guias.Select(g => g.NumeroGuiaPrestador).Distinct().Count() == pedido.Guias.Count,
                "Há guia repetida no pedido de cancelamento.");

            tipo = new XElement(Ans + "tipoCancelamento",
                pedido.Guias.Select(g => new XElement(Ans + "tipoCancelamentoGuia",
                    new XElement(Ans + "tipoGuia", TipoGuiaFaturamento),
                    new XElement(Ans + "numeroGuiaPrestador", g.NumeroGuiaPrestador),
                    Opcional("numeroGuiaOperadora", g.NumeroGuiaOperadora),
                    Opcional("numeroProtocolo", g.NumeroProtocolo))));
        }

        var corpo = new XElement(Ans + "cancelaGuia", TissMensagemWs.DadosPrestador(pedido.Remetente), tipo);

        return TissMensagemWs.Fechar("cancelaGuiaWS",
            TissMensagemWs.Cabecalho("CANCELA_GUIA", pedido.Remetente, pedido.Sequencial, pedido.Momento),
            corpo);
    }

    /// <summary>O envelope SOAP 1.1 pronto para o POST, em ISO-8859-1.</summary>
    public static string MontarEnvelope(PedidoCancelaGuiaTiss pedido) => TissMensagemWs.Envelope(MontarPedido(pedido));

    /// <summary>O envelope com a senha mascarada, para log ou chamado.</summary>
    public static string MontarEnvelopeParaExibir(PedidoCancelaGuiaTiss pedido) => TissMensagemWs.ParaExibir(MontarEnvelope(pedido));

    /// <summary>Lê o que voltou do <see cref="TissWebserviceCliente.EnviarAsync"/>, juntando o status HTTP à falha.</summary>
    public static RespostaCancelaGuiaTiss LerResposta(RespostaBrutaTissWs bruta)
    {
        if (bruta.Corpo is null)
            return new RespostaCancelaGuiaTiss { Falha = bruta.Falha ?? "sem resposta da operadora" };
        var lida = LerResposta(bruta.Corpo);
        return lida with { Falha = TissMensagemWs.FalhaComHttp(lida.Falha, bruta.StatusHttp) };
    }

    /// <summary>
    /// Lê o <c>reciboCancelaGuiaWS</c>: o recibo por guia ou por lote, a recusa ou a falha.
    /// Nunca lança por conteúdo inesperado — devolve <c>Cancelado = null</c> com o motivo.
    /// </summary>
    public static RespostaCancelaGuiaTiss LerResposta(string xmlDaResposta)
    {
        if (TissMensagemWs.Abrir(xmlDaResposta, out var doc) is { } falha)
            return new RespostaCancelaGuiaTiss { Falha = falha };

        // O elemento do web service e o recibo dentro dele têm o MESMO nome no XSD
        // (reciboCancelaGuia dentro de reciboCancelaGuia): parte-se do elemento WS.
        var externo = doc!.Descendants(Ans + "reciboCancelaGuiaWS").FirstOrDefault()?.Element(Ans + "reciboCancelaGuia");
        if (externo is null)
            return new RespostaCancelaGuiaTiss { Falha = "resposta sem <reciboCancelaGuia>" };

        var erro = externo.Element(Ans + "mensagemErro");
        if (erro is not null)
            return new RespostaCancelaGuiaTiss { Cancelado = false, Motivos = [TissMensagemWs.LerMotivo(erro)] };

        var retorno = externo.Element(Ans + "reciboCancelaGuia")?.Element(Ans + "retornoStatus");
        var lote = retorno?.Element(Ans + "loteCancelado");
        if (lote is not null)
        {
            var status = TissMensagemWs.TextoOuNulo(lote.Element(Ans + "statusCancelamento"));
            if (status is null)
                return new RespostaCancelaGuiaTiss { Falha = "recibo do lote sem status de cancelamento" };
            return new RespostaCancelaGuiaTiss
            {
                Cancelado = Concluir([status]),
                NumeroLote = TissMensagemWs.TextoOuNulo(lote.Element(Ans + "numeroLote")),
                // No XSD esta tag é "numeroprotocolo", toda em minúsculas — diferente da do pedido.
                NumeroProtocoloDoLote = TissMensagemWs.TextoOuNulo(lote.Element(Ans + "numeroprotocolo")),
                StatusDoLote = status
            };
        }

        var guias = retorno?.Element(Ans + "guiasCanceladas")?.Elements(Ans + "dadosGuia")
            .Select(g => new GuiaCanceladaTiss
            {
                NumeroGuiaPrestador = TissMensagemWs.TextoOuNulo(g.Element(Ans + "numeroGuiaPrestador")) ?? string.Empty,
                NumeroGuiaOperadora = TissMensagemWs.TextoOuNulo(g.Element(Ans + "numeroGuiaOperadora")),
                NumeroProtocolo = TissMensagemWs.TextoOuNulo(g.Element(Ans + "numeroProtocolo")),
                Status = TissMensagemWs.TextoOuNulo(g.Element(Ans + "statusCancelamento")) ?? string.Empty
            }).ToList();
        if (guias is null || guias.Count == 0)
            return new RespostaCancelaGuiaTiss { Falha = "recibo sem lote e sem guias" };

        return new RespostaCancelaGuiaTiss { Cancelado = Concluir(guias.Select(g => g.Status)), Guias = guias };
    }

    /// <summary>O status do cancelamento por extenso, como o XSD 4.01.00 o comenta em <c>dm_statusCancelamento</c>.</summary>
    public static string? DescreverStatus(string? codigo) => codigo switch
    {
        "1" => "Cancelado com sucesso",
        "2" => "Não cancelado",
        "3" => "Guia inexistente",
        "4" => "Em processamento",
        "5" => "Lote inexistente",
        "6" => "Protocolo inexistente",
        _ => null
    };

    /// <summary>
    /// Tudo cancelado = <c>true</c>; alguma recusa da tabela (2, 3, 5, 6) = <c>false</c>; o resto
    /// — em processamento ou código que o XSD não lista — = <c>null</c>, porque ainda não é resposta.
    /// </summary>
    private static bool? Concluir(IEnumerable<string> status)
    {
        var lista = status.ToList();
        if (lista.All(s => s == StatusCancelado)) return true;
        if (lista.Any(s => s is "2" or "3" or "5" or "6")) return false;
        return null;
    }

    private static XElement? Opcional(string nome, string? valor)
        => valor is null ? null : new XElement(Ans + nome, valor);
}
