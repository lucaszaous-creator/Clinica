namespace Clinica.Domain.Entities;

/// <summary>Recibo de uma etapa do fechamento. Gravado na mesma transação do efeito.</summary>
public sealed class EtapaFechamentoSessao
{
    public int AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }
    public string Etapa { get; set; } = string.Empty;
    public string Pedido { get; set; } = string.Empty;
    public int ResultadoId { get; set; }
}
