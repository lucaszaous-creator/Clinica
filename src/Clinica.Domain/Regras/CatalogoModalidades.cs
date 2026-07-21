namespace Clinica.Domain.Regras;

/// <summary>Uma entrada do catálogo de modalidades (dado de referência carregado do banco).</summary>
public sealed record EntradaModalidade(string Codigo, string Nome, ModalidadeAtendimento Base, bool Ativo);

/// <summary>
/// Cache em memória do catálogo de modalidades, para servir NOME e BASE de forma síncrona
/// a conversores/telas (que não podem consultar o banco de forma assíncrona).
///
/// - A BASE (enum <see cref="ModalidadeAtendimento"/>) continua definindo o comportamento
///   no motor de regras (quais códigos são gerados).
/// - O catálogo permite modalidades adicionais (variantes) que reutilizam uma base existente,
///   com nome próprio e podendo ser ativadas/desativadas.
///
/// Populado no início do app e após salvar em Configurações. Enquanto vazio, tudo cai
/// nos padrões do código (as modalidades embutidas), então nada quebra.
/// </summary>
public static class CatalogoModalidades
{
    // Troca atômica de um dicionário imutável (leitura concorrente sem lock).
    private static volatile IReadOnlyDictionary<string, EntradaModalidade> _porCodigo =
        new Dictionary<string, EntradaModalidade>();

    /// <summary>Substitui o catálogo em cache (chamado após carregar/salvar).</summary>
    public static void Atualizar(IEnumerable<EntradaModalidade> entradas)
        => _porCodigo = entradas.ToDictionary(e => e.Codigo, StringComparer.OrdinalIgnoreCase);

    private static EntradaModalidade? Buscar(string? codigo)
        => codigo is not null && _porCodigo.TryGetValue(codigo, out var e) ? e : null;

    /// <summary>Nome exibido da modalidade pelo código; cai no padrão da base se não estiver no catálogo.</summary>
    public static string Nome(string? codigo)
        => Buscar(codigo)?.Nome ?? ModalidadeInfo.NomeExibicao(Base(codigo));

    /// <summary>Base (comportamento) da modalidade pelo código; se desconhecido, tenta interpretar o código como o próprio enum.</summary>
    public static ModalidadeAtendimento Base(string? codigo)
    {
        if (Buscar(codigo) is { } e) return e.Base;
        return Enum.TryParse<ModalidadeAtendimento>(codigo, out var m) ? m : ModalidadeAtendimento.AcupunturaComEletro;
    }

    /// <summary>Modalidades ativas (código + nome), na ordem do enum base e depois por nome — para os combos.</summary>
    public static IReadOnlyList<EntradaModalidade> Ativas
        => _porCodigo.Values.Where(e => e.Ativo).OrderBy(e => e.Base).ThenBy(e => e.Nome).ToList();
}
