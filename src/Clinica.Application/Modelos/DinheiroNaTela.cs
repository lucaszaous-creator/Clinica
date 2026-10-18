using System.Globalization;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>De onde veio uma parcela da projeção — a tela escreve a procedência de cada uma.</summary>
public enum OrigemDaParcela
{
    /// <summary>Conta a pagar ou a receber já lançada, com vencimento.</summary>
    ContaLancada,

    /// <summary>Ocorrência de conta fixa que ainda não foi gerada (o molde diz que ela vem).</summary>
    ContaFixaNaoGerada,

    /// <summary>Depósito de maquininha esperado (o líquido, que é o que cai na conta).</summary>
    Cartao,

    /// <summary>Receita de guia de convênio lançada como "a receber", sem data prometida.</summary>
    GuiaAReceber,

    /// <summary>Qualquer outro previsto sem vencimento.</summary>
    PrevistoSemData
}

/// <summary>
/// Uma parcela da projeção de caixa: quanto, quando, e DE ONDE o sistema tirou esse número.
/// <see cref="Data"/> nula é parcela sem data prometida — ela não entra na linha do tempo.
/// </summary>
public sealed record ParcelaProjetada(
    DateOnly? Data,
    TipoLancamento Tipo,
    decimal Valor,
    string Descricao,
    string Procedencia,
    OrigemDaParcela Origem)
{
    /// <summary>O valor com o sinal do caixa: entrada soma, saída subtrai.</summary>
    public decimal ComSinal => Tipo == TipoLancamento.Entrada ? Valor : -Valor;
}

/// <summary>Uma semana da projeção, com o acumulado desde hoje.</summary>
public sealed record SemanaProjetada(
    DateOnly Inicio,
    DateOnly Fim,
    decimal Entradas,
    decimal Saidas,
    decimal Acumulado,
    IReadOnlyList<ParcelaProjetada> Parcelas)
{
    public decimal Variacao => Entradas - Saidas;

    public bool Vazia => Parcelas.Count == 0;
}

/// <summary>
/// A projeção de caixa dos próximos dias (out/2026, etapa "Dinheiro" do Gerente).
///
/// Três blocos que NUNCA se somam num número só, pela mesma honestidade do fluxo de caixa:
/// a linha do tempo (o que tem data dentro do horizonte), o ATRASADO (o que já devia ter
/// acontecido — pôr na semana de hoje seria apostar que acontece agora) e o SEM DATA (a guia
/// a receber que o convênio não prometeu quando paga — inventar o dia seria inventar o número).
/// </summary>
public sealed record ProjecaoDeCaixa(
    DateOnly Hoje,
    DateOnly Ate,
    IReadOnlyList<SemanaProjetada> Semanas,
    IReadOnlyList<ParcelaProjetada> Atrasadas,
    IReadOnlyList<ParcelaProjetada> SemData)
{
    public decimal Entradas => Semanas.Sum(s => s.Entradas);

    public decimal Saidas => Semanas.Sum(s => s.Saidas);

    /// <summary>Quanto o caixa sobe ou desce no horizonte. NÃO é saldo em conta.</summary>
    public decimal Variacao => Entradas - Saidas;

    /// <summary>A semana em que o acumulado chega ao ponto mais baixo, quando ele fica negativo.</summary>
    public SemanaProjetada? PiorSemana => Semanas
        .Where(s => s.Acumulado < 0m)
        .OrderBy(s => s.Acumulado).ThenBy(s => s.Inicio)
        .FirstOrDefault();

    public decimal AtrasadoAReceber => Atrasadas.Where(p => p.Tipo == TipoLancamento.Entrada).Sum(p => p.Valor);

    public decimal AtrasadoAPagar => Atrasadas.Where(p => p.Tipo == TipoLancamento.Saida).Sum(p => p.Valor);

    public decimal SemDataAReceber => SemData.Where(p => p.Tipo == TipoLancamento.Entrada).Sum(p => p.Valor);

    public decimal SemDataAPagar => SemData.Where(p => p.Tipo == TipoLancamento.Saida).Sum(p => p.Valor);

    /// <summary>Quanto da linha do tempo veio de cada origem, para os cartões do topo.</summary>
    public decimal Total(OrigemDaParcela origem, TipoLancamento tipo) => Semanas
        .SelectMany(s => s.Parcelas)
        .Where(p => p.Origem == origem && p.Tipo == tipo)
        .Sum(p => p.Valor);

    public bool Vazia => Semanas.All(s => s.Vazia) && Atrasadas.Count == 0 && SemData.Count == 0;
}

/// <summary>
/// O que a entrada DINHEIRO do Gerente afirma (out/2026): o resultado por competência ao lado
/// do de caixa e a projeção de caixa de 90 dias. Puro — a tela só desenha, e cada frase que a
/// direção lê tem teste (<c>DinheiroNaTelaTests</c>).
/// </summary>
public static class DinheiroNaTela
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O horizonte da projeção, em dias.</summary>
    public const int HorizontePadrao = 90;

    /// <summary>A frase que a tela escreve sob a coluna da competência.</summary>
    public const string RegimeDeCompetencia =
        "Competência: o que pertence ao mês pela data do fato, recebido ou não, pago ou não. Só entra o que tem lançamento no financeiro.";

    /// <summary>A frase que a tela escreve sob a projeção: o que ela é, e o que ela não é.</summary>
    public const string NaoESaldo =
        "Não é saldo em conta: a clínica não cadastra saldo inicial. É quanto o caixa sobe ou desce a partir de hoje, com o que o sistema já sabe.";

    // ==================== Competência ====================

    /// <summary>A frase do mês por competência — a mesma forma da do caixa.</summary>
    public static string ResumoCompetencia(ResultadoPorCompetencia c) => c.Vazio
        ? "Nenhum lançamento com competência neste mês."
        : c.Prejuizo
            ? $"Pela competência, o mês fecha {Dinheiro(Math.Abs(c.Resultado))} no vermelho."
            : $"Pela competência, sobram {Dinheiro(c.Resultado)}"
              + (c.Margem is { } m ? $" — {LeituraDoResultado.Margem(m)} do faturado." : ".");

    /// <summary>
    /// O que ainda não tem valor no financeiro. Nula quando não há nada — e "não conferido"
    /// tem frase própria, porque não é o mesmo que "nada".
    /// </summary>
    public static string? ProducaoSemValor(ResultadoPorCompetencia c)
    {
        if (c.GuiasSemReceita is not { } guias || c.SessoesParticularesSemReceita is not { } sessoes)
            return "Não foi possível conferir se há produção do mês sem valor lançado.";
        if (guias == 0 && sessoes == 0) return null;

        var partes = new List<string>(2);
        if (guias > 0) partes.Add(guias == 1 ? "1 guia baixada" : $"{guias} guias baixadas");
        if (sessoes > 0) partes.Add(sessoes == 1 ? "1 sessão particular" : $"{sessoes} sessões particulares");
        return $"{string.Join(" e ", partes)} do mês ainda sem valor lançado — ficam fora deste número, que está abaixo do real.";
    }

    /// <summary>
    /// A diferença entre os dois regimes, explicada. Ela é o que pertence ao mês e ainda não
    /// se moveu, mais o que se moveu agora e é de outro mês — sem a frase, dois "resultados do
    /// mês" diferentes na mesma tela pareceriam erro.
    /// </summary>
    public static string DiferencaEntreRegimes(ResultadoMensal caixa, ResultadoPorCompetencia c)
    {
        var diferenca = c.Resultado - caixa.Resultado;
        var pendente = $"a receber {Dinheiro(c.ReceitaAReceber)}, a pagar {Dinheiro(c.DespesasAPagar)}";
        if (diferenca == 0m)
            return $"Os dois regimes dão o mesmo resultado ({pendente}).";
        var lado = diferenca > 0m ? "acima" : "abaixo";
        return $"São {Dinheiro(Math.Abs(diferenca))} {lado} do caixa: o que pertence ao mês e ainda não se moveu ({pendente}), "
               + "mais o que se moveu agora e é de outro mês.";
    }

    // ==================== Projeção de caixa ====================

    /// <summary>
    /// Monta a projeção a partir das quatro fontes, sem ir ao banco. Contas em aberto e
    /// depósitos de cartão vencidos vão para o bloco ATRASADO; parcelas sem data, para o SEM
    /// DATA; o resto cai na semana do seu dia. As fixas não geradas chegam já filtradas (sem
    /// as ocorrências que viraram lançamento — inclusive as canceladas, que não vão acontecer).
    /// </summary>
    public static ProjecaoDeCaixa Projetar(
        DateOnly hoje,
        int dias,
        IReadOnlyList<LancamentoFinanceiro> contasEmAberto,
        IReadOnlyList<(LancamentoRecorrente Molde, DateOnly Vencimento)> fixasNaoGeradas,
        IReadOnlyList<DepositoEsperado> depositos,
        IReadOnlyList<LancamentoFinanceiro> previstosSemVencimento)
    {
        if (dias < 1) dias = 1;
        var ate = hoje.AddDays(dias - 1);

        var comData = new List<ParcelaProjetada>();
        var atrasadas = new List<ParcelaProjetada>();
        var semData = new List<ParcelaProjetada>();

        foreach (var l in contasEmAberto)
        {
            if (l.Status != StatusLancamento.Previsto || l.DataVencimento is not { } v) continue;
            if (v > ate) continue;
            var p = new ParcelaProjetada(v, l.Tipo, l.Valor, ContasDaTela.Descricao(l), ProcedenciaDaConta(l, v, hoje),
                OrigemDaParcela.ContaLancada);
            (v < hoje ? atrasadas : comData).Add(p);
        }

        foreach (var (molde, vencimento) in fixasNaoGeradas)
        {
            if (vencimento < hoje || vencimento > ate) continue;
            comData.Add(new ParcelaProjetada(vencimento, molde.Tipo, molde.Valor, molde.Descricao,
                $"Conta fixa ({molde.PeriodicidadeTexto}) ainda não gerada · vence {Data(vencimento)} · valor do molde",
                OrigemDaParcela.ContaFixaNaoGerada));
        }

        foreach (var d in depositos)
        {
            if (d.Previsao > ate) continue;
            var vendas = d.Quantidade == 1 ? "1 venda" : $"{d.Quantidade} vendas";
            var procedencia = $"Cartão · {d.Adquirente} · {vendas} · bruto {Dinheiro(d.Bruto)}, taxa {Dinheiro(d.Taxa)}"
                              + (d.Previsao < hoje ? $" · devia ter caído em {Data(d.Previsao)}" : $" · previsto para {Data(d.Previsao)}");
            var p = new ParcelaProjetada(d.Previsao, TipoLancamento.Entrada, d.Liquido, $"Depósito {d.Adquirente}",
                procedencia, OrigemDaParcela.Cartao);
            (d.Previsao < hoje ? atrasadas : comData).Add(p);
        }

        foreach (var l in previstosSemVencimento)
        {
            if (l.Status != StatusLancamento.Previsto || l.DataVencimento is not null) continue;
            var guia = l.Tipo == TipoLancamento.Entrada
                       && (l.FormaPagamento == FormaPagamento.Convenio || l.CodigoFaturamentoId is not null);
            semData.Add(new ParcelaProjetada(null, l.Tipo, l.Valor, ContasDaTela.Descricao(l),
                guia
                    ? $"Receita de guia a receber · baixada em {Data(l.Data)} · o convênio não prometeu data"
                    : $"Previsto sem vencimento · competência {Data(l.Data)}",
                guia ? OrigemDaParcela.GuiaAReceber : OrigemDaParcela.PrevistoSemData));
        }

        var semanas = new List<SemanaProjetada>();
        var acumulado = 0m;
        for (var inicio = hoje; inicio <= ate; inicio = inicio.AddDays(7))
        {
            var fim = inicio.AddDays(6) < ate ? inicio.AddDays(6) : ate;
            var daSemana = comData
                .Where(p => p.Data >= inicio && p.Data <= fim)
                .OrderBy(p => p.Data).ThenBy(p => p.Tipo).ThenByDescending(p => p.Valor)
                .ToList();
            var entradas = daSemana.Where(p => p.Tipo == TipoLancamento.Entrada).Sum(p => p.Valor);
            var saidas = daSemana.Where(p => p.Tipo == TipoLancamento.Saida).Sum(p => p.Valor);
            acumulado += entradas - saidas;
            semanas.Add(new SemanaProjetada(inicio, fim, entradas, saidas, acumulado, daSemana));
        }

        return new ProjecaoDeCaixa(hoje, ate, semanas,
            atrasadas.OrderBy(p => p.Data).ThenBy(p => p.Tipo).ToList(),
            semData.OrderBy(p => p.Tipo).ThenByDescending(p => p.Valor).ToList());
    }

    /// <summary>A frase do topo da projeção.</summary>
    public static string ResumoDaProjecao(ProjecaoDeCaixa p)
    {
        if (p.Vazia)
            return "Nada previsto para os próximos dias: sem contas lançadas, contas fixas ou depósitos de cartão a esperar.";
        var direcao = p.Variacao >= 0m ? "sobe" : "desce";
        return $"Até {Data(p.Ate)} devem entrar {Dinheiro(p.Entradas)} e sair {Dinheiro(p.Saidas)}: o caixa {direcao} {Dinheiro(Math.Abs(p.Variacao))}.";
    }

    /// <summary>O pior momento, quando o acumulado fica negativo — é a semana que pede providência.</summary>
    public static string? PiorMomento(ProjecaoDeCaixa p) => p.PiorSemana is { } s
        ? $"O ponto mais baixo é a semana de {Data(s.Inicio)}: {Dinheiro(Math.Abs(s.Acumulado))} abaixo de hoje, se nada mais entrar."
        : null;

    /// <summary>O que ficou fora da linha do tempo, e por quê. Nula quando não ficou nada.</summary>
    public static string? ForaDaLinhaDoTempo(ProjecaoDeCaixa p)
    {
        var partes = new List<string>();
        if (p.Atrasadas.Count > 0)
            partes.Add($"já vencido: {Dinheiro(p.AtrasadoAReceber)} a receber e {Dinheiro(p.AtrasadoAPagar)} a pagar");
        if (p.SemData.Count > 0)
            partes.Add($"sem data: {Dinheiro(p.SemDataAReceber)} a receber e {Dinheiro(p.SemDataAPagar)} a pagar");
        return partes.Count == 0
            ? null
            : $"Fora da conta acima — {string.Join("; ", partes)}. Pôr isso numa semana seria adivinhar quando acontece.";
    }

    /// <summary>"03/11 a 09/11" — ou o dia só, quando a semana é de um dia.</summary>
    public static string Periodo(SemanaProjetada s) =>
        s.Inicio == s.Fim ? Data(s.Inicio) : $"{Data(s.Inicio)} a {Data(s.Fim)}";

    /// <summary>O nome da origem, para o filtro e a legenda.</summary>
    public static string Rotulo(OrigemDaParcela o) => o switch
    {
        OrigemDaParcela.ContaLancada => "Conta lançada",
        OrigemDaParcela.ContaFixaNaoGerada => "Conta fixa a gerar",
        OrigemDaParcela.Cartao => "Cartão",
        OrigemDaParcela.GuiaAReceber => "Guia a receber",
        _ => "Sem vencimento"
    };

    private static string ProcedenciaDaConta(LancamentoFinanceiro l, DateOnly vencimento, DateOnly hoje)
    {
        var de = ContasDaTela.DeContaFixa(l) ? "Conta fixa já gerada" : "Conta lançada";
        var quando = vencimento < hoje ? $"venceu em {Data(vencimento)}" : $"vence {Data(vencimento)}";
        var contexto = ContasDaTela.Contexto(l);
        return string.IsNullOrEmpty(contexto) ? $"{de} · {quando}" : $"{de} · {quando} · {contexto}";
    }

    private static string Data(DateOnly d) => d.ToString("dd/MM", PtBr);

    private static string Dinheiro(decimal v) => v.ToString("C", PtBr);
}
