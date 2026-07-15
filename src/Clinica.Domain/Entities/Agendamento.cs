namespace Clinica.Domain.Entities;

/// <summary>Situação de um agendamento na agenda da recepção.</summary>
public enum StatusAgendamento
{
    Agendado,
    Realizado, // presença confirmada; gerou atendimento
    Cancelado,
    Faltou
}

/// <summary>Como o agendamento nasceu.</summary>
public enum OrigemAgendamento
{
    Manual,          // marcado pela secretária
    RetornoSugerido  // sugerido automaticamente (retorno de 24h do 2º código)
}

/// <summary>
/// Um horário marcado para o paciente. Ao confirmar a presença, gera o atendimento
/// (e os códigos de faturamento) automaticamente.
/// </summary>
public class Agendamento
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public DateTime DataHora { get; set; }

    public ModalidadeAtendimento ModalidadePrevista { get; set; }

    public StatusAgendamento Status { get; set; } = StatusAgendamento.Agendado;

    public OrigemAgendamento Origem { get; set; } = OrigemAgendamento.Manual;

    public string? Observacoes { get; set; }

    /// <summary>Preenchido quando a presença é confirmada e um atendimento é gerado.</summary>
    public int? AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }
}
