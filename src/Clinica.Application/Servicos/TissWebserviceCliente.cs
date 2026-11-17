using System.Net.Http.Headers;

namespace Clinica.Application.Servicos;

/// <summary>
/// Entrega uma mensagem TISS a um web service de operadora (SOAP 1.1 sobre HTTPS) e devolve o
/// texto da resposta. Não conhece transação nenhuma: quem monta e quem lê é a classe da
/// transação (<see cref="TissElegibilidadeWs"/>, <see cref="TissLoteGuiasWs"/>,
/// <see cref="TissStatusProtocoloWs"/>, <see cref="TissCancelaGuiaWs"/>). O <see cref="HttpClient"/> vem de fora, para
/// o teste trocar a rede por um manipulador de mentira.
/// </summary>
public sealed class TissWebserviceCliente(HttpClient http)
{
    // Os endereços e o registro da Saúde Petrobras moram no catálogo (OperadorasTiss, nov/2026).
    // Os nomes abaixo continuam porque a ferramenta tools/TissPetrobras e os testes os usam.

    /// <summary>Verificação de elegibilidade da Saúde Petrobras (<see cref="OperadorasTiss.SaudePetrobras"/>).</summary>
    public static readonly Uri ElegibilidadeSaudePetrobras = OperadorasTiss.SaudePetrobras.Endereco(Clinica.Domain.Entities.TipoTransacaoTiss.Elegibilidade)!;

    /// <summary>Registro ANS da Saúde Petrobras (APS), o destino das mensagens.</summary>
    public const string RegistroAnsSaudePetrobras = "422631";

    private const string BaseSaudePetrobras = "https://webservices.crc.com.br/tiss/ws/40100/";

    /// <summary>Envio de lote de guias da Saúde Petrobras (<see cref="TissLoteGuiasWs"/>).</summary>
    public static readonly Uri LoteGuiasSaudePetrobras = OperadorasTiss.SaudePetrobras.Endereco(Clinica.Domain.Entities.TipoTransacaoTiss.EnvioGuia)!;

    /// <summary>Situação do protocolo de um lote da Saúde Petrobras (<see cref="TissStatusProtocoloWs"/>).</summary>
    public static readonly Uri StatusProtocoloSaudePetrobras = OperadorasTiss.SaudePetrobras.Endereco(Clinica.Domain.Entities.TipoTransacaoTiss.StatusProtocolo)!;

    /// <summary>Cancelamento de guia ou de lote da Saúde Petrobras (<see cref="TissCancelaGuiaWs"/>).</summary>
    public static readonly Uri CancelaGuiaSaudePetrobras = OperadorasTiss.SaudePetrobras.Endereco(Clinica.Domain.Entities.TipoTransacaoTiss.CancelamentoGuia)!;

    /// <summary>
    /// Demonstrativo de retorno (análise de conta e pagamento) da Saúde Petrobras. Só o endereço:
    /// a mensagem desta transação ainda não tem classe que a monte e leia.
    /// </summary>
    public static readonly Uri DemonstrativoRetornoSaudePetrobras = new(BaseSaudePetrobras + "tissSolicitacaoDemonstrativoRetorno");

    /// <summary>
    /// Verifica a elegibilidade de uma carteira. Falha de rede e resposta HTTP de erro sem corpo
    /// viram <c>Elegivel = null</c> com o motivo — nunca "não elegível".
    /// </summary>
    public async Task<RespostaElegibilidadeTiss> VerificarElegibilidadeAsync(Uri endereco, PedidoElegibilidadeTiss pedido,
        CancellationToken cancelamento = default, CodificacaoTissWs codificacao = CodificacaoTissWs.Iso88591)
    {
        var bruta = await EnviarAsync(endereco, TissElegibilidadeWs.MontarEnvelope(pedido), cancelamento, codificacao);
        if (bruta.Corpo is null)
            return new RespostaElegibilidadeTiss { Falha = bruta.Falha };

        var lida = TissElegibilidadeWs.LerResposta(bruta.Corpo);
        return lida.Elegivel is null && lida.Falha is not null && bruta.StatusHttp >= 400
            ? lida with { Falha = $"{lida.Falha} (HTTP {bruta.StatusHttp})" }
            : lida;
    }

    /// <summary>
    /// Entrega um envelope SOAP já montado (por <c>MontarEnvelope</c> da classe da transação) e
    /// devolve o que voltou, sem interpretar: quem lê é o <c>LerResposta</c> da mesma classe.
    /// Rede fora do ar e resposta sem corpo voltam como <see cref="RespostaBrutaTissWs.Falha"/>,
    /// nunca como exceção — e nunca com a cara de recusa da operadora.
    /// </summary>
    public async Task<RespostaBrutaTissWs> EnviarAsync(Uri endereco, string envelope, CancellationToken cancelamento = default,
        CodificacaoTissWs codificacao = CodificacaoTissWs.Iso88591)
    {
        Exigir(endereco.Scheme == Uri.UriSchemeHttps, "O web service TISS só é chamado por HTTPS: a mensagem leva login e dado de beneficiário.");

        // A declaração, os bytes e o Content-Type na MESMA codificação — a da operadora no catálogo.
        var (bytes, charset) = Codificar(envelope, codificacao);
        using var conteudo = new ByteArrayContent(bytes);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = charset };
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, endereco) { Content = conteudo };
        requisicao.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        string corpo;
        int status;
        try
        {
            using var resposta = await http.SendAsync(requisicao, cancelamento);
            status = (int)resposta.StatusCode;
            // Bytes, e não ReadAsStringAsync: quem decide a codificação é a declaração do XML,
            // e a operadora responde em ISO-8859-1 sem necessariamente dizer isso no cabeçalho HTTP.
            corpo = LerXml(await resposta.Content.ReadAsByteArrayAsync(cancelamento));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new RespostaBrutaTissWs(null, null, "sem resposta da operadora (" + ex.Message + ")");
        }

        // O SOAP devolve a falha (login, hash, schema) com HTTP 500 e o motivo NO CORPO: por isso
        // o corpo volta inteiro, com o status ao lado, e só a falta dele é falha de transporte.
        if (string.IsNullOrWhiteSpace(corpo))
            return new RespostaBrutaTissWs(null, status, $"resposta vazia da operadora (HTTP {status})");
        // HTTP de CLIENTE (4xx) SEM mensagem SOAP — 415 de formato, 404 de endereço, 403 da proteção:
        // a operadora não leu a mensagem. O corpo (página de erro) não é XML TISS: vira falha com o
        // status e um trecho curto, para o log dizer o que houve; a fila não repete
        // (FilaTiss.RecusaDefinitivaDoHttp). A página de 5xx continua voltando como corpo — "não se
        // sabe" —, como antes; o conector põe o trecho dela no log.
        if (status is >= 400 and < 500 && !PareceSoap(corpo))
            return new RespostaBrutaTissWs(null, status, $"a operadora recusou a mensagem (HTTP {status}): {Trecho(corpo)}");
        return new RespostaBrutaTissWs(corpo, status, null);
    }

    /// <summary>
    /// O envelope pronto para a rede: a declaração <c>&lt;?xml … encoding=…?&gt;</c> reescrita para a
    /// codificação da operadora, os bytes nela e o <c>charset</c> do Content-Type. Em ISO-8859-1 (o
    /// padrão, Saúde Petrobras) os bytes são EXATAMENTE os de antes. O hash não muda: ele já está no
    /// texto, calculado sobre o conteúdo das tags (não sobre os bytes).
    /// </summary>
    public static (byte[] Bytes, string Charset) Codificar(string envelope, CodificacaoTissWs codificacao)
    {
        System.Text.Encoding encoding = codificacao == CodificacaoTissWs.Utf8
            ? new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            : TissElegibilidadeWs.Latin1;
        var nome = NomeDa(codificacao);
        return (encoding.GetBytes(ComDeclaracao(envelope, nome)), nome);
    }

    /// <summary>O nome da codificação na declaração XML e no <c>charset</c>.</summary>
    public static string NomeDa(CodificacaoTissWs codificacao) => codificacao == CodificacaoTissWs.Utf8 ? "utf-8" : "ISO-8859-1";

    /// <summary>O texto com a declaração XML dizendo <paramref name="nomeDaCodificacao"/> (põe uma quando não há).</summary>
    public static string ComDeclaracao(string envelope, string nomeDaCodificacao)
    {
        var declaracao = $"<?xml version=\"1.0\" encoding=\"{nomeDaCodificacao}\"?>";
        if (!envelope.StartsWith("<?xml", StringComparison.Ordinal)) return declaracao + envelope;
        var fim = envelope.IndexOf("?>", StringComparison.Ordinal);
        return declaracao + envelope[(fim + 2)..];
    }

    /// <summary>Até 300 caracteres do corpo, numa linha só — para o log e a linha da fila.</summary>
    public static string Trecho(string corpo)
    {
        var linha = System.Text.RegularExpressions.Regex.Replace(corpo, @"\s+", " ").Trim();
        return linha.Length <= 300 ? linha : linha[..300] + "…";
    }

    /// <summary>O corpo tem cara de envelope SOAP (o que as classes da transação sabem ler)?</summary>
    public static bool PareceSoap(string corpo)
        => corpo.Contains("Envelope", StringComparison.Ordinal) && corpo.TrimStart().StartsWith('<');

    private static string LerXml(byte[] bytes)
    {
        using var memoria = new MemoryStream(bytes);
        using var leitor = new StreamReader(memoria, TissElegibilidadeWs.Latin1, detectEncodingFromByteOrderMarks: true);
        var texto = leitor.ReadToEnd();
        // Declarado UTF-8 (ou sem declaração, que em XML é UTF-8): relê na codificação certa.
        var inicio = texto.Length > 200 ? texto[..200] : texto;
        var declaraLatin1 = inicio.Contains("ISO-8859-1", StringComparison.OrdinalIgnoreCase)
                            || inicio.Contains("latin1", StringComparison.OrdinalIgnoreCase);
        return declaraLatin1 ? texto : System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿');
    }

    private static void Exigir(bool condicao, string mensagem)
    {
        if (!condicao) throw new ArgumentException(mensagem);
    }
}
