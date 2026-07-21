namespace Clinica.Domain.Entities;

/// <summary>
/// Catálogo de especialidades de consulta selecionáveis na consulta avulsa (dado, não código).
/// Inclui as embutidas (semeadas) e permite adicionar novas — a especialidade é um rótulo que
/// discrimina a consulta nos relatórios. A rotação da Petrobras continua usando apenas as
/// especialidades embutidas dela (Psiquiatria/Geriatria/Ginecologia), que não podem ser excluídas.
/// </summary>
public class EspecialidadeCadastro
{
    /// <summary>Código único (chave). Para as embutidas, é o nome do enum (ex.: "ClinicaDaDor").</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Nome exibido nas telas e relatórios.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Disponível para novos lançamentos. Inativa some das listas; o histórico é preservado.</summary>
    public bool Ativo { get; set; } = true;
}
