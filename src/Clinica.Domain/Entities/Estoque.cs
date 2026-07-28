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
    Perda
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

    /// <summary>+1 para entrada, −1 para o que sai. É o que soma o saldo.</summary>
    public int Sinal => Tipo == TipoMovimentoEstoque.Entrada ? 1 : -1;

    /// <summary>Quantidade com sinal, pronta para somar.</summary>
    public decimal QuantidadeComSinal => Quantidade * Sinal;
}
