namespace Clinica.Domain.Entities;

/// <summary>
/// Quanto uma operadora paga por um tipo de guia (parcela 20).
///
/// Existe porque o valor da guia era **digitado à mão em cada conciliação**. Para quem
/// fatura volume de convênio isso tem três consequências, e nenhuma é pequena:
///
/// 1. **Erro de digitação vira dinheiro perdido em silêncio.** Um R$ 45 no lugar de
///    R$ 145 não é recusado por ninguém — o sistema aceita e a diferença só apareceria
///    numa conferência que a clínica não faz.
/// 2. **A conciliação é digitação, não conferência.** Com a tabela cadastrada, o sistema
///    PROPÕE e a pessoa confere contra o demonstrativo — que é o trabalho que importa.
/// 3. **Ninguém sabia o que a operadora deveria pagar.** Sem valor de referência, não há
///    como notar que a guia veio menor que o contratado.
///
/// A **vigência** é o que faz o cadastro valer a pena: a operadora reajusta a tabela, e o
/// que vale na guia de março é o valor de março. Mesma regra da taxa de cartão, do tributo
/// e do repasse — o que já aconteceu fica como aconteceu.
///
/// O valor daqui é só a ORIGEM do número. O que vale no caixa é o valor COPIADO no
/// lançamento: reajustar a tabela não pode reescrever o que a operadora já pagou.
/// </summary>
public class PrecoConvenio
{
    public int Id { get; set; }

    /// <summary>
    /// Código do convênio no catálogo. É a identidade da operadora — não a família de
    /// regra: duas operadoras podem compartilhar a família e pagar valores diferentes,
    /// que é exatamente o caso que esta tabela existe para resolver.
    /// </summary>
    public string ConvenioCodigo { get; set; } = string.Empty;

    /// <summary>Natureza da guia: consulta, acupuntura, eletro, BSV.</summary>
    public TipoCodigo Tipo { get; set; } = TipoCodigo.Acupuntura;

    /// <summary>
    /// Especialidade, quando o valor depende dela. Null = vale para qualquer especialidade
    /// daquele tipo — e é o caso comum, porque a maior parte das tabelas cobra por
    /// procedimento, não por especialidade.
    /// </summary>
    public Especialidade? Especialidade { get; set; }

    /// <summary>Quanto a operadora paga por esta guia.</summary>
    public decimal Valor { get; set; }

    public DateOnly? VigenteDe { get; set; }

    public DateOnly? VigenteAte { get; set; }

    public bool Ativo { get; set; } = true;

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>O preço vale no dia informado?</summary>
    public bool VigenteEm(DateOnly dia)
        => Ativo
           && (VigenteDe is null || dia >= VigenteDe)
           && (VigenteAte is null || dia <= VigenteAte);

    /// <summary>
    /// pt-BR FIXO, não a cultura da máquina — mesma razão do <c>Tributo.Descricao</c>: este
    /// texto é copiado para a procedência do lançamento e fica gravado, e dois postos
    /// escreveriam "R$ 145,00" e "R$145.00" na mesma coluna.
    /// </summary>
    private static readonly System.Globalization.CultureInfo Brasil = new("pt-BR");

    /// <summary>Como o preço aparece escrito na tela e na procedência do lançamento.</summary>
    public string Descricao => string.Format(
        Brasil, "{0} · {1}{2} — {3:C}",
        ConvenioCodigo,
        Tipo,
        Especialidade is { } e ? $" ({e})" : string.Empty,
        Valor);

    /// <summary>Vigência escrita, para a lista.</summary>
    public string Vigencia => (VigenteDe, VigenteAte) switch
    {
        (null, null) => "sem prazo",
        ({ } de, null) => string.Format(Brasil, "desde {0:dd/MM/yyyy}", de),
        (null, { } ate) => string.Format(Brasil, "até {0:dd/MM/yyyy}", ate),
        var (de, ate) => string.Format(Brasil, "{0:dd/MM/yyyy} a {1:dd/MM/yyyy}", de, ate)
    };
}
