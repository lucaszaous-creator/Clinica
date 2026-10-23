using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Clinica.Application.Servicos;

/// <summary>Um documento pedido numa linha ("atestado Maria Silva 2 dias").</summary>
/// <param name="Tipo">O tipo do tablet: atestado, comparecimento, receita, exame ou relatorio.</param>
/// <param name="Nome">A paciente citada (sem acento, minúsculo).</param>
/// <param name="Dias">Os dias de afastamento do atestado; nulo quando não vieram.</param>
/// <param name="Texto">O que veio depois dos dois-pontos (receita, exame, relatório), como foi escrito.</param>
public sealed record DocumentoPorTexto(string Tipo, string Nome, int? Dias, string Texto);

/// <summary>
/// As regras do documento pedido pelo WhatsApp (@SemDor fase 4, out/2026 — docs/semdor-assistente.md).
/// O médico escreve ao número da clínica; a ponte grava um RASCUNHO e devolve um link de uso único
/// para o portal, onde ele revisa, emite e assina. Aqui moram a leitura da frase, o texto proposto,
/// o token, o prazo e as frases da resposta. Nenhuma delas emite documento: quem emite é o portal,
/// pelos mesmos serviços do atendimento no tablet.
/// </summary>
public static partial class DocumentoPeloWhatsAppRegras
{
    /// <summary>O link vale 15 minutos e abre uma vez só.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(15);

    /// <summary>Teto do afastamento num pedido escrito: mais que isso é conversa, não atalho.</summary>
    public const int DiasMaximos = 60;

    /// <summary>O caminho da página no portal; o token vai depois do <c>#</c>, que o navegador não envia ao servidor.</summary>
    public const string Pagina = "/profissional/assinar.html";

    public const int TamanhoMaximoDoTexto = 4000;

    private static readonly (string Tipo, string[] Inicios)[] Tipos =
    [
        // A ordem importa: "atestado de comparecimento" é comparecimento, não atestado.
        ("comparecimento", ["atestado de comparecimento", "declaracao de comparecimento", "comparecimento"]),
        ("atestado", ["atestado medico", "atestado"]),
        ("receita", ["receita", "receituario"]),
        ("exame", ["pedido de exames", "pedido de exame", "exames", "exame"]),
        ("relatorio", ["relatorio medico", "relatorio"]),
    ];

    /// <summary>O que vem antes do tipo e não muda o pedido ("por favor, emite um atestado…").</summary>
    private static readonly string[] Preambulos =
        ["por favor", "pf", "pfv", "emitir", "emite", "emita", "gerar", "gera", "gere", "fazer", "faz", "faca",
         "preciso de", "preciso", "quero", "um", "uma", "o", "a"];

    /// <summary>Palavras que não são do nome da paciente.</summary>
    private static readonly HashSet<string> ForaDoNome =
        ["a", "o", "da", "do", "de", "para", "pra", "pro", "paciente", "sra", "sr", "dona", "seu", "senhora", "senhor",
         "hoje", "dia", "dias", "e", "com", "por", "afastamento"];

    [GeneratedRegex(@"(\d{1,3})\s*dias?\b")]
    private static partial Regex DiasRegex();

    /// <summary>"dois dias" escrito por extenso vira "2 dias" antes da leitura.</summary>
    [GeneratedRegex(@"\b(um|uma|dois|duas|tres|quatro|cinco|seis|sete|oito|nove|dez|quinze|trinta)\s+dias?\b")]
    private static partial Regex DiasPorExtensoRegex();

    private static readonly Dictionary<string, int> PorExtenso = new()
    {
        ["um"] = 1, ["uma"] = 1, ["dois"] = 2, ["duas"] = 2, ["tres"] = 3, ["quatro"] = 4, ["cinco"] = 5, ["seis"] = 6,
        ["sete"] = 7, ["oito"] = 8, ["nove"] = 9, ["dez"] = 10, ["quinze"] = 15, ["trinta"] = 30,
    };

    /// <summary>
    /// A frase pede um documento? Só quando o tipo vem no começo (depois de "por favor", "emite
    /// um"…). "Quem pediu atestado?" é pergunta e começa por "quem" — não vira rascunho.
    /// </summary>
    public static DocumentoPorTexto? Ler(string texto)
    {
        var original = (texto ?? "").Trim();
        if (original.Length == 0) return null;
        var doisPontos = original.IndexOf(':');
        var cabeca = ChatEquipeRegras.TextoDeBusca(doisPontos >= 0 ? original[..doisPontos] : original);
        var corpo = doisPontos >= 0 ? original[(doisPontos + 1)..].Trim() : "";

        var resto = SemPreambulo(cabeca);
        foreach (var (tipo, inicios) in Tipos)
        {
            var inicio = inicios.FirstOrDefault(x => resto.StartsWith(" " + x + " ", StringComparison.Ordinal));
            if (inicio is null) continue;
            var depois = DiasPorExtensoRegex().Replace(resto[(inicio.Length + 1)..], m => PorExtenso[m.Groups[1].Value] + " dias");
            int? dias = null;
            var md = DiasRegex().Match(depois);
            if (md.Success && int.TryParse(md.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var d))
            {
                dias = d;
                depois = depois.Remove(md.Index, md.Length);
            }
            var nome = depois.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !ForaDoNome.Contains(w) && !w.Any(char.IsDigit) && w.Length >= 2).Take(4);
            return new DocumentoPorTexto(tipo, string.Join(' ', nome), dias, corpo.Length > TamanhoMaximoDoTexto ? corpo[..TamanhoMaximoDoTexto] : corpo);
        }
        return null;
    }

    /// <summary>
    /// A frase fala de documento, mesmo sem dizer qual ("emitir documento para a Maria",
    /// "preciso de uma declaração")? É o que impede o pedido de cair no "não entendi" — e de ir
    /// à IA: texto de documento nunca é reescrito (@SemDor fase 4).
    /// </summary>
    public static bool PedeDocumento(string texto)
    {
        if (Ler(texto) is not null) return true;
        var original = (texto ?? "").Trim();
        var doisPontos = original.IndexOf(':');
        var resto = SemPreambulo(ChatEquipeRegras.TextoDeBusca(doisPontos >= 0 ? original[..doisPontos] : original));
        return SemTipo.Any(x => resto.StartsWith(" " + x + " ", StringComparison.Ordinal));
    }

    /// <summary>Os pedidos que dizem "documento" sem dizer o tipo.</summary>
    private static readonly string[] SemTipo = ["documento", "documentos", "declaracao", "declaracoes"];

    /// <summary>A resposta a quem pediu documento sem dizer o tipo (com a emissão por mensagem ligada).</summary>
    public const string QualDocumento = "Qual documento? Mande o tipo e o nome da paciente, por exemplo:"
        + "\n• \"atestado Maria Silva 2 dias\""
        + "\n• \"comparecimento Maria Silva\""
        + "\n• \"receita Maria Silva: dipirona 500 mg, 1 comprimido de 6/6 h se dor\""
        + "\n• \"exame Maria Silva: hemograma completo\""
        + "\n• \"relatório Maria Silva: evolução do tratamento\"";

    /// <summary>
    /// A resposta onde a emissão por mensagem não existe: no chat dos apps, e no WhatsApp enquanto a
    /// fase 4 está desligada. Antes disso o pedido caía no "não entendi" (relato da clínica, 23/10/2026).
    /// </summary>
    public const string PorAquiNao = "Por aqui ainda não emito documento. Emita pelo Consultório: abra a paciente e use Emitir.";

    /// <summary>A cabeça da frase, limpa e sem o que vem antes do tipo ("por favor, emite um…"), entre espaços.</summary>
    private static string SemPreambulo(string cabeca)
    {
        var resto = " " + Limpar(cabeca) + " ";
        for (var voltas = 0; voltas < 6; voltas++)
        {
            var p = Preambulos.FirstOrDefault(x => resto.StartsWith(" " + x + " ", StringComparison.Ordinal));
            if (p is null) break;
            resto = resto[(p.Length + 1)..];
        }
        return resto;
    }

    private static string Limpar(string limpo)
    {
        var chars = limpo.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// O que falta para o rascunho nascer, já na frase da resposta; nulo quando está completo.
    /// Receita, exame e relatório pedem o conteúdo depois dos dois-pontos — o sistema não inventa
    /// conduta. O atestado pede os dias.
    /// </summary>
    public static string? OQueFalta(DocumentoPorTexto d)
    {
        var rotulo = Rotulo(d.Tipo);
        if (d.Nome.Length == 0)
            return $"Diga de quem é o documento, por exemplo: \"{Exemplo(d.Tipo)}\".";
        if (d.Tipo == "atestado" && d.Dias is null)
            return $"Quantos dias de afastamento? Mande, por exemplo: \"atestado {d.Nome} 2 dias\".";
        if (d.Tipo == "atestado" && d.Dias is < 1 or > DiasMaximos)
            return $"O atestado por aqui vai de 1 a {DiasMaximos} dias. Para outro prazo, emita pelo Consultório.";
        if (d.Tipo is "receita" or "exame" or "relatorio" && d.Texto.Length == 0)
            return $"Escreva o conteúdo depois dos dois-pontos, por exemplo: \"{Exemplo(d.Tipo)}\". O sistema não completa {rotulo.ToLowerInvariant()} sozinho.";
        return null;
    }

    public static string Rotulo(string tipo) => tipo switch
    {
        "atestado" => "Atestado",
        "comparecimento" => "Declaração de comparecimento",
        "receita" => "Receita",
        "exame" => "Pedido de exame",
        "relatorio" => "Relatório",
        _ => "Documento",
    };

    private static string Exemplo(string tipo) => tipo switch
    {
        "atestado" => "atestado Maria Silva 2 dias",
        "comparecimento" => "comparecimento Maria Silva",
        "receita" => "receita Maria Silva: dipirona 500 mg, 1 comprimido de 6/6 h se dor",
        "exame" => "exame Maria Silva: hemograma completo",
        _ => "relatório Maria Silva: evolução do tratamento",
    };

    /// <summary>O texto proposto no rascunho. O médico revisa no portal antes de emitir.</summary>
    public static string TextoProposto(DocumentoPorTexto d) => d.Tipo switch
    {
        "atestado" => $"Atesto, para os devidos fins, que o(a) paciente acima esteve em atendimento nesta data e necessita de {d.Dias} dia{(d.Dias == 1 ? "" : "s")} de afastamento de suas atividades.",
        "comparecimento" => d.Texto.Length > 0 ? d.Texto : "Compareceu para atendimento nesta data.",
        _ => d.Texto,
    };

    /// <summary>O token do link: 32 bytes aleatórios em hexadecimal (64 caracteres), como a sessão do portal.</summary>
    public static string NovoToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>O link, sem o token no caminho nem na consulta: ele vai depois do <c>#</c>.</summary>
    public static string Link(string portal, string token) => portal.TrimEnd('/') + Pagina + "#" + token;

    /// <summary>A resposta com o link. Não leva nome completo: a mensagem fica no celular.</summary>
    public static string TextoDoLink(string tipo, string paciente, string link)
        => $"*{Rotulo(tipo)} em rascunho* para {RemarcacaoPeloPacienteRegras.PrimeiroNome(paciente)}.\n"
           + "Revise, emita e assine no portal (o link vale 15 minutos e abre uma vez só):\n"
           + link
           + "\n\nA senha do certificado é digitada só no portal, nunca aqui.";
}
