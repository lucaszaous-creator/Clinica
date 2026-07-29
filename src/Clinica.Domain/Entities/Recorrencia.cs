namespace Clinica.Domain.Entities;

/// <summary>De quanto em quanto tempo a conta se repete.</summary>
public enum PeriodicidadeRecorrencia
{
    Semanal,
    Quinzenal,
    Mensal,
    Bimestral,
    Trimestral,
    Semestral,
    Anual
}

/// <summary>
/// Uma conta que se repete — aluguel, luz, internet, contador, a mensalidade do
/// software, o repasse fixo de um profissional.
///
/// A recorrência é um MOLDE, não um lançamento. Ela não entra em nenhum total: quem
/// entra no caixa é o <see cref="LancamentoFinanceiro"/> que ela GERA, e que a partir
/// daí tem vida própria — pode ter o valor corrigido (a conta de luz nunca vem igual),
/// ser paga adiantada ou cancelada, sem que isso reescreva o molde nem as parcelas dos
/// outros meses. Se a recorrência fosse "o lançamento que se repete", corrigir a luz de
/// março mudaria a de janeiro, que já foi paga e conciliada.
///
/// Mudar o valor daqui vale só para o que ainda não foi gerado — mesma regra da vigência
/// do repasse e do preço do pacote.
/// </summary>
public class LancamentoRecorrente
{
    public int Id { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public TipoLancamento Tipo { get; set; } = TipoLancamento.Saida;

    /// <summary>Valor previsto de cada ocorrência. Sempre positivo — o sinal vem do tipo.</summary>
    public decimal Valor { get; set; }

    public PeriodicidadeRecorrencia Periodicidade { get; set; } = PeriodicidadeRecorrencia.Mensal;

    /// <summary>
    /// Primeira data de vencimento. É ela que ancora toda a série: as ocorrências
    /// seguintes saem daqui somando a periodicidade, e não do dia em que a conta foi
    /// cadastrada.
    /// </summary>
    public DateOnly PrimeiroVencimento { get; set; }

    /// <summary>
    /// Último vencimento previsto. Null = sem fim (aluguel). Contrato com prazo põe a
    /// data e a série para sozinha — melhor que depender de alguém lembrar de desativar.
    /// </summary>
    public DateOnly? UltimoVencimento { get; set; }

    public int? CategoriaFinanceiraId { get; set; }
    public CategoriaFinanceira? Categoria { get; set; }

    public FormaPagamento? FormaPagamento { get; set; }

    /// <summary>
    /// Desativada para de gerar, mas o que já foi gerado permanece: contas de meses
    /// passados não somem porque o contrato acabou.
    /// </summary>
    public bool Ativa { get; set; } = true;

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>Texto curto da periodicidade, para a lista.</summary>
    public string PeriodicidadeTexto => Periodicidade switch
    {
        PeriodicidadeRecorrencia.Semanal => "toda semana",
        PeriodicidadeRecorrencia.Quinzenal => "a cada 15 dias",
        PeriodicidadeRecorrencia.Mensal => "todo mês",
        PeriodicidadeRecorrencia.Bimestral => "a cada 2 meses",
        PeriodicidadeRecorrencia.Trimestral => "a cada 3 meses",
        PeriodicidadeRecorrencia.Semestral => "a cada 6 meses",
        PeriodicidadeRecorrencia.Anual => "todo ano",
        _ => Periodicidade.ToString()
    };

    /// <summary>
    /// O vencimento seguinte a <paramref name="vencimento"/>, ou null quando a série
    /// terminou.
    ///
    /// O mês é somado com <see cref="DateOnly.AddMonths"/>, que ENCURTA o dia quando o
    /// mês seguinte é mais curto: vencimento em 31/01 vira 28/02. E é justamente por isso
    /// que a próxima ocorrência sai sempre do <see cref="PrimeiroVencimento"/> mais N
    /// períodos, nunca da anterior mais um — encadear a partir do 28/02 travaria a conta
    /// no dia 28 para o resto da vida, e ninguém entenderia por que o aluguel do dia 31
    /// virou aluguel do dia 28 em março.
    /// </summary>
    public DateOnly? ProximoDepoisDe(DateOnly vencimento)
    {
        var n = 0;
        DateOnly atual;
        do
        {
            n++;
            atual = Somar(PrimeiroVencimento, n);
            // Série com passo de zero dia não existe, mas se existisse laçaria para
            // sempre — a guarda é barata e o custo de não tê-la é o app travado.
            if (n > 5000) return null;
        } while (atual <= vencimento);

        return UltimoVencimento is { } fim && atual > fim ? null : atual;
    }

    /// <summary>Todas as ocorrências com vencimento até <paramref name="ate"/>, da primeira à última.</summary>
    public IReadOnlyList<DateOnly> VencimentosAte(DateOnly ate)
    {
        var lista = new List<DateOnly>();
        if (PrimeiroVencimento > ate) return lista;
        if (UltimoVencimento is { } f && f < PrimeiroVencimento) return lista;

        var n = 0;
        while (true)
        {
            var data = Somar(PrimeiroVencimento, n);
            if (data > ate) break;
            if (UltimoVencimento is { } fim && data > fim) break;
            lista.Add(data);
            n++;
            if (n > 5000) break;
        }

        return lista;
    }

    private DateOnly Somar(DateOnly origem, int periodos) => Periodicidade switch
    {
        PeriodicidadeRecorrencia.Semanal => origem.AddDays(7 * periodos),
        PeriodicidadeRecorrencia.Quinzenal => origem.AddDays(15 * periodos),
        PeriodicidadeRecorrencia.Mensal => origem.AddMonths(periodos),
        PeriodicidadeRecorrencia.Bimestral => origem.AddMonths(2 * periodos),
        PeriodicidadeRecorrencia.Trimestral => origem.AddMonths(3 * periodos),
        PeriodicidadeRecorrencia.Semestral => origem.AddMonths(6 * periodos),
        PeriodicidadeRecorrencia.Anual => origem.AddYears(periodos),
        _ => origem.AddMonths(periodos)
    };

    /// <summary>
    /// Chave de idempotência da ocorrência: <c>REC:{id}:{aaaa-MM-dd}</c>. Gravada no
    /// lançamento gerado e com índice único no banco — rodar a geração duas vezes (dois
    /// postos abrindo o app na mesma manhã) não pode criar o aluguel duas vezes. Mesmo
    /// padrão do <c>ContatoCampanha.Origem</c>, pelo mesmo motivo.
    /// </summary>
    public string OrigemDe(DateOnly vencimento) => $"REC:{Id}:{vencimento:yyyy-MM-dd}";
}
