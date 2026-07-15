using Clinica.Domain.Entities;

namespace Clinica.Domain.Regras;

/// <summary>
/// Estratégia de faturamento por convênio. Uma implementação por fluxograma.
/// Recebe o paciente e o atendimento e devolve os códigos a faturar, já com
/// ordem, data prevista e forma de obtenção do 2º código.
/// </summary>
public interface IRegraConvenio
{
    Convenio Convenio { get; }

    ResultadoFaturamento Gerar(Paciente paciente, Atendimento atendimento, ContextoFaturamento contexto);
}
