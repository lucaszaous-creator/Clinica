namespace Clinica.Domain.Regras;

/// <summary>Uma entrada do catálogo de especialidades (dado de referência carregado do banco).</summary>
public sealed record EntradaEspecialidade(string Codigo, string Nome, bool Ativo);

/// <summary>
/// Cache em memória do catálogo de especialidades de consulta. A especialidade é um rótulo
/// (discrimina a consulta nos relatórios); apenas as embutidas participam da rotação da
/// Petrobras, identificadas pelo enum <see cref="Especialidade"/> quando o código coincide.
/// Populado no início do app e após salvar em Configurações; vazio, cai nos padrões do enum.
/// </summary>
public static class CatalogoEspecialidades
{
    private static volatile IReadOnlyDictionary<string, EntradaEspecialidade> _porCodigo =
        new Dictionary<string, EntradaEspecialidade>();

    /// <summary>Substitui o catálogo em cache (chamado após carregar/salvar).</summary>
    public static void Atualizar(IEnumerable<EntradaEspecialidade> entradas)
        => _porCodigo = entradas.ToDictionary(e => e.Codigo, StringComparer.OrdinalIgnoreCase);

    private static EntradaEspecialidade? Buscar(string? codigo)
        => codigo is not null && _porCodigo.TryGetValue(codigo, out var e) ? e : null;

    /// <summary>Nome exibido pelo código; cai no padrão do enum (ou no próprio código) se não estiver no catálogo.</summary>
    public static string Nome(string? codigo)
    {
        if (Buscar(codigo) is { } e) return e.Nome;
        if (Enum.TryParse<Especialidade>(codigo, out var esp)) return EspecialidadeInfo.NomeExibicao(esp);
        return codigo ?? string.Empty;
    }

    /// <summary>Enum correspondente quando o código é uma especialidade embutida (para rotação/relatórios legados).</summary>
    public static Especialidade? BaseEnum(string? codigo)
        => Enum.TryParse<Especialidade>(codigo, out var esp) ? esp : null;

    /// <summary>Especialidades ativas (código + nome), ordenadas por nome — para os combos.</summary>
    public static IReadOnlyList<EntradaEspecialidade> Ativas
        => _porCodigo.Values.Where(e => e.Ativo).OrderBy(e => e.Nome).ToList();
}
