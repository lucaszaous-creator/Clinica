using System.Net.Http.Headers;
using System.Text;

namespace Clinica.Tiss.Conector;

/// <summary>
/// Entrega um envelope SOAP 1.1 a um web service TISS por HTTPS e devolve o corpo da resposta.
/// Não conhece transação nenhuma. A mensagem vai e volta em ISO-8859-1, a codificação do padrão.
///
/// É a mesma entrega do <c>TissWebserviceCliente</c> (que só sabe elegibilidade), sem a leitura
/// da resposta: aqui quem lê é o processador do tipo. Na integração as duas podem virar uma.
/// </summary>
public sealed class TransporteTissHttp(HttpClient http) : ITransporteTiss
{
    public async Task<RespostaDoTransporte> EnviarAsync(Uri endereco, string envelope, CancellationToken ct)
    {
        if (endereco.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("O web service TISS só é chamado por HTTPS: a mensagem leva login e dado de beneficiário.");

        using var conteudo = new ByteArrayContent(Encoding.Latin1.GetBytes(envelope));
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "ISO-8859-1" };
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, endereco) { Content = conteudo };
        requisicao.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        try
        {
            using var resposta = await http.SendAsync(requisicao, ct);
            var status = (int)resposta.StatusCode;
            // O SOAP devolve a falha (login, hash, schema) com HTTP 500 e o motivo NO CORPO:
            // lê-se o corpo antes de olhar o status.
            var corpo = LerXml(await resposta.Content.ReadAsByteArrayAsync(ct));
            return string.IsNullOrWhiteSpace(corpo)
                ? new RespostaDoTransporte(status, null, $"resposta vazia da operadora (HTTP {status})")
                : new RespostaDoTransporte(status, corpo, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Só o TIPO da falha: a mensagem de uma exceção de rede pode repetir pedaços do pedido.
            return new RespostaDoTransporte(null, null, "sem resposta da operadora (" + ex.GetType().Name + ")");
        }
    }

    private static string LerXml(byte[] bytes)
    {
        var texto = Encoding.Latin1.GetString(bytes);
        var inicio = texto.Length > 200 ? texto[..200] : texto;
        var declaraLatin1 = inicio.Contains("ISO-8859-1", StringComparison.OrdinalIgnoreCase)
                            || inicio.Contains("latin1", StringComparison.OrdinalIgnoreCase);
        // Declarado UTF-8 (ou sem declaração, que em XML é UTF-8): relê na codificação certa.
        return declaraLatin1 ? texto : Encoding.UTF8.GetString(bytes).TrimStart('﻿');
    }
}
