using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Um item do estoque com o saldo já somado — o que a tela lista.</summary>
public sealed record SaldoItemEstoque(
    int ItemId,
    string Nome,
    string Unidade,
    decimal Saldo,
    decimal EstoqueMinimo,
    bool Ativo,
    decimal? CustoMedio)
{
    /// <summary>Abaixo do mínimo (com mínimo configurado) — hora de repor.</summary>
    public bool AbaixoDoMinimo => EstoqueMinimo > 0 && Saldo <= EstoqueMinimo;

    public string SaldoRotulo => $"{Saldo:0.##} {Unidade}";
}

/// <summary>Um lote que vence (ou venceu) com saldo do item ainda em casa.</summary>
public sealed record ValidadeProxima(
    int ItemId,
    string Nome,
    DateOnly Validade,
    decimal Quantidade,
    string? Lote)
{
    public bool Vencido(DateOnly hoje) => Validade < hoje;

    public int DiasRestantes(DateOnly hoje) => Validade.DayNumber - hoje.DayNumber;
}

/// <summary>Quanto de insumo uma sessão consumiu.</summary>
public sealed record CustoDoAtendimento(
    int AtendimentoId,
    decimal Custo,
    IReadOnlyList<string> Itens);

/// <summary>
/// Estoque de insumos (feature 10): entrada, baixa por sessão, alerta de mínimo e de
/// validade, e custo por atendimento.
///
/// O saldo NUNCA é campo: é a soma dos movimentos. Guardar um total e
/// mantê-lo em dia é como o estoque para de bater — uma gravação que falha no meio e o
/// número fica errado para sempre, sem ninguém saber desde quando.
/// </summary>
public sealed class EstoqueService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Janela padrão do alerta de validade.</summary>
    public const int DiasAlertaValidade = 60;

    public EstoqueService(IClinicaRepositorio repo) => _repo = repo;

    // ==================== Itens ====================

    public Task<IReadOnlyList<ItemEstoque>> ItensAsync(
        bool somenteAtivos = false, CancellationToken ct = default)
        => _repo.ItensEstoqueAsync(somenteAtivos, ct);

    public Task<ItemEstoque?> ObterItemAsync(int itemId, CancellationToken ct = default)
        => _repo.ObterItemEstoqueAsync(itemId, ct);

    public async Task<ItemEstoque> SalvarItemAsync(
        ItemEstoque dados, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
            throw new InvalidOperationException("Dê um nome ao item.");
        if (string.IsNullOrWhiteSpace(dados.Unidade))
            throw new InvalidOperationException("Informe a unidade (un, cx, ml…).");
        if (dados.EstoqueMinimo < 0)
            throw new InvalidOperationException("O estoque mínimo não pode ser negativo.");

        var destino = dados.Id == 0 ? null : await _repo.ObterItemEstoqueAsync(dados.Id, ct);
        if (destino is null)
        {
            destino = new ItemEstoque { CriadoEm = DateTime.Now, CriadoPor = operador };
            await _repo.AdicionarItemEstoqueAsync(destino, ct);
        }

        destino.Nome = dados.Nome.Trim();
        destino.Unidade = dados.Unidade.Trim();
        destino.EstoqueMinimo = dados.EstoqueMinimo;
        destino.Ativo = dados.Ativo;
        destino.Observacoes = Limpar(dados.Observacoes);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    /// <summary>
    /// Apaga um item e TODO o histórico dele. Só faz sentido para quem foi cadastrado
    /// errado — item que já se movimentou deve ser inativado, não apagado.
    /// </summary>
    public async Task ExcluirItemAsync(int itemId, CancellationToken ct = default)
    {
        await _repo.RemoverItemEstoqueAsync(itemId, ct);
        await _repo.SalvarAsync(ct);
    }

    // ==================== Saldo ====================

    public async Task<IReadOnlyList<SaldoItemEstoque>> SaldosAsync(
        bool somenteAtivos = false, CancellationToken ct = default)
    {
        var itens = await _repo.ItensEstoqueAsync(somenteAtivos, ct);
        var saldos = await _repo.SaldosEstoqueAsync(ct);
        var custos = await CustosMediosAsync(ct);

        return itens
            .Select(i => new SaldoItemEstoque(
                i.Id, i.Nome, i.Unidade,
                saldos.TryGetValue(i.Id, out var saldo) ? saldo : 0m,
                i.EstoqueMinimo, i.Ativo,
                custos.TryGetValue(i.Id, out var custo) ? custo : null))
            .ToList();
    }

    /// <summary>Itens que precisam ser repostos.</summary>
    public async Task<IReadOnlyList<SaldoItemEstoque>> AbaixoDoMinimoAsync(CancellationToken ct = default)
        => (await SaldosAsync(somenteAtivos: true, ct))
            .Where(s => s.AbaixoDoMinimo)
            .OrderBy(s => s.Saldo)
            .ToList();

    /// <summary>
    /// Lotes vencidos ou a vencer dentro da janela, dos itens que ainda têm saldo.
    /// Alertar sobre lote de item zerado seria barulho: não há o que descartar.
    /// </summary>
    public async Task<IReadOnlyList<ValidadeProxima>> ValidadesAsync(
        DateOnly? hoje = null, int? dias = null, CancellationToken ct = default)
    {
        var dia = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        var limite = dia.AddDays(dias ?? DiasAlertaValidade);

        var itens = await _repo.ItensEstoqueAsync(somenteAtivos: true, ct);
        var saldos = await _repo.SaldosEstoqueAsync(ct);
        var comSaldo = itens.Where(i => saldos.TryGetValue(i.Id, out var s) && s > 0).ToList();
        if (comSaldo.Count == 0) return [];

        // Sem recorte de data, e por dois motivos: entrada antiga com validade vencida
        // ainda interessa (o insumo pode continuar na prateleira), e entrada lançada com
        // data à frente — compra registrada adiantada — não pode sumir do alerta.
        //
        // Recortar pelo relógio da máquina, em especial, seria trair o parâmetro `hoje`:
        // quem passa a data de referência espera que ELA mande na resposta inteira.
        var movimentos = await _repo.MovimentosNoPeriodoAsync(
            DateOnly.MinValue, DateOnly.MaxValue, ct);

        return movimentos
            .Where(m => m.Tipo == TipoMovimentoEstoque.Entrada && m.Validade is not null)
            .Where(m => m.Validade!.Value <= limite)
            .Where(m => comSaldo.Any(i => i.Id == m.ItemEstoqueId))
            .Select(m => new ValidadeProxima(
                m.ItemEstoqueId,
                comSaldo.First(i => i.Id == m.ItemEstoqueId).Nome,
                m.Validade!.Value,
                m.Quantidade,
                m.Lote))
            .OrderBy(v => v.Validade)
            .ToList();
    }

    // ==================== Movimentos ====================

    public Task<IReadOnlyList<MovimentoEstoque>> MovimentosAsync(
        int itemId, CancellationToken ct = default)
        => _repo.MovimentosDoItemAsync(itemId, ct);

    /// <summary>Entrada de insumo (compra, doação, devolução).</summary>
    public Task<MovimentoEstoque> EntrarAsync(
        int itemId, decimal quantidade, decimal? custoUnitario = null, DateOnly? data = null,
        DateOnly? validade = null, string? lote = null, string? observacao = null,
        string? operador = null, CancellationToken ct = default)
        => MovimentarAsync(new MovimentoEstoque
        {
            ItemEstoqueId = itemId,
            Tipo = TipoMovimentoEstoque.Entrada,
            Quantidade = quantidade,
            CustoUnitario = custoUnitario,
            Data = data ?? DateOnly.FromDateTime(DateTime.Today),
            Validade = validade,
            Lote = lote,
            Observacao = observacao
        }, operador, ct);

    /// <summary>Baixa por sessão: o consumo que dá custo ao atendimento.</summary>
    public Task<MovimentoEstoque> BaixarAsync(
        int itemId, decimal quantidade, int? atendimentoId = null, int? pacienteId = null,
        DateOnly? data = null, string? observacao = null, string? operador = null,
        CancellationToken ct = default)
        => MovimentarAsync(new MovimentoEstoque
        {
            ItemEstoqueId = itemId,
            Tipo = TipoMovimentoEstoque.Saida,
            Quantidade = quantidade,
            Data = data ?? DateOnly.FromDateTime(DateTime.Today),
            AtendimentoId = atendimentoId,
            PacienteId = pacienteId,
            Observacao = observacao
        }, operador, ct);

    /// <summary>Perda: quebra, vencimento, extravio. Sai do saldo com o motivo escrito.</summary>
    public Task<MovimentoEstoque> PerderAsync(
        int itemId, decimal quantidade, string motivo, DateOnly? data = null,
        string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Perda sem motivo escrito vira estoque que não bate.");

        return MovimentarAsync(new MovimentoEstoque
        {
            ItemEstoqueId = itemId,
            Tipo = TipoMovimentoEstoque.Perda,
            Quantidade = quantidade,
            Data = data ?? DateOnly.FromDateTime(DateTime.Today),
            Observacao = motivo
        }, operador, ct);
    }

    /// <summary>
    /// Grava o movimento. Saída que deixaria o saldo negativo é recusada: estoque
    /// negativo não existe no mundo, e aceitar o número esconde o erro de contagem em
    /// vez de mostrá-lo.
    /// </summary>
    public async Task<MovimentoEstoque> MovimentarAsync(
        MovimentoEstoque dados, string? operador = null, CancellationToken ct = default)
    {
        var item = await _repo.ObterItemEstoqueAsync(dados.ItemEstoqueId, ct)
            ?? throw new InvalidOperationException("Item de estoque não encontrado.");

        if (dados.Quantidade <= 0)
            throw new InvalidOperationException("A quantidade deve ser maior que zero.");

        if (dados.Tipo != TipoMovimentoEstoque.Entrada)
        {
            var saldos = await _repo.SaldosEstoqueAsync(ct);
            var saldo = saldos.TryGetValue(item.Id, out var atual) ? atual : 0m;
            if (dados.Quantidade > saldo)
                throw new InvalidOperationException(
                    $"O saldo de {item.Nome} é {saldo:0.##} {item.Unidade} — não dá para baixar "
                    + $"{dados.Quantidade:0.##}.");
        }

        var movimento = new MovimentoEstoque
        {
            ItemEstoqueId = item.Id,
            Tipo = dados.Tipo,
            Quantidade = dados.Quantidade,
            CustoUnitario = dados.Tipo == TipoMovimentoEstoque.Entrada ? dados.CustoUnitario : null,
            Data = dados.Data == default ? DateOnly.FromDateTime(DateTime.Today) : dados.Data,
            Validade = dados.Tipo == TipoMovimentoEstoque.Entrada ? dados.Validade : null,
            Lote = Limpar(dados.Lote),
            AtendimentoId = dados.AtendimentoId,
            PacienteId = dados.PacienteId,
            Observacao = Limpar(dados.Observacao),
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        await _repo.AdicionarMovimentoEstoqueAsync(movimento, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = $"Estoque{movimento.Tipo}",
            Detalhe = $"{item.Nome} — {movimento.Quantidade:0.##} {item.Unidade}",
            PacienteId = movimento.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return movimento;
    }

    // ==================== Custo ====================

    /// <summary>
    /// Custo médio por item: valor das entradas dividido pela quantidade entrada. É o
    /// número que dá preço à baixa, já que a saída raramente sabe de qual lote saiu.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, decimal>> CustosMediosAsync(CancellationToken ct = default)
    {
        var movimentos = await _repo.MovimentosNoPeriodoAsync(
            DateOnly.MinValue, DateOnly.MaxValue, ct);

        return movimentos
            .Where(m => m.Tipo == TipoMovimentoEstoque.Entrada
                        && m.CustoUnitario is not null && m.Quantidade > 0)
            .GroupBy(m => m.ItemEstoqueId)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(
                    g.Sum(m => m.CustoUnitario!.Value * m.Quantidade) / g.Sum(m => m.Quantidade), 4));
    }

    /// <summary>
    /// Quanto de insumo aquela sessão consumiu, em dinheiro. Usa o custo do movimento
    /// quando ele existe e o médio do item quando não — e não some quando falta preço:
    /// o item aparece na lista com custo zero, para a falta ficar visível.
    /// </summary>
    public async Task<CustoDoAtendimento> CustoDoAtendimentoAsync(
        int atendimentoId, CancellationToken ct = default)
    {
        var movimentos = await _repo.MovimentosDoAtendimentoAsync(atendimentoId, ct);
        var medios = await CustosMediosAsync(ct);

        decimal total = 0m;
        var itens = new List<string>();

        foreach (var m in movimentos.Where(m => m.Tipo != TipoMovimentoEstoque.Entrada))
        {
            var unitario = m.CustoUnitario
                           ?? (medios.TryGetValue(m.ItemEstoqueId, out var medio) ? medio : 0m);

            total += unitario * m.Quantidade;
            itens.Add($"{m.Item?.Nome ?? "item"} — {m.Quantidade:0.##} {m.Item?.Unidade ?? string.Empty}".Trim());
        }

        return new CustoDoAtendimento(atendimentoId, Math.Round(total, 2), itens);
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
