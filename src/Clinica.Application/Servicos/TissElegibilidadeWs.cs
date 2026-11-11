using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a clínica informa para perguntar à operadora se uma carteira está ativa
/// (transação TISS <c>VERIFICA_ELEGIBILIDADE</c>, web service <c>tissVerificaElegibilidade</c>).
/// Só leva o que o padrão 4.01.00 pede: o nome do beneficiário NÃO vai (saiu na 4.00.00).
/// </summary>
public sealed record PedidoElegibilidadeTiss
{
    /// <summary>Registro ANS da operadora, 6 dígitos (destino da mensagem).</summary>
    public required string RegistroAnsOperadora { get; init; }

    /// <summary>Código do prestador na operadora (até 14 caracteres). Na Saúde Petrobras é o CNPJ.</summary>
    public required string CodigoPrestadorNaOperadora { get; init; }

    /// <summary>Número da carteira do beneficiário (até 20 caracteres), como a operadora o grava.</summary>
    public required string NumeroCarteira { get; init; }

    /// <summary>Validade da carteira; opcional no padrão.</summary>
    public DateOnly? ValidadeCarteira { get; init; }

    /// <summary>Login do prestador no web service (o mesmo da conectividade da operadora).</summary>
    public required string Login { get; init; }

    /// <summary>
    /// A senha JÁ em MD5 (32 hexadecimais) — use <see cref="TissElegibilidadeWs.SenhaEmMd5"/>.
    /// A senha em claro nunca entra na mensagem, e por isso também não entra neste objeto.
    /// </summary>
    public required string SenhaMd5 { get; init; }

    /// <summary>Sequencial da transação (1 a 12 caracteres), único por mensagem enviada.</summary>
    public required string Sequencial { get; init; }

    /// <summary>Data e hora do registro da transação (hora local da clínica).</summary>
    public required DateTime Momento { get; init; }
}

/// <summary>O que a operadora respondeu a uma verificação de elegibilidade.</summary>
public sealed record RespostaElegibilidadeTiss
{
    /// <summary>
    /// <c>true</c> = elegível; <c>false</c> = a operadora negou; <c>null</c> = não houve resposta
    /// de elegibilidade (falha de login, de schema, de hash, glosa da mensagem ou resposta ilegível).
    /// Três estados de propósito: "não sei" não pode ter a cara de "não elegível".
    /// </summary>
    public bool? Elegivel { get; init; }

    /// <summary>Nome do beneficiário devolvido pela operadora (quando há recibo).</summary>
    public string? NomeBeneficiario { get; init; }

    public string? NomeSocialBeneficiario { get; init; }

    public string? NumeroCarteira { get; init; }

    public DateOnly? ValidadeCarteira { get; init; }

    /// <summary>Motivos da negativa ou da recusa da mensagem: código da tabela de glosas e descrição.</summary>
    public IReadOnlyList<(string Codigo, string? Descricao)> Motivos { get; init; } = [];

    /// <summary>
    /// Falha de transporte do padrão (<c>LoginInvalido</c>, <c>HashInvalido</c>, <c>SchemaInvalido</c>,
    /// <c>VersaoInvalida</c>…) ou o texto do erro SOAP, quando a operadora não chegou a avaliar a carteira.
    /// </summary>
    public string? Falha { get; init; }

    /// <summary>Uma frase para a tela ou para o console, sem dado além do que a operadora devolveu.</summary>
    public string Resumo => Elegivel switch
    {
        true => "Elegível.",
        false => Motivos.Count == 0
            ? "Não elegível (a operadora não informou o motivo)."
            : "Não elegível: " + DescreverMotivos(),
        null => Falha is not null
            ? "A operadora não avaliou a carteira: " + Falha
            : Motivos.Count > 0
                ? "A operadora recusou a mensagem: " + DescreverMotivos()
                : "Resposta sem elegibilidade reconhecível."
    };

    private string DescreverMotivos()
        => string.Join("; ", Motivos.Select(m => string.IsNullOrWhiteSpace(m.Descricao) ? m.Codigo : $"{m.Codigo} - {m.Descricao}"));
}

/// <summary>
/// Monta e lê as mensagens do web service TISS de elegibilidade (padrão ANS 4.01.00).
/// É só texto: não abre conexão, não lê configuração e não guarda nada — quem envia é o
/// <see cref="TissWebserviceCliente"/>. Assim a mensagem inteira se testa contra o XSD oficial
/// sem rede e sem credencial.
/// </summary>
public static class TissElegibilidadeWs
{
    /// <summary>Versão do padrão em que as mensagens deste arquivo são escritas.</summary>
    public const string VersaoPadrao = "4.01.00";

    private static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>
    /// MD5 da senha em hexadecimal minúsculo, como o manual da operadora pede para a tag
    /// <c>senhaPrestador</c>. MD5 aqui é exigência do padrão, não proteção: trate o resultado
    /// com o mesmo cuidado da senha (quem o tem, entra).
    /// </summary>
    public static string SenhaEmMd5(string senhaEmClaro)
        => Convert.ToHexString(MD5.HashData(Latin1.GetBytes(senhaEmClaro))).ToLowerInvariant();

    /// <summary>O elemento <c>pedidoElegibilidadeWS</c>, já com o hash.</summary>
    public static XElement MontarPedido(PedidoElegibilidadeTiss pedido)
    {
        Exigir(pedido.RegistroAnsOperadora.Length == 6 && pedido.RegistroAnsOperadora.All(char.IsAsciiDigit),
            "O registro ANS da operadora tem 6 dígitos.");
        ExigirTamanho(pedido.CodigoPrestadorNaOperadora, 14, "código do prestador na operadora");
        ExigirTamanho(pedido.NumeroCarteira, 20, "número da carteira");
        ExigirTamanho(pedido.Login, 20, "login do prestador");
        ExigirTamanho(pedido.Sequencial, 12, "sequencial da transação");
        Exigir(pedido.SenhaMd5.Length == 32 && pedido.SenhaMd5.All(Uri.IsHexDigit),
            "A senha vai em MD5 (32 hexadecimais): use TissElegibilidadeWs.SenhaEmMd5.");

        var cabecalho = new XElement(Ans + "cabecalho",
            new XElement(Ans + "identificacaoTransacao",
                new XElement(Ans + "tipoTransacao", "VERIFICA_ELEGIBILIDADE"),
                new XElement(Ans + "sequencialTransacao", pedido.Sequencial),
                new XElement(Ans + "dataRegistroTransacao", pedido.Momento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new XElement(Ans + "horaRegistroTransacao", pedido.Momento.ToString("HH:mm:ss", CultureInfo.InvariantCulture))),
            new XElement(Ans + "origem",
                new XElement(Ans + "identificacaoPrestador",
                    new XElement(Ans + "codigoPrestadorNaOperadora", pedido.CodigoPrestadorNaOperadora))),
            new XElement(Ans + "destino",
                new XElement(Ans + "registroANS", pedido.RegistroAnsOperadora)),
            new XElement(Ans + "Padrao", VersaoPadrao),
            new XElement(Ans + "loginSenhaPrestador",
                new XElement(Ans + "loginPrestador", pedido.Login),
                new XElement(Ans + "senhaPrestador", pedido.SenhaMd5)));

        var corpo = new XElement(Ans + "pedidoElegibilidade",
            new XElement(Ans + "dadosPrestador",
                new XElement(Ans + "codigoPrestadorNaOperadora", pedido.CodigoPrestadorNaOperadora)),
            new XElement(Ans + "numeroCarteira", pedido.NumeroCarteira),
            pedido.ValidadeCarteira is { } validade
                ? new XElement(Ans + "validadeCarteira", validade.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                : null);

        var mensagem = new XElement(Ans + "pedidoElegibilidadeWS",
            new XAttribute(XNamespace.Xmlns + "ans", Ans.NamespaceName),
            cabecalho,
            corpo);
        // O hash cobre o conteúdo de todas as tags da mensagem, na ordem do documento — e é
        // calculado ANTES de a própria tag existir, para não entrar na conta.
        mensagem.Add(new XElement(Ans + "hash", CalcularHash(mensagem)));
        return mensagem;
    }

    /// <summary>
    /// O envelope SOAP 1.1 pronto para o POST, declarado em ISO-8859-1 — a codificação do
    /// padrão TISS e a mesma em que o hash é calculado. Quem envia grava os bytes com
    /// <see cref="Latin1"/>, senão a declaração mente sobre o conteúdo.
    /// </summary>
    public static string MontarEnvelope(PedidoElegibilidadeTiss pedido)
    {
        var envelope = new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", Soap.NamespaceName),
            new XElement(Soap + "Header"),
            new XElement(Soap + "Body", MontarPedido(pedido)));
        return "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>" + envelope.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// A mesma mensagem com a senha trocada por asteriscos: é o que pode ir para log, console
    /// ou anexo de chamado. O hash fica como está (ele não revela a senha).
    /// </summary>
    public static string MontarEnvelopeParaExibir(PedidoElegibilidadeTiss pedido)
    {
        var doc = XDocument.Parse(MontarEnvelope(pedido));
        foreach (var senha in doc.Descendants(Ans + "senhaPrestador"))
            senha.Value = new string('*', 32);
        return doc.ToString();
    }

    /// <summary>
    /// Lê o que a operadora devolveu: o recibo de elegibilidade, a glosa da mensagem ou a falha
    /// SOAP. Nunca lança por conteúdo inesperado — devolve <c>Elegivel = null</c> com o motivo.
    /// </summary>
    public static RespostaElegibilidadeTiss LerResposta(string xmlDaResposta)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlDaResposta);
        }
        catch (Exception ex)
        {
            return new RespostaElegibilidadeTiss { Falha = "resposta que não é XML (" + ex.Message + ")" };
        }

        var falhaTiss = doc.Descendants(Ans + "tissFault").FirstOrDefault()?.Value.Trim();
        if (!string.IsNullOrEmpty(falhaTiss))
            return new RespostaElegibilidadeTiss { Falha = falhaTiss };

        var falhaSoap = doc.Descendants(Soap + "Fault").FirstOrDefault();
        if (falhaSoap is not null)
        {
            var texto = falhaSoap.Elements().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value.Trim();
            return new RespostaElegibilidadeTiss { Falha = string.IsNullOrEmpty(texto) ? "erro SOAP sem descrição" : texto };
        }

        var resposta = doc.Descendants(Ans + "respostaElegibilidade").FirstOrDefault();
        if (resposta is null)
            return new RespostaElegibilidadeTiss { Falha = "resposta sem <respostaElegibilidade>" };

        var recibo = resposta.Element(Ans + "reciboElegibilidade");
        if (recibo is null)
        {
            // A operadora glosou a MENSAGEM (carteira inexistente, prestador não reconhecido…):
            // não é "não elegível", é "não avaliado".
            var glosa = resposta.Element(Ans + "codigoGlosa");
            return new RespostaElegibilidadeTiss
            {
                Motivos = glosa is null ? [] : [LerMotivo(glosa)],
                Falha = glosa is null ? "resposta sem recibo e sem glosa" : null
            };
        }

        var situacao = recibo.Element(Ans + "respostaSolicitacao")?.Value.Trim();
        return new RespostaElegibilidadeTiss
        {
            Elegivel = situacao switch { "S" => true, "N" => false, _ => null },
            Falha = situacao is "S" or "N" ? null : "recibo sem respostaSolicitacao S/N",
            NomeBeneficiario = TextoOuNulo(recibo.Element(Ans + "nomeBeneficiario")),
            NomeSocialBeneficiario = TextoOuNulo(recibo.Element(Ans + "nomeSocialBeneficiario")),
            NumeroCarteira = TextoOuNulo(recibo.Element(Ans + "numeroCarteira")),
            ValidadeCarteira = DateOnly.TryParseExact(recibo.Element(Ans + "validadeCarteira")?.Value.Trim(),
                "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var validade) ? validade : null,
            Motivos = recibo.Element(Ans + "motivosNegativa")?.Elements(Ans + "motivoNegativa").Select(LerMotivo).ToList() ?? []
        };
    }

    /// <summary>
    /// MD5 (hexadecimal minúsculo) da concatenação do conteúdo de todas as tags, na ordem do
    /// documento, em ISO-8859-1 — a regra do Componente de Comunicação do padrão TISS.
    /// </summary>
    public static string CalcularHash(XElement mensagem)
    {
        var sb = new StringBuilder();
        foreach (var texto in mensagem.DescendantNodes().OfType<XText>())
            sb.Append(texto.Value);
        return Convert.ToHexString(MD5.HashData(Latin1.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    /// <summary>ISO-8859-1: a codificação das mensagens TISS (e a do hash).</summary>
    public static Encoding Latin1 => Encoding.Latin1;

    private static (string Codigo, string? Descricao) LerMotivo(XElement motivo)
        => (motivo.Element(Ans + "codigoGlosa")?.Value.Trim() ?? string.Empty, TextoOuNulo(motivo.Element(Ans + "descricaoGlosa")));

    private static string? TextoOuNulo(XElement? elemento)
        => string.IsNullOrWhiteSpace(elemento?.Value) ? null : elemento!.Value.Trim();

    private static void ExigirTamanho(string valor, int maximo, string nome)
        => Exigir(!string.IsNullOrWhiteSpace(valor) && valor.Length <= maximo, $"O {nome} é obrigatório e tem até {maximo} caracteres.");

    private static void Exigir(bool condicao, string mensagem)
    {
        if (!condicao) throw new ArgumentException(mensagem);
    }
}
