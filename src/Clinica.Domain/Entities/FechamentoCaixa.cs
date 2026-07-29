namespace Clinica.Domain.Entities;

/// <summary>
/// A conferência da gaveta no fim do expediente: o que o sistema diz que entrou em
/// espécie contra o que a pessoa do balcão contou.
///
/// Três decisões carregam esta entidade:
///
/// 1. **Só dinheiro vivo.** Cartão e PIX não passam pela gaveta — o dinheiro deles cai
///    na conta dias depois, e incluí-los faria a conferência nunca bater, o que treinaria
///    a clínica a ignorar a divergência. Uma conferência que nunca fecha não é controle,
///    é ruído.
/// 2. **O valor do sistema é COPIADO no fechamento**, não recalculado na leitura. Um
///    lançamento digitado amanhã com a data de ontem reescreveria a conferência de ontem,
///    e a diferença que a pessoa justificou sumiria — junto com a explicação dela.
/// 3. **Divergência exige justificativa escrita.** Aceitar em silêncio é o mesmo que não
///    conferir: o valor de uma conferência está inteiro na obrigação de explicar o que
///    não bateu.
/// </summary>
public class FechamentoCaixa
{
    public int Id { get; set; }

    /// <summary>
    /// O dia conferido. NÃO é único, e de propósito: reabrir para recontagem guarda o
    /// fechamento anterior e grava outro por cima, então um dia recontado tem mais de uma
    /// linha. A que vale é a ÚLTIMA — apagar a anterior apagaria a contagem que de fato
    /// foi feita antes, junto com a justificativa de quem a fez.
    /// </summary>
    public DateOnly Data { get; set; }

    /// <summary>
    /// O que o sistema apurou de ENTRADA EM ESPÉCIE no dia, copiado no momento do
    /// fechamento. É a foto que faz a conferência ser um fato datado, e não uma conta
    /// que muda toda vez que alguém abre a tela.
    /// </summary>
    public decimal ValorSistema { get; set; }

    /// <summary>O que a pessoa contou na gaveta.</summary>
    public decimal ValorContado { get; set; }

    /// <summary>
    /// Saídas em espécie no dia (sangria, troco, compra paga do caixa), também copiadas.
    /// Sem elas o sistema cobraria da gaveta um dinheiro que saiu dela legitimamente.
    /// </summary>
    public decimal SaidasEspecie { get; set; }

    /// <summary>
    /// Quantos lançamentos em espécie compunham o valor do sistema. Guardado junto porque
    /// "faltou R$ 50 em 3 lançamentos" e "faltou R$ 50 em 40" pedem investigações
    /// diferentes.
    /// </summary>
    public int QuantidadeLancamentos { get; set; }

    /// <summary>Obrigatória quando há diferença. Vazia quando bateu.</summary>
    public string? Justificativa { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>Quem contou. Vem do LOGIN, nunca do usuário do Windows.</summary>
    public string? ConferidoPor { get; set; }

    public DateTime ConferidoEm { get; set; } = DateTime.Now;

    /// <summary>
    /// Reaberto para recontagem. O fechamento não é apagado — fica marcado, com o motivo,
    /// e o dia volta a aparecer como não conferido. Apagar deixaria o dia parecendo nunca
    /// ter sido contado, que é uma história diferente da que aconteceu.
    /// </summary>
    public bool Reaberto { get; set; }

    public string? MotivoReabertura { get; set; }

    /// <summary>
    /// O que a gaveta deveria ter: o que entrou menos o que saiu em espécie.
    /// </summary>
    public decimal Esperado => ValorSistema - SaidasEspecie;

    /// <summary>
    /// Contado menos esperado. Positivo é sobra, negativo é falta — e as duas pedem
    /// explicação, porque dinheiro a mais na gaveta costuma ser venda não lançada.
    /// </summary>
    public decimal Diferenca => ValorContado - Esperado;

    /// <summary>Bateu na vírgula.</summary>
    public bool Bateu => Diferenca == 0m;

    /// <summary>Texto curto da situação, para a lista.</summary>
    public string Situacao => Reaberto
        ? "Reaberto"
        : Bateu
            ? "Bateu"
            : Diferenca > 0m
                ? "Sobra"
                : "Falta";
}
