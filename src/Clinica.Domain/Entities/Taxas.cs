namespace Clinica.Domain.Entities;

/// <summary>
/// Como o cartão foi passado. Débito, crédito à vista e parcelado têm taxas diferentes na
/// mesma maquininha — é a distinção que faz o líquido bater.
/// </summary>
public enum ModalidadeCartao
{
    Debito,
    CreditoAVista,
    CreditoParcelado
}

/// <summary>
/// Taxa da maquininha, por adquirente/bandeira/modalidade, COM VIGÊNCIA.
///
/// A vigência é a razão de esta entidade existir em vez de um campo solto: a adquirente
/// renegocia a taxa, e mudar o percentual hoje não pode reescrever o líquido do mês que
/// já foi conciliado. Mesma regra do <see cref="RegraRepasse"/> — o que já aconteceu fica
/// como aconteceu.
///
/// O percentual daqui é só a ORIGEM do número. O que vale no lançamento é o valor da taxa
/// COPIADO na hora da venda (<see cref="LancamentoFinanceiro.ValorTaxa"/>), pelo mesmo
/// motivo de o pacote copiar o catálogo: procedência não é referência viva.
/// </summary>
public class TaxaCartao
{
    public int Id { get; set; }

    /// <summary>Quem processa (Cielo, Rede, Stone, PagSeguro…). Texto livre: a clínica troca de maquininha.</summary>
    public string Adquirente { get; set; } = string.Empty;

    /// <summary>Bandeira (Visa, Master, Elo…). Null = vale para todas as bandeiras do adquirente.</summary>
    public string? Bandeira { get; set; }

    public ModalidadeCartao Modalidade { get; set; } = ModalidadeCartao.CreditoAVista;

    /// <summary>
    /// Faixa de parcelas a que a taxa se aplica, no crédito parcelado. Null nos demais.
    /// A adquirente cobra por faixa ("2 a 6x"), não por parcela.
    /// </summary>
    public int? ParcelasDe { get; set; }

    public int? ParcelasAte { get; set; }

    /// <summary>Percentual de 0 a 100 descontado do bruto.</summary>
    public decimal Percentual { get; set; }

    /// <summary>
    /// Em quantos dias o dinheiro cai. É o que permite ao caixa responder "quanto tenho a
    /// receber" — a venda de hoje no crédito não é dinheiro de hoje.
    /// </summary>
    public int DiasParaReceber { get; set; }

    public DateOnly? VigenteDe { get; set; }

    public DateOnly? VigenteAte { get; set; }

    public bool Ativa { get; set; } = true;

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>A taxa vale no dia informado?</summary>
    public bool VigenteEm(DateOnly dia)
        => Ativa
           && (VigenteDe is null || dia >= VigenteDe)
           && (VigenteAte is null || dia <= VigenteAte);

    /// <summary>A faixa cobre este número de parcelas? Sem faixa declarada, cobre qualquer um.</summary>
    public bool CobreParcelas(int parcelas)
        => (ParcelasDe is null || parcelas >= ParcelasDe)
           && (ParcelasAte is null || parcelas <= ParcelasAte);

    /// <summary>Como a taxa aparece escrita na tela e no lançamento.</summary>
    public string Descricao
    {
        get
        {
            var bandeira = string.IsNullOrWhiteSpace(Bandeira) ? "todas as bandeiras" : Bandeira!;
            var faixa = Modalidade == ModalidadeCartao.CreditoParcelado && ParcelasDe is not null
                ? $" {ParcelasDe}–{ParcelasAte ?? ParcelasDe}x"
                : string.Empty;
            return $"{Adquirente} · {bandeira} · {Rotular(Modalidade)}{faixa} — {Percentual:0.##}%";
        }
    }

    public static string Rotular(ModalidadeCartao modalidade) => modalidade switch
    {
        ModalidadeCartao.Debito => "débito",
        ModalidadeCartao.CreditoAVista => "crédito à vista",
        ModalidadeCartao.CreditoParcelado => "crédito parcelado",
        _ => modalidade.ToString()
    };
}
