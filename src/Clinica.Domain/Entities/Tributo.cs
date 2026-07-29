namespace Clinica.Domain.Entities;

/// <summary>
/// Um tributo que incide sobre o que a clínica recebe (parcela 15).
///
/// Substitui a alíquota única da parcela 9, que tinha dois defeitos que só aparecem no
/// escritório do contador:
///
/// 1. **Uma alíquota só não separa nada.** A clínica no Lucro Presumido paga ISS, PIS,
///    COFINS, IRPJ e CSLL — cinco guias, cinco vencimentos, cinco linhas na apuração. Um
///    número somado no lançamento responde "quanto saiu" e não responde "de quê", que é
///    justamente a pergunta que o contador faz.
/// 2. **Não tinha vigência.** Alíquota muda — o município reajusta o ISS, a empresa troca
///    de faixa no Simples. Sem vigência, mudar o número hoje mudaria o cálculo de qualquer
///    coisa recalculada amanhã sobre o mês passado, que já foi declarado.
///
/// A **base de cálculo** existe porque nem todo tributo incide sobre a receita inteira: no
/// Lucro Presumido, o IRPJ de 15% incide sobre 32% da receita de serviço, e a alíquota
/// efetiva é 4,8%. Sem o campo, a clínica teria de fazer essa conta de cabeça e digitar o
/// resultado — e digitaria 15%, que é o número que ela conhece.
/// </summary>
public class Tributo
{
    public int Id { get; set; }

    /// <summary>Sigla curta (ISS, PIS, COFINS, IRPJ, CSLL, SIMPLES). É o que aparece no detalhe.</summary>
    public string Sigla { get; set; } = string.Empty;

    public string Nome { get; set; } = string.Empty;

    /// <summary>Alíquota nominal, de 0 a 100 — o número que o contador informa.</summary>
    public decimal Percentual { get; set; }

    /// <summary>
    /// Quanto da receita forma a base, de 0 a 100. Padrão 100 (incide sobre tudo).
    /// No Lucro Presumido o IRPJ entra como 15% sobre base 32%.
    /// </summary>
    public decimal BasePercentual { get; set; } = 100m;

    public DateOnly? VigenteDe { get; set; }

    public DateOnly? VigenteAte { get; set; }

    public bool Ativo { get; set; } = true;

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>
    /// O que de fato sai de cada real recebido: alíquota aplicada sobre a base.
    /// É este número que multiplica o valor do recebimento — nunca o nominal.
    /// </summary>
    public decimal AliquotaEfetiva => Percentual * BasePercentual / 100m;

    /// <summary>O tributo vale no dia informado?</summary>
    public bool VigenteEm(DateOnly dia)
        => Ativo
           && (VigenteDe is null || dia >= VigenteDe)
           && (VigenteAte is null || dia <= VigenteAte);

    /// <summary>
    /// pt-BR FIXO, não a cultura da máquina.
    ///
    /// <see cref="Descricao"/> não é só texto de tela: ele é COPIADO para
    /// <c>LancamentoFinanceiro.DetalheImposto</c> e fica gravado no banco. Com a cultura
    /// ambiente, dois postos da mesma clínica — um em pt-BR, outro que o Windows deixou
    /// em en-US — escreveriam "PIS 0,65%" e "PIS 0.65%" na mesma coluna, e o contador
    /// receberia uma coluna com dois idiomas.
    ///
    /// É o espelho da regra do <c>ParametrosService</c>, não a contradição dela: lá se LÊ
    /// número com ponto invariante porque a string precisa voltar a ser número; aqui se
    /// ESCREVE prosa para um leitor brasileiro, e essa prosa nunca é reinterpretada.
    /// A barra do formato de data também é culture-dependente, então entra na mesma regra.
    /// </summary>
    private static readonly System.Globalization.CultureInfo Brasil = new("pt-BR");

    /// <summary>
    /// Como o tributo aparece escrito no detalhe copiado para o lançamento. A base só é
    /// mencionada quando não é 100% — dizer "sobre 100%" em toda linha viraria ruído, e
    /// omiti-la quando ela é 32% esconderia de onde veio a diferença.
    /// </summary>
    public string Descricao => BasePercentual == 100m
        ? string.Format(Brasil, "{0} {1:0.##}%", Sigla, Percentual)
        : string.Format(Brasil, "{0} {1:0.##}% s/ {2:0.##}% = {3:0.####}%",
            Sigla, Percentual, BasePercentual, AliquotaEfetiva);

    /// <summary>Vigência escrita, para a lista.</summary>
    public string Vigencia => (VigenteDe, VigenteAte) switch
    {
        (null, null) => "sem prazo",
        ({ } de, null) => string.Format(Brasil, "desde {0:dd/MM/yyyy}", de),
        (null, { } ate) => string.Format(Brasil, "até {0:dd/MM/yyyy}", ate),
        var (de, ate) => string.Format(Brasil, "{0:dd/MM/yyyy} a {1:dd/MM/yyyy}", de, ate)
    };
}
