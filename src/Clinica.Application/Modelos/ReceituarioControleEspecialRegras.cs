using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// As regras do RECEITUÁRIO DE CONTROLE ESPECIAL (nov/2026), num lugar só e com teste — as telas (Consultório,
/// gestão, aplicativos) e o PDF perguntam aqui.
///
/// Por que existe: a receita 2026/0484 (claritromicina + amoxicilina) foi recusada na farmácia — antimicrobiano e a
/// lista C1 da Portaria 344 exigem o receituário de controle especial, em duas vias, com o comprador identificado.
/// </summary>
public static class ReceituarioControleEspecialRegras
{
    /// <summary>
    /// Termos que SUGEREM emitir como controle especial: antimicrobianos (RDC 471/2021) e os da lista C1 mais comuns
    /// na clínica. É uma SUGESTÃO — quem decide é quem prescreve; a lista é curta de propósito, para não gritar à toa.
    /// A clínica revisa a lista (ela mora aqui, com teste); princípio ativo, sem acento, em minúsculas.
    /// </summary>
    public static readonly IReadOnlyList<string> Termos =
    [
        // Antimicrobianos
        "amoxicilina", "ampicilina", "azitromicina", "claritromicina", "eritromicina", "cefalexina", "cefadroxila",
        "cefuroxima", "ceftriaxona", "ciprofloxacino", "levofloxacino", "moxifloxacino", "norfloxacino",
        "doxiciclina", "tetraciclina", "minociclina", "sulfametoxazol", "trimetoprima", "nitrofurantoina",
        "metronidazol", "clindamicina", "fosfomicina", "secnidazol", "tinidazol", "linezolida", "penicilina",
        "benzilpenicilina", "amoxicilina + clavulanato", "clavulanato",
        // Lista C1 (Portaria 344) — antidepressivos, anticonvulsivantes e antipsicóticos de uso comum
        "amitriptilina", "nortriptilina", "imipramina", "clomipramina", "fluoxetina", "sertralina", "paroxetina",
        "citalopram", "escitalopram", "venlafaxina", "desvenlafaxina", "duloxetina", "bupropiona", "mirtazapina",
        "trazodona", "buspirona", "vortioxetina", "carbamazepina", "oxcarbazepina", "gabapentina", "pregabalina",
        "topiramato", "lamotrigina", "valproato", "acido valproico", "divalproato", "quetiapina", "risperidona",
        "olanzapina", "aripiprazol", "haloperidol", "ciclobenzaprina", "lítio", "litio", "carbonato de litio"
    ];

    /// <summary>Os termos da lista que aparecem no texto da receita (sem acento, palavra inteira).</summary>
    public static IReadOnlyList<string> TermosEncontrados(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return [];
        var normal = Normalizar(texto);
        return Termos.Select(Normalizar).Distinct()
            .Where(t => Regex.IsMatch(normal, $@"(?<![a-z0-9]){Regex.Escape(t)}(?![a-z0-9])"))
            .ToList();
    }

    /// <summary>Sugere emitir como controle especial (há pelo menos um termo da lista).</summary>
    public static bool Sugere(string? texto) => TermosEncontrados(texto).Count > 0;

    public static string FraseDaSugestao(IReadOnlyList<string> termos)
        => $"Esta receita tem {string.Join(", ", termos)}: a farmácia exige o Receituário de Controle Especial.";

    /// <summary>Um campo do COMPRADOR (o paciente) que o receituário exige e a ficha não tem.</summary>
    public sealed record CampoDoComprador(string Chave, string Rotulo);

    public static readonly CampoDoComprador Nome = new("nome", "Nome completo");
    public static readonly CampoDoComprador Rg = new("rg", "RG");
    public static readonly CampoDoComprador OrgaoEmissor = new("orgao", "Órgão emissor");
    public static readonly CampoDoComprador Endereco = new("endereco", "Endereço completo");
    public static readonly CampoDoComprador Cidade = new("cidade", "Cidade");
    public static readonly CampoDoComprador Uf = new("uf", "UF");
    public static readonly CampoDoComprador Telefone = new("telefone", "Telefone");

    /// <summary>O que falta na ficha para o bloco do comprador — a ordem é a do modelo do ITI.</summary>
    public static IReadOnlyList<CampoDoComprador> FaltasDoComprador(Paciente p)
    {
        var faltas = new List<CampoDoComprador>();
        if (string.IsNullOrWhiteSpace(p.Nome)) faltas.Add(Nome);
        if (string.IsNullOrWhiteSpace(p.Rg)) faltas.Add(Rg);
        if (string.IsNullOrWhiteSpace(p.RgOrgaoEmissor)) faltas.Add(OrgaoEmissor);
        if (string.IsNullOrWhiteSpace(p.Endereco)) faltas.Add(Endereco);
        if (string.IsNullOrWhiteSpace(p.Cidade)) faltas.Add(Cidade);
        if (!UfValida(p.Uf)) faltas.Add(Uf);
        if (string.IsNullOrWhiteSpace(p.Telefone)) faltas.Add(Telefone);
        return faltas;
    }

    /// <summary>As 27 UFs.</summary>
    public static readonly IReadOnlySet<string> Ufs = new HashSet<string>(StringComparer.Ordinal)
    {
        "AC","AL","AP","AM","BA","CE","DF","ES","GO","MA","MT","MS","MG","PA","PB","PR","PE","PI","RJ","RN","RS",
        "RO","RR","SC","SP","SE","TO"
    };

    public static bool UfValida(string? uf) => uf is { Length: 2 } && Ufs.Contains(uf.ToUpperInvariant());

    /// <summary>
    /// Cidade e UF DA CLÍNICA para o bloco do emitente. Vêm dos campos quando preenchidos; senão, do fim do endereço
    /// cadastrado ("RUA …, IMBETIBA,MACAÉ-RJ" → Macaé, RJ). Ninguém digita o emitente.
    /// </summary>
    public static (string? Cidade, string? Uf) CidadeUfDaClinica(DadosPrestador? prestador)
    {
        var cidade = prestador?.Cidade?.Trim(); var uf = prestador?.Uf?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(cidade) && UfValida(uf)) return (cidade, uf);
        var m = Regex.Match(prestador?.Endereco ?? "", @"[,\s]\s*([^,]+?)\s*[-/]\s*([A-Za-z]{2})\s*$");
        if (m.Success && UfValida(m.Groups[2].Value))
            return (string.IsNullOrWhiteSpace(cidade) ? TitleCase(m.Groups[1].Value) : cidade, m.Groups[2].Value.ToUpperInvariant());
        return (string.IsNullOrWhiteSpace(cidade) ? null : cidade, UfValida(uf) ? uf : null);
    }

    /// <summary>O endereço da clínica SEM a cidade-UF do fim (que vai nos campos próprios do emitente).</summary>
    public static string EnderecoSemCidade(string? endereco)
        => Regex.Replace((endereco ?? "").Trim(), @"\s*[,]\s*[^,]+?\s*[-/]\s*[A-Za-z]{2}\s*$", "").Trim(' ', ',');

    /// <summary>
    /// O campo da prescrição tem lugar para 15 linhas de ~92 caracteres. Mais que isso não cabe na via — e a tela
    /// diz antes de emitir (o documento emitido não se reescreve).
    /// </summary>
    public const int LinhasNaVia = 15;
    public const int CaracteresPorLinha = 92;

    public static int LinhasEstimadas(string? texto)
        => (texto ?? "").Replace("\r", "").Split('\n')
            .Sum(l => Math.Max(1, (int)Math.Ceiling(l.TrimEnd().Length / (double)CaracteresPorLinha)));

    public static string? ErroDeTamanho(string? texto)
        => LinhasEstimadas(texto) > LinhasNaVia
            ? $"A prescrição passa de {LinhasNaVia} linhas e não cabe no Receituário de Controle Especial. Divida em dois receituários."
            : null;

    private static string Normalizar(string texto)
    {
        var d = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(d.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    private static string TitleCase(string s)
        => CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(s.Trim().ToLower(CultureInfo.GetCultureInfo("pt-BR")));
}
