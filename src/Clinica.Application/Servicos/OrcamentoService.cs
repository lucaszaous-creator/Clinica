using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Uma categoria de despesa com o teto do mês e o que já foi gasto.</summary>
public sealed record OrcamentoApurado(
    int CategoriaId,
    string Categoria,
    decimal Teto,
    decimal Gasto,
    decimal? Comprometido)
{
    /// <summary>Quanto do teto já foi consumido, de 0 em diante. Nulo com teto zero.</summary>
    public double? PercentualUsado => Teto > 0m
        ? Math.Round((double)(Gasto / Teto) * 100.0, 1)
        : null;

    /// <summary>O que ainda cabe no mês. Negativo = estourou.</summary>
    public decimal Saldo => Teto - Gasto;

    public bool Estourou => Gasto > Teto;

    /// <summary>
    /// Passou de <see cref="OrcamentoService.FaixaDeAtencao"/> do teto sem ter estourado
    /// ainda — é aqui que dá para segurar a compra do fim do mês.
    /// </summary>
    public bool EmAtencao => !Estourou
                             && PercentualUsado >= OrcamentoService.FaixaDeAtencao;

    /// <summary>
    /// Contas PREVISTAS da categoria que ainda vão vencer no mês. Somadas ao gasto, é
    /// o que o mês fecha se nada mudar — e é o número que muda a decisão, porque o teto
    /// costuma estourar no que já foi contratado, não no que já foi pago.
    /// </summary>
    public decimal? Projetado => Comprometido is { } c ? Gasto + c : null;

    public bool VaiEstourar => Projetado is { } p && p > Teto && !Estourou;
}

/// <summary>
/// Teto de gasto por categoria (parcela 31).
///
/// A clínica cadastra plano de contas desde a parcela 4 e vê o gasto por categoria no
/// fluxo de caixa desde a 13 — as duas coisas respondem "quanto saiu de material este
/// mês". Nenhuma responde **"quanto podia sair"**. Sem teto, o gasto só é julgado contra
/// o mês anterior, que é a mesma armadilha que as metas resolveram para o faturamento:
/// gastar 10% menos que um mês ruim aparece como economia.
///
/// Três decisões:
///
/// 1. **O teto vale por categoria e por mês, e é fato datado** — como a meta. Reajustar o
///    teto de agosto não pode reescrever o de julho, senão a clínica perde a régua contra
///    a qual julgou o mês passado.
/// 2. **Só categoria de SAÍDA tem teto.** Teto de receita seria meta, e meta já existe;
///    misturar as duas faria a tela somar laranja com maçã.
/// 3. **O comprometido conta separado do gasto.** Conta prevista que ainda vai vencer não
///    é dinheiro que saiu, mas é dinheiro que já foi decidido — e o teto costuma estourar
///    no que foi contratado, não no que foi pago. Somar os dois num número só esconderia
///    qual dos dois problemas a clínica tem.
/// </summary>
public sealed class OrcamentoService
{
    /// <summary>Percentual do teto a partir do qual a categoria entra em atenção.</summary>
    public const double FaixaDeAtencao = 80.0;

    private readonly IClinicaRepositorio _repo;
    private readonly ContasService _contas;

    public OrcamentoService(IClinicaRepositorio repo, ContasService contas)
    {
        _repo = repo;
        _contas = contas;
    }

    public Task<IReadOnlyList<OrcamentoCategoria>> DoMesAsync(
        int ano, int mes, CancellationToken ct = default)
        => _repo.OrcamentosDoMesAsync(ano, mes, ct);

    /// <summary>
    /// Define (ou corrige) o teto de uma categoria no mês. Uma linha por categoria e mês:
    /// dois tetos para a mesma pergunta dariam duas réguas, e a tela escolheria uma sem
    /// dizer qual.
    /// </summary>
    public async Task<OrcamentoCategoria> DefinirAsync(
        int ano, int mes, int categoriaId, decimal teto,
        string? operador = null, CancellationToken ct = default)
    {
        if (mes is < 1 or > 12)
            throw new InvalidOperationException("Mês precisa estar entre 1 e 12.");

        if (teto < 0m)
            throw new InvalidOperationException("Teto negativo não significa nada — use zero.");

        var categoria = (await _repo.CategoriasFinanceirasAsync(ct))
            .FirstOrDefault(c => c.Id == categoriaId)
            ?? throw new InvalidOperationException("Categoria não encontrada.");

        if (categoria.Tipo != TipoLancamento.Saida)
            throw new InvalidOperationException(
                "Só categoria de SAÍDA tem teto. Alvo de receita é meta, e a direção já "
                + "define metas na tela própria — misturar as duas faria a tela somar "
                + "laranja com maçã.");

        var existente = await _repo.ObterOrcamentoAsync(ano, mes, categoriaId, ct);

        if (existente is not null)
        {
            var antes = existente.Teto;
            existente.Teto = teto;

            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
                Acao = "OrcamentoAlterado",
                Detalhe = $"{categoria.Nome} {mes:00}/{ano}: {antes:C} → {teto:C}"
            }, ct);
            await _repo.SalvarAsync(ct);
            return existente;
        }

        var orcamento = new OrcamentoCategoria
        {
            Ano = ano,
            Mes = mes,
            CategoriaFinanceiraId = categoriaId,
            Teto = teto,
            CriadoPor = operador
        };

        await _repo.AdicionarOrcamentoAsync(orcamento, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "OrcamentoDefinido",
            Detalhe = $"{categoria.Nome} {mes:00}/{ano}: {teto:C}"
        }, ct);
        await _repo.SalvarAsync(ct);

        return orcamento;
    }

    public async Task ExcluirAsync(
        int orcamentoId, string? operador = null, CancellationToken ct = default)
    {
        var orcamento = await _repo.ObterOrcamentoPorIdAsync(orcamentoId, ct)
            ?? throw new InvalidOperationException("Orçamento não encontrado.");

        await _repo.RemoverOrcamentoAsync(orcamentoId, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "OrcamentoExcluido",
            Detalhe = $"{orcamento.Categoria?.Nome ?? "categoria"} {orcamento.Mes:00}/{orcamento.Ano}"
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Os tetos do mês com o gasto ao lado. Devolve vazio quando a clínica não definiu
    /// nenhum — "nenhum teto definido" é diferente de "tudo dentro do orçamento".
    /// </summary>
    public async Task<IReadOnlyList<OrcamentoApurado>> ApurarMesAsync(
        int ano, int mes, CancellationToken ct = default)
    {
        var tetos = await _repo.OrcamentosDoMesAsync(ano, mes, ct);
        if (tetos.Count == 0) return [];

        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        // O gasto vem do FluxoCaixa pela mesma projeção que a tela de caixa usa: recontar
        // aqui daria um segundo número para a mesma pergunta.
        var lancamentos = await _repo.LancamentosDatadosNoPeriodoAsync(inicio, fim, ct);

        var realizados = lancamentos
            .Where(l => l.Tipo == TipoLancamento.Saida)
            .Where(l => l.Status == StatusLancamento.Realizado)
            .Where(l => l.DataDoFluxo >= inicio && l.DataDoFluxo <= fim)
            .GroupBy(l => l.CategoriaId)
            .ToDictionary(g => g.Key ?? 0, g => g.Sum(l => l.Valor));

        var previstos = lancamentos
            .Where(l => l.Tipo == TipoLancamento.Saida)
            .Where(l => l.Status == StatusLancamento.Previsto)
            .Where(l => l.DataDoFluxo >= inicio && l.DataDoFluxo <= fim)
            .GroupBy(l => l.CategoriaId)
            .ToDictionary(g => g.Key ?? 0, g => g.Sum(l => l.Valor));

        return tetos
            .Select(t => new OrcamentoApurado(
                t.CategoriaFinanceiraId,
                t.Categoria?.Nome ?? "categoria",
                t.Teto,
                realizados.TryGetValue(t.CategoriaFinanceiraId, out var gasto) ? gasto : 0m,
                previstos.TryGetValue(t.CategoriaFinanceiraId, out var previsto) ? previsto : null))
            // O que estourou primeiro, depois o que está perto: a tela é lida de cima
            // para baixo, e a ordem é o que diz o que resolver antes.
            .OrderByDescending(o => o.Estourou)
            .ThenByDescending(o => o.PercentualUsado ?? 0)
            .ToList();
    }
}
