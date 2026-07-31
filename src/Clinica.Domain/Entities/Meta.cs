namespace Clinica.Domain.Entities;

/// <summary>
/// O que a direção resolveu medir. São três porque são os três que ela persegue de
/// verdade: quanto entra, quantas sessões acontecem e quanto da agenda foi usada.
/// </summary>
public enum IndicadorMeta
{
    /// <summary>Receita realizada no mês, em reais.</summary>
    Faturamento,

    /// <summary>Sessões atendidas no mês (o volume, independente do preço).</summary>
    Sessoes,

    /// <summary>Ocupação média da agenda no mês, de 0 a 100.</summary>
    Ocupacao
}

/// <summary>
/// A meta de um mês para um indicador (parcela 28).
///
/// Até aqui o painel da direção comparava o mês com o **mês anterior** — variação, não
/// desempenho. Variação responde "melhorou?" e nunca responde "chegamos onde a gente
/// disse que ia chegar?", que é a única pergunta que faz alguém mudar de conduta no dia
/// 12 em vez de descobrir no dia 31.
///
/// Duas decisões de modelagem que parecem detalhe e não são:
///
/// 1. **Meta é fato datado, não configuração.** Ela mora numa linha por mês, e não numa
///    chave do <c>ParametrosService</c>. Se fosse configuração, subir a meta em março
///    reescreveria o alvo de janeiro e fevereiro — e a clínica perderia a única prova de
///    que bateu (ou não bateu) o que tinha combinado na época. É a mesma razão de a taxa
///    de cartão e o preço do convênio terem vigência.
/// 2. **Meta ausente é diferente de meta zero.** Não existe valor padrão: um mês sem
///    linha significa "a direção não decidiu", e a tela mostra "—". Inventar zero faria
///    todo mês não planejado aparecer como meta batida, e a direção aprenderia a ignorar
///    o verde.
///
/// <see cref="ProfissionalId"/> nulo é a meta da CLÍNICA. Com profissional, é a meta
/// dele — porque "a clínica bateu" costuma esconder um profissional lotado carregando
/// outro com a agenda vazia, e é essa diferença que muda a conversa.
/// </summary>
public class MetaMensal
{
    public int Id { get; set; }

    public int Ano { get; set; }

    /// <summary>1 a 12.</summary>
    public int Mes { get; set; }

    public IndicadorMeta Indicador { get; set; }

    /// <summary>
    /// O alvo. A unidade é a do indicador: reais em <see cref="IndicadorMeta.Faturamento"/>,
    /// contagem em <see cref="IndicadorMeta.Sessoes"/>, percentual (0 a 100) em
    /// <see cref="IndicadorMeta.Ocupacao"/>.
    /// </summary>
    public decimal Valor { get; set; }

    /// <summary>Null = meta da clínica inteira.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>Primeiro dia do mês da meta — o recorte que os serviços de leitura usam.</summary>
    public DateOnly Inicio => new(Ano, Mes, 1);

    /// <summary>Último dia do mês da meta.</summary>
    public DateOnly Fim => Inicio.AddMonths(1).AddDays(-1);

    /// <summary>Rótulo do indicador, escrito como a direção fala.</summary>
    public static string Rotular(IndicadorMeta indicador) => indicador switch
    {
        IndicadorMeta.Faturamento => "Faturamento",
        IndicadorMeta.Sessoes => "Sessões",
        IndicadorMeta.Ocupacao => "Ocupação",
        _ => indicador.ToString()
    };

    /// <summary>Unidade do indicador, para a tela não somar laranja com maçã.</summary>
    public static string Unidade(IndicadorMeta indicador) => indicador switch
    {
        IndicadorMeta.Faturamento => "R$",
        IndicadorMeta.Sessoes => "sessões",
        IndicadorMeta.Ocupacao => "%",
        _ => string.Empty
    };
}

/// <summary>
/// Teto de gasto de uma categoria num mês (parcela 31).
///
/// A clínica cadastra plano de contas desde a parcela 4 e vê o gasto por categoria no
/// fluxo de caixa desde a 13 — as duas coisas respondem "quanto saiu de material este
/// mês". Nenhuma responde **"quanto podia sair"**. Sem teto, o gasto só é julgado contra
/// o mês anterior, que é a mesma armadilha que a meta resolveu para o faturamento:
/// gastar 10% menos que um mês ruim aparece como economia.
///
/// É irmão de <see cref="MetaMensal"/> e segue as mesmas regras — uma linha por mês
/// (fato datado, reajustar agosto não reescreve julho) e ausência diferente de zero
/// (sem linha é "não decidimos", zero é "decidimos que não se gasta nada").
///
/// Só categoria de SAÍDA tem teto: alvo de receita é meta, e meta já existe.
/// </summary>
public class OrcamentoCategoria
{
    public int Id { get; set; }

    public int Ano { get; set; }

    /// <summary>1 a 12.</summary>
    public int Mes { get; set; }

    public int CategoriaFinanceiraId { get; set; }
    public CategoriaFinanceira? Categoria { get; set; }

    /// <summary>Quanto pode sair da categoria no mês, em reais.</summary>
    public decimal Teto { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }
}
