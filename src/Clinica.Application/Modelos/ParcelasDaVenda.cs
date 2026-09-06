using Clinica.Domain.Entities;

namespace Clinica.Application.Modelos;

/// <summary>
/// Como o paciente paga o pacote que está comprando (set/2026).
///
/// A venda de pacote nasceu na parcela 4 <b>sem dinheiro</b>: <c>PacoteService.VenderAsync</c>
/// gravava o pacote e mais nada — nem receita, nem previsão, nem cobrança. Vender dez
/// sessões no balcão não produzia uma linha no caixa, e o mês do particular fechava com
/// uma diferença sem nome. Este record é a metade que faltava, e ela é DECISÃO de quem
/// está no balcão, nunca padrão: à vista ou a prazo, com ou sem entrada, quantas parcelas.
///
/// Duas formas, e nada além delas:
/// <list type="bullet">
///   <item><see cref="AVista"/> — um lançamento REALIZADO na data da compra, com a forma.</item>
///   <item><see cref="APrazo"/> — a entrada (opcional) REALIZADA hoje e o restante em N
///   parcelas PREVISTAS, mensais, contadas do primeiro vencimento. Cada parcela é uma
///   conta a receber COM DONO (o paciente), então a inadimplência a enxerga quando vence
///   e o balcão é avisado na próxima visita.</item>
/// </list>
/// </summary>
public sealed record PagamentoDaVenda
{
    /// <summary>Forma do que é pago AGORA: o total (à vista) ou a entrada (a prazo).</summary>
    public FormaPagamento? FormaDoPagoAgora { get; init; }

    /// <summary>Tudo pago na compra — um lançamento realizado.</summary>
    public bool TudoAgora { get; init; }

    /// <summary>A prazo: o que o paciente pagou na compra. Zero = nada agora.</summary>
    public decimal EntradaAgora { get; init; }

    /// <summary>A prazo: em quantas parcelas o restante se divide.</summary>
    public int Parcelas { get; init; }

    /// <summary>A prazo: vencimento da primeira parcela — as demais são mensais a partir dela.</summary>
    public DateOnly? PrimeiroVencimento { get; init; }

    /// <summary>Categoria do plano de contas para todos os lançamentos da venda.</summary>
    public int? CategoriaId { get; init; }

    public static PagamentoDaVenda AVista(FormaPagamento forma, int? categoriaId = null)
        => new() { TudoAgora = true, FormaDoPagoAgora = forma, CategoriaId = categoriaId };

    public static PagamentoDaVenda APrazo(
        int parcelas, DateOnly primeiroVencimento, decimal entradaAgora = 0m,
        FormaPagamento? formaDaEntrada = null, int? categoriaId = null)
        => new()
        {
            Parcelas = parcelas,
            PrimeiroVencimento = primeiroVencimento,
            EntradaAgora = entradaAgora,
            FormaDoPagoAgora = formaDaEntrada,
            CategoriaId = categoriaId
        };
}

/// <summary>Uma parcela desenhada — o que a tela mostra ANTES de gravar e o que o serviço grava.</summary>
public sealed record ParcelaDaVenda(
    int Numero, int Total, decimal Valor, DateOnly Vencimento, bool PagaAgora)
{
    public string Rotulo => PagaAgora
        ? $"entrada {Valor:C}"
        : $"{Numero}/{Total} · {Valor:C} · vence {Vencimento:dd/MM/yyyy}";
}

/// <summary>
/// Monta os lançamentos de uma venda de pacote. PURO: recebe a venda e a decisão, devolve a
/// lista — sem banco, sem relógio. É o mesmo desenho que a tela usa para mostrar a prévia
/// ("3 parcelas de R$ 400,00, vencendo 06/10, 06/11 e 06/12") e que o serviço grava, para
/// a prévia nunca prometer parcelas diferentes das gravadas.
///
/// Regras que valem registro:
/// <list type="bullet">
///   <item><b>Os centavos que sobram vão para a PRIMEIRA parcela</b> — a soma das parcelas é
///   sempre o valor da venda, ao centavo. Três parcelas de R$ 100,01 são 33,35 + 33,33 + 33,33.</item>
///   <item><b>Mensal, contado do PRIMEIRO vencimento</b> (<c>AddMonths(k)</c> sobre a
///   primeira data), nunca "a anterior mais 30 dias" — a regra da conta recorrente
///   (parcela 12): encadear faria o dia 31 virar dia 28 para sempre por causa de fevereiro.</item>
///   <item><b>Pacote de valor zero não gera lançamento</b> (o voucher presenteado, a cortesia):
///   uma parcela de R$ 0,00 seria uma conta a receber que ninguém deve.</item>
///   <item><b>Entrada igual ao total é à vista</b>, sem parcela nenhuma: "3 parcelas de
///   R$ 0,00" para uma venda quitada na hora é o mesmo defeito acima.</item>
/// </list>
/// </summary>
public static class ParcelasDaVenda
{
    /// <summary>Desenha as parcelas (sem gravar). Lança quando a decisão não fecha.</summary>
    public static IReadOnlyList<ParcelaDaVenda> Desenhar(
        decimal valorDaVenda, DateOnly dataCompra, PagamentoDaVenda pagamento)
    {
        ArgumentNullException.ThrowIfNull(pagamento);

        if (valorDaVenda < 0m)
            throw new InvalidOperationException("O valor do pacote não pode ser negativo.");
        if (valorDaVenda == 0m) return [];

        if (pagamento.TudoAgora)
        {
            if (pagamento.FormaDoPagoAgora is null)
                throw new InvalidOperationException("Diga como o paciente pagou (Pix, cartão, dinheiro…).");
            return [new ParcelaDaVenda(1, 1, valorDaVenda, dataCompra, PagaAgora: true)];
        }

        if (pagamento.EntradaAgora < 0m)
            throw new InvalidOperationException("A entrada não pode ser negativa.");
        if (pagamento.EntradaAgora > valorDaVenda)
            throw new InvalidOperationException("A entrada é maior que o valor do pacote.");
        if (pagamento.EntradaAgora > 0m && pagamento.FormaDoPagoAgora is null)
            throw new InvalidOperationException("Diga como a entrada foi paga (Pix, cartão, dinheiro…).");

        var parcelas = new List<ParcelaDaVenda>();
        if (pagamento.EntradaAgora > 0m)
            parcelas.Add(new ParcelaDaVenda(0, 0, pagamento.EntradaAgora, dataCompra, PagaAgora: true));

        var restante = valorDaVenda - pagamento.EntradaAgora;
        if (restante == 0m) return parcelas;

        if (pagamento.Parcelas < 1)
            throw new InvalidOperationException("Diga em quantas parcelas o restante será pago.");
        if (pagamento.Parcelas > 48)
            throw new InvalidOperationException("Mais de 48 parcelas não é venda de pacote — confira o número.");
        if (pagamento.PrimeiroVencimento is not { } primeiro)
            throw new InvalidOperationException("Diga quando vence a primeira parcela.");
        if (primeiro < dataCompra)
            throw new InvalidOperationException("A primeira parcela não pode vencer antes da compra.");

        var n = pagamento.Parcelas;
        var cada = Math.Round(restante / n, 2, MidpointRounding.ToZero);
        var primeira = restante - cada * (n - 1);

        for (var k = 0; k < n; k++)
            parcelas.Add(new ParcelaDaVenda(
                k + 1, n, k == 0 ? primeira : cada, primeiro.AddMonths(k), PagaAgora: false));

        return parcelas;
    }

    /// <summary>
    /// Os lançamentos que a venda grava — um por parcela desenhada, já apontando para o
    /// pacote pela NAVEGAÇÃO (o Id ainda não existe quando eles são montados).
    /// </summary>
    public static IReadOnlyList<LancamentoFinanceiro> Montar(
        PacotePaciente pacote, string paciente, PagamentoDaVenda pagamento, string? operador)
    {
        ArgumentNullException.ThrowIfNull(pacote);

        var desenho = Desenhar(pacote.Valor, pacote.DataCompra, pagamento);

        return desenho.Select(p => new LancamentoFinanceiro
        {
            Data = pacote.DataCompra,
            Tipo = TipoLancamento.Entrada,
            Descricao = Descrever(pacote.Nome, paciente, p),
            Valor = p.Valor,
            Status = p.PagaAgora ? StatusLancamento.Realizado : StatusLancamento.Previsto,
            DataPagamento = p.PagaAgora ? pacote.DataCompra : null,
            // A parcela prevista não tem forma — ela é decidida no dia em que o paciente
            // paga, e é o `RealizarAsync` que a grava.
            FormaPagamento = p.PagaAgora ? pagamento.FormaDoPagoAgora : null,
            DataVencimento = p.PagaAgora ? null : p.Vencimento,
            CategoriaFinanceiraId = pagamento.CategoriaId,
            PacienteId = pacote.PacienteId,
            PacotePaciente = pacote,
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        }).ToList();
    }

    /// <summary>Frase da venda para a trilha e para o snackbar — a decisão por extenso.</summary>
    public static string Resumir(IReadOnlyList<ParcelaDaVenda> desenho)
    {
        if (desenho.Count == 0) return "sem valor a receber";

        var agora = desenho.Where(p => p.PagaAgora).Sum(p => p.Valor);
        var previstas = desenho.Where(p => !p.PagaAgora).ToList();

        if (previstas.Count == 0) return $"à vista, {agora:C}";

        var parcelas = previstas.Count == 1
            ? $"1 parcela de {previstas[0].Valor:C} vencendo {previstas[0].Vencimento:dd/MM/yyyy}"
            : $"{previstas.Count} parcelas ({previstas.Sum(p => p.Valor):C}) a partir de {previstas[0].Vencimento:dd/MM/yyyy}";

        return agora > 0m ? $"entrada {agora:C} + {parcelas}" : parcelas;
    }

    // A coluna aceita 200 caracteres; nome do pacote (120) mais nome do paciente (120)
    // passam disso, e o Postgres RECUSA a gravação inteira (a lição do 22001). Corta
    // aqui, em texto de descrição — nunca em registro clínico.
    private static string Descrever(string pacote, string paciente, ParcelaDaVenda p)
    {
        var miolo = p.PagaAgora
            ? (p.Total == 1 ? string.Empty : " — entrada")
            : $" — parcela {p.Numero}/{p.Total}";
        var texto = $"Pacote {pacote}{miolo} ({paciente})";
        return texto.Length <= 200 ? texto : texto[..200];
    }
}
