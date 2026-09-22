namespace Clinica.Domain.Entities;

/// <summary>Crédito devido pela adquirente. Não é uma nova receita nem uma dívida do paciente.</summary>
public class ParcelaRecebivelCartao
{
    public int Id { get; set; }
    public int LancamentoFinanceiroId { get; set; }
    public LancamentoFinanceiro Lancamento { get; set; } = null!;
    public int Numero { get; set; }
    public decimal Bruto { get; set; }
    public decimal Taxa { get; set; }
    public DateOnly Previsao { get; set; }
    public DateOnly? RecebidoEm { get; set; }
    public DateTime? ConciliadoEm { get; set; }
    public string? IdBancario { get; set; }
    public string? ContaBancaria { get; set; }
    public DateOnly? DataExtrato { get; set; }
    public DateOnly? RecebimentoAntesDaConciliacao { get; set; }
    public decimal Liquido => Bruto - Taxa;
}
