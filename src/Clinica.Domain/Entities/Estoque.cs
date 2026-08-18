namespace Clinica.Domain.Entities;

/// <summary>
/// O que aconteceu com o insumo. São três de propósito: acerto de inventário entra
/// como <see cref="Entrada"/> ou <see cref="Perda"/> com observação, e não como um
/// quarto tipo — "ajuste" que some e aparece sem dizer o quê é o buraco por onde o
/// estoque deixa de bater.
/// </summary>
public enum TipoMovimentoEstoque
{
    /// <summary>Compra, doação, devolução ao estoque.</summary>
    Entrada,

    /// <summary>Consumo — normalmente numa sessão.</summary>
    Saida,

    /// <summary>Quebra, vencimento, extravio.</summary>
    Perda,

    /// <summary>
    /// Acerto de INVENTÁRIO: a contagem física não bateu com o saldo do sistema
    /// (parcela 30).
    ///
    /// Existe porque a clínica não tinha como registrar a diferença com honestidade. Ela
    /// só podia lançar `Perda`, e perda é uma afirmação: alguém quebrou, venceu ou
    /// extraviou. Quando a diferença é SOBRA (contou mais do que o sistema tinha) ou
    /// erro de digitação antigo, chamar de perda mente sobre o que aconteceu — e é
    /// exatamente essa mentira que faz o custo médio do insumo parar de valer.
    ///
    /// A quantidade do ajuste é sempre POSITIVA, como nos demais; o que diz a direção é
    /// <see cref="MovimentoEstoque.AjusteParaCima"/>.
    /// </summary>
    Ajuste
}

/// <summary>
/// Um insumo da clínica (agulha, moxa, ventosa, algodão). Feature 10 da proposta.
///
/// O saldo NÃO é campo: é a soma dos movimentos. Guardar um total e mantê-lo em dia é
/// como o estoque para de bater — uma gravação que falha no meio e o número fica errado
/// para sempre, sem ninguém saber desde quando.
/// </summary>
public class ItemEstoque
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Unidade de contagem: "un", "cx", "ml", "par".</summary>
    public string Unidade { get; set; } = "un";

    /// <summary>Abaixo disto o item entra no alerta de reposição. Zero desliga o alerta.</summary>
    public decimal EstoqueMinimo { get; set; }

    public bool Ativo { get; set; } = true;

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    public List<MovimentoEstoque> Movimentos { get; set; } = new();
}

/// <summary>
/// Uma entrada, saída ou perda de insumo.
///
/// A validade fica no MOVIMENTO, não no item: o mesmo insumo entra em lotes com
/// vencimentos diferentes, e guardar uma validade só por item apagaria o lote que vence
/// primeiro — justamente o que o alerta precisa enxergar.
/// </summary>
public class MovimentoEstoque
{
    /// <summary>
    /// No acerto de inventário: a contagem achou MAIS do que o sistema tinha. Null nos
    /// demais tipos, que já dizem a direção pelo próprio nome.
    ///
    /// É um campo separado, e não uma quantidade negativa, porque quantidade negativa
    /// espalharia `Math.Abs` por todo cálculo de saldo e custo — e um esquecido daria
    /// saldo negativo silencioso.
    /// </summary>
    public bool? AjusteParaCima { get; set; }

    public int Id { get; set; }

    public int ItemEstoqueId { get; set; }
    public ItemEstoque? Item { get; set; }

    public TipoMovimentoEstoque Tipo { get; set; }

    /// <summary>Sempre positiva; quem dá o sinal é o <see cref="Tipo"/>.</summary>
    public decimal Quantidade { get; set; }

    /// <summary>Custo unitário da entrada. Nas saídas é opcional (o serviço usa o médio).</summary>
    public decimal? CustoUnitario { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Vencimento deste lote. Só faz sentido em entrada.</summary>
    public DateOnly? Validade { get; set; }

    public string? Lote { get; set; }

    /// <summary>Atendimento que consumiu o insumo — é o que dá custo por sessão.</summary>
    public int? AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    public int? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>
    /// Quanto este movimento SOMA ao saldo — com o sinal certo, inclusive no AJUSTE.
    ///
    /// ⚠️ Substitui o par `Sinal`/`QuantidadeComSinal`, que dizia "−1 para tudo que não é
    /// entrada" e erraria o ajuste de inventário PARA CIMA (parcela 30) — a contagem que
    /// acha a mais é entrada de saldo. O par nunca teve um chamador, e é só por isso que
    /// o erro nunca doeu: quem somava saldo era o repositório, com a regra escrita à mão
    /// lá dentro. Agora a regra mora AQUI, num lugar só, e o repositório e o extrato do
    /// item a chamam — duas somas de saldo divergem exatamente no ajuste, que é o
    /// movimento raro que ninguém testa de cabeça.
    /// </summary>
    public decimal Delta => DeltaDe(Tipo, AjusteParaCima, Quantidade);

    /// <summary>A mesma conta, para quem só tem as colunas projetadas (o repositório).</summary>
    public static decimal DeltaDe(TipoMovimentoEstoque tipo, bool? ajusteParaCima, decimal quantidade)
        => tipo switch
        {
            TipoMovimentoEstoque.Entrada => quantidade,
            TipoMovimentoEstoque.Ajuste => ajusteParaCima == true ? quantidade : -quantidade,
            _ => -quantidade
        };
}
