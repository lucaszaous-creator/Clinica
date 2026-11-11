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
    /// <summary>
    /// Endereço de produção da verificação de elegibilidade da Saúde Petrobras (conectividade CRC),
    /// do "Manual de Utilização Webservice TISS 4.01.00" da operadora. O manual não traz endereço
    /// de homologação: toda chamada é em produção.
    /// </summary>
    public static readonly Uri ElegibilidadeSaudePetrobras = new("https://webservices.crc.com.br/tiss/ws/40100/tissVerificaElegibilidade");

    /// <summary>Registro ANS da Saúde Petrobras (APS), o destino das mensagens.</summary>
    public const string RegistroAnsSaudePetrobras = "422631";

    private const string BaseSaudePetrobras = "https://webservices.crc.com.br/tiss/ws/40100/";

    /// <summary>Envio de lote de guias da Saúde Petrobras (<see cref="TissLoteGuiasWs"/>). Só produção, como os demais.</summary>
    public static readonly Uri LoteGuiasSaudePetrobras = new(BaseSaudePetrobras + "tissLoteGuias");

    /// <summary>Situação do protocolo de um lote da Saúde Petrobras (<see cref="TissStatusProtocoloWs"/>).</summary>
    public static readonly Uri StatusProtocoloSaudePetrobras = new(BaseSaudePetrobras + "tissSolicitacaoStatusProtocolo");

    /// <summary>Cancelamento de guia ou de lote da Saúde Petrobras (<see cref="TissCancelaGuiaWs"/>).</summary>
    public static readonly Uri CancelaGuiaSaudePetrobras = new(BaseSaudePetrobras + "tissCancelaGuia");

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
        CancellationToken cancelamento = default)
    {
        var bruta = await EnviarAsync(endereco, TissElegibilidadeWs.MontarEnvelope(pedido), cancelamento);
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
    public async Task<RespostaBrutaTissWs> EnviarAsync(Uri endereco, string envelope, CancellationToken cancelamento = default)
    {
        Exigir(endereco.Scheme == Uri.UriSchemeHttps, "O web service TISS só é chamado por HTTPS: a mensagem leva login e dado de beneficiário.");

        // Os bytes saem na codificação que o envelope DECLARA (ISO-8859-1), a mesma do hash.
        using var conteudo = new ByteArrayContent(TissElegibilidadeWs.Latin1.GetBytes(envelope));
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "ISO-8859-1" };
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
        return string.IsNullOrWhiteSpace(corpo)
            ? new RespostaBrutaTissWs(null, status, $"resposta vazia da operadora (HTTP {status})")
            : new RespostaBrutaTissWs(corpo, status, null);
    }

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
