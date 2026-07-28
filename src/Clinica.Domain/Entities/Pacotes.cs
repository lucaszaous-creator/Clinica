namespace Clinica.Domain.Entities;

/// <summary>
/// O que a clínica está vendendo. Não confundir com <see cref="AutorizacaoSessoes"/>,
/// que é COTA DO CONVÊNIO — outra coisa: aquilo o convênio autoriza, isto a clínica vende.
/// </summary>
public enum TipoPacote
{
    /// <summary>Um número fechado de sessões ("pacote de 10").</summary>
    Sessoes,

    /// <summary>Sessões livres dentro de um período (mensalidade).</summary>
    Plano,

    /// <summary>Crédito avulso, normalmente presenteado.</summary>
    Voucher
}

/// <summary>Situação de um pacote vendido.</summary>
public enum StatusPacote
{
    Ativo,

    /// <summary>Todas as sessões contratadas foram usadas.</summary>
    Esgotado,

    /// <summary>Passou da validade com saldo sobrando.</summary>
    Expirado,

    /// <summary>Cancelado pela clínica — permanece no histórico com o motivo.</summary>
    Cancelado
}

/// <summary>
/// Um pacote do catálogo: o que está à venda no balcão, com preço e validade padrão.
/// Existe para a venda não depender de alguém lembrar quantas sessões e por quanto —
/// é o mesmo papel do catálogo de convênios e modalidades.
/// </summary>
public class PacoteCatalogo
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public TipoPacote Tipo { get; set; } = TipoPacote.Sessoes;

    /// <summary>Sessões incluídas. Null = ilimitado no período (é o caso do plano).</summary>
    public int? SessoesIncluidas { get; set; }

    public decimal Valor { get; set; }

    /// <summary>Dias de validade a contar da compra. Null = sem prazo.</summary>
    public int? ValidadeDias { get; set; }

    public bool Ativo { get; set; } = true;

    public int Ordem { get; set; }

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }
}

/// <summary>
/// Um pacote VENDIDO a um paciente. Feature 08 da proposta.
///
/// Os dados do catálogo são copiados na venda (nome, sessões, valor) em vez de
/// referenciados: mudar o preço de tabela em novembro não pode reescrever o que o
/// paciente comprou em março. O vínculo com o catálogo fica só como procedência.
/// </summary>
public class PacotePaciente
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>De onde veio, quando veio do catálogo. Null = venda avulsa.</summary>
    public int? PacoteCatalogoId { get; set; }
    public PacoteCatalogo? Catalogo { get; set; }

    /// <summary>Nome copiado do catálogo na venda — o que vale é o que foi vendido.</summary>
    public string Nome { get; set; } = string.Empty;

    public TipoPacote Tipo { get; set; } = TipoPacote.Sessoes;

    /// <summary>Sessões contratadas. Null = ilimitado dentro da validade (plano).</summary>
    public int? SessoesContratadas { get; set; }

    public decimal Valor { get; set; }

    public DateOnly DataCompra { get; set; }

    /// <summary>Último dia em que o pacote pode ser usado. Null = sem prazo.</summary>
    public DateOnly? ValidoAte { get; set; }

    /// <summary>Pagamento que quitou o pacote, quando já lançado no caixa.</summary>
    public int? LancamentoFinanceiroId { get; set; }
    public LancamentoFinanceiro? Lancamento { get; set; }

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? CanceladoEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public List<ConsumoPacote> Consumos { get; set; } = new();

    public bool Cancelado => CanceladoEm is not null;

    /// <summary>Consumo cancelado devolve a sessão ao saldo — por isso não conta aqui.</summary>
    public int SessoesUsadas => Consumos.Count(c => !c.Cancelado);

    /// <summary>Sessões restantes. Null quando o pacote é ilimitado no período.</summary>
    public int? SaldoSessoes
        => SessoesContratadas is { } contratadas ? contratadas - SessoesUsadas : null;

    public bool Expirado(DateOnly hoje) => ValidoAte is { } limite && hoje > limite;

    public bool Esgotado => SaldoSessoes is { } saldo && saldo <= 0;

    /// <summary>
    /// Situação calculada na hora. O status não é guardado porque ele muda sozinho com
    /// o calendário: um pacote guardado como "Ativo" viraria mentira à meia-noite do
    /// vencimento, e ninguém roda tarefa noturna nesta clínica.
    /// </summary>
    public StatusPacote Situacao(DateOnly hoje)
    {
        if (Cancelado) return StatusPacote.Cancelado;
        if (Esgotado) return StatusPacote.Esgotado;
        if (Expirado(hoje)) return StatusPacote.Expirado;
        return StatusPacote.Ativo;
    }

    /// <summary>Dá para debitar uma sessão hoje?</summary>
    public bool PodeConsumir(DateOnly hoje) => Situacao(hoje) == StatusPacote.Ativo;
}

/// <summary>
/// Uma sessão debitada do pacote.
///
/// É FATO datado, como todo o resto do sistema: para desfazer, cancela-se com motivo em
/// vez de apagar. Sem isso, "o paciente diz que sobrou uma sessão" não teria como ser
/// conferido — e é exatamente a conversa que acontece no balcão.
/// </summary>
public class ConsumoPacote
{
    public int Id { get; set; }

    public int PacotePacienteId { get; set; }
    public PacotePaciente? Pacote { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Atendimento que consumiu a sessão, quando o débito foi automático.</summary>
    public int? AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    public int? AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }

    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? CanceladoEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public bool Cancelado => CanceladoEm is not null;
}
