using System.Globalization;
using Clinica.Application.Modelos;
using Clinica.Crm.Contracts;

namespace Clinica.Application.Servicos;

/// <summary>Uma reação desenhada no balão: o emoji, quantas vezes e se a clínica é uma de quem reagiu (para tirar).</summary>
public sealed record ReacaoNoBalao(string Emoji, int Quantas, bool DaClinica);

/// <summary>Um trecho do texto do balão com a formatação do WhatsApp (*negrito*, _itálico_, ~tachado~).</summary>
public sealed record TrechoFormatado(string Texto, bool Negrito = false, bool Italico = false, bool Tachado = false);

/// <summary>Uma sugestão de horário do "Enviar mais tarde".</summary>
public sealed record HorarioSugerido(string Rotulo, DateTimeOffset Quando);

/// <summary>
/// OS RECURSOS DA v3 E DA v4 NA TELA (nov/2026, F3 — contrato da F2, Shared/Clinica/crm-ux/F2-campos-e-rotas.md): o que o
/// balão, a lista e o campo AFIRMAM sobre reação, formatação, "escrevendo", fixadas, etiquetas, rascunho, agendadas e
/// "Tentar de novo". A Meta manda: reação, endereço, contato e serviço só dentro da janela de 24 h; reação só em mensagem
/// de até 30 dias. Com teste (<c>RecursosDoWhatsAppNaTelaTests</c>).
/// </summary>
public static partial class ConversasNaTela
{
    /// <summary>Os emojis da barra de reação (a mesma ordem do WhatsApp).</summary>
    public static readonly IReadOnlyList<string> EmojisDeReacao = ["👍", "❤️", "😂", "😮", "😢", "🙏"];

    /// <summary>A Meta só aceita reação em mensagem de até 30 dias.</summary>
    public static readonly TimeSpan IdadeMaximaDaReacao = TimeSpan.FromDays(30);

    /// <summary>As reações do balão, agrupadas por emoji na ordem em que apareceram.</summary>
    public static IReadOnlyList<ReacaoNoBalao>? Reacoes(AppMessage m)
    {
        if (m.Reactions is not { Length: > 0 } todas) return null;
        return todas.Where(r => !string.IsNullOrWhiteSpace(r.Emoji)).GroupBy(r => r.Emoji)
            .Select(g => new ReacaoNoBalao(g.Key, g.Count(), g.Any(r => !r.FromPatient))).ToList();
    }

    /// <summary>
    /// Por que a clínica NÃO pode reagir a esta mensagem agora — nulo quando pode. Só no WhatsApp, só com a janela de 24 h
    /// aberta, só em mensagem com o número do WhatsApp e de até 30 dias.
    /// </summary>
    public static string? MotivoParaNaoReagir(ItemDaConversa m, bool janelaLivre, bool sms, DateTimeOffset agora)
        => sms ? "O SMS não tem reação"
            : !janelaLivre ? "Janela de 24 h fechada: a Meta não aceita reação"
            : string.IsNullOrEmpty(m.Id) ? "Esta mensagem ainda não tem o número do WhatsApp"
            : m.At is { } at && agora - at > IdadeMaximaDaReacao ? "A Meta só aceita reação em mensagem de até 30 dias"
            : null;

    /// <summary>
    /// A mensagem enviada pede "Tentar de novo": falhou (ou ficou sem confirmação) e nenhuma outra é o "Tentar de novo"
    /// dela (<c>RetryOf</c>). Quando a nova sai, o "!" da original some.
    /// </summary>
    public static bool PedeTentarDeNovo(AppMessage m, IEnumerable<AppMessage> todas)
        => Tick(m.Status)?.Estado is EstadoDaEntrega.Falhou
           && !todas.Any(x => string.Equals(x.RetryOf, m.Id, StringComparison.Ordinal));

    /// <summary>
    /// A formatação do WhatsApp: *negrito*, _itálico_ e ~tachado~, quando o símbolo abre colado a uma letra e fecha colado
    /// a outra (como no WhatsApp: "2 * 3 = 6" não vira negrito). Sem formatação, um trecho só com o texto.
    /// </summary>
    public static IReadOnlyList<TrechoFormatado> Formatar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return [];
        var trechos = new List<TrechoFormatado>();
        Formatar(texto, false, false, false, trechos);
        return trechos;
    }

    private static void Formatar(string t, bool n, bool i, bool s, List<TrechoFormatado> saida)
    {
        var inicio = 0;
        for (var k = 0; k < t.Length; k++)
        {
            var c = t[k];
            if (c is not ('*' or '_' or '~')) continue;
            var antes = k == 0 || !char.IsLetterOrDigit(t[k - 1]);
            if (!antes || k + 1 >= t.Length || char.IsWhiteSpace(t[k + 1]) || t[k + 1] == c) continue;
            var fim = t.IndexOf(c, k + 1);
            while (fim > 0 && (char.IsWhiteSpace(t[fim - 1]) || fim + 1 < t.Length && char.IsLetterOrDigit(t[fim + 1])))
                fim = t.IndexOf(c, fim + 1);
            if (fim < 0 || t.IndexOf('\n', k, fim - k) >= 0) continue;
            if (k > inicio) saida.Add(new(t[inicio..k], n, i, s));
            Formatar(t[(k + 1)..fim], n || c == '*', i || c == '_', s || c == '~', saida);
            inicio = fim + 1;
            k = fim;
        }
        if (inicio < t.Length) saida.Add(new(t[inicio..], n, i, s));
    }

    /// <summary>
    /// "Bia está escrevendo…", "Bia e Carla estão escrevendo…" — quem MAIS da equipe escreve agora (o CRM inclui quem pediu:
    /// a tela o tira). Nulo quando ninguém.
    /// </summary>
    public static string? QuemEscreve(IEnumerable<string>? escrevendo, string? meuAgente, IReadOnlyDictionary<string, string>? nomes)
    {
        var outros = (escrevendo ?? []).Where(a => !string.IsNullOrWhiteSpace(a) && a != meuAgente).Distinct()
            .Select(a => nomes is not null && nomes.TryGetValue(a, out var n) ? MensagensDeContato.PrimeiroNome(n) : "Alguém da equipe").ToList();
        return outros.Count switch
        {
            0 => null,
            1 => $"{outros[0]} está escrevendo…",
            _ => $"{string.Join(", ", outros.Take(outros.Count - 1))} e {outros[^1]} estão escrevendo…"
        };
    }

    /// <summary>O rascunho na linha da lista (como o "Rascunho:" verde do WhatsApp): o começo do texto numa linha. Nulo sem texto.</summary>
    public static string? PreviaDoRascunho(string? texto)
    {
        var t = string.Join(' ', (texto ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t.Length == 0 || TermoDaBarra(texto) is not null ? null : t.Length <= 60 ? t : t[..59].TrimEnd() + "…";
    }

    /// <summary>As fixadas primeiro, na ordem em que a pessoa fixou; as outras na ordem que vieram. Estável.</summary>
    public static IReadOnlyList<T> FixadasPrimeiro<T>(IEnumerable<T> linhas, Func<T, string> id, IReadOnlyList<string> fixadas)
        => linhas.Select((l, k) => (l, k, f: IndiceNa(fixadas, id(l))))
            .OrderBy(x => x.f < 0 ? int.MaxValue : x.f).ThenBy(x => x.k).Select(x => x.l).ToList();

    private static int IndiceNa(IReadOnlyList<string> lista, string id)
    {
        for (var k = 0; k < lista.Count; k++) if (lista[k] == id) return k;
        return -1;
    }

    /// <summary>Por que não dá para fixar mais uma — nulo quando dá (a conversa já fixada sempre pode ser desafixada).</summary>
    public static string? MotivoParaNaoFixar(bool jaFixada, int fixadas, int maximo)
        => !jaFixada && fixadas >= maximo ? $"Você já fixou {maximo}: desafixe uma antes" : null;

    /// <summary>Os nomes das etiquetas da conversa (as arquivadas também, porque continuam nela), na ordem da clínica.</summary>
    public static IReadOnlyList<AppLabel> EtiquetasDa(IEnumerable<string>? ids, IReadOnlyList<AppLabel> daClinica)
    {
        var deLa = (ids ?? []).ToHashSet(StringComparer.Ordinal);
        return daClinica.Where(e => deLa.Contains(e.Id)).ToList();
    }

    /// <summary>As conversas que têm a etiqueta (o filtro da pílula da etiqueta, como no WhatsApp Business).</summary>
    public static IReadOnlySet<string> IdsComEtiqueta(IEnumerable<AppConversationSummary> conversas, string etiqueta)
        => conversas.Where(c => c.Labels?.Contains(etiqueta) == true).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// A cor da etiqueta: uma das quatro do desenho aprovado (laranja, verde, vermelho, azul), estável pelo id — a clínica
    /// não escolhe a cor, para nunca sair do contraste medido.
    /// </summary>
    public static string TomDaEtiqueta(string id)
    {
        var h = 0;
        foreach (var c in id) h = unchecked(h * 31 + c);
        return (Math.Abs(h % 4)) switch { 0 => "laranja", 1 => "verde", 2 => "vermelho", _ => "azul" };
    }

    /// <summary>O balão da mensagem agendada: "Agendada para amanhã às 08:00" (ou o estado, quando já não está na fila).</summary>
    public static string TextoDaAgendada(AppScheduledMessage s, DateTimeOffset agora) => s.State switch
    {
        "enviando" => "Enviando agora",
        "falhou" => string.IsNullOrWhiteSpace(s.Error) ? "Não saiu no horário" : s.Error!,
        "cancelada" => "Cancelada",
        _ => $"Agendada para {QuandoCurto(s.At, agora)}"
    };

    /// <summary>As agendadas que ainda aparecem na conversa: na fila, enviando ou que falharam (a enviada já é mensagem).</summary>
    public static IReadOnlyList<AppScheduledMessage> AgendadasAVista(AppConversation c)
        => (c.Scheduled ?? []).Where(s => s.State is "agendada" or "enviando" or "falhou").OrderBy(s => s.At).ToList();

    /// <summary>
    /// As sugestões do "Enviar mais tarde": hoje às 14h ou 17h (se ainda não passou), amanhã às 08h e às 14h — só dentro do
    /// que a Meta deixa (o fim da janela de 24 h, para texto livre). Sem janela, nenhuma: fora dela só modelo.
    /// </summary>
    public static IReadOnlyList<HorarioSugerido> HorariosSugeridos(DateTimeOffset agora, DateTimeOffset? fimDaJanela)
    {
        if (fimDaJanela is not { } fim || fim <= agora) return [];
        var hoje = new DateTimeOffset(agora.Date, agora.Offset);
        var opcoes = new[] { (hoje.AddHours(14), "Hoje, 14:00"), (hoje.AddHours(17), "Hoje, 17:00"), (hoje.AddDays(1).AddHours(8), "Amanhã, 08:00"),
            (hoje.AddDays(1).AddHours(14), "Amanhã, 14:00") };
        return opcoes.Where(o => o.Item1 > agora.AddMinutes(1) && o.Item1 <= fim).Select(o => new HorarioSugerido(o.Item2, o.Item1)).ToList();
    }

    /// <summary>O fim da janela de 24 h (para texto livre), a partir da última mensagem do paciente; nulo sem mensagem dele.</summary>
    public static DateTimeOffset? FimDaJanela(DateTimeOffset ultimaRecebida)
        => PacienteJaEscreveu(ultimaRecebida) ? ultimaRecebida + DuracaoDaJanela : null;

    private static string QuandoCurto(DateTimeOffset at, DateTimeOffset agora)
    {
        var l = at.ToOffset(agora.Offset);
        var d = DateOnly.FromDateTime(l.DateTime);
        var hoje = DateOnly.FromDateTime(agora.DateTime);
        var hora = l.ToString("HH:mm", CultureInfo.InvariantCulture);
        return d == hoje ? $"hoje às {hora}" : d == hoje.AddDays(1) ? $"amanhã às {hora}" : $"{l.ToString("dd/MM", CultureInfo.InvariantCulture)} às {hora}";
    }

    // ---------------------------------------------------------------- as mensagens automáticas, compactas (pedido do dono, 24/11)

    /// <summary>Saiu sozinha (o menu, o "encaminhado para…", o lembrete automático): sem pessoa da equipe como autora.</summary>
    public static bool EhAutomatica(AppMessage m)
        => !EhRecebida(m) && (string.IsNullOrWhiteSpace(m.AgentId) || m.AgentId == "automation");

    /// <summary>A primeira mensagem do paciente depois da de índice <paramref name="indice"/> (a resposta ao menu), ou nula.</summary>
    public static AppMessage? ProximaRecebida(IReadOnlyList<AppMessage> mensagens, int indice)
    {
        for (var k = indice + 1; k < mensagens.Count; k++)
            if (EhRecebida(mensagens[k]) && string.IsNullOrEmpty(mensagens[k].ReactionTo)) return mensagens[k];
        return null;
    }

    /// <summary>
    /// A linha que resume a mensagem automática na conversa (ela abre inteira ao clique): "Menu enviado · paciente escolheu
    /// Clínica SemDor", "Lembrete enviado", "Encaminhado para Clínica SemDor". A conversa da equipe fica à vista, e o que a
    /// máquina mandou vira uma linha (pedido do dono, 24/11/2026: a conversa estava poluída antes de alguém entrar).
    /// </summary>
    public static string ResumoDaAutomatica(AppMessage m, AppMessage? resposta)
    {
        if (m.Template is { } modelo)
            return modelo.Name.Contains("lembrete", StringComparison.OrdinalIgnoreCase) ? "Lembrete enviado"
                : $"{ModeloDoWhatsApp.NomeNaTela(modelo.Name)} enviado";
        var opcoes = OpcoesDoMenu(m.Text);
        if (opcoes.Count >= 2)
        {
            var escolha = EscolhaNoMenu(opcoes, resposta?.Text);
            return escolha is null ? "Menu enviado" : $"Menu enviado · paciente escolheu {escolha}";
        }
        var limpo = TextoSemFormatacao(m.Text);
        var linha = limpo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "Mensagem automática";
        var frase = linha.Split(['.', '!', '?'], 2)[0].Trim();
        if (frase.StartsWith("Certo", StringComparison.OrdinalIgnoreCase) && linha.Length > frase.Length + 1)
            frase = linha[(frase.Length + 1)..].Split(['.', '!', '?'], 2)[0].Trim();
        frase = frase.Length == 0 ? linha : frase;
        return frase.Length <= 70 ? frase : frase[..69].TrimEnd() + "…";
    }

    /// <summary>As opções do menu: as linhas "1. Clínica SemDor", na ordem (sem a descrição em itálico de baixo).</summary>
    public static IReadOnlyList<string> OpcoesDoMenu(string? texto)
        => (texto ?? "").Split('\n').Select(l => l.Trim())
            .Select(l => System.Text.RegularExpressions.Regex.Match(l, @"^(\d{1,2})[.)\-]\s*(.+)$"))
            .Where(x => x.Success).Select(x => TextoSemFormatacao(x.Groups[2].Value).Trim()).ToList();

    /// <summary>O que o paciente escolheu: o número ("2") ou o nome da opção (sem caixa nem acento). Nulo quando não casa.</summary>
    public static string? EscolhaNoMenu(IReadOnlyList<string> opcoes, string? resposta)
    {
        var r = TextoSemFormatacao(resposta).Trim();
        if (r.Length == 0 || r.Length > 80) return null;
        if (int.TryParse(r.TrimEnd('.', ')'), out var n) && n >= 1 && n <= opcoes.Count) return opcoes[n - 1];
        return opcoes.FirstOrDefault(o => Contem(o, r) || Contem(r, o));
    }

    /// <summary>O texto sem os símbolos da formatação do WhatsApp (para o resumo e a comparação).</summary>
    public static string TextoSemFormatacao(string? texto) => string.Concat(Formatar(texto).Select(t => t.Texto));

    // ---------------------------------------------------------------- o histórico aos poucos (nov/2026, "rolar para cima carrega")

    /// <summary>Quantos itens da linha do tempo a conversa desenha de cada vez (a conversa importada do celular tem centenas).</summary>
    public const int ItensPorVez = 60;

    /// <summary>
    /// Os últimos <paramref name="quantos"/> itens da linha do tempo, sem o leitor perder o dia: se o corte cai no meio de um dia,
    /// o separador daquele dia vem junto, em cima. <c>Escondidos</c> diz quantos ficaram para o "Mensagens anteriores".
    /// </summary>
    public static (IReadOnlyList<ItemDaConversa> Itens, int Escondidos) UltimosDaLinha(IReadOnlyList<ItemDaConversa> linha, int quantos)
    {
        if (quantos <= 0 || linha.Count <= quantos) return (linha, 0);
        var inicio = linha.Count - quantos;
        var itens = linha.Skip(inicio).ToList();
        if (itens[0].Tipo != TipoNaLinhaDoTempo.Dia)
            for (var k = inicio - 1; k >= 0; k--)
                if (linha[k].Tipo == TipoNaLinhaDoTempo.Dia) { itens.Insert(0, linha[k]); break; }
        return (itens, inicio);
    }

    /// <summary>Quantos itens desenhar para que a mensagem <paramref name="id"/> (a citada) esteja na tela; nulo quando já está ou não existe.</summary>
    public static int? ItensAte(IReadOnlyList<ItemDaConversa> linha, string id, int desenhados)
    {
        for (var k = 0; k < linha.Count; k++)
            if (linha[k].Id == id) return linha.Count - k > desenhados ? linha.Count - k : null;
        return null;
    }
}
