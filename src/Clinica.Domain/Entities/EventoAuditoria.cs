namespace Clinica.Domain.Entities;

/// <summary>
/// Trilha de auditoria: registra QUEM fez O QUÊ e QUANDO nas ações que alteram o
/// faturamento (baixa, estorno, glosa, lotes). Responde "quem deu baixa nessa guia?"
/// sem depender da memória de ninguém — e é exigência básica de LGPD para dado de saúde.
/// Registros são só-escrita: nunca editados nem apagados pelo app.
/// </summary>
public class EventoAuditoria
{
    public int Id { get; set; }

    /// <summary>Momento do evento (hora de parede da máquina que executou a ação).</summary>
    public DateTime DataHora { get; set; } = DateTime.Now;

    /// <summary>Quem executou (usuário do Windows da máquina; "?" quando não informado).</summary>
    public string Operador { get; set; } = "?";

    /// <summary>Ação executada (ex.: "BaixaGuia", "EstornoBaixa", "Glosa", "LoteCriado").</summary>
    public string Acao { get; set; } = string.Empty;

    /// <summary>Contexto legível do evento (nº da guia, motivo, lote etc.).</summary>
    public string? Detalhe { get; set; }

    // Referências opcionais para filtrar a trilha por guia/lote/paciente.
    public int? CodigoId { get; set; }
    public int? LoteTissId { get; set; }
    public int? PacienteId { get; set; }
}
