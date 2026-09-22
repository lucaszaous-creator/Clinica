namespace Clinica.Domain.Entities;

/// <summary>Confirmação explícita do consumo da sessão, inclusive quando não houve material.</summary>
public sealed class ConferenciaConsumoProcedimento
{
    public int Id { get; set; }
    public int AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }
    public bool SemConsumo { get; set; }
    public string Pedido { get; set; } = string.Empty;
    public DateTime ConferidoEm { get; set; }
    public string ConferidoPor { get; set; } = string.Empty;
}
