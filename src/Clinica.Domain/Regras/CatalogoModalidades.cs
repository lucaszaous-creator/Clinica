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

    /// <summary>
    /// Nome exibido quando se tem o código E a família: o CÓDIGO vence, a família é o
    /// caminho de baixo. É o mesmo desenho de <c>CatalogoConvenios.Nome(codigo, familia)</c>,
    /// e existe pela mesma razão — sem ele cada tela improvisa.
    ///
    /// ⚠️ Escrever <c>Nome(codigo ?? familia.ToString())</c> NÃO funciona, e a armadilha já
    /// custou uma tela inteira (parcela 67): quem grava esses códigos normaliza "vale para a
    /// família" como STRING VAZIA, nunca nulo — porque <c>NULL</c> não é único no PostgreSQL.
    /// O <c>??</c> não dispara com string vazia, o catálogo não a encontra,
    /// <c>Enum.TryParse("")</c> falha, e o caminho termina no literal de fallback do
    /// <see cref="Base"/>: toda linha de família aparecia escrita "Acupuntura +
    /// eletroacupuntura".
    /// </summary>
    public static string Nome(string? codigo, ModalidadeAtendimento familia)
        => string.IsNullOrWhiteSpace(codigo)
            ? ModalidadeInfo.NomeExibicao(familia)
            : Nome(codigo);

    /// <summary>
    /// O QUE A SESSÃO É, em uma frase: a modalidade e — só quando ela é CONSULTA — a
    /// especialidade ao lado ("Consulta · Psiquiatria").
    ///
    /// A especialidade da consulta é gravada desde sempre (é ela que a operadora cobra na
    /// guia, e é por ela que a Consulta de guias filtra), e NENHUMA tela de quem atende a
    /// lia: o "Meu dia" do médico, a "Minha semana" e a agenda do balcão escreviam
    /// "Consulta" para a de psiquiatria e para a de geriatria. Dado gravado sem leitor, no
    /// lugar em que a diferença muda quem senta na cadeira.
    ///
    /// ⚠️ Ela só entra quando a modalidade é consulta: nas outras a especialidade não tem
    /// onde morar (<c>RemarcarAsync</c> a limpa), e escrevê-la ali seria afirmar sobre a
    /// sessão algo que o horário não guarda.
    ///
    /// A composição mora AQUI, e não em cada tela, pela razão de sempre: são quatro
    /// leitores (a lista do dia e a grade do balcão, o dia e a semana do médico), e quatro
    /// frases divergem na primeira correção.
    /// </summary>
    public static string NomeComEspecialidade(
        string? codigo, ModalidadeAtendimento familia, string? especialidadeCodigo)
    {
        var nome = Nome(codigo, familia);

        // A família sai do MESMO caminho do nome — código quando há, família como caminho
        // de baixo. `Base("")` cairia no padrão dele (acupuntura com eletro) e diria que a
        // consulta não é consulta.
        var baseDaModalidade = string.IsNullOrWhiteSpace(codigo) ? familia : Base(codigo);
        if (baseDaModalidade != ModalidadeAtendimento.Consulta) return nome;
        if (string.IsNullOrWhiteSpace(especialidadeCodigo)) return nome;

        var especialidade = CatalogoEspecialidades.Nome(especialidadeCodigo);
        return string.IsNullOrWhiteSpace(especialidade) ? nome : $"{nome} · {especialidade}";
    }

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
