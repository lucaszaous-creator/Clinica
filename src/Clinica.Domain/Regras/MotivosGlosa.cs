namespace Clinica.Domain.Regras;

/// <summary>Um motivo de glosa padronizado (tabela de tipos de glosa do padrão TISS/ANS).</summary>
public sealed record MotivoGlosa(string Codigo, string Descricao)
{
    public override string ToString() => $"{Codigo} — {Descricao}";
}

/// <summary>
/// Catálogo dos motivos de glosa mais comuns da tabela de domínio do TISS (ANS).
/// Padroniza o registro (antes era texto livre) e permite estatística por motivo.
/// O código vai no XML de recurso de glosa; a descrição, nas telas e relatórios.
/// </summary>
public static class MotivosGlosa
{
    public const string CodigoOutros = "9999";

    /// <summary>Subconjunto prático da tabela ANS, na ordem em que aparece nos combos.</summary>
    public static readonly IReadOnlyList<MotivoGlosa> Todos = new List<MotivoGlosa>
    {
        new("1201", "Carteirinha inválida ou vencida"),
        new("1202", "Beneficiário não identificado"),
        new("1205", "Beneficiário com cobertura suspensa"),
        new("1302", "Guia sem autorização prévia"),
        new("1304", "Senha de autorização inválida/vencida"),
        new("1705", "Cobrança em duplicidade"),
        new("1802", "Data de atendimento divergente da autorizada"),
        new("1810", "Guia com preenchimento incompleto/incorreto"),
        new("2001", "Procedimento não coberto pelo contrato"),
        new("2002", "Procedimento não autorizado"),
        new("2006", "Quantidade executada acima da autorizada"),
        new("2018", "Prazo de validade da guia expirado"),
        new(CodigoOutros, "Outro motivo (detalhar na observação)")
    };

    /// <summary>Descrição do motivo pelo código (o próprio código se não catalogado).</summary>
    public static string Descricao(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return string.Empty;
        return Todos.FirstOrDefault(m => m.Codigo == codigo)?.Descricao ?? codigo;
    }
}
