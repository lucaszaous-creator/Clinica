namespace Clinica.Domain.Entities;

/// <summary>Natureza do lançamento no caixa.</summary>
public enum TipoLancamento
{
    Entrada,
    Saida
}

/// <summary>Situação do lançamento no fluxo de caixa.</summary>
public enum StatusLancamento
{
    /// <summary>Ainda vai acontecer (a receber / a pagar).</summary>
    Previsto,

    /// <summary>Dinheiro efetivamente entrou ou saiu.</summary>
    Realizado,

    /// <summary>Cancelado — permanece no histórico, fora dos totais.</summary>
    Cancelado
}

/// <summary>Como o dinheiro entrou ou saiu.</summary>
public enum FormaPagamento
{
    Dinheiro,
    Pix,
    CartaoDebito,
    CartaoCredito,
    Transferencia,
    Boleto,

    /// <summary>Recebimento do convênio (repasse da operadora sobre guias faturadas).</summary>
    Convenio,

    Outro
}

/// <summary>
/// Categoria do plano de contas, criada pela clínica (mesmo padrão de convênios,
/// modalidades e especialidades: catálogo editável em runtime).
/// </summary>
public class CategoriaFinanceira
{
    public int Id { get; set; }

    /// <summary>Código estável usado para referência (ex.: "CONSULTA_PARTICULAR").</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    /// <summary>Categoria de entrada ou de saída — evita classificar despesa como receita.</summary>
    public TipoLancamento Tipo { get; set; }

    public bool Ativa { get; set; } = true;

    /// <summary>Ordem de exibição nas listas.</summary>
    public int Ordem { get; set; }
}

/// <summary>
/// Um movimento de caixa da clínica (entrada ou saída).
///
/// IMPORTANTE — separação em relação ao faturamento: o dinheiro vive AQUI, nunca nas
/// entidades de faturamento. <see cref="CodigoFaturamento"/> e <see cref="Atendimento"/>
/// continuam sem qualquer campo de valor; é o lançamento que aponta para eles.
/// A dependência tem um sentido só (financeiro → faturamento), então o módulo de
/// faturamento segue funcionando sem saber que o financeiro existe.
/// </summary>
public class LancamentoFinanceiro
{
    public int Id { get; set; }

    /// <summary>Data de competência (quando o fato ocorreu).</summary>
    public DateOnly Data { get; set; }

    public TipoLancamento Tipo { get; set; }

    public string Descricao { get; set; } = string.Empty;

    /// <summary>Valor sempre positivo; quem dá o sinal é <see cref="Tipo"/>.</summary>
    public decimal Valor { get; set; }

    public StatusLancamento Status { get; set; } = StatusLancamento.Previsto;

    /// <summary>Quando efetivamente entrou/saiu. Preenchida ao realizar o lançamento.</summary>
    public DateOnly? DataPagamento { get; set; }

    /// <summary>
    /// Quando a conta VENCE (parcela 12). Diferente das outras duas datas, e a diferença
    /// é o dia a dia do financeiro: <see cref="Data"/> é a competência (o mês a que a
    /// despesa pertence), <see cref="DataPagamento"/> é quando o dinheiro se moveu, e
    /// esta é a data em que ele PRECISA se mover. Sem ela o módulo não sabia responder
    /// "o que vence esta semana" — a pergunta que se faz toda segunda-feira.
    ///
    /// Null é estado legítimo: venda no balcão paga na hora não vence, ela já aconteceu.
    /// </summary>
    public DateOnly? DataVencimento { get; set; }

    /// <summary>
    /// Ocorrência da conta recorrente que gerou este lançamento
    /// (<c>REC:{id}:{aaaa-MM-dd}</c>), com índice único. É a chave de idempotência: o
    /// aluguel de agosto só pode nascer uma vez, mesmo que a geração rode em dois postos
    /// na mesma manhã. Null em lançamento avulso, que é a maioria.
    /// </summary>
    public string? OrigemRecorrencia { get; set; }

    public FormaPagamento? FormaPagamento { get; set; }

    public int? CategoriaFinanceiraId { get; set; }
    public CategoriaFinanceira? Categoria { get; set; }

    // ---------- Vínculos com o faturamento (o "os dois se comunicam") ----------

    /// <summary>Paciente de quem veio a receita, quando aplicável.</summary>
    public int? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Atendimento que originou a receita, quando aplicável.</summary>
    public int? AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    /// <summary>
    /// Guia/código de faturamento que originou a receita. É o vínculo que permite
    /// conciliar: guia efetivada no convênio ↔ dinheiro recebido.
    /// </summary>
    public int? CodigoFaturamentoId { get; set; }
    public CodigoFaturamento? CodigoFaturamento { get; set; }

    /// <summary>Convênio pagador (nulo em receita particular).</summary>
    public Convenio? Convenio { get; set; }

    /// <summary>Código do convênio no catálogo, quando criado pela clínica.</summary>
    public string? ConvenioCodigo { get; set; }

    // ---------- Taxa da maquininha e imposto (parcela 9) ----------
    //
    // Regra que manda aqui: o VALOR do lançamento continua sendo o BRUTO — o que o
    // paciente pagou. Taxa e imposto são deduções registradas ao lado, e o líquido é
    // CALCULADO. Guardar o líquido como campo daria duas verdades sobre o mesmo dinheiro,
    // e no dia em que divergissem ninguém saberia qual acreditar.

    /// <summary>Quem processou o cartão. Null nas outras formas de pagamento.</summary>
    public string? Adquirente { get; set; }

    public string? Bandeira { get; set; }

    public ModalidadeCartao? ModalidadeCartao { get; set; }

    /// <summary>Número de parcelas no crédito parcelado.</summary>
    public int? Parcelas { get; set; }

    /// <summary>
    /// Percentual aplicado, guardado como PROCEDÊNCIA do valor da taxa. A taxa da
    /// maquininha é renegociada; sem isto, ninguém saberia de onde saiu o desconto de
    /// um recebimento de seis meses atrás.
    /// </summary>
    public decimal? TaxaPercentual { get; set; }

    /// <summary>
    /// Quanto a adquirente reteve, em dinheiro, COPIADO na hora da venda. É o valor que
    /// vale — mudar a taxa do catálogo não reescreve o que já foi descontado.
    /// </summary>
    public decimal? ValorTaxa { get; set; }

    /// <summary>Alíquota de imposto retido (ISS/Simples), quando houver.</summary>
    public decimal? AliquotaImposto { get; set; }

    /// <summary>Imposto retido em dinheiro, também copiado na emissão.</summary>
    public decimal? ValorImposto { get; set; }

    /// <summary>
    /// De QUÊ é o imposto, copiado na emissão: "ISS 3% + PIS 0,65% + COFINS 3%"
    /// (parcela 15). A alíquota somada responde "quanto saiu" e não "de quê" — e "de quê"
    /// é a pergunta que o contador faz no fim do mês, quando precisa separar as guias.
    ///
    /// Copiado, e não montado na leitura, pela mesma razão do valor da taxa: as alíquotas
    /// têm vigência, e remontar o texto hoje descreveria a retenção de março com os
    /// números de setembro.
    /// </summary>
    public string? DetalheImposto { get; set; }

    /// <summary>
    /// Quando o dinheiro efetivamente cai. Diferente de <see cref="DataPagamento"/>: o
    /// paciente pagou hoje no crédito, e a clínica recebe em 30 dias. É o que sustenta
    /// o "a receber" do caixa.
    /// </summary>
    public DateOnly? PrevisaoRecebimento { get; set; }

    /// <summary>
    /// O que sobra de verdade: bruto menos taxa menos imposto. Calculado, nunca gravado.
    /// </summary>
    public decimal ValorLiquido => Valor - (ValorTaxa ?? 0m) - (ValorImposto ?? 0m);

    /// <summary>Houve alguma dedução — a tela só mostra a linha do líquido quando há.</summary>
    public bool TemDeducao => ValorTaxa is > 0m || ValorImposto is > 0m;

    /// <summary>
    /// Conta ainda em aberto que já passou do vencimento. Só <see cref="StatusLancamento.Previsto"/>
    /// vence: o que foi pago não está atrasado, e o cancelado não é devido.
    /// </summary>
    public bool EstaVencido(DateOnly hoje) =>
        Status == StatusLancamento.Previsto && DataVencimento is { } v && v < hoje;

    /// <summary>
    /// Dias até o vencimento (negativo quando já venceu). Null quando não há vencimento
    /// — e null não é zero: "não vence" e "vence hoje" são coisas diferentes.
    /// </summary>
    public int? DiasAteVencer(DateOnly hoje) =>
        DataVencimento is { } v ? v.DayNumber - hoje.DayNumber : null;

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }
}
