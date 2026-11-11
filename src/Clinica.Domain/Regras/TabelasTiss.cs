using System.Text.RegularExpressions;

namespace Clinica.Domain.Regras;

/// <summary>O registro do conselho lido para o TISS: código do conselho, número e código IBGE da UF.</summary>
public sealed record ConselhoTiss(string Conselho, string Numero, string Uf);

/// <summary>
/// As tabelas de domínio do padrão TISS 4.01.00 que o lote usa (nov/2026): conselho
/// profissional, UF e CBO. Os valores são os do esquema oficial da ANS
/// (<c>tissSimpleTypesV4_01_00.xsd</c>: <c>dm_conselhoProfissional</c>, <c>dm_UF</c>, <c>dm_CBOS</c>) —
/// e a rede que prova isso é o teste que valida o XML gerado contra esse esquema.
/// </summary>
public static partial class TabelasTiss
{
    /// <summary>CBO "desconhecido ou não informado" do esquema — sai quando nada melhor é conhecido.</summary>
    public const string CbosNaoInformado = "999999";

    /// <summary>CNES de quem não tem CNES, como o padrão TISS manda preencher.</summary>
    public const string CnesNaoInformado = "9999999";

    private static readonly Dictionary<string, string> Conselhos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CRESS"] = "01", ["COREN"] = "02", ["CRF"] = "03", ["CRFA"] = "04", ["CREFITO"] = "05",
        ["CRM"] = "06", ["CRN"] = "07", ["CRO"] = "08", ["CRP"] = "09",
        ["CRBIO"] = "11", ["CRBM"] = "12", ["CREF"] = "13", ["CRMV"] = "14", ["CRTR"] = "15"
    };

    private static readonly Dictionary<string, string> Ufs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RO"] = "11", ["AC"] = "12", ["AM"] = "13", ["RR"] = "14", ["PA"] = "15", ["AP"] = "16", ["TO"] = "17",
        ["MA"] = "21", ["PI"] = "22", ["CE"] = "23", ["RN"] = "24", ["PB"] = "25", ["PE"] = "26", ["AL"] = "27",
        ["SE"] = "28", ["BA"] = "29", ["MG"] = "31", ["ES"] = "32", ["RJ"] = "33", ["SP"] = "35",
        ["PR"] = "41", ["SC"] = "42", ["RS"] = "43", ["MS"] = "50", ["MT"] = "51", ["GO"] = "52", ["DF"] = "53"
    };

    /// <summary>O CBO que a especialidade da casa costuma ter. Só as que têm resposta única.</summary>
    private static readonly Dictionary<string, string> CbosDaEspecialidade = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Acupuntura"] = "225105",
        ["Psiquiatria"] = "225133",
        ["Geriatria"] = "225180",
        ["Ginecologia"] = "225250",
        ["Endocrinologia"] = "225155",
        ["Psicologia"] = "251510"
    };

    /// <summary>Código IBGE da UF pela sigla ("RJ" → "33"), ou nulo.</summary>
    public static string? Uf(string? sigla)
        => sigla is not null && Ufs.TryGetValue(sigla.Trim(), out var codigo) ? codigo : null;

    /// <summary>
    /// Lê o registro do conselho como a clínica o cadastra em Equipe ("CRM-RJ 123456",
    /// "CRM/RJ 52.12345-6", "CRP 05/12345", "CRM 123456 RJ"). Nulo quando falta o conselho, o
    /// número ou a UF — e a exportação avisa, nomeando o profissional, em vez de adivinhar.
    /// </summary>
    public static ConselhoTiss? LerConselho(string? registro)
    {
        if (string.IsNullOrWhiteSpace(registro)) return null;
        var texto = registro.Trim().ToUpperInvariant();

        var sigla = SiglaDoConselho().Match(texto);
        if (!sigla.Success || !Conselhos.TryGetValue(sigla.Value, out var conselho)) return null;
        var resto = texto[(sigla.Index + sigla.Length)..];

        // A UF é a primeira sigla de estado que aparecer depois do conselho.
        string? uf = null;
        foreach (Match m in DuasLetras().Matches(resto))
            if (Ufs.TryGetValue(m.Value, out var codigo)) { uf = codigo; resto = resto.Remove(m.Index, m.Length); break; }

        // O número: os dígitos do maior bloco numérico (pontos e traço são formatação). O CRP
        // escreve a região antes da barra ("05/12345") — o bloco maior é a inscrição.
        var numero = BlocoNumerico().Matches(resto)
            .Select(m => Digitos().Replace(m.Value, string.Empty))
            .OrderByDescending(d => d.Length)
            .FirstOrDefault();

        return uf is null || string.IsNullOrEmpty(numero) || numero.Length > 15 ? null : new ConselhoTiss(conselho, numero, uf);
    }

    /// <summary>
    /// O CBO do profissional numa guia: o cadastrado em Equipe; sem ele, o da especialidade
    /// do profissional; sem ele, o da especialidade da guia. Nulo = não há como saber.
    /// </summary>
    public static string? Cbos(string? cadastrado, string? especialidadeDoProfissional, string? especialidadeDaGuia)
    {
        var limpo = Digitos().Replace(cadastrado ?? string.Empty, string.Empty);
        if (limpo.Length == 6) return limpo;
        if (especialidadeDoProfissional is not null && CbosDaEspecialidade.TryGetValue(especialidadeDoProfissional, out var a)) return a;
        if (especialidadeDaGuia is not null && CbosDaEspecialidade.TryGetValue(especialidadeDaGuia, out var b)) return b;
        return null;
    }

    [GeneratedRegex("CREFITO|CRESS|COREN|CRBIO|CRFA|CRBM|CREF|CRMV|CRTR|CRM|CRN|CRO|CRP|CRF")]
    private static partial Regex SiglaDoConselho();

    [GeneratedRegex(@"(?<![A-Z])[A-Z]{2}(?![A-Z])")]
    private static partial Regex DuasLetras();

    [GeneratedRegex(@"\d[\d.\-]*")]
    private static partial Regex BlocoNumerico();

    [GeneratedRegex(@"\D")]
    private static partial Regex Digitos();
}
