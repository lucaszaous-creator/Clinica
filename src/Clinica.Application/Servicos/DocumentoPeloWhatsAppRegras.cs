using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Clinica.Application.Servicos;

/// <summary>Um documento pedido numa linha ("atestado Maria Silva 2 dias").</summary>
/// <param name="Tipo">O tipo do tablet: atestado, comparecimento, receita, exame ou relatorio.</param>
/// <param name="Nome">A paciente citada (sem acento, minúsculo).</param>
/// <param name="Dias">Os dias de afastamento do atestado; nulo quando não vieram.</param>
/// <param name="Texto">O que veio depois dos dois-pontos (receita, exame, relatório), como foi escrito.</param>
/// <param name="Cpf">O CPF citado (só os 11 dígitos), quando veio; acha a paciente sem depender do nome.</param>
public sealed record DocumentoPorTexto(string Tipo, string Nome, int? Dias, string Texto, string? Cpf = null);

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
        ("receita", ["receita", "receituario", "prescricao"]),
        ("exame", ["pedido de exames", "pedido de exame", "exames", "exame"]),
        ("relatorio", ["relatorio medico", "relatorio", "laudo"]),
    ];

    /// <summary>O que vem antes do tipo e não muda o pedido ("por favor, emite um atestado…").</summary>
    private static readonly string[] Preambulos =
        ["por favor", "pf", "pfv", "emitir", "emite", "emita", "gerar", "gera", "gere", "fazer", "faz", "faca",
         "preciso de", "preciso", "quero", "um", "uma", "o", "a"];

    /// <summary>Palavras que não são do nome da paciente.</summary>
    private static readonly HashSet<string> ForaDoNome =
        ["a", "o", "da", "do", "de", "para", "pra", "pro", "paciente", "sra", "sr", "dona", "seu", "senhora", "senhor",
         "hoje", "dia", "dias", "e", "com", "por", "afastamento", "cpf"];

    /// <summary>O CPF na frase, com ou sem pontuação ("104.009.757-00", "10400975700").</summary>
    [GeneratedRegex(@"(?<!\d)(\d{3})\.?(\d{3})\.?(\d{3})[-.]?(\d{2})(?!\d)")]
    private static partial Regex CpfRegex();

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
        var cabecaOriginal = doisPontos >= 0 ? original[..doisPontos] : original;
        // O CPF sai da cabeça ANTES da leitura dos dias: "atestado 104.009.757-00 2 dias" não pode
        // virar 757 dias nem deixar dígitos no nome.
        string? cpf = null;
        if (CpfRegex().Match(cabecaOriginal) is { Success: true } mc)
        {
            cpf = string.Concat(mc.Groups[1].Value, mc.Groups[2].Value, mc.Groups[3].Value, mc.Groups[4].Value);
            cabecaOriginal = cabecaOriginal.Remove(mc.Index, mc.Length);
        }
        var cabeca = ChatEquipeRegras.TextoDeBusca(cabecaOriginal);
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
            return new DocumentoPorTexto(tipo, string.Join(' ', nome), dias, corpo.Length > TamanhoMaximoDoTexto ? corpo[..TamanhoMaximoDoTexto] : corpo, cpf);
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
        if (d.Cpf is { } cpf && !Clinica.Domain.Cpf.Valido(cpf))
            return "Esse CPF não confere (dígito verificador). Confira os números, ou mande pelo nome.";
        if (d.Nome.Length == 0 && d.Cpf is null)
            return $"Diga de quem é o documento — pelo nome ou pelo CPF —, por exemplo: \"{Exemplo(d.Tipo)}\".";
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

/// <summary>
/// O documento pedido ao @SemDor no chat dos apps (out/2026, decisão da direção de 23/10): a frase é a
/// mesma do WhatsApp, mas o documento nasce na hora e volta anexado à conversa (a 2ª via em PDF).
/// </summary>
public static class DocumentoPeloChatRegras
{
    public const string SoQuemPrescreve = "Emitir documento pelo @SemDor é para quem prescreve, com o cadastro de profissional ligado ao login.";

    /// <summary>No canal, o documento vai para a conversa de quem pediu com o sistema: ele tem dado de saúde.</summary>
    public static string EmParticular(string quemPediu)
        => $"Emiti o documento pedido por {quemPediu} na conversa com o {ChatEquipeRegras.NomeDoSistema}: documento de paciente não fica à vista no canal.";

    /// <summary>O tipo do documento clínico para o tipo lido na frase.</summary>
    public static Clinica.Domain.Entities.TipoDocumentoClinico Tipo(string tipo) => tipo switch
    {
        "atestado" => Clinica.Domain.Entities.TipoDocumentoClinico.Atestado,
        "comparecimento" => Clinica.Domain.Entities.TipoDocumentoClinico.Comparecimento,
        "receita" => Clinica.Domain.Entities.TipoDocumentoClinico.Receita,
        "exame" => Clinica.Domain.Entities.TipoDocumentoClinico.PedidoExame,
        "relatorio" => Clinica.Domain.Entities.TipoDocumentoClinico.RelatorioEvolucao,
        _ => throw new InvalidOperationException("Tipo de documento desconhecido."),
    };

    /// <summary>A resposta com o documento anexado. Diz que é sem assinatura digital — garantia que o código não dá não se promete.</summary>
    public static string Emitido(Clinica.Domain.Entities.TipoDocumentoClinico tipo, string numero, string paciente)
        => $"{Clinica.Domain.Entities.TipoDocumentoInfo.Rotular(tipo)} nº {numero} emitido para {paciente}. Abra a 2ª via (PDF) no anexo abaixo para imprimir ou enviar. "
           + "Saiu sem assinatura digital: imprima e assine, ou assine com o certificado na central de Documentos.";

    /// <summary>Nenhuma paciente da agenda de quem pediu bate com o nome ou o CPF.</summary>
    public static string NaoAchei(DocumentoPorTexto d)
        => d.Cpf is not null
            ? "Não achei esse CPF entre as pacientes da sua agenda (60 dias para trás e 30 para a frente). Confira os números; o documento por aqui é só de paciente seu."
            : $"Não achei \"{d.Nome}\" na sua agenda (60 dias para trás e 30 para a frente). Confira o nome ou mande com o CPF; o documento por aqui é só de paciente seu.";

    /// <summary>Mais de uma paciente bate: a lista e como desempatar.</summary>
    public static string Varias(DocumentoPorTexto d, IEnumerable<(string Nome, DateTime Quando)> pacientes)
    {
        var lista = pacientes.ToList();
        var sb = new System.Text.StringBuilder($"Achei {lista.Count} pacientes com \"{d.Nome}\" na sua agenda. Mande de novo com o nome completo ou o CPF:");
        foreach (var (nome, quando) in lista.Take(8)) sb.Append($"\n• {nome} ({quando:dd/MM})");
        return sb.ToString();
    }
}
