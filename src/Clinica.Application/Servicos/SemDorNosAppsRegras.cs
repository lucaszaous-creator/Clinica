using Clinica.Application.Abstracoes;

namespace Clinica.Application.Servicos;

/// <summary>
/// As regras do @SemDor com IA nos apps (out/2026 — Clinica#307, docs/semdor-assistente.md). A IA
/// mora no CRM e só REESCREVE a pergunta que as regras do chat não entenderam; quem responde
/// continua sendo a clínica (<c>SistemaEquipeService</c>), com as permissões de quem perguntou.
/// Aqui moram os prazos e as frases — o que o chat AFIRMA — para o app, a ponte e os testes.
/// </summary>
public static class SemDorNosAppsRegras
{
    /// <summary>
    /// O CRM lê a fila a cada poucos segundos enquanto a IA está ligada. Sem leitura há mais do que
    /// isto, o app não promete releitura: a pessoa recebe a ajuda de sempre, na hora.
    /// </summary>
    public static readonly TimeSpan JanelaDoPulso = TimeSpan.FromMinutes(2);

    /// <summary>De quanto em quanto a ponte regrava o pulso (ler a fila a cada 5 s não vira uma escrita a cada 5 s).</summary>
    public static readonly TimeSpan PulsoGravadoACada = TimeSpan.FromSeconds(30);

    /// <summary>A pergunta que espera mais que isto recebe a ajuda: ninguém fica olhando um "relendo" sem fim.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(2);

    /// <summary>Teto do texto que vai à fila (o mesmo do WhatsApp da equipe).</summary>
    public const int TamanhoMaximo = 1000;

    public const string Aguardando = "aguardando";
    public const string Lendo = "lendo";
    public const string Respondida = "respondida";
    public const string SemResposta = "sem-resposta";

    /// <summary>O CRM passou há pouco?</summary>
    public static bool IaDisponivel(DateTime? perguntasLidasEm, DateTime agora)
        => perguntasLidasEm is { } em && agora - em < JanelaDoPulso && em <= agora.AddMinutes(1);

    /// <summary>A resposta do sistema foi a ajuda de "não entendi"?</summary>
    public static bool NaoEntendeu(CartaoChat? cartao)
        => cartao is { Tipo: "ajuda", Titulo: SistemaEquipeRegras.TituloNaoEntendi };

    /// <summary>
    /// O que aparece logo atrás da pergunta enquanto o CRM relê. Diz o que sai da clínica (só o
    /// texto escrito) e de onde vem a resposta (a agenda) — garantia que o código dá, e só ela.
    /// </summary>
    public const string Relendo = "Não reconheci a pergunta de primeira. O @SemDor está relendo — à IA vai só o texto que você escreveu; a resposta vem da agenda da clínica.";

    /// <summary>A resposta depois da releitura: diz como a pergunta foi entendida, para quem leu conferir.</summary>
    public static string Entendida(string reescrita, string resposta)
        => $"Entendi como \"{reescrita.Trim()}\".\n\n{resposta}";

    /// <summary>Limpa o que a IA devolveu: uma linha, sem aspas nas pontas, com teto; vazio vira nulo.</summary>
    public static string? Reescrita(string? texto)
    {
        var t = (texto ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim().Trim('"', '“', '”', '\'').Trim();
        if (t.Length == 0) return null;
        return t.Length > 200 ? t[..200] : t;
    }
}

/// <summary>
/// Os avisos "Atenção" do @SemDor no sino da Recepção (Clinica#307). O CRM manda a lista inteira a
/// cada minuto; o que o sino AFIRMA sai daqui: se a lista ainda vale, como o aviso se escreve e se o
/// link pode ser aberto.
/// </summary>
public static class AvisosDoSemDorRegras
{
    /// <summary>
    /// Lista mais velha que isto não é mostrada como atual: o CRM parou de mandar (desligado, sem
    /// rede), e um aviso velho no sino diria "sem resposta" de quem talvez já foi respondido.
    /// </summary>
    public static readonly TimeSpan ListaValePor = TimeSpan.FromMinutes(10);

    public static bool ListaAtual(DateTime? recebidaEm, DateTime agora)
        => recebidaEm is { } r && agora - r < ListaValePor && r <= agora.AddMinutes(5);

    /// <summary>"hoje, 09:42" ou "22/10, 18:10".</summary>
    public static string Quando(DateTime em, DateTime agora)
        => em.Date == agora.Date ? $"hoje, {em:HH:mm}" : $"{em:dd/MM}, {em:HH:mm}";

    /// <summary>"1 sem resposta", "3 sem resposta".</summary>
    public static string Contador(int n) => $"{n} sem resposta";

    /// <summary>O aviso passageiro, uma vez por aviso novo.</summary>
    public static string Frase(string paciente, string assunto)
        => $"@SemDor: {paciente} escreveu no WhatsApp — {assunto.ToLowerInvariant()}. Responda pelo CRM.";

    /// <summary>Só abre no navegador o que é https, sem usuário e senha no endereço.</summary>
    public static bool LinkSeguro(string? link)
        => Uri.TryCreate(link, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.UserInfo.Length == 0;

    /// <summary>
    /// A conversa do aviso (o CRM manda <c>…/#inbox/{id}</c>): é por ela que o sino abre a conversa na
    /// aba WhatsApp do próprio app (fase 3, 24/10: a moldura saiu). Nulo = link sem conversa.
    /// </summary>
    public static string? ConversaDoLink(string? link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var u)) return null;
        const string prefixo = "#inbox/";
        if (!u.Fragment.StartsWith(prefixo, StringComparison.Ordinal)) return null;
        var id = Uri.UnescapeDataString(u.Fragment[prefixo.Length..]).Trim();
        return id.Length is > 0 and <= 120 && !id.Contains('/') ? id : null;
    }
}
