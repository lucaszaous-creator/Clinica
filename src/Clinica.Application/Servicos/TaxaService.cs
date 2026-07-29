using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a maquininha e o fisco descontam de um recebimento, calculado na hora da venda.
/// Tudo aqui é COPIADO para o lançamento — nada fica como referência viva.
/// </summary>
public sealed record DeducoesRecebimento(
    decimal? TaxaPercentual,
    decimal? ValorTaxa,
    decimal? AliquotaImposto,
    decimal? ValorImposto,
    DateOnly? PrevisaoRecebimento,
    string? Procedencia)
{
    public static readonly DeducoesRecebimento Nenhuma =
        new(null, null, null, null, null, null);

    public decimal Total => (ValorTaxa ?? 0m) + (ValorImposto ?? 0m);
}

/// <summary>
/// Taxas de cartão e imposto retido (parcela 9).
///
/// O buraco que este serviço fecha: até aqui `LancamentoFinanceiro` só tinha o valor
/// BRUTO. A clínica passava R$ 150 na maquininha, o caixa dizia R$ 150, e o extrato da
/// adquirente trazia R$ 145,20 trinta dias depois — a diferença aparecia no fim do mês
/// como um caixa que não bate, sem ninguém saber de onde veio.
///
/// Três decisões que sustentam o resto:
///
/// 1. **O bruto continua sendo o valor do lançamento.** Taxa e imposto são deduções ao
///    lado, e o líquido é CALCULADO (<see cref="LancamentoFinanceiro.ValorLiquido"/>).
///    Gravar o líquido daria duas verdades sobre o mesmo dinheiro.
/// 2. **A taxa é copiada, não referenciada.** O catálogo tem vigência e é renegociado; o
///    que vale no recebimento de março é o percentual de março. Mesma regra do pacote que
///    copia o catálogo e do repasse que trava o período.
/// 3. **Sem taxa cadastrada, não se inventa desconto.** Devolve
///    <see cref="DeducoesRecebimento.Nenhuma"/> e o lançamento fica só com o bruto — que
///    é a verdade disponível. Chutar uma taxa "de mercado" produziria um líquido errado
///    com aparência de exato.
/// </summary>
public sealed class TaxaService
{
    private readonly IClinicaRepositorio _repo;
    private readonly ParametrosService? _parametros;

    public TaxaService(IClinicaRepositorio repo, ParametrosService? parametros = null)
    {
        _repo = repo;
        _parametros = parametros;
    }

    // ==================== Catálogo ====================

    public Task<IReadOnlyList<TaxaCartao>> CatalogoAsync(
        bool somenteAtivas = false, CancellationToken ct = default)
        => _repo.TaxasCartaoAsync(somenteAtivas, ct);

    public async Task<TaxaCartao> SalvarAsync(
        TaxaCartao dados, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.Adquirente))
            throw new InvalidOperationException("Diga de qual adquirente é a taxa.");
        if (dados.Percentual is < 0m or > 100m)
            throw new InvalidOperationException("O percentual da taxa vai de 0 a 100.");
        if (dados.DiasParaReceber < 0)
            throw new InvalidOperationException("Prazo de recebimento não pode ser negativo.");
        if (dados.ParcelasDe is { } de && dados.ParcelasAte is { } ate && de > ate)
            throw new InvalidOperationException("A faixa de parcelas começa antes de terminar.");
        if (dados.VigenteDe is { } inicio && dados.VigenteAte is { } fim && inicio > fim)
            throw new InvalidOperationException("A vigência começa antes de terminar.");

        var taxa = dados.Id == 0
            ? null
            : await _repo.ObterTaxaCartaoAsync(dados.Id, ct)
              ?? throw new InvalidOperationException("Taxa não encontrada.");

        if (taxa is null)
        {
            taxa = new TaxaCartao { CriadoPor = operador };
            await _repo.AdicionarTaxaCartaoAsync(taxa, ct);
        }

        taxa.Adquirente = dados.Adquirente.Trim();
        taxa.Bandeira = Limpar(dados.Bandeira);
        taxa.Modalidade = dados.Modalidade;
        taxa.ParcelasDe = dados.ParcelasDe;
        taxa.ParcelasAte = dados.ParcelasAte;
        taxa.Percentual = dados.Percentual;
        taxa.DiasParaReceber = dados.DiasParaReceber;
        taxa.VigenteDe = dados.VigenteDe;
        taxa.VigenteAte = dados.VigenteAte;
        taxa.Ativa = dados.Ativa;
        taxa.Observacoes = Limpar(dados.Observacoes);

        await _repo.SalvarAsync(ct);
        return taxa;
    }

    public async Task ExcluirAsync(int taxaId, CancellationToken ct = default)
    {
        await _repo.RemoverTaxaCartaoAsync(taxaId, ct);
        await _repo.SalvarAsync(ct);
    }

    // ==================== Cálculo ====================

    /// <summary>
    /// Acha a taxa que vale para esta venda. A mais ESPECÍFICA ganha: uma regra da
    /// bandeira vence a regra genérica do adquirente — senão a clínica cadastraria a
    /// exceção e continuaria vendo o número da regra geral.
    /// </summary>
    public async Task<TaxaCartao?> ResolverAsync(
        ModalidadeCartao modalidade, DateOnly data, string? adquirente = null,
        string? bandeira = null, int parcelas = 1, CancellationToken ct = default)
    {
        var candidatas = (await _repo.TaxasCartaoAsync(somenteAtivas: true, ct))
            .Where(t => t.Modalidade == modalidade)
            .Where(t => t.VigenteEm(data))
            .Where(t => t.CobreParcelas(parcelas))
            .Where(t => adquirente is null
                        || string.Equals(t.Adquirente, adquirente, StringComparison.OrdinalIgnoreCase))
            .Where(t => t.Bandeira is null
                        || bandeira is null
                        || string.Equals(t.Bandeira, bandeira, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return candidatas
            // Bandeira declarada é mais específica que "todas".
            .OrderByDescending(t => t.Bandeira is not null)
            // Faixa de parcelas declarada é mais específica que "qualquer".
            .ThenByDescending(t => t.ParcelasDe is not null)
            // Entre iguais, a que começou a valer mais tarde é a renegociação mais nova.
            .ThenByDescending(t => t.VigenteDe ?? DateOnly.MinValue)
            .FirstOrDefault();
    }

    /// <summary>
    /// Deduções de um recebimento. Só o cartão tem taxa de adquirente; o imposto, quando
    /// configurado, incide em qualquer forma de recebimento.
    /// </summary>
    public async Task<DeducoesRecebimento> CalcularAsync(
        decimal valorBruto, DateOnly data, FormaPagamento? forma,
        string? adquirente = null, string? bandeira = null, int parcelas = 1,
        bool reterImposto = false, CancellationToken ct = default)
    {
        if (valorBruto <= 0m) return DeducoesRecebimento.Nenhuma;

        decimal? taxaPercentual = null, valorTaxa = null;
        DateOnly? previsao = null;
        string? procedencia = null;

        // As parcelas ENTRAM aqui: sem elas, crédito em 10x seria tratado como crédito à
        // vista e pegaria a taxa errada — justamente a modalidade mais cara.
        if (ModalidadeDe(forma, parcelas) is { } modalidade)
        {
            var taxa = await ResolverAsync(modalidade, data, adquirente, bandeira, parcelas, ct);
            if (taxa is not null)
            {
                taxaPercentual = taxa.Percentual;
                valorTaxa = Arredondar(valorBruto * taxa.Percentual / 100m);
                previsao = data.AddDays(taxa.DiasParaReceber);
                procedencia = taxa.Descricao;
            }
        }

        decimal? aliquota = null, valorImposto = null;
        if (reterImposto)
        {
            var configurada = _parametros is null
                ? 0m
                : await _parametros.ObterAliquotaImpostoAsync(ct);
            if (configurada > 0m)
            {
                aliquota = configurada;
                valorImposto = Arredondar(valorBruto * configurada / 100m);
            }
        }

        return new DeducoesRecebimento(
            taxaPercentual, valorTaxa, aliquota, valorImposto, previsao, procedencia);
    }

    /// <summary>
    /// Que modalidade de cartão corresponde à forma de pagamento. Null = não é cartão, e
    /// portanto não há taxa de adquirente (Pix e dinheiro caem na hora e inteiros).
    /// </summary>
    public static ModalidadeCartao? ModalidadeDe(FormaPagamento? forma, int parcelas = 1) => forma switch
    {
        FormaPagamento.CartaoDebito => ModalidadeCartao.Debito,
        FormaPagamento.CartaoCredito when parcelas > 1 => ModalidadeCartao.CreditoParcelado,
        FormaPagamento.CartaoCredito => ModalidadeCartao.CreditoAVista,
        _ => null
    };

    /// <summary>
    /// Dinheiro arredondado ao centavo, meio para cima — o mesmo critério do extrato da
    /// adquirente. Truncar deixaria centavos sobrando na conciliação todo mês.
    /// </summary>
    private static decimal Arredondar(decimal valor)
        => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    private static string? Limpar(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
