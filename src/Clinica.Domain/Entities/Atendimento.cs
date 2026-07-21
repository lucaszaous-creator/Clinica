namespace Clinica.Domain.Entities;

/// <summary>Uma visita do paciente em uma data. Ao ser lançado, gera os códigos de faturamento pela regra do convênio.</summary>
public class Atendimento
{
    public int Id { get; set; }

    /// <summary>Número/protocolo do atendimento (ex.: 2026-000123) — base do lastro de faturamento.</summary>
    public string? Numero { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public DateOnly Data { get; set; }

    public ModalidadeAtendimento Modalidade { get; set; }

    /// <summary>Código da modalidade no catálogo (identifica a variante/nome). Null = embutida = Modalidade.ToString().</summary>
    public string? ModalidadeCodigo { get; set; }

    /// <summary>Especialidade da consulta quando a modalidade é Consulta (discrimina para os relatórios).</summary>
    public Especialidade? EspecialidadeConsulta { get; set; }

    /// <summary>Código da especialidade no catálogo. Null = embutida = EspecialidadeConsulta.ToString().</summary>
    public string? EspecialidadeConsultaCodigo { get; set; }

    /// <summary>Categoria definida pela regra no momento do atendimento.</summary>
    public Categoria Categoria { get; set; }

    public string? Observacoes { get; set; }

    public List<CodigoFaturamento> Codigos { get; set; } = new();
}
