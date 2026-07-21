namespace Clinica.Domain.Entities;

/// <summary>
/// Catálogo de modalidades de atendimento selecionáveis no lançamento (dado, não código).
/// Inclui as modalidades embutidas (semeadas) e permite adicionar variantes que reutilizam o
/// COMPORTAMENTO de uma modalidade existente no motor de regras (<see cref="Base"/>), com nome
/// próprio e ativação independente — ex.: "Auriculoterapia" que fatura como "Acupuntura (apenas)".
/// </summary>
public class ModalidadeCadastro
{
    /// <summary>Código único (chave). Para as embutidas, é o nome do enum (ex.: "AcupunturaSimples").</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Nome exibido nas telas e documentos.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Modalidade embutida cujo comportamento esta entrada usa no motor de regras.</summary>
    public ModalidadeAtendimento Base { get; set; }

    /// <summary>Disponível para novos lançamentos. Inativa some das listas; o histórico é preservado.</summary>
    public bool Ativo { get; set; } = true;
}
