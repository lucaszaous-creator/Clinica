using Clinica.Domain;

namespace Clinica.Application.Modelos;

/// <summary>A projeção da receita por atendimento — ver <c>IClinicaRepositorio.ReceitaPorAtendimentoAsync</c>.</summary>
public sealed record ReceitaDeAtendimento(
    decimal Valor, DateOnly Dia, int AtendimentoId, int PacienteId,
    ModalidadeAtendimento Modalidade, string? ModalidadeCodigo, string? EspecialidadeCodigo, int? ProfissionalId);
