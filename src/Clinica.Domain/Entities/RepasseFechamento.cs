namespace Clinica.Domain.Entities;

/// <summary>De onde veio o dinheiro que dá direito ao repasse.</summary>
public enum OrigemItemRepasse
{
    /// <summary>Guia de convênio que a operadora PAGOU (receita realizada ligada à guia).</summary>
    Convenio,

    /// <summary>Sessão particular cujo dinheiro ENTROU (receita realizada ligada ao atendimento).</summary>
    Particular
}

/// <summary>Se a linha soma ou desconta no repasse.</summary>
public enum NaturezaItemRepasse
{
    /// <summary>Recebimento que gera repasse ao profissional.</summary>
    Credito,

    /// <summary>
    /// Desconto de um crédito já fechado cuja receita deixou de existir depois (receita
    /// cancelada, atendimento estornado). Entra no PRÓXIMO fechamento, com valor negativo —
    /// o fechamento antigo, já conferido e talvez pago, não é reescrito.
    /// </summary>
    Estorno
}

/// <summary>
/// O FECHAMENTO do repasse de um profissional numa competência (nov/2026).
///
/// A regra que a clínica pediu, por escrito: <b>repasse só depois de a guia ser conciliada</b>
/// — o financeiro ou o gerente afirmou, na Conciliação, que o convênio PAGOU aquela guia.
/// Por isso o fechamento não é por PERÍODO de atendimento (o modelo antigo,
/// <see cref="RepasseApurado"/>), e sim por ITEM recebido: cada guia paga entra uma única vez,
/// no primeiro fechamento depois do pagamento, mesmo que o atendimento seja de outro mês. É o
/// que o demonstrativo da clínica já mostrava à mão: a competência 09/2026 da SulAmérica trazia
/// consultas de 06/10 e 07/10, porque foi naquele pagamento que elas vieram.
///
/// A trava contra pagar duas vezes é do BANCO, não da tela: <see cref="ItemRepasse.Chave"/>
/// tem índice único entre os itens não cancelados.
///
/// Situação: <b>conferido</b> ao nascer (quem fecha é quem conferiu, e o nome vai ao
/// demonstrativo), <b>pago</b> quando a saída no caixa é realizada, <b>cancelado</b> com
/// motivo — e a linha fica. Pago não se cancela: estorna-se o pagamento no Caixa antes.
/// </summary>
public class RepasseFechamento
{
    public int Id { get; set; }

    public int ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Primeiro dia do mês da competência (09/2026 = 2026-09-01).</summary>
    public DateOnly Competencia { get; set; }

    /// <summary>Último dia de recebimento considerado (inclusive) — o corte do fechamento.</summary>
    public DateOnly RecebidosAte { get; set; }

    /// <summary>Nome do profissional como saiu no demonstrativo (congelado).</summary>
    public string ProfissionalNome { get; set; } = string.Empty;

    /// <summary>Conselho e número (ex.: "CRM-RJ 123456"), congelado. Pode faltar.</summary>
    public string? ProfissionalRegistro { get; set; }

    /// <summary>Quantidade de itens de crédito.</summary>
    public int Quantidade { get; set; }

    /// <summary>Soma dos créditos menos os estornos. É o que se paga.</summary>
    public decimal Valor { get; set; }

    /// <summary>Quem conferiu (o operador do login, nunca o usuário da máquina).</summary>
    public string ConferidoPor { get; set; } = string.Empty;

    public DateTime ConferidoEm { get; set; } = DateTime.Now;

    public string? Observacoes { get; set; }

    /// <summary>Saída no caixa que paga este repasse (nasce prevista).</summary>
    public int? LancamentoFinanceiroId { get; set; }
    public LancamentoFinanceiro? Lancamento { get; set; }

    public DateTime? CanceladoEm { get; set; }
    public string? CanceladoPor { get; set; }
    public string? MotivoCancelamento { get; set; }

    public List<ItemRepasse> Itens { get; set; } = [];

    public bool Cancelado => CanceladoEm is not null;

    /// <summary>A saída no caixa foi realizada (exige o <see cref="Lancamento"/> carregado).</summary>
    public bool Pago => !Cancelado && Lancamento?.Status == StatusLancamento.Realizado;
}

/// <summary>
/// Uma linha do fechamento: um recebimento (ou o desconto de um) e quanto dele é repasse.
/// Tudo o que o demonstrativo imprime é CONGELADO aqui — paciente, convênio, guia, regra —
/// porque o demonstrativo de setembro precisa sair igual em dezembro, mesmo que a ficha, o
/// cadastro do convênio ou a regra tenham mudado.
/// </summary>
public class ItemRepasse
{
    public int Id { get; set; }

    public int RepasseFechamentoId { get; set; }
    public RepasseFechamento? Fechamento { get; set; }

    public OrigemItemRepasse Origem { get; set; }

    public NaturezaItemRepasse Natureza { get; set; } = NaturezaItemRepasse.Credito;

    /// <summary>
    /// Identidade do que está sendo pago, com índice único entre itens vivos:
    /// <c>G:{codigoFaturamentoId}</c> (guia), <c>P:{atendimentoId}</c> (particular), e o
    /// estorno de qualquer um deles com o prefixo <c>E:</c>. É a trava de pagar duas vezes.
    /// </summary>
    public string Chave { get; set; } = string.Empty;

    /// <summary>
    /// Cópia do <see cref="RepasseFechamento.CanceladoEm"/>: o índice único filtrado só enxerga
    /// colunas da própria tabela. Cancelar o fechamento preenche as duas, no mesmo SaveChanges.
    /// </summary>
    public DateTime? CanceladoEm { get; set; }

    public int AtendimentoId { get; set; }
    public int? CodigoFaturamentoId { get; set; }

    /// <summary>A receita REALIZADA que prova o recebimento.</summary>
    public int? LancamentoReceitaId { get; set; }

    /// <summary>No estorno, o item de crédito que ele desconta.</summary>
    public int? ItemEstornadoId { get; set; }

    public int PacienteId { get; set; }
    public string PacienteNome { get; set; } = string.Empty;

    public DateOnly DataAtendimento { get; set; }

    /// <summary>Código do convênio no catálogo ("PARTICULAR" no particular).</summary>
    public string ConvenioCodigo { get; set; } = string.Empty;
    public string ConvenioNome { get; set; } = string.Empty;

    /// <summary>Tipo da guia (Consulta, Acupuntura…). Nulo no particular.</summary>
    public TipoCodigo? TipoGuia { get; set; }

    /// <summary>Rótulo do que foi feito, como sai no demonstrativo ("Consulta").</summary>
    public string Descricao { get; set; } = string.Empty;

    public string? NumeroGuia { get; set; }

    /// <summary>GRD / conta / lote do pagamento da operadora (o agrupador do demonstrativo).</summary>
    public string? ReferenciaPagamento { get; set; }

    public DateOnly DataRecebimento { get; set; }

    /// <summary>Quanto a clínica recebeu por este item (base do percentual).</summary>
    public decimal ValorRecebido { get; set; }

    public int? RegraRepasseId { get; set; }

    /// <summary>A regra como estava escrita quando o item foi fechado.</summary>
    public string RegraDescricao { get; set; } = string.Empty;

    /// <summary>Repasse deste item. Negativo no estorno.</summary>
    public decimal Valor { get; set; }
}
