namespace Clinica.Domain.Entities;

/// <summary>Os dois documentos impressos que faltavam da página 21 da proposta.</summary>
public enum TipoDocumentoFinanceiro
{
    /// <summary>Comprovante do que já foi pago.</summary>
    Recibo,

    /// <summary>Proposta do que vai custar, com prazo de validade.</summary>
    Orcamento
}

public static class TipoDocumentoFinanceiroInfo
{
    public static string Rotular(TipoDocumentoFinanceiro tipo) => tipo switch
    {
        TipoDocumentoFinanceiro.Recibo => "Recibo",
        TipoDocumentoFinanceiro.Orcamento => "Orçamento",
        _ => tipo.ToString()
    };

    public static IReadOnlyList<TipoDocumentoFinanceiro> Todos { get; } =
    [
        TipoDocumentoFinanceiro.Recibo,
        TipoDocumentoFinanceiro.Orcamento
    ];
}

/// <summary>
/// Recibo ou orçamento emitido. Segue as MESMAS regras dos documentos clínicos da
/// parcela 3, porque o problema é o mesmo: papel entregue existe no mundo.
///
/// Não se apaga nem se reescreve — cancela-se com motivo e emite-se outro. E o conteúdo
/// fica gravado aqui em vez de recalculado na reimpressão: um recibo de R$ 300 não pode
/// virar R$ 350 na segunda via porque a tabela de preços subiu no meio.
///
/// O valor mora aqui e o pagamento continua morando em <see cref="LancamentoFinanceiro"/>:
/// o recibo APONTA para o lançamento, não o substitui.
/// </summary>
public class DocumentoFinanceiro
{
    public int Id { get; set; }

    /// <summary>Número sequencial por ano e por tipo (<c>REC 2026/0001</c>).</summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>Código curto impresso para conferir a via em papel.</summary>
    public string CodigoVerificacao { get; set; } = string.Empty;

    public TipoDocumentoFinanceiro Tipo { get; set; }

    /// <summary>Paciente, quando o documento é de um. Null em recibo a terceiro.</summary>
    public int? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>
    /// A quem o documento se destina, copiado na emissão. Existe porque quem paga nem
    /// sempre é o paciente (pai, empresa, plano), e o recibo vale para quem pagou.
    /// </summary>
    public string Destinatario { get; set; } = string.Empty;

    /// <summary>CPF/CNPJ de quem recebe o documento.</summary>
    public string? DocumentoDestinatario { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Até quando o orçamento vale. Ignorado no recibo.</summary>
    public DateOnly? ValidoAte { get; set; }

    public string? Titulo { get; set; }

    /// <summary>Texto livre: "referente a", condições de pagamento, ressalvas.</summary>
    public string? Corpo { get; set; }

    public string? Observacoes { get; set; }

    public FormaPagamento? FormaPagamento { get; set; }

    /// <summary>Lançamento que este recibo comprova.</summary>
    public int? LancamentoFinanceiroId { get; set; }
    public LancamentoFinanceiro? Lancamento { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public DateTime? CanceladoEm { get; set; }

    public string? MotivoCancelamento { get; set; }

    public List<ItemDocumentoFinanceiro> Itens { get; set; } = new();

    public bool Cancelado => CanceladoEm is not null;

    /// <summary>Soma das linhas — o valor do documento.</summary>
    public decimal ValorTotal => Itens.Sum(i => i.ValorTotal);

    public string TituloImpresso
        => string.IsNullOrWhiteSpace(Titulo) ? TipoDocumentoFinanceiroInfo.Rotular(Tipo) : Titulo!;

    /// <summary>Prefixo do número, para recibo e orçamento não disputarem a mesma sequência.</summary>
    public static string Prefixo(TipoDocumentoFinanceiro tipo)
        => tipo == TipoDocumentoFinanceiro.Recibo ? "REC" : "ORC";
}

/// <summary>Uma linha do recibo ou do orçamento.</summary>
public class ItemDocumentoFinanceiro
{
    public int Id { get; set; }

    public int DocumentoFinanceiroId { get; set; }
    public DocumentoFinanceiro? Documento { get; set; }

    public int Ordem { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public decimal Quantidade { get; set; } = 1;

    public decimal ValorUnitario { get; set; }

    public decimal ValorTotal => Quantidade * ValorUnitario;
}
