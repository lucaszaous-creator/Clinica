namespace Clinica.Domain.Entities;

/// <summary>Ciclo de vida de uma consulta autorizada.</summary>
public enum StatusConsulta
{
    Ativa,     // vigente
    Vencida,   // passou da validade sem renovação
    Renovada   // substituída por uma nova consulta
}

/// <summary>
/// Consulta autorizada do paciente (cobre laudos, receitas e dúvidas — 22 dias Unimed / 30 dias Amil e Petrobras).
/// Renovável: ao vencer, é preciso gerar uma nova. A validade vem dos Parâmetros do convênio.
/// FATURAMENTO apenas — não há valor/recebível.
/// </summary>
public class Consulta
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Convênio no momento da emissão (a validade depende dele).</summary>
    public Convenio Convenio { get; set; }

    /// <summary>Data em que a consulta foi emitida/autorizada.</summary>
    public DateOnly DataEmissao { get; set; }

    /// <summary>Dias de validade aplicados (dos Parâmetros do convênio na emissão).</summary>
    public int ValidadeDias { get; set; }

    /// <summary>Data de vencimento (emissão + validade).</summary>
    public DateOnly DataVencimento { get; set; }

    public StatusConsulta Status { get; set; } = StatusConsulta.Ativa;

    public string? Observacoes { get; set; }

    /// <summary>Está vencida na data de referência.</summary>
    public bool EstaVencida(DateOnly referencia) => referencia > DataVencimento;

    /// <summary>Dias até o vencimento (negativo = já venceu).</summary>
    public int DiasParaVencer(DateOnly referencia) => DataVencimento.DayNumber - referencia.DayNumber;
}
