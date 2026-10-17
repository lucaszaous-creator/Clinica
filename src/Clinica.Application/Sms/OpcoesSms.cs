namespace Clinica.Application.Sms;

/// <summary>
/// Os provedores que a casa sabe chamar (out/2026). A clínica ainda vai contratar o
/// serviço — por isso três caminhos, e o terceiro existe para não amarrar a escolha a
/// uma lista que nós escrevemos:
/// </summary>
public enum ProvedorSms
{
    /// <summary>Nenhum: o canal está desligado e nada sai.</summary>
    Desligado,

    /// <summary>Zenvia (API v2, <c>X-API-TOKEN</c>) — o maior provedor brasileiro.</summary>
    Zenvia,

    /// <summary>Twilio (Programmable Messaging, conta + token).</summary>
    Twilio,

    /// <summary>
    /// QUALQUER provedor que aceite um POST JSON: endereço, cabeçalho de autenticação e o
    /// modelo do corpo com <c>{telefone}</c>, <c>{mensagem}</c> e <c>{remetente}</c>.
    /// Trocar de fornecedor vira preencher três campos, não publicar versão — a decisão do
    /// armazenamento S3-compatível (parcela 53).
    /// </summary>
    Generico,

    /// <summary>
    /// Infobip (SMS API, <c>Authorization: App &lt;chave&gt;</c>) — a MESMA conta do WhatsApp do
    /// SemDor CRM (out/2026). Uma conta só para os dois sistemas: um contrato, uma fatura, e o
    /// remetente que o paciente já reconhece. ⚠️ Valor NOVO no fim do enum: ele é gravado como
    /// TEXTO em Configurações, e o binário anterior lê o nome desconhecido como Desligado
    /// (<c>ObterCamposSmsAsync</c> faz ida e volta pelo nome) — não lança, só não envia até
    /// atualizar.
    /// </summary>
    Infobip
}

/// <summary>Os campos CRUS da tela de Configurações → SMS.</summary>
public sealed record CamposSms(
    ProvedorSms Provedor,
    string? Endereco,
    string? Conta,
    string? Token,
    string? Remetente,
    string? CabecalhoAutenticacao,
    string? ModeloCorpo,
    int AntecedenciaHoras,
    bool AdiantarFimDeSemana,
    bool EmailSoParaQuemNaoTemCelular);

/// <summary>
/// O provedor pronto para enviar. <b>Meia configuração é configuração nenhuma</b> (a regra
/// do <c>OpcoesEmail</c>): é <see cref="De"/> — num lugar só — que responde "está ligado?",
/// e a tela e o serviço leem a mesma resposta.
/// </summary>
public sealed record OpcoesSms(
    ProvedorSms Provedor,
    string Endereco,
    string? Conta,
    string Token,
    string? Remetente,
    string CabecalhoAutenticacao,
    string ModeloCorpo,
    int AntecedenciaHoras,
    bool AdiantarFimDeSemana)
{
    public static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(20);

    public const string EnderecoZenvia = "https://api.zenvia.com/v2/channels/sms/messages";
    public const string EnderecoTwilio = "https://api.twilio.com/2010-04-01/Accounts/{conta}/Messages.json";

    /// <summary>O caminho do envio de SMS da Infobip, acrescentado ao endereço-base da conta.</summary>
    public const string CaminhoInfobip = "/sms/2/text/advanced";

    /// <summary>
    /// O endereço-base da conta Infobip é próprio de cada cliente (<c>https://xxxx.api.infobip.com</c>).
    /// Só HTTPS e só o domínio da Infobip: a chave vai no cabeçalho, e um endereço digitado
    /// errado não pode levá-la a outro servidor.
    /// </summary>
    public static string? EnderecoInfobip(string? baseInformada)
    {
        var b = Limpo(baseInformada);
        if (b is null || !Uri.TryCreate(b, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.EndsWith(".api.infobip.com", StringComparison.OrdinalIgnoreCase)) return null;
        return "https://" + uri.Host + CaminhoInfobip;
    }

    /// <summary>O corpo padrão do genérico — o formato mais comum entre os provedores nacionais.</summary>
    public const string ModeloCorpoPadrao = "{\"to\":\"{telefone}\",\"message\":\"{mensagem}\",\"from\":\"{remetente}\"}";

    public static OpcoesSms? De(CamposSms c)
    {
        var token = Limpo(c.Token);
        var antecedencia = c.AntecedenciaHoras is >= 1 and <= 168
            ? c.AntecedenciaHoras
            : Clinica.Domain.ConfirmacaoPorLink.AntecedenciaPadraoHoras;

        switch (c.Provedor)
        {
            case ProvedorSms.Zenvia:
                // A Zenvia exige o remetente (o "from" da conta).
                if (token is null || Limpo(c.Remetente) is null) return null;
                return new OpcoesSms(ProvedorSms.Zenvia, EnderecoZenvia, null, token, Limpo(c.Remetente),
                    "X-API-TOKEN", string.Empty, antecedencia, c.AdiantarFimDeSemana);

            case ProvedorSms.Twilio:
                var conta = Limpo(c.Conta);
                if (token is null || conta is null || Limpo(c.Remetente) is null) return null;
                return new OpcoesSms(ProvedorSms.Twilio, EnderecoTwilio.Replace("{conta}", conta), conta, token,
                    Limpo(c.Remetente), "Authorization", string.Empty, antecedencia, c.AdiantarFimDeSemana);

            case ProvedorSms.Infobip:
                var infobip = EnderecoInfobip(c.Endereco);
                if (infobip is null || token is null || Limpo(c.Remetente) is null) return null;
                return new OpcoesSms(ProvedorSms.Infobip, infobip, null, token, Limpo(c.Remetente),
                    "Authorization", string.Empty, antecedencia, c.AdiantarFimDeSemana);

            case ProvedorSms.Generico:
                var endereco = Limpo(c.Endereco);
                if (endereco is null || token is null) return null;
                if (!Uri.TryCreate(endereco, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                    return null; // SMS com token em texto aberto pela rede não é configuração, é vazamento.
                var modelo = Limpo(c.ModeloCorpo) ?? ModeloCorpoPadrao;
                if (!modelo.Contains("{telefone}") || !modelo.Contains("{mensagem}")) return null;
                return new OpcoesSms(ProvedorSms.Generico, endereco, null, token, Limpo(c.Remetente),
                    Limpo(c.CabecalhoAutenticacao) ?? "Authorization", modelo, antecedencia, c.AdiantarFimDeSemana);

            default:
                return null;
        }
    }

    /// <summary>
    /// O que falta para o canal ligar — a frase da tela. Nulo quando está tudo certo ou
    /// quando o provedor escolhido é "desligado" (aí não falta nada: é uma escolha).
    /// </summary>
    public static string? OQueFalta(CamposSms c)
    {
        if (c.Provedor == ProvedorSms.Desligado || De(c) is not null) return null;
        var faltas = new List<string>();
        if (Limpo(c.Token) is null) faltas.Add("o token da API");
        if (c.Provedor is ProvedorSms.Zenvia or ProvedorSms.Twilio or ProvedorSms.Infobip && Limpo(c.Remetente) is null)
            faltas.Add("o remetente");
        if (c.Provedor == ProvedorSms.Twilio && Limpo(c.Conta) is null) faltas.Add("o identificador da conta (Account SID)");
        if (c.Provedor == ProvedorSms.Infobip && EnderecoInfobip(c.Endereco) is null) faltas.Add("o endereço da conta Infobip (https://….api.infobip.com)");
        if (c.Provedor == ProvedorSms.Generico)
        {
            var e = Limpo(c.Endereco);
            if (e is null) faltas.Add("o endereço da API");
            else if (!e.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) faltas.Add("um endereço HTTPS (http:// mandaria o token aberto pela rede)");
            var m = Limpo(c.ModeloCorpo) ?? ModeloCorpoPadrao;
            if (!m.Contains("{telefone}") || !m.Contains("{mensagem}")) faltas.Add("{telefone} e {mensagem} no modelo do corpo");
        }
        return faltas.Count == 0 ? null : "Falta " + string.Join(", ", faltas) + ".";
    }

    private static string? Limpo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>
/// Quem entrega um SMS. Interface pela razão do e-mail: o teste prova que a rodada escolhe
/// as sessões certas, grava antes de mandar e não reenvia — sem provedor de verdade.
/// </summary>
public interface IGatewaySms
{
    /// <summary>
    /// Envia UM SMS. Devolve o identificador que o provedor deu (pode ser nulo no
    /// genérico). Lança quando o provedor recusa ou não responde, com a frase dele.
    /// </summary>
    Task<string?> EnviarAsync(OpcoesSms opcoes, string celularE164, string texto, CancellationToken ct = default);
}
