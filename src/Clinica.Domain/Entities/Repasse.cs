namespace Clinica.Domain.Entities;

/// <summary>Como o repasse do profissional é calculado.</summary>
public enum BaseRepasse
{
    /// <summary>Percentual sobre a receita efetivamente recebida dos atendimentos dele.</summary>
    PercentualDaReceita,

    /// <summary>Valor fixo por atendimento realizado, independente do que entrou.</summary>
    ValorPorAtendimento
}

/// <summary>
/// Quanto a clínica repassa a um profissional. Fecha a metade que faltava da feature 09.
///
/// A regra tem vigência porque acordo muda: subir o percentual em setembro não pode
/// reescrever o repasse de agosto, que já foi pago. Apuração antiga continua olhando a
/// regra que valia no período.
/// </summary>
public class RegraRepasse
{
    public int Id { get; set; }

    public int ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    public BaseRepasse Base { get; set; } = BaseRepasse.PercentualDaReceita;

    /// <summary>Percentual de 0 a 100. Usado quando a base é percentual da receita.</summary>
    public decimal? Percentual { get; set; }

    /// <summary>Valor fixo por atendimento. Usado quando a base é por atendimento.</summary>
    public decimal? ValorPorAtendimento { get; set; }

    /// <summary>Restringe a regra a uma modalidade do catálogo. Null = todas.</summary>
    public string? ModalidadeCodigo { get; set; }

    /// <summary>Restringe a regra a um convênio do catálogo. Null = todos.</summary>
    public string? ConvenioCodigo { get; set; }

    public DateOnly? VigenteDe { get; set; }

    public DateOnly? VigenteAte { get; set; }

    public bool Ativa { get; set; } = true;

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>A regra vale no dia informado?</summary>
    public bool VigenteEm(DateOnly dia)
        => Ativa
           && (VigenteDe is null || dia >= VigenteDe)
           && (VigenteAte is null || dia <= VigenteAte);

    /// <summary>Como a regra aparece escrita na tela e no lançamento.</summary>
    public string Descricao => Base == BaseRepasse.PercentualDaReceita
        ? $"{Percentual:0.##}% da receita"
        : $"{ValorPorAtendimento:C} por atendimento";
}

/// <summary>
/// O repasse de um profissional já APURADO para um período.
///
/// Existe para o repasse não ser pago duas vezes. Sem um registro do que já foi apurado,
/// nada impede alguém de gerar o mesmo mês de novo em outra máquina — e o erro só
/// apareceria no extrato, depois do pagamento. Cancelar mantém o registro, com motivo.
/// </summary>
public class RepasseApurado
{
    public int Id { get; set; }

    public int ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    public DateOnly Inicio { get; set; }

    public DateOnly Fim { get; set; }

    /// <summary>Quantos atendimentos entraram na conta.</summary>
    public int Atendimentos { get; set; }

    /// <summary>Receita considerada (nas regras por percentual). Zero nas de valor fixo.</summary>
    public decimal BaseCalculo { get; set; }

    public decimal Valor { get; set; }

    /// <summary>Como foi calculado, congelado no momento da apuração.</summary>
    public string? RegraDescricao { get; set; }

    /// <summary>Saída gerada no caixa para pagar o repasse.</summary>
    public int? LancamentoFinanceiroId { get; set; }
    public LancamentoFinanceiro? Lancamento { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? CanceladoEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public bool Cancelado => CanceladoEm is not null;

    /// <summary>O período informado encosta no desta apuração?</summary>
    public bool Sobrepoe(DateOnly inicio, DateOnly fim) => inicio <= Fim && fim >= Inicio;
}
