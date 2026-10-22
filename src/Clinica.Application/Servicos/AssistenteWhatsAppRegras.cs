using System.Text;
using System.Text.RegularExpressions;
using Clinica.Application.Abstracoes;

namespace Clinica.Application.Servicos;

/// <summary>Um pedido à recepção escrito numa linha ("remarcar a Maria das 10h").</summary>
/// <param name="Nome">O paciente citado (sem acento, minúsculo); vazio quando não citou ninguém.</param>
/// <param name="Hora">A hora citada ("10h", "10:30"), para escolher entre dois horários do mesmo nome.</param>
public sealed record PedidoPorTexto(string Tipo, string Nome, DateOnly? Dia, TimeOnly? Hora, string Texto);

/// <summary>
/// O sistema da clínica respondendo pelo WhatsApp (out/2026). A pessoa da equipe manda mensagem ao
/// número da clínica ("qual a minha agenda de hoje?"), o CRM reconhece o celular dela e a ponte
/// responde com as MESMAS regras do Sistema SemDor do chat (<see cref="SistemaEquipeRegras"/>). O
/// que muda é a forma: o WhatsApp não desenha cartão nem botão, então a resposta é texto — e o
/// pedido à recepção, que no app nasce de um botão na linha do horário, aqui nasce de uma frase.
/// </summary>
public static class AssistenteWhatsAppRegras
{
    /// <summary>A Meta aceita 4096 caracteres; a folga cobre o rodapé que diz que a lista foi cortada.</summary>
    public const int TamanhoMaximo = 3500;

    /// <summary>
    /// A resposta do sistema como mensagem de WhatsApp: o título em negrito, os números do dia numa
    /// linha, uma linha por horário com o selo em itálico e as perguntas prontas como sugestão
    /// escrita. Lista que não cabe é CORTADA dizendo quantas ficaram de fora — nunca em silêncio.
    /// </summary>
    public static string Texto(string texto, CartaoChat? cartao)
    {
        if (cartao is null) return Cortar(texto);
        var sb = new StringBuilder();
        sb.Append('*').Append(cartao.Titulo).Append('*');
        if (!string.IsNullOrWhiteSpace(cartao.Subtitulo)) sb.Append('\n').Append(cartao.Subtitulo);
        if (cartao.Resumo is { Count: > 0 } r)
            sb.Append("\n\n").Append(string.Join(" · ", r.Select(x => $"{x.Valor} {x.Rotulo}")));

        var linhas = cartao.Linhas ?? [];
        if (linhas.Count > 0) sb.Append('\n');
        var rodape = cartao.Rodape;
        var sugestoes = cartao.Sugestoes is { Count: > 0 } s ? "\n\nPergunte também: " + string.Join(" · ", s.Select(x => $"\"{x}\"")) : "";
        for (var i = 0; i < linhas.Count; i++)
        {
            var linha = Linha(linhas[i]);
            var reserva = 120 + sugestoes.Length + (rodape?.Length ?? 0);
            if (sb.Length + linha.Length + reserva > TamanhoMaximo)
            {
                sb.Append($"\n…e mais {linhas.Count - i}. A lista inteira está na Agenda do sistema.");
                rodape = null;
                break;
            }
            sb.Append('\n').Append(linha);
        }
        if (!string.IsNullOrWhiteSpace(rodape)) sb.Append("\n\n").Append(rodape);
        sb.Append(sugestoes);
        return Cortar(sb.ToString());
    }

    private static string Linha(LinhaDoCartao l)
    {
        var sb = new StringBuilder();
        if (l.Hora.Length > 0) sb.Append(l.Hora).Append(' ');
        sb.Append(l.Titulo);
        if (!string.IsNullOrWhiteSpace(l.Detalhe)) sb.Append(" — ").Append(l.Detalhe);
        if (!string.IsNullOrWhiteSpace(l.Selo)) sb.Append(" · _").Append(l.Selo).Append('_');
        return sb.ToString();
    }

    private static string Cortar(string t) => t.Length <= TamanhoMaximo ? t : t[..(TamanhoMaximo - 1)] + "…";

    /// <summary>O que dá para mandar — a resposta da ajuda e de tudo o que o sistema não entendeu.</summary>
    public static string Ajuda(bool naoEntendeu, bool trataPedidos)
    {
        var sb = new StringBuilder();
        sb.Append(naoEntendeu ? "*Não entendi a mensagem.* Você pode mandar, por exemplo:" : "*O que posso responder:*");
        sb.Append("\n\n• \"Minha agenda de hoje\" — também amanhã, sexta, 24/10, \"agenda da Dra. Helena\"");
        sb.Append("\n• \"Quem confirmou\" · \"Quem não confirmou\" · \"Quem pediu para remarcar\"");
        sb.Append("\n• \"Próximo paciente\" · \"Quem já chegou\"");
        sb.Append("\n• \"Paciente Maria Silva\" — os próximos horários dela");
        sb.Append(trataPedidos ? "\n• \"Pedidos\" — os pedidos da equipe em aberto" : "\n• \"Meus pedidos\" — o que você pediu à recepção");
        sb.Append("\n\n*Pedir à recepção* (ela faz pela agenda e a resposta volta aqui):");
        sb.Append("\n• \"Remarcar Maria Silva 24/10 10h\"");
        sb.Append("\n• \"Cancelar o João de amanhã\"");
        sb.Append("\n• \"Encaixar Ana hoje à tarde\"");
        sb.Append("\n• \"Avisar Maria que vou atrasar 20 minutos\"");
        sb.Append("\n• \"Recepção: ligar para o laboratório\"");
        return sb.ToString();
    }

    // ================================================================ o pedido escrito

    private static readonly (string Tipo, string[] Verbos)[] Verbos =
    [
        ("remarcar", ["remarcar", "remarca", "remarque", "reagendar", "reagenda"]),
        ("cancelar", ["cancelar", "cancela", "cancele", "desmarcar", "desmarca", "desmarque"]),
        ("encaixar", ["encaixar", "encaixa", "encaixe"]),
        ("avisar", ["avisar", "avisa", "avise", "ligar", "liga", "ligue"]),
    ];

    /// <summary>O que vem antes do verbo e não muda o pedido ("por favor, pede pra recepção remarcar…").</summary>
    private static readonly HashSet<string> Preambulo =
        ["por", "favor", "pf", "pfv", "pode", "podem", "voce", "voces", "pedir", "pede", "peca", "a", "à", "recepcao",
         "para", "pra", "pro", "me", "preciso", "quero", "gostaria", "de", "solicito", "solicitar", "oi", "ola", "bom", "dia"];

    /// <summary>Palavras que não são do nome do paciente.</summary>
    private static readonly HashSet<string> ForaDoNome =
        ["a", "o", "as", "os", "da", "do", "das", "dos", "de", "paciente", "sra", "sr", "dona", "seu", "senhora", "senhor",
         "horario", "sessao", "consulta", "atendimento", "hoje", "amanha", "ontem", "depois", "dia", "no", "na", "em", "e",
         "agenda", "minha", "meu", "manha", "tarde", "noite", "segunda", "terca", "quarta", "quinta", "sexta", "sabado", "domingo",
         "feira", "proxima", "proximo", "por", "favor"];

    /// <summary>Onde o nome termina e começa o recado ("avisar Maria QUE vou atrasar").</summary>
    private static readonly HashSet<string> FimDoNome = ["que", "sobre", "para", "pra", "porque", "pq", "pois", "dizendo", "falando"];

    private static readonly Regex HoraRegex = new(@"(?<![\d/])(\d{1,2})(?:\s*h\s*(\d{2})?|:(\d{2}))(?![\d/])", RegexOptions.Compiled);

    /// <summary>
    /// A frase é um PEDIDO à recepção? Só quando o verbo vem no começo (depois de "por favor",
    /// "pede pra recepção"…): "quem pediu para remarcar" é PERGUNTA sobre a agenda e começa por
    /// "quem" — confundir as duas criaria um pedido que ninguém fez. "Recepção: …" é o recado livre.
    /// </summary>
    public static PedidoPorTexto? LerPedido(string texto, DateOnly hoje)
    {
        var original = (texto ?? "").Trim();
        if (original.Length == 0) return null;
        var limpo = ChatEquipeRegras.TextoDeBusca(original);
        var palavras = Palavras(limpo);
        if (palavras.Count == 0) return null;

        // "Recepção: ligar para o laboratório" — o recado que não é de paciente nenhum.
        if (palavras[0].TrimEnd(':') == "recepcao" && original.IndexOf(':') is var dp && dp > 0 && dp < 12)
        {
            var recado = original[(dp + 1)..].Trim();
            return recado.Length == 0 ? null : new PedidoPorTexto("outro", "", null, null, recado);
        }

        var i = 0;
        while (i < palavras.Count && Preambulo.Contains(palavras[i]) && TipoDoVerbo(palavras[i]) is null) i++;
        if (i >= palavras.Count || i > 6) return null;
        var tipo = TipoDoVerbo(palavras[i]);
        if (tipo is null) return null;

        var resto = palavras.Skip(i + 1).ToList();
        var nome = new List<string>();
        foreach (var w in resto)
        {
            if (FimDoNome.Contains(w)) break;
            if (ForaDoNome.Contains(w) || w.Any(char.IsDigit)) continue;
            if (w.Length < 2) continue;
            nome.Add(w);
            if (nome.Count == 4) break;
        }

        var (dia, citado) = SistemaEquipeRegras.Dia(" " + string.Join(' ', palavras) + " ", hoje);
        TimeOnly? hora = null;
        var mh = HoraRegex.Match(limpo);
        if (mh.Success && int.TryParse(mh.Groups[1].Value, out var h) && h is >= 0 and <= 23)
        {
            var min = mh.Groups[2].Success ? int.Parse(mh.Groups[2].Value) : mh.Groups[3].Success ? int.Parse(mh.Groups[3].Value) : 0;
            if (min is >= 0 and <= 59) hora = new TimeOnly(h, min);
        }
        return new PedidoPorTexto(tipo, string.Join(' ', nome), citado ? dia : null, hora, original);
    }

    private static string? TipoDoVerbo(string w) => Verbos.FirstOrDefault(v => v.Verbos.Contains(w)).Tipo;

    private static List<string> Palavras(string limpo)
    {
        var sb = new StringBuilder(limpo.Length);
        foreach (var c in limpo) sb.Append(char.IsLetterOrDigit(c) || c is '/' or ':' ? c : ' ');
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// O paciente do horário é o citado? Cada palavra escrita tem de começar uma palavra do nome
    /// ("maria sil" acha "Maria da Silva"; "ana" não acha "Mariana").
    /// </summary>
    public static bool NomeBate(string citado, string nomeDoPaciente)
    {
        var partes = citado.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0) return false;
        var doNome = Palavras(ChatEquipeRegras.TextoDeBusca(nomeDoPaciente));
        return partes.All(p => doNome.Any(n => n.StartsWith(p, StringComparison.Ordinal)));
    }
}
