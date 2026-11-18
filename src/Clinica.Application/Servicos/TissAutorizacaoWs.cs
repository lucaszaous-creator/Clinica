using System.Globalization;
using System.Xml.Linq;

namespace Clinica.Application.Servicos;

/// <summary>
/// O pedido de AUTORIZAÇÃO de um atendimento (SP/SADT): o que a clínica pede e para quem. A carteira,
/// o procedimento e o profissional solicitante (com o CBO — o Manual do Credenciado da Amil o exige
/// na autorização de consulta). O executante é a clínica (código na operadora e CNES).
/// </summary>
public sealed record PedidoAutorizacaoTiss
{
    public required RemetenteTissWs Remetente { get; init; }
    public required string Sequencial { get; init; }
    public required DateTime Momento { get; init; }

    /// <summary>O número da guia de solicitação no prestador (único por pedido).</summary>
    public required string NumeroGuiaPrestador { get; init; }
    public required string NumeroCarteira { get; init; }
    public required string NomeContratado { get; init; }
    public required string Cnes { get; init; }
    public string? NomeProfissional { get; init; }
    public required string ConselhoProfissional { get; init; }
    public required string NumeroConselho { get; init; }
    public required string UfConselho { get; init; }
    public required string Cbos { get; init; }
    public string CaraterAtendimento { get; init; } = "1";
    public required DateOnly DataSolicitacao { get; init; }
    public string CodigoTabela { get; init; } = "22";
    public required string CodigoProcedimento { get; init; }
    public required string DescricaoProcedimento { get; init; }
    public int Quantidade { get; init; } = 1;
}

/// <summary>A pergunta da situação de um pedido de autorização: pelo número da guia de solicitação.</summary>
public sealed record PedidoStatusAutorizacaoTiss
{
    public required RemetenteTissWs Remetente { get; init; }
    public required string Sequencial { get; init; }
    public required DateTime Momento { get; init; }
    public required string NumeroGuiaPrestador { get; init; }
    public required string NumeroCarteira { get; init; }
}

/// <summary>O que a operadora respondeu ao pedido (ou à pergunta da situação) de autorização.</summary>
public sealed record RespostaAutorizacaoTiss
{
    /// <summary><c>dm_statusSolicitacao</c>: 1 autorizado, 2 em análise, 3 negado, 4/5 aguardando, 6 cancelada, 7 parcial. Nulo = não se sabe.</summary>
    public string? Status { get; init; }
    public string? NumeroGuiaPrestador { get; init; }
    public string? NumeroGuiaOperadora { get; init; }
    public string? Senha { get; init; }
    public DateOnly? DataAutorizacao { get; init; }
    public DateOnly? ValidadeSenha { get; init; }
    public IReadOnlyList<(string Codigo, string? Descricao)> Motivos { get; init; } = [];

    /// <summary>A operadora recusou a MENSAGEM (<c>mensagemErro</c>): ela respondeu, e respondeu que não.</summary>
    public bool Recusada { get; init; }

    /// <summary>Falha de transporte, erro SOAP ou resposta ilegível: não se sabe.</summary>
    public string? Falha { get; init; }

    public bool Autorizada => Status is "1" or "7";
    public bool EmAnalise => Status is "2" or "4" or "5";
    public bool Negada => Status == "3" || Recusada;
    public bool Cancelada => Status == "6";

    public string Resumo
        // A senha fica só na autorização (e na guia): a frase vai para a fila e para a trilha.
        => Autorizada ? $"Autorizado{(Status == "7" ? " parcialmente" : "")} pela operadora"
                        + (string.IsNullOrWhiteSpace(Senha) ? ", mas sem senha na resposta" : "")
                        + (ValidadeSenha is { } v ? $"; senha válida até {v:dd/MM/yyyy}" : "") + "."
           : EmAnalise ? $"Em análise na operadora ({TissAutorizacaoWs.DescreverStatus(Status)})."
           : Negada ? "Negado pela operadora" + (Motivos.Count > 0 ? ": " + TissMensagemWs.DescreverMotivos(Motivos) : ".")
           : Cancelada ? "A operadora informa a solicitação como cancelada."
           : "Não se sabe a resposta da autorização: " + (Falha ?? "resposta sem situação reconhecível") + ".";
}

/// <summary>
/// Monta e lê as mensagens do web service TISS de AUTORIZAÇÃO (padrão 4.03.00, nov/2026, Amil):
/// <c>solicitacaoProcedimentoWS</c> → <c>autorizacaoProcedimentoWS</c> e
/// <c>solicitacaoStatusAutorizacaoWS</c> → <c>situacaoAutorizacaoWS</c>. As duas respostas são o mesmo
/// <c>ct_situacaoAutorizacao</c>: o erro da mensagem ou a autorização dos serviços com o status.
/// </summary>
public static class TissAutorizacaoWs
{
    private static readonly XNamespace Ans = TissMensagemWs.Ans;

    /// <summary>"1 - Solicitação de autorização prévia" (<c>dm_etapasAutorizacao</c>).</summary>
    public const string EtapaAutorizacaoPrevia = "1";

    /// <summary>O elemento <c>solicitacaoProcedimentoWS</c>, já com o hash.</summary>
    public static XElement MontarPedido(PedidoAutorizacaoTiss p)
    {
        TissMensagemWs.ExigirTamanho(p.NumeroGuiaPrestador, 20, "número da guia de solicitação");
        TissMensagemWs.ExigirTamanho(p.NumeroCarteira, 20, "número da carteira");
        TissMensagemWs.ExigirTamanho(p.NomeContratado, 70, "nome do contratado");
        TissMensagemWs.ExigirTamanho(p.Cnes, 7, "CNES");
        TissMensagemWs.ExigirTamanho(p.ConselhoProfissional, 2, "conselho profissional");
        TissMensagemWs.ExigirTamanho(p.NumeroConselho, 15, "número no conselho");
        TissMensagemWs.ExigirTamanho(p.UfConselho, 2, "UF do conselho");
        TissMensagemWs.ExigirTamanho(p.Cbos, 6, "CBO do profissional (obrigatório na autorização)");
        TissMensagemWs.ExigirTamanho(p.CodigoProcedimento, 10, "código do procedimento");
        TissMensagemWs.ExigirTamanho(p.DescricaoProcedimento, 150, "descrição do procedimento");
        TissMensagemWs.Exigir(p.Quantidade is >= 1 and <= 999, "A quantidade solicitada vai de 1 a 999.");

        var guia = new XElement(Ans + "solicitacaoSP-SADT",
            new XElement(Ans + "cabecalhoSolicitacao",
                new XElement(Ans + "registroANS", p.Remetente.RegistroAnsOperadora),
                new XElement(Ans + "numeroGuiaPrestador", p.NumeroGuiaPrestador)),
            new XElement(Ans + "tipoEtapaAutorizacao", EtapaAutorizacaoPrevia),
            new XElement(Ans + "dadosBeneficiario",
                new XElement(Ans + "numeroCarteira", p.NumeroCarteira),
                new XElement(Ans + "atendimentoRN", "N")),
            new XElement(Ans + "dadosSolicitante",
                new XElement(Ans + "contratadoSolicitante",
                    new XElement(Ans + "codigoPrestadorNaOperadora", p.Remetente.CodigoPrestadorNaOperadora)),
                new XElement(Ans + "nomeContratadoSolicitante", p.NomeContratado),
                new XElement(Ans + "profissionalSolicitante",
                    string.IsNullOrWhiteSpace(p.NomeProfissional) ? null : new XElement(Ans + "nomeProfissional", Cortar(p.NomeProfissional, 70)),
                    new XElement(Ans + "conselhoProfissional", p.ConselhoProfissional),
                    new XElement(Ans + "numeroConselhoProfissional", p.NumeroConselho),
                    new XElement(Ans + "UF", p.UfConselho),
                    new XElement(Ans + "CBOS", p.Cbos))),
            new XElement(Ans + "caraterAtendimento", p.CaraterAtendimento),
            new XElement(Ans + "dataSolicitacao", p.DataSolicitacao.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Ans + "procedimentosSolicitados",
                new XElement(Ans + "procedimento",
                    new XElement(Ans + "codigoTabela", p.CodigoTabela),
                    new XElement(Ans + "codigoProcedimento", p.CodigoProcedimento),
                    new XElement(Ans + "descricaoProcedimento", p.DescricaoProcedimento)),
                new XElement(Ans + "quantidadeSolicitada", p.Quantidade.ToString(CultureInfo.InvariantCulture))),
            new XElement(Ans + "dadosExecutante",
                new XElement(Ans + "codigonaOperadora", p.Remetente.CodigoPrestadorNaOperadora),
                new XElement(Ans + "CNES", p.Cnes)));

        return TissMensagemWs.Fechar("solicitacaoProcedimentoWS",
            TissMensagemWs.Cabecalho("SOLICITACAO_PROCEDIMENTOS", p.Remetente, p.Sequencial, p.Momento),
            new XElement(Ans + "solicitacaoProcedimento", guia));
    }

    public static string MontarEnvelope(PedidoAutorizacaoTiss pedido) => TissMensagemWs.Envelope(MontarPedido(pedido));

    /// <summary>O elemento <c>solicitacaoStatusAutorizacaoWS</c>, já com o hash.</summary>
    public static XElement MontarPedidoStatus(PedidoStatusAutorizacaoTiss p)
    {
        TissMensagemWs.ExigirTamanho(p.NumeroGuiaPrestador, 20, "número da guia de solicitação");
        TissMensagemWs.ExigirTamanho(p.NumeroCarteira, 20, "número da carteira");
        var corpo = new XElement(Ans + "solicitacaoStatusAutorizacao",
            new XElement(Ans + "identificacaoSolicitacao",
                new XElement(Ans + "registroANS", p.Remetente.RegistroAnsOperadora),
                new XElement(Ans + "numeroGuiaPrestador", p.NumeroGuiaPrestador)),
            new XElement(Ans + "dadosBeneficiario",
                new XElement(Ans + "numeroCarteira", p.NumeroCarteira),
                new XElement(Ans + "atendimentoRN", "N")),
            new XElement(Ans + "dadosContratado",
                new XElement(Ans + "codigoPrestadorNaOperadora", p.Remetente.CodigoPrestadorNaOperadora)));
        return TissMensagemWs.Fechar("solicitacaoStatusAutorizacaoWS",
            TissMensagemWs.Cabecalho("SOLICITA_STATUS_AUTORIZACAO", p.Remetente, p.Sequencial, p.Momento),
            corpo);
    }

    public static string MontarEnvelopeStatus(PedidoStatusAutorizacaoTiss pedido) => TissMensagemWs.Envelope(MontarPedidoStatus(pedido));

    /// <summary>Lê o que voltou do <see cref="TissWebserviceCliente.EnviarAsync"/>, juntando o status HTTP à falha.</summary>
    public static RespostaAutorizacaoTiss LerResposta(RespostaBrutaTissWs bruta)
    {
        if (bruta.Corpo is null)
            return new RespostaAutorizacaoTiss { Falha = bruta.Falha ?? "sem resposta da operadora" };
        var lida = LerResposta(bruta.Corpo);
        return lida with { Falha = TissMensagemWs.FalhaComHttp(lida.Falha, bruta.StatusHttp) };
    }

    /// <summary>
    /// Lê o <c>autorizacaoProcedimentoWS</c> ou o <c>situacaoAutorizacaoWS</c>. Nunca lança por conteúdo
    /// inesperado: devolve <c>Status = null</c> com a falha — "não se sabe" nunca tem a cara de "negado".
    /// </summary>
    public static RespostaAutorizacaoTiss LerResposta(string xmlDaResposta)
    {
        if (TissMensagemWs.Abrir(xmlDaResposta, out var doc) is { } falha)
            return new RespostaAutorizacaoTiss { Falha = falha };

        var situacao = doc!.Descendants(Ans + "autorizacaoProcedimento").FirstOrDefault()
                       ?? doc.Descendants(Ans + "situacaoAutorizacao").FirstOrDefault();
        if (situacao is null)
            return new RespostaAutorizacaoTiss { Falha = "resposta sem situação da autorização" };

        if (situacao.Element(Ans + "mensagemErro") is { } erro)
            return new RespostaAutorizacaoTiss { Recusada = true, Motivos = [TissMensagemWs.LerMotivo(erro)] };

        var servico = situacao.Element(Ans + "autorizacaoServico");
        var status = TissMensagemWs.TextoOuNulo(servico?.Element(Ans + "statusSolicitacao"));
        if (servico is null || status is null)
            return new RespostaAutorizacaoTiss { Falha = "resposta sem a autorização do serviço e sem mensagem de erro" };

        var dados = servico.Element(Ans + "dadosAutorizacao");
        return new RespostaAutorizacaoTiss
        {
            Status = status,
            NumeroGuiaPrestador = TissMensagemWs.TextoOuNulo(dados?.Element(Ans + "numeroGuiaPrestador")),
            NumeroGuiaOperadora = TissMensagemWs.TextoOuNulo(dados?.Element(Ans + "numeroGuiaOperadora")),
            Senha = TissMensagemWs.TextoOuNulo(dados?.Element(Ans + "senha")),
            DataAutorizacao = TissMensagemWs.LerData(dados?.Element(Ans + "dataAutorizacao")),
            ValidadeSenha = TissMensagemWs.LerData(dados?.Element(Ans + "dataValidadeSenha")),
            Motivos = servico.Element(Ans + "motivosNegativa")?.Elements(Ans + "codigoGlosa")
                .Select(c => (c.Value.Trim(), (string?)null)).ToList() ?? []
        };
    }

    /// <summary>O status da solicitação por extenso (<c>dm_statusSolicitacao</c>, XSD 4.03.00).</summary>
    public static string? DescreverStatus(string? codigo) => codigo switch
    {
        "1" => "autorizado", "2" => "em análise", "3" => "negado", "4" => "aguardando justificativa técnica do solicitante",
        "5" => "aguardando documentação do prestador", "6" => "solicitação cancelada", "7" => "autorizado parcialmente",
        _ => null
    };

    private static string Cortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
