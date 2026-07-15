using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// Contexto histórico necessário para algumas regras (ex.: Petrobras precisa saber quais
/// especialidades já foram usadas no mês para respeitar o limite de 1 por especialidade/mês).
/// </summary>
public sealed class ContextoFaturamento
{
    /// <summary>Códigos já lançados para o paciente no mesmo mês do atendimento.</summary>
    public IReadOnlyList<CodigoFaturamento> CodigosNoMes { get; init; } = new List<CodigoFaturamento>();

    public static ContextoFaturamento Vazio { get; } = new();
}
