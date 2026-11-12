using System.Xml.Linq;

namespace Clinica.Application.Servicos;

/// <summary>
/// A pergunta "em que pé está o lote?" (transação TISS <c>SOLIC_STATUS_PROTOCOLO</c>, serviço
/// <c>tissSolicitacaoStatusProtocolo</c>): pede-se pelo número do protocolo de recebimento.
/// </summary>
public sealed record PedidoStatusProtocoloTiss
{
    public required RemetenteTissWs Remetente { get; init; }

    /// <summary>Sequencial da transação (1 a 12 caracteres), único por mensagem enviada.</summary>
    public required string Sequencial { get; init; }

    /// <summary>Data e hora do registro da transação (hora local da clínica).</summary>
    public required DateTime Momento { get; init; }

    /// <summary>O protocolo devolvido no envio do lote (1 a 12 caracteres).</summary>
    public required string NumeroProtocolo { get; init; }
}

/// <summary>O que a operadora respondeu sobre a situação de um protocolo.</summary>
public sealed record RespostaStatusProtocoloTiss
{
    /// <summary>
    /// <c>true</c> = a operadora devolveu o detalhe do lote; <c>false</c> = ela respondeu que não
    /// há o que mostrar (<c>mensagemErro</c>, ou situação 7 "Não localizado"); <c>null</c> = NÃO
    /// SE SABE — falha de login, schema, hash, rede ou resposta ilegível.
    /// </summary>
    public bool? Localizado { get; init; }

    /// <summary>Código da situação do protocolo (<c>dm_statusProtocolo</c>, 1 a 9).</summary>
    public string? Situacao { get; init; }

    /// <summary>A situação por extenso, como o XSD a nomeia; <c>null</c> para código que ele não lista.</summary>
    public string? SituacaoDescricao => TissStatusProtocoloWs.DescreverSituacao(Situacao);

    public string? Protocolo { get; init; }

    public string? NumeroLote { get; init; }

    public DateOnly? DataEnvio { get; init; }

    public decimal? ValorProcessado { get; init; }

    public decimal? ValorGlosa { get; init; }

    public decimal? ValorLiberado { get; init; }

    /// <summary>
    /// As guias do lote. O padrão não traz situação POR GUIA: cada uma vem com o número do
    /// prestador, o da operadora (quando ela manda), os valores e as glosas.
    /// </summary>
    public IReadOnlyList<GuiaNoProtocoloTiss> Guias { get; init; } = [];

    /// <summary>O motivo da operadora quando ela responde com erro no lugar do lote.</summary>
    public IReadOnlyList<(string Codigo, string? Descricao)> Motivos { get; init; } = [];

    /// <summary>Falha de transporte do padrão, erro SOAP ou falha de rede.</summary>
    public string? Falha { get; init; }

    /// <summary>Uma frase para a tela ou para o console, sem dado de beneficiário.</summary>
    public string Resumo => Localizado switch
    {
        true => $"Protocolo {Protocolo}: {SituacaoDescricao ?? "situação " + Situacao}"
                + (Guias.Count > 0 ? $", {Guias.Count} guia(s)" : string.Empty)
                + (Guias.Any(g => g.Glosada) ? $", {Guias.Count(g => g.Glosada)} com glosa" : string.Empty)
                + ".",
        false => Motivos.Count > 0
            ? "A operadora não devolveu o lote: " + TissMensagemWs.DescreverMotivos(Motivos)
            : "A operadora não localizou o protocolo.",
        null => "Não se sabe a situação do protocolo: " + (Falha ?? "resposta sem situação reconhecível") + "."
    };
}

/// <summary>
/// Monta e lê as mensagens do web service TISS de situação de protocolo (padrão ANS 4.01.00):
/// <c>solicitacaoStatusProtocoloWS</c> na ida, <c>situacaoProtocoloWS</c> na volta.
/// </summary>
public static class TissStatusProtocoloWs
{
    private static readonly XNamespace Ans = TissMensagemWs.Ans;

    /// <summary>Situação 7 do <c>dm_statusProtocolo</c>: a operadora não achou o protocolo.</summary>
    public const string SituacaoNaoLocalizado = "7";

    /// <summary>O elemento <c>solicitacaoStatusProtocoloWS</c>, já com o hash.</summary>
    public static XElement MontarPedido(PedidoStatusProtocoloTiss pedido)
    {
        TissMensagemWs.ExigirTamanho(pedido.NumeroProtocolo, 12, "número do protocolo");

        var corpo = new XElement(Ans + "solicitacaoStatusProtocolo",
            TissMensagemWs.DadosPrestador(pedido.Remetente),
            new XElement(Ans + "numeroProtocolo", pedido.NumeroProtocolo));

        return TissMensagemWs.Fechar("solicitacaoStatusProtocoloWS",
            TissMensagemWs.Cabecalho("SOLIC_STATUS_PROTOCOLO", pedido.Remetente, pedido.Sequencial, pedido.Momento),
            corpo);
    }

    /// <summary>O envelope SOAP 1.1 pronto para o POST, em ISO-8859-1.</summary>
    public static string MontarEnvelope(PedidoStatusProtocoloTiss pedido) => TissMensagemWs.Envelope(MontarPedido(pedido));

    /// <summary>O envelope com a senha mascarada, para log ou chamado.</summary>
    public static string MontarEnvelopeParaExibir(PedidoStatusProtocoloTiss pedido) => TissMensagemWs.ParaExibir(MontarEnvelope(pedido));

    /// <summary>Lê o que voltou do <see cref="TissWebserviceCliente.EnviarAsync"/>, juntando o status HTTP à falha.</summary>
    public static RespostaStatusProtocoloTiss LerResposta(RespostaBrutaTissWs bruta)
    {
        if (bruta.Corpo is null)
            return new RespostaStatusProtocoloTiss { Falha = bruta.Falha ?? "sem resposta da operadora" };
        var lida = LerResposta(bruta.Corpo);
        return lida with { Falha = TissMensagemWs.FalhaComHttp(lida.Falha, bruta.StatusHttp) };
    }

    /// <summary>
    /// Lê o <c>situacaoProtocoloWS</c>: o detalhe do lote, o erro da operadora ou a falha.
    /// Nunca lança por conteúdo inesperado — devolve <c>Localizado = null</c> com o motivo.
    /// </summary>
    public static RespostaStatusProtocoloTiss LerResposta(string xmlDaResposta)
    {
        if (TissMensagemWs.Abrir(xmlDaResposta, out var doc) is { } falha)
            return new RespostaStatusProtocoloTiss { Falha = falha };

        var situacao = doc!.Descendants(Ans + "situacaoProtocolo").FirstOrDefault();
        if (situacao is null)
            return new RespostaStatusProtocoloTiss { Falha = "resposta sem <situacaoProtocolo>" };

        // O erro pode vir no lugar da situação inteira ou no lugar do detalhe do lote.
        var lote = situacao.Element(Ans + "situacaoDoProtocolo")?.Element(Ans + "lote");
        var erro = situacao.Element(Ans + "mensagemErro") ?? lote?.Element(Ans + "mensagemErro");
        if (erro is not null)
            return new RespostaStatusProtocoloTiss { Localizado = false, Motivos = [TissMensagemWs.LerMotivo(erro)] };

        var detalhe = lote?.Element(Ans + "detalheLote");
        var codigo = TissMensagemWs.TextoOuNulo(detalhe?.Element(Ans + "statusProtocolo"));
        if (detalhe is null || codigo is null)
            return new RespostaStatusProtocoloTiss { Falha = "resposta sem detalhe do lote e sem mensagem de erro" };

        var valores = detalhe.Element(Ans + "valorTotalLote");
        return new RespostaStatusProtocoloTiss
        {
            Localizado = codigo != SituacaoNaoLocalizado,
            Situacao = codigo,
            Protocolo = TissMensagemWs.TextoOuNulo(detalhe.Element(Ans + "numeroProtocolo")),
            NumeroLote = TissMensagemWs.TextoOuNulo(detalhe.Element(Ans + "numeroLote")),
            DataEnvio = TissMensagemWs.LerData(detalhe.Element(Ans + "dataEnvioLote")),
            ValorProcessado = TissMensagemWs.LerValor(valores?.Element(Ans + "valorProcessado")),
            ValorGlosa = TissMensagemWs.LerValor(valores?.Element(Ans + "valorGlosa")),
            ValorLiberado = TissMensagemWs.LerValor(valores?.Element(Ans + "valorLiberado")),
            Guias = detalhe.Element(Ans + "guiasTISS")?.Elements(Ans + "guiasMedicas")
                .Select(g => g.Element(Ans + "guias")).OfType<XElement>()
                .Select(TissMensagemWs.LerGuia).ToList() ?? []
        };
    }

    /// <summary>A situação do protocolo por extenso, como o XSD 4.01.00 a comenta em <c>dm_statusProtocolo</c>.</summary>
    public static string? DescreverSituacao(string? codigo) => codigo switch
    {
        "1" => "Recebido",
        "2" => "Em análise",
        "3" => "Liberado para pagamento",
        "4" => "Encerrado sem pagamento",
        "5" => "Analisado e aguardando liberação para pagamento",
        "6" => "Pagamento efetuado",
        "7" => "Não localizado",
        "8" => "Aguardando informação complementar",
        "9" => "Cancelado",
        _ => null
    };
}
