using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Clinica.Application.Sms;

/// <summary>
/// A entrega pelos provedores HTTP, com o <see cref="HttpClient"/> do próprio .NET — sem
/// SDK de fornecedor: os apps se auto-atualizam, e um pacote por provedor seria uma
/// dependência a mais por uma chamada POST.
///
/// <b>A frase do provedor sobe inteira</b> na exceção (truncada a 300 caracteres): "saldo
/// insuficiente", "remetente não autorizado" e "número inválido" são as três respostas que
/// a clínica vai encontrar, e cada uma pede uma ação diferente.
/// </summary>
public sealed class GatewaySmsHttp : IGatewaySms
{
    private readonly HttpClient _http;

    public GatewaySmsHttp() : this(new HttpClient { Timeout = OpcoesSms.TempoLimite }) { }

    public GatewaySmsHttp(HttpClient http) => _http = http;

    public async Task<string?> EnviarAsync(OpcoesSms opcoes, string celularE164, string texto, CancellationToken ct = default)
    {
        using var pedido = Montar(opcoes, celularE164, texto);
        using var resposta = await _http.SendAsync(pedido, ct);
        var corpo = await resposta.Content.ReadAsStringAsync(ct);

        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"O provedor de SMS recusou ({(int)resposta.StatusCode}): {Cortar(corpo)}");

        return LerId(corpo);
    }

    /// <summary>Monta a requisição de cada provedor. Público para o teste conferir o formato sem rede.</summary>
    public static HttpRequestMessage Montar(OpcoesSms opcoes, string celularE164, string texto)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, opcoes.Endereco);
        switch (opcoes.Provedor)
        {
            case ProvedorSms.Zenvia:
                // A Zenvia quer o número sem o "+".
                pedido.Headers.Add("X-API-TOKEN", opcoes.Token);
                pedido.Content = new StringContent(JsonSerializer.Serialize(new
                {
                    from = opcoes.Remetente,
                    to = celularE164.TrimStart('+'),
                    contents = new[] { new { type = "text", text = texto } }
                }), Encoding.UTF8, "application/json");
                break;

            case ProvedorSms.Twilio:
                pedido.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opcoes.Conta}:{opcoes.Token}")));
                pedido.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["To"] = celularE164,
                    ["From"] = opcoes.Remetente ?? string.Empty,
                    ["Body"] = texto
                });
                break;

            default:
                // Genérico: o cabeçalho "Authorization" leva "Bearer <token>"; qualquer outro
                // nome (X-Api-Key, X-Auth-Token…) leva o token cru — é como os provedores fazem.
                if (string.Equals(opcoes.CabecalhoAutenticacao, "Authorization", StringComparison.OrdinalIgnoreCase))
                    pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opcoes.Token);
                else
                    pedido.Headers.TryAddWithoutValidation(opcoes.CabecalhoAutenticacao, opcoes.Token);
                pedido.Content = new StringContent(
                    PreencherModelo(opcoes.ModeloCorpo, celularE164, texto, opcoes.Remetente),
                    Encoding.UTF8, "application/json");
                break;
        }
        return pedido;
    }

    /// <summary>
    /// Troca os marcadores pelo valor ESCAPADO para JSON — sem o escape, uma aspa no nome
    /// da clínica quebraria o corpo e o provedor recusaria todo SMS com "JSON inválido".
    /// O telefone vai com o "+"; quem não o quer escreve <c>{telefone_sem_mais}</c>.
    /// </summary>
    public static string PreencherModelo(string modelo, string celularE164, string texto, string? remetente)
        => modelo
            .Replace("{telefone_sem_mais}", Escapar(celularE164.TrimStart('+')))
            .Replace("{telefone}", Escapar(celularE164))
            .Replace("{mensagem}", Escapar(texto))
            .Replace("{remetente}", Escapar(remetente ?? string.Empty));

    private static string Escapar(string valor)
    {
        var json = JsonSerializer.Serialize(valor);
        return json[1..^1];
    }

    /// <summary>O id que o provedor devolveu: "id" (Zenvia, genéricos) ou "sid" (Twilio).</summary>
    private static string? LerId(string corpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            foreach (var nome in new[] { "id", "sid", "messageId", "message_id" })
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty(nome, out var v))
                    return v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
        }
        catch (JsonException) { /* corpo que não é JSON: o envio passou, só não há id */ }
        return null;
    }

    private static string Cortar(string s) => s.Length <= 300 ? s : s[..300] + "…";
}
