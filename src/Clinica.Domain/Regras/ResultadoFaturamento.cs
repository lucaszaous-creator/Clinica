using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>Resultado da aplicação de uma regra de convênio a um atendimento.</summary>
public sealed class ResultadoFaturamento
{
    public List<CodigoFaturamento> Codigos { get; } = new();

    public Categoria Categoria { get; set; }

    /// <summary>Instruções/alertas para a secretária (ex.: "ligar para o paciente após 24h").</summary>
    public List<string> Avisos { get; } = new();
}
