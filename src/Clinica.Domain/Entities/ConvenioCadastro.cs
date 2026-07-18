namespace Clinica.Domain.Entities;

/// <summary>
/// Catálogo de convênios selecionáveis no cadastro (dado, não código). Inclui os 4
/// convênios embutidos (semeados) e permite adicionar variantes que reutilizam a
/// REGRA de faturamento de uma família existente (<see cref="Familia"/>), com nome
/// próprio e ativação independente. A lógica de faturamento permanece no código.
/// </summary>
public class ConvenioCadastro
{
    /// <summary>Código único (chave). Para os embutidos, é o nome da família (ex.: "Amil").</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Nome exibido nas telas e documentos.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Família de regra de faturamento que este convênio usa.</summary>
    public Convenio Familia { get; set; }

    /// <summary>Disponível para novos cadastros. Inativo some das listas; o histórico é preservado.</summary>
    public bool Ativo { get; set; } = true;
}
