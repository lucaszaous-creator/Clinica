namespace Clinica.Domain.Regras;

/// <summary>Uma entrada do catálogo de convênios (dado de referência carregado do banco).</summary>
public sealed record EntradaConvenio(string Codigo, string Nome, Convenio Familia, bool Ativo);

/// <summary>
/// Cache em memória do catálogo de convênios, para servir NOME e FAMÍLIA de forma
/// síncrona a conversores/telas (que não podem consultar o banco de forma assíncrona).
///
/// - A FAMÍLIA (enum <see cref="Convenio"/>) continua definindo a REGRA de faturamento.
/// - O catálogo permite convênios adicionais (variantes) que reutilizam uma família existente,
///   com nome próprio e podendo ser ativados/desativados.
///
/// Populado no início do app e após salvar em Configurações. Enquanto vazio, tudo cai
/// nos padrões do código (os 4 convênios embutidos), então nada quebra.
/// </summary>
public static class CatalogoConvenios
{
    // Troca atômica de um dicionário imutável (leitura concorrente sem lock).
    private static volatile IReadOnlyDictionary<string, EntradaConvenio> _porCodigo =
        new Dictionary<string, EntradaConvenio>();

    /// <summary>Substitui o catálogo em cache (chamado após carregar/salvar).</summary>
    public static void Atualizar(IEnumerable<EntradaConvenio> entradas)
        => _porCodigo = entradas.ToDictionary(e => e.Codigo, StringComparer.OrdinalIgnoreCase);

    private static EntradaConvenio? Buscar(string? codigo)
        => codigo is not null && _porCodigo.TryGetValue(codigo, out var e) ? e : null;

    /// <summary>Nome exibido do convênio pelo código; cai no padrão da família se não estiver no catálogo.</summary>
    public static string Nome(string? codigo)
        => Buscar(codigo)?.Nome ?? ConvenioInfo.NomeExibicaoPadrao(Familia(codigo));

    /// <summary>Família (regra) do convênio pelo código; se desconhecido, tenta interpretar o código como o próprio enum.</summary>
    public static Convenio Familia(string? codigo)
    {
        if (Buscar(codigo) is { } e) return e.Familia;
        return Enum.TryParse<Convenio>(codigo, out var c) ? c : Convenio.UnimedPadrao;
    }

    /// <summary>Nome de um convênio embutido (pela família), refletindo rename salvo no catálogo.</summary>
    public static string Nome(Convenio familia) => Nome(familia.ToString());

    /// <summary>Convênios ativos (código + nome), ordenados por nome — para os combos de cadastro.</summary>
    public static IReadOnlyList<EntradaConvenio> Ativos
        => _porCodigo.Values.Where(e => e.Ativo).OrderBy(e => e.Nome).ToList();
}
