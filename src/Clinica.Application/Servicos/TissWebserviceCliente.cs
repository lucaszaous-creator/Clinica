using System.Net.Http.Headers;

namespace Clinica.Application.Servicos;

/// <summary>
/// Entrega uma mensagem TISS a um web service de operadora (SOAP 1.1 sobre HTTPS) e devolve o
/// texto da resposta. Não conhece transação nenhuma: quem monta e quem lê é a classe da
/// transação (<see cref="TissElegibilidadeWs"/>). O <see cref="HttpClient"/> vem de fora, para
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

    /// <summary>
    /// Verifica a elegibilidade de uma carteira. Falha de rede e resposta HTTP de erro sem corpo
    /// viram <c>Elegivel = null</c> com o motivo — nunca "não elegível".
    /// </summary>
    public async Task<RespostaElegibilidadeTiss> VerificarElegibilidadeAsync(Uri endereco, PedidoElegibilidadeTiss pedido,
        CancellationToken cancelamento = default)
    {
        Exigir(endereco.Scheme == Uri.UriSchemeHttps, "O web service TISS só é chamado por HTTPS: a mensagem leva login e dado de beneficiário.");

        using var conteudo = new ByteArrayContent(TissElegibilidadeWs.Latin1.GetBytes(TissElegibilidadeWs.MontarEnvelope(pedido)));
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
            return new RespostaElegibilidadeTiss { Falha = "sem resposta da operadora (" + ex.Message + ")" };
        }

        // O SOAP devolve a falha (login, hash, schema) com HTTP 500 e o motivo NO CORPO: lê-se o
        // corpo antes de olhar o status.
        if (string.IsNullOrWhiteSpace(corpo))
            return new RespostaElegibilidadeTiss { Falha = $"resposta vazia da operadora (HTTP {status})" };

        var lida = TissElegibilidadeWs.LerResposta(corpo);
        return lida.Elegivel is null && lida.Falha is not null && status >= 400
            ? lida with { Falha = $"{lida.Falha} (HTTP {status})" }
            : lida;
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
