using System.Globalization;
using System.Xml.Linq;

namespace Clinica.Application.Servicos;

/// <summary>
/// Quem assina as mensagens do web service TISS: a clínica (código na operadora, login e senha
/// em MD5) e a operadora de destino. É o mesmo em toda transação — lote, situação do protocolo e
/// cancelamento —, por isso mora num objeto só, montado uma vez a partir da configuração.
/// </summary>
public sealed record RemetenteTissWs
{
    /// <summary>Registro ANS da operadora, 6 dígitos (destino da mensagem).</summary>
    public required string RegistroAnsOperadora { get; init; }

    /// <summary>Código do prestador na operadora (até 14 caracteres). Na Saúde Petrobras é o CNPJ.</summary>
    public required string CodigoPrestadorNaOperadora { get; init; }

    /// <summary>Login do prestador no web service (o mesmo da conectividade da operadora).</summary>
    public required string Login { get; init; }

    /// <summary>
    /// A senha JÁ em MD5 (32 hexadecimais) — use <see cref="TissElegibilidadeWs.SenhaEmMd5"/>.
    /// A senha em claro nunca entra na mensagem, e por isso também não entra neste objeto.
    /// </summary>
    public required string SenhaMd5 { get; init; }
}

/// <summary>
/// O que voltou do POST, antes de qualquer leitura: o corpo (quando houve), o status HTTP e a
/// falha de transporte (rede fora do ar, resposta vazia). Quem lê é a classe da transação.
/// </summary>
public sealed record RespostaBrutaTissWs(string? Corpo, int? StatusHttp, string? Falha);

/// <summary>
/// Uma guia como a operadora a devolve no protocolo de recebimento e na situação do protocolo
/// (<c>ct_guiaDados</c> do XSD 4.01.00). O padrão NÃO traz situação por guia: o que há é o valor
/// processado/glosado/liberado e os motivos de glosa. A carteira do beneficiário vem na resposta
/// e é deixada de fora de propósito: quem acha a guia é o número do prestador.
/// </summary>
public sealed record GuiaNoProtocoloTiss
{
    /// <summary>O número que a clínica mandou — a chave garantida para achar a guia de volta.</summary>
    public string NumeroGuiaPrestador { get; init; } = string.Empty;

    /// <summary>O número da guia na operadora. Opcional no padrão: <c>null</c> quando ela não manda.</summary>
    public string? NumeroGuiaOperadora { get; init; }

    public DateOnly? DataRealizacao { get; init; }

    public decimal? ValorProcessado { get; init; }

    public decimal? ValorGlosa { get; init; }

    public decimal? ValorLiberado { get; init; }

    /// <summary>Glosas da guia inteira (<c>glosaGuia</c>).</summary>
    public IReadOnlyList<(string Codigo, string? Descricao)> Glosas { get; init; } = [];

    /// <summary>Glosas dos procedimentos da guia (<c>glosasProcedimento</c>), todas juntas.</summary>
    public IReadOnlyList<(string Codigo, string? Descricao)> GlosasDeProcedimento { get; init; } = [];

    /// <summary>Há glosa na guia ou em algum procedimento dela.</summary>
    public bool Glosada => Glosas.Count > 0 || GlosasDeProcedimento.Count > 0;
}

/// <summary>
/// O que as mensagens do web service TISS 4.01.00 têm em comum: o cabeçalho com login, o hash
/// no fim, o envelope SOAP em ISO-8859-1, a máscara da senha para exibir e a leitura das falhas
/// (tissFault, erro SOAP, resposta que não é XML). É só texto, como o
/// <see cref="TissElegibilidadeWs"/>: não abre conexão e não guarda nada.
/// </summary>
public static class TissMensagemWs
{
    /// <summary>Versão do padrão em que as mensagens são escritas.</summary>
    public const string VersaoPadrao = TissElegibilidadeWs.VersaoPadrao;

    public static readonly XNamespace Ans = "http://www.ans.gov.br/padroes/tiss/schemas";
    public static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>
    /// O <c>cabecalho</c> de uma mensagem do prestador para a operadora, na ordem do XSD
    /// (<c>cabecalhoTransacao</c>), com <c>loginSenhaPrestador</c>.
    /// </summary>
    public static XElement Cabecalho(string tipoTransacao, RemetenteTissWs remetente, string sequencial, DateTime momento)
    {
        Exigir(remetente.RegistroAnsOperadora.Length == 6 && remetente.RegistroAnsOperadora.All(char.IsAsciiDigit),
            "O registro ANS da operadora tem 6 dígitos.");
        ExigirTamanho(remetente.CodigoPrestadorNaOperadora, 14, "código do prestador na operadora");
        ExigirTamanho(remetente.Login, 20, "login do prestador");
        ExigirTamanho(sequencial, 12, "sequencial da transação");
        Exigir(remetente.SenhaMd5.Length == 32 && remetente.SenhaMd5.All(Uri.IsHexDigit),
            "A senha vai em MD5 (32 hexadecimais): use TissElegibilidadeWs.SenhaEmMd5.");

        return new XElement(Ans + "cabecalho",
            new XElement(Ans + "identificacaoTransacao",
                new XElement(Ans + "tipoTransacao", tipoTransacao),
                new XElement(Ans + "sequencialTransacao", sequencial),
                new XElement(Ans + "dataRegistroTransacao", momento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new XElement(Ans + "horaRegistroTransacao", momento.ToString("HH:mm:ss", CultureInfo.InvariantCulture))),
            new XElement(Ans + "origem",
                new XElement(Ans + "identificacaoPrestador",
                    new XElement(Ans + "codigoPrestadorNaOperadora", remetente.CodigoPrestadorNaOperadora))),
            new XElement(Ans + "destino",
                new XElement(Ans + "registroANS", remetente.RegistroAnsOperadora)),
            new XElement(Ans + "Padrao", VersaoPadrao),
            new XElement(Ans + "loginSenhaPrestador",
                new XElement(Ans + "loginPrestador", remetente.Login),
                new XElement(Ans + "senhaPrestador", remetente.SenhaMd5)));
    }

    /// <summary>O bloco <c>dadosPrestador</c> (<c>ct_contratadoDados</c>): UMA identificação só.</summary>
    public static XElement DadosPrestador(RemetenteTissWs remetente)
        => new(Ans + "dadosPrestador",
            new XElement(Ans + "codigoPrestadorNaOperadora", remetente.CodigoPrestadorNaOperadora));

    /// <summary>
    /// O elemento do web service (<c>loteGuiasWS</c>, <c>cancelaGuiaWS</c>…): cabeçalho, corpo e o
    /// hash por último. O hash cobre o conteúdo de todas as tags, na ordem do documento, e é
    /// calculado ANTES de a própria tag existir, para não entrar na conta.
    /// </summary>
    public static XElement Fechar(string elementoWs, XElement cabecalho, XElement corpo)
    {
        var mensagem = new XElement(Ans + elementoWs,
            new XAttribute(XNamespace.Xmlns + "ans", Ans.NamespaceName),
            cabecalho,
            corpo);
        mensagem.Add(new XElement(Ans + "hash", TissElegibilidadeWs.CalcularHash(mensagem)));
        return mensagem;
    }

    /// <summary>
    /// O envelope SOAP 1.1 pronto para o POST, declarado em ISO-8859-1 — a codificação do padrão
    /// e a mesma do hash. Sem formatação: espaço entre tags mudaria o que a operadora confere.
    /// </summary>
    public static string Envelope(XElement mensagem)
    {
        var envelope = new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", Soap.NamespaceName),
            new XElement(Soap + "Header"),
            new XElement(Soap + "Body", mensagem));
        return "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>" + envelope.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// O mesmo envelope com a senha trocada por asteriscos: é o que pode ir para log, console ou
    /// anexo de chamado. Continua levando carteira e número de guia — não é texto para publicar.
    /// </summary>
    public static string ParaExibir(string envelope)
    {
        var doc = XDocument.Parse(envelope);
        foreach (var senha in doc.Descendants(Ans + "senhaPrestador"))
            senha.Value = new string('*', 32);
        return doc.ToString();
    }

    /// <summary>
    /// Abre a resposta e devolve a FALHA que impede a leitura — texto vazio, resposta que não é
    /// XML (página de erro), <c>tissFault</c> do padrão ou erro SOAP — ou <c>null</c> quando há
    /// o que ler em <paramref name="doc"/>. Nunca lança.
    /// </summary>
    public static string? Abrir(string? xmlDaResposta, out XDocument? doc)
    {
        doc = null;
        if (string.IsNullOrWhiteSpace(xmlDaResposta))
            return "resposta vazia";

        try
        {
            doc = XDocument.Parse(xmlDaResposta);
        }
        catch (Exception ex)
        {
            return "resposta que não é XML (" + ex.Message + ")";
        }

        var falhaTiss = doc.Descendants(Ans + "tissFault").FirstOrDefault()?.Value.Trim();
        if (!string.IsNullOrEmpty(falhaTiss))
            return falhaTiss;

        var falhaSoap = doc.Descendants(Soap + "Fault").FirstOrDefault();
        if (falhaSoap is not null)
        {
            var texto = falhaSoap.Elements().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value.Trim();
            return string.IsNullOrEmpty(texto) ? "erro SOAP sem descrição" : texto;
        }

        return null;
    }

    /// <summary>
    /// Junta o status HTTP à falha já lida: o SOAP devolve a falha (login, hash, schema) com
    /// HTTP 500 e o motivo no corpo, e os dois interessam a quem investiga.
    /// </summary>
    public static string? FalhaComHttp(string? falha, int? statusHttp)
        => falha is not null && statusHttp >= 400 ? $"{falha} (HTTP {statusHttp})" : falha;

    /// <summary>Um <c>ct_motivoGlosa</c>: código da tabela de glosas e a descrição, quando vem.</summary>
    public static (string Codigo, string? Descricao) LerMotivo(XElement motivo)
        => (motivo.Element(Ans + "codigoGlosa")?.Value.Trim() ?? string.Empty, TextoOuNulo(motivo.Element(Ans + "descricaoGlosa")));

    /// <summary>Os <c>motivoGlosa</c> filhos diretos de um bloco (ou nenhum, se o bloco não veio).</summary>
    public static IReadOnlyList<(string Codigo, string? Descricao)> LerMotivos(XElement? bloco)
        => bloco?.Elements(Ans + "motivoGlosa").Select(LerMotivo).ToList() ?? [];

    /// <summary>Lê uma guia do protocolo (<c>ct_guiaDados</c>).</summary>
    public static GuiaNoProtocoloTiss LerGuia(XElement guia)
    {
        var valores = guia.Element(Ans + "vlInformadoGuia");
        return new GuiaNoProtocoloTiss
        {
            NumeroGuiaPrestador = TextoOuNulo(guia.Element(Ans + "numeroGuiaPrestador")) ?? string.Empty,
            NumeroGuiaOperadora = TextoOuNulo(guia.Element(Ans + "numeroGuiaOperadora")),
            DataRealizacao = LerData(guia.Element(Ans + "dataRealizacao")),
            ValorProcessado = LerValor(valores?.Element(Ans + "valorProcessado")),
            ValorGlosa = LerValor(valores?.Element(Ans + "valorGlosa")),
            ValorLiberado = LerValor(valores?.Element(Ans + "valorLiberado")),
            Glosas = LerMotivos(guia.Element(Ans + "glosaGuia")),
            GlosasDeProcedimento = guia.Descendants(Ans + "glosasProcedimento")
                .SelectMany(g => g.Elements(Ans + "motivoGlosa")).Select(LerMotivo).ToList()
        };
    }

    /// <summary>"1001 - DESCRIÇÃO; 1002" — os motivos numa frase.</summary>
    public static string DescreverMotivos(IEnumerable<(string Codigo, string? Descricao)> motivos)
        => string.Join("; ", motivos.Select(m => string.IsNullOrWhiteSpace(m.Descricao) ? m.Codigo : $"{m.Codigo} - {m.Descricao}"));

    public static string? TextoOuNulo(XElement? elemento)
        => string.IsNullOrWhiteSpace(elemento?.Value) ? null : elemento!.Value.Trim();

    public static DateOnly? LerData(XElement? elemento)
        => DateOnly.TryParseExact(elemento?.Value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;

    public static decimal? LerValor(XElement? elemento)
        => decimal.TryParse(elemento?.Value.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;

    internal static void ExigirTamanho(string? valor, int maximo, string nome)
        => Exigir(!string.IsNullOrWhiteSpace(valor) && valor.Length <= maximo, $"O {nome} é obrigatório e tem até {maximo} caracteres.");

    internal static void ExigirTamanhoOpcional(string? valor, int maximo, string nome)
        => Exigir(valor is null || (!string.IsNullOrWhiteSpace(valor) && valor.Length <= maximo),
            $"O {nome}, quando informado, tem de 1 a {maximo} caracteres.");

    internal static void Exigir(bool condicao, string mensagem)
    {
        if (!condicao) throw new ArgumentException(mensagem);
    }
}
