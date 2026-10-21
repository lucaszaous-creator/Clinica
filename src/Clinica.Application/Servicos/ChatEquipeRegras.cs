using System.Globalization;
using System.Text;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// As regras do chat da equipe (out/2026, mockup v3). Tudo o que decide o que a tela AFIRMA —
/// quem vê qual canal, quem foi citado, quando toca, o texto da prévia, onde a mensagem longa
/// é recolhida e quem está online — mora aqui, puro e testado; o componente só desenha.
/// </summary>
public static class ChatEquipeRegras
{
    public const string CanalGeral = "canal:geral";
    public const string CanalRecepcao = "canal:recepcao";
    public const string CanalEnfermagem = "canal:enfermagem";

    /// <summary>O mesmo teto do anexo de prontuário: o banco é remoto e o arquivo vai na linha.</summary>
    public const long TamanhoMaximoArquivo = 10L * 1024 * 1024;

    /// <summary>Quem não deu sinal há mais que isto está fora — a presença tem validade, não é apagada.</summary>
    public static readonly TimeSpan ValidadePresenca = TimeSpan.FromSeconds(90);

    /// <summary>"Está escrevendo…" vale por poucos segundos: quem parou de digitar some sozinho.</summary>
    public static readonly TimeSpan ValidadeEscrevendo = TimeSpan.FromSeconds(8);

    /// <summary>Acima disto a mensagem chega recolhida, com "Ver inteiro".</summary>
    public const int LimiteRecolher = 500;
    public const int LinhasRecolher = 8;

    public static IReadOnlyList<string> Canais { get; } = [CanalGeral, CanalRecepcao, CanalEnfermagem];

    /// <summary>
    /// O nome do arquivo sem a pasta de onde ele veio. Corta no último "\" OU "/", e não
    /// pelo <c>Path.GetFileName</c>: no Linux (web de leitura, testes) ele não reconhece "\",
    /// e o anexo vindo de um posto Windows guardaria "C:\laudos\laudo.pdf" inteiro.
    /// </summary>
    public static string NomeDoArquivo(string nome)
    {
        var limpo = (nome ?? "").Trim();
        var corte = limpo.LastIndexOfAny(['\\', '/']);
        if (corte >= 0) limpo = limpo[(corte + 1)..];
        return limpo.Length > 200 ? limpo[^200..] : limpo;
    }

    public static string NomeDoCanal(string conversa) => conversa switch
    {
        CanalGeral => "Geral",
        CanalRecepcao => "Recepção",
        CanalEnfermagem => "Enfermagem",
        _ => conversa,
    };

    public static string DescricaoDoCanal(string conversa) => conversa switch
    {
        CanalGeral => "Toda a equipe",
        CanalRecepcao => "Balcão, enfermagem e direção",
        CanalEnfermagem => "Enfermagem, quem atende e direção",
        _ => "",
    };

    public static bool EhCanal(string conversa) => Canais.Contains(conversa);

    /// <summary>A chave da conversa direta entre duas pessoas — menor id primeiro, então é a mesma dos dois lados.</summary>
    public static string Direta(int a, int b)
    {
        if (a == b) throw new InvalidOperationException("A conversa direta é entre duas pessoas diferentes.");
        return $"direta:{Math.Min(a, b)}:{Math.Max(a, b)}";
    }

    /// <summary>Os dois participantes de uma direta; nulo quando a chave não é de direta.</summary>
    public static (int A, int B)? ParticipantesDaDireta(string conversa)
    {
        var p = conversa.Split(':');
        if (p.Length != 3 || p[0] != "direta") return null;
        if (!int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out var a)
            || !int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out var b) || a >= b) return null;
        return (a, b);
    }

    /// <summary>Na direta, a outra pessoa; nulo quando não é direta ou quando eu não participo dela.</summary>
    public static int? OutroDaDireta(string conversa, int eu)
        => ParticipantesDaDireta(conversa) is { } p ? (p.A == eu ? p.B : p.B == eu ? p.A : null) : null;

    /// <summary>Quem participa de cada canal. # Geral é de todos; os outros seguem quem trabalha junto.</summary>
    public static bool ParticipaDoCanal(PerfilAcesso perfil, string canal) => canal switch
    {
        CanalGeral => true,
        CanalRecepcao => perfil is PerfilAcesso.Recepcao or PerfilAcesso.Enfermagem or PerfilAcesso.Gerente,
        CanalEnfermagem => perfil is PerfilAcesso.Enfermagem or PerfilAcesso.Profissional
            or PerfilAcesso.Psicologia or PerfilAcesso.Gerente,
        _ => false,
    };

    public static bool PodeVer(UsuarioSistema u, string conversa)
        => u.Ativo && (EhCanal(conversa) ? ParticipaDoCanal(u.Perfil, conversa) : OutroDaDireta(conversa, u.Id) is not null);

    public static IReadOnlyList<string> CanaisDe(UsuarioSistema u) => Canais.Where(c => ParticipaDoCanal(u.Perfil, c)).ToList();

    /// <summary>Rótulo curto da função ("enf.", "recepção"), que vai ao lado do nome.</summary>
    public static string RotuloDoPerfil(PerfilAcesso p) => p switch
    {
        PerfilAcesso.Recepcao => "recepção",
        PerfilAcesso.Profissional => "atendimento",
        PerfilAcesso.Financeiro => "financeiro",
        PerfilAcesso.Faturista => "faturamento",
        PerfilAcesso.Enfermagem => "enfermagem",
        PerfilAcesso.Gerente => "direção",
        PerfilAcesso.Psicologia => "psicologia",
        _ => "",
    };

    public static string Iniciais(string nome)
    {
        // Só palavras do NOME: "(fictício)", "Dra." e "·" não são de ninguém — "O(" no avatar
        // se lê como defeito, e "DD" para Dra. Helena Duarte esconde o H.
        var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetter(p[0]) && !p.EndsWith('.'))
            .Where(p => p.Length > 2 || char.IsUpper(p[0])).ToList();
        if (partes.Count == 0) return "?";
        var i = partes.Count == 1 ? partes[0][..Math.Min(2, partes[0].Length)]
            : $"{partes[0][0]}{partes[^1][0]}";
        return i.ToUpperInvariant();
    }

    /// <summary>
    /// Quem foi citado com @: o token depois do @ casa com o PRIMEIRO NOME (sem acento, sem caixa).
    /// Dois "Ana" na equipe tocam para as duas — citar não pode silenciar quem foi chamado.
    /// </summary>
    public static IReadOnlyList<int> Mencionados(string texto, IEnumerable<(int Id, string Nome)> pessoas)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] != '@' || (i > 0 && char.IsLetterOrDigit(texto[i - 1]))) continue;
            var j = i + 1;
            while (j < texto.Length && (char.IsLetter(texto[j]) || texto[j] == '-')) j++;
            if (j > i + 1) tokens.Add(Normalizar(texto[(i + 1)..j]));
        }
        if (tokens.Count == 0) return [];
        return pessoas.Where(p => tokens.Contains(Normalizar(p.Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "")))
            .Select(p => p.Id).Distinct().ToList();
    }

    public static string? GravarMencoes(IReadOnlyList<int> ids) => ids.Count == 0 ? null : "|" + string.Join('|', ids) + "|";

    public static bool Mencionou(string? mencoes, int usuarioId) => mencoes is not null && mencoes.Contains($"|{usuarioId}|", StringComparison.Ordinal);

    private static string Normalizar(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    /// <summary>A prévia da bolinha e da lista: a primeira linha, cortada; anexo sem texto diz o que é.</summary>
    public static string Previa(string texto, string? anexo = null, int limite = 90)
    {
        var t = string.Join(' ', (texto ?? "").Split(new[] { '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries));
        if (t.Length == 0) return anexo is null ? "" : $"[{anexo}]";
        return t.Length <= limite ? t : t[..CortarNaPalavra(t, limite)].TrimEnd() + "…";
    }

    /// <summary>A mensagem é longa o bastante para chegar recolhida?</summary>
    public static bool Recolhida(string texto)
        => texto.Length > LimiteRecolher || texto.Count(c => c == '\n') >= LinhasRecolher;

    /// <summary>O trecho mostrado da mensagem recolhida — cortado em palavra, nunca no meio dela.</summary>
    public static string Recorte(string texto)
    {
        if (!Recolhida(texto)) return texto;
        var linhas = texto.Split('\n');
        var t = linhas.Length > LinhasRecolher ? string.Join('\n', linhas.Take(LinhasRecolher - 2)) : texto;
        var limite = Math.Min(t.Length, LimiteRecolher - 100);
        return t[..CortarNaPalavra(t, limite)].TrimEnd() + "…";
    }

    private static int CortarNaPalavra(string t, int limite)
    {
        if (limite >= t.Length) return t.Length;
        var corte = t.LastIndexOf(' ', limite);
        return corte > limite / 2 ? corte : limite;
    }

    public static string Caracteres(int n) => $"{n.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"))} caractere{(n == 1 ? "" : "s")}";

    /// <summary>
    /// Se a mensagem toca para mim. Só a direta, a citação e o pedido da enfermagem tocam; canal só
    /// acende o número. Mensagem minha nunca toca, e silenciado não toca (o número continua subindo).
    /// </summary>
    public static SomChat Som(string conversa, int autorId, string? mencoes, int eu, PreferenciasSomChat prefs, DateTime agora)
    {
        if (autorId == eu || prefs.SilenciadoAte > agora) return SomChat.Nenhum;
        if (Mencionou(mencoes, eu) && prefs.Mencao) return SomChat.Mencao;
        if (OutroDaDireta(conversa, eu) is not null && prefs.Direta) return SomChat.Direta;
        return SomChat.Nenhum;
    }

    public static SomChat SomDoPedido(PreferenciasSomChat prefs, DateTime agora)
        => prefs.Pedido && !(prefs.SilenciadoAte > agora) ? SomChat.Pedido : SomChat.Nenhum;

    /// <summary>Presença: online, em atendimento (sala ocupada) ou fora — pela validade do último sinal.</summary>
    public static PresencaChat Presenca(DateTime? vistoEm, bool emAtendimento, DateTime agora)
    {
        if (vistoEm is null || agora - vistoEm.Value > ValidadePresenca) return PresencaChat.Fora;
        return emAtendimento ? PresencaChat.EmAtendimento : PresencaChat.Online;
    }

    public static string RotuloPresenca(PresencaChat p, DateTime? atendimentoAte) => p switch
    {
        PresencaChat.Online => "online",
        PresencaChat.EmAtendimento => atendimentoAte is { } ate ? $"online · em atendimento até {ate:HH\\:mm}" : "online · em atendimento",
        _ => "fora",
    };

    public static bool Escrevendo(PresencaEquipe? p, string conversa, DateTime agora)
        => p is not null && p.EscrevendoEm == conversa && p.EscrevendoAte > agora && agora - p.VistoEm <= ValidadePresenca;

    /// <summary>
    /// "Lida por": na direta, "Lida" quando a outra pessoa leu; no canal, quantos além de mim leram.
    /// Nulo quando ninguém leu ainda — a tela não diz "Lida por 0".
    /// </summary>
    public static string? LidaPor(int mensagemId, string conversa, int eu, IReadOnlyList<(int UsuarioId, int UltimaLidaId)> leituras)
    {
        var leram = leituras.Count(l => l.UsuarioId != eu && l.UltimaLidaId >= mensagemId);
        if (leram == 0) return null;
        return OutroDaDireta(conversa, eu) is not null ? "Lida" : $"Lida por {leram}";
    }

    /// <summary>O dia como separador da conversa: "Hoje", "Ontem" ou a data.</summary>
    public static string Dia(DateOnly dia, DateOnly hoje)
        => dia == hoje ? "Hoje" : dia == hoje.AddDays(-1) ? "Ontem" : dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Tamanho(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB".Replace('.', ','),
        _ => (bytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + " MB",
    };
}

public enum SomChat { Nenhum, Direta, Mencao, Pedido }

public enum PresencaChat { Fora, Online, EmAtendimento }

/// <summary>As preferências de som de UMA pessoa, neste computador.</summary>
public sealed record PreferenciasSomChat(bool Direta = true, bool Mencao = true, bool Pedido = true, DateTime? SilenciadoAte = null);
