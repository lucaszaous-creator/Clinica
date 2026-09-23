using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
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
    public string? CodigoInterno { get; init; }
    public string TextoBusca { get; init; } = string.Empty;
    public GrupoEstoque Grupo { get; init; }
    public UsoEstoque Uso { get; init; }
    public decimal? EstoqueMaximo { get; init; }
    public string? LocalArmazenamento { get; init; }

    /// <summary>Abaixo do mínimo (com mínimo configurado) — hora de repor.</summary>
    public bool AbaixoDoMinimo => EstoqueMinimo > 0 && Saldo <= EstoqueMinimo;

    public string SaldoRotulo => $"{Saldo:0.###} {Unidade}";
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
    IReadOnlyList<string> Itens)
{
    public bool Completo { get; init; } = true;
}

/// <summary>
/// O custo de insumo de uma sessão, com data e paciente — a linha da tela.
///
/// É o mesmo número do <see cref="CustoDoAtendimento"/> visto pelo período: lá se
/// pergunta "quanto custou ESTA sessão", aqui "quanto custa uma sessão nesta clínica".
/// </summary>
public sealed record CustoDeSessao(
    int AtendimentoId,
    DateOnly Data,
    string? Paciente,
    decimal Custo,
    IReadOnlyList<string> Itens)
{
    public bool Completo { get; init; } = true;
}

/// <summary>
/// O que o período gastou de insumo, e quanto sai uma sessão em média.
///
/// A média é anulável de propósito: período sem nenhuma baixa por sessão não tem média
/// zero, tem média NENHUMA — e a tela mostra "—". É a mesma regra dos indicadores: 0%
/// e "não deu para medir" são coisas diferentes, e confundi-las faria a direção
/// concluir que a sessão sai de graça quando na verdade ninguém baixou insumo.
/// </summary>
public sealed record ResumoCustoSessoes(
    int Sessoes,
    decimal Total,
    decimal? MedioPorSessao,
    CustoDeSessao? MaisCara)
{
    public int SessoesComCustoIncompleto { get; init; }
}

/// <summary>
/// Estoque de insumos (feature 10): entrada, baixa por sessão, alerta de mínimo e de
/// validade, e custo por atendimento.
///
/// O saldo NUNCA é campo: é a soma dos movimentos. Guardar um total e
/// mantê-lo em dia é como o estoque para de bater — uma gravação que falha no meio e o
/// número fica errado para sempre, sem ninguém saber desde quando.
/// </summary>
public sealed partial class EstoqueService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>Janela padrão do alerta de validade.</summary>
    public const int DiasAlertaValidade = 60;

    public EstoqueService(IClinicaRepositorio repo) => _repo = repo;

    private static void ValidarTexto(string? valor, int limite, string campo)
    {
        if (valor?.Trim().Length > limite)
            throw new InvalidOperationException($"{campo}: use até {limite} caracteres.");
    }

    // ==================== Itens ====================

    public Task<ItemEstoque?> ObterItemAsync(int itemId, CancellationToken ct = default)
        => _repo.ObterItemEstoqueAsync(itemId, ct);

    public Task<ItemEstoque> SalvarItemAsync(
        ItemEstoque dados, string? operador = null, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
            throw new InvalidOperationException("Dê um nome ao item.");
        if (string.IsNullOrWhiteSpace(dados.Unidade))
            throw new InvalidOperationException("Informe a unidade (un, cx, ml…).");
        if (dados.EstoqueMinimo < 0)
            throw new InvalidOperationException("O estoque mínimo não pode ser negativo.");

        ValidarTexto(dados.Nome, 120, "Nome");
        ValidarTexto(dados.Unidade, 10, "Unidade");
        ValidarTexto(dados.CodigoInterno, 40, "Código interno");
        ValidarTexto(dados.CodigoBarras, 60, "Código de barras");
        ValidarTexto(dados.Fabricante, 120, "Fabricante");
        ValidarTexto(dados.Apresentacao, 120, "Apresentação");
        ValidarTexto(dados.UnidadeCompra, 10, "Unidade de compra");
        ValidarTexto(dados.LocalArmazenamento, 100, "Local de armazenamento");
        ValidarTexto(dados.Observacoes, 500, "Observações");
        if (!Enum.IsDefined(dados.Grupo) || !Enum.IsDefined(dados.Uso))
            throw new InvalidOperationException("Grupo ou uso do estoque inválido.");
        if (dados.FatorCompra <= 0 || dados.FatorCompra >= 100000000000m || decimal.Round(dados.FatorCompra, 3) != dados.FatorCompra)
            throw new InvalidOperationException("O fator de compra deve ser positivo, com até três casas decimais.");
        if (dados.FatorCompra != 1m && string.IsNullOrWhiteSpace(dados.UnidadeCompra))
            throw new InvalidOperationException("Informe a unidade de compra para a conversão.");
        if (dados.EstoqueMinimo >= 100000000000m || decimal.Round(dados.EstoqueMinimo, 3) != dados.EstoqueMinimo ||
            dados.EstoqueMaximo is { } maximo && (maximo < dados.EstoqueMinimo || maximo >= 100000000000m || decimal.Round(maximo, 3) != maximo))
            throw new InvalidOperationException("Use limites com até três casas decimais e máximo maior ou igual ao mínimo.");
        var codigo = Limpar(dados.CodigoInterno)?.ToUpperInvariant();
        if (codigo is not null && (await _repo.ItensEstoqueAsync(false, ct))
            .Any(i => i.Id != dados.Id && string.Equals(i.CodigoInterno, codigo, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("O código interno já pertence a outro item, inclusive inativo.");

        var destino = dados.Id == 0 ? null : await _repo.ObterItemEstoqueAsync(dados.Id, ct)
            ?? throw new InvalidOperationException("Item não encontrado. Atualize o estoque.");
        if (destino is not null && !string.Equals(destino.Unidade, dados.Unidade.Trim(), StringComparison.OrdinalIgnoreCase)
            && (await _repo.MovimentosDoItemAsync(destino.Id, ct)).Count > 0)
            throw new InvalidOperationException("Item com movimentos não pode mudar de unidade. Cadastre outro item para a nova unidade.");
        if (destino is null)
        {
            destino = new ItemEstoque { CriadoEm = DateTime.Now, CriadoPor = operador };
            await _repo.AdicionarItemEstoqueAsync(destino, ct);
        }

        destino.CodigoInterno = codigo;
        destino.CodigoBarras = Limpar(dados.CodigoBarras);
        destino.Fabricante = Limpar(dados.Fabricante);
        destino.Apresentacao = Limpar(dados.Apresentacao);
        destino.Grupo = dados.Grupo;
        destino.Uso = dados.Uso;
        destino.ExigirLote = dados.ExigirLote || dados.Grupo == GrupoEstoque.Medicamento;
        destino.ExigirValidade = dados.ExigirValidade || dados.Grupo == GrupoEstoque.Medicamento;
        destino.UnidadeCompra = Limpar(dados.UnidadeCompra);
        destino.FatorCompra = dados.FatorCompra;
        destino.EstoqueMaximo = dados.EstoqueMaximo;
        destino.LocalArmazenamento = Limpar(dados.LocalArmazenamento);
        destino.Nome = dados.Nome.Trim();
        destino.Unidade = dados.Unidade.Trim();
        destino.EstoqueMinimo = dados.EstoqueMinimo;
        destino.Ativo = dados.Ativo;
        destino.Observacoes = Limpar(dados.Observacoes);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "CadastroEstoque",
            Detalhe = $"{destino.Nome} — código {codigo ?? "não informado"}; {destino.Grupo}; {destino.Uso}; ativo: {destino.Ativo}"
        }, ct);
        await _repo.SalvarAsync(ct);
        return destino;
    }, ct);

    /// <summary>
    /// Apaga apenas um cadastro sem movimentos; itens com histórico devem ser inativados.
    /// </summary>
    public async Task ExcluirItemAsync(int itemId, CancellationToken ct = default)
    {
        await _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            if ((await _repo.MovimentosDoItemAsync(itemId, ct)).Count > 0)
                throw new InvalidOperationException("Item com movimentação deve ser inativado. O histórico do estoque precisa ser preservado.");
            await _repo.RemoverItemEstoqueAsync(itemId, ct);
            return await _repo.SalvarAsync(ct);
        }, ct);
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
                custos.TryGetValue(i.Id, out var custo) ? custo : null)
                { CodigoInterno = i.CodigoInterno, Grupo = i.Grupo, Uso = i.Uso,
                  TextoBusca = string.Join(" ", new[] { i.Nome, i.CodigoInterno, i.CodigoBarras, i.Fabricante, i.Apresentacao, i.LocalArmazenamento }),
                  EstoqueMaximo = i.EstoqueMaximo, LocalArmazenamento = i.LocalArmazenamento })
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

        return movimentos.GroupBy(m => m.ItemEstoqueId)
            .Where(g => comSaldo.Any(i => i.Id == g.Key))
            .SelectMany(g => RazaoEstoque.Calcular(g).Lotes)
            .Where(l => l.Quantidade > 0 && l.Entrada.Validade is { } v && v <= limite)
            .Select(l => new ValidadeProxima(
                l.Entrada.ItemEstoqueId,
                comSaldo.First(i => i.Id == l.Entrada.ItemEstoqueId).Nome,
                l.Entrada.Validade!.Value,
                l.Quantidade,
                l.Entrada.Lote))
            .OrderBy(v => v.Validade)
            .ToList();
    }

    // ==================== Movimentos ====================

    /// <summary>Registra material e obrigação financeira na mesma transação.</summary>
    public Task<MovimentoEstoque> ComprarAsync(MovimentoEstoque dados, string fornecedor,
        DateOnly vencimento, bool pago = false, FormaPagamento? forma = null,
        string? operador = null, CancellationToken ct = default, bool emUnidadeCompra = false)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
        {
            if (dados.Tipo != TipoMovimentoEstoque.Entrada || dados.CustoUnitario is not > 0)
                throw new InvalidOperationException("A compra exige uma entrada com custo unitário maior que zero.");
            if (string.IsNullOrWhiteSpace(fornecedor) || fornecedor.Trim().Length > 100)
                throw new InvalidOperationException("Informe o fornecedor da compra (até 100 caracteres).");
            if (pago && (forma is null || !Enum.IsDefined(forma.Value) || forma == FormaPagamento.Convenio))
                throw new InvalidOperationException("Informe como a compra foi paga.");
            dados.Fornecedor = fornecedor.Trim();
            var movimento = await MovimentarAsync(dados, operador, ct, emUnidadeCompra);
            var item = await _repo.ObterItemEstoqueAsync(dados.ItemEstoqueId, ct);
            var descricao = $"Compra de {item!.Nome} — {fornecedor.Trim()}";
            var conta = await new FinanceiroService(_repo).LancarAsync(movimento.Data, TipoLancamento.Saida,
                descricao.Length <= 200 ? descricao : descricao[..200],
                Math.Round(dados.Quantidade * dados.CustoUnitario.Value, 2, MidpointRounding.AwayFromZero),
                pago ? StatusLancamento.Realizado : StatusLancamento.Previsto,
                formaPagamento: pago ? forma : null, dataVencimento: vencimento,
                observacoes: $"Entrada de estoque #{movimento.Id}. {dados.Observacao}".Trim(), operador: operador, ct: ct);
            conta.Contraparte = fornecedor.Trim();
            conta.DocumentoReferencia = Limpar(dados.DocumentoEntrada);
            movimento.LancamentoFinanceiroId = conta.Id;
            await _repo.SalvarAsync(ct);
            return movimento;
        }, ct);

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
    /// <summary>
    /// Acerto de INVENTÁRIO: a contagem física não bateu com o saldo do sistema
    /// (parcela 30).
    ///
    /// Recebe a quantidade CONTADA, não a diferença — quem está com a caixa na mão conta
    /// "tenho 37", e obrigá-lo a calcular "então ajusta 4 para baixo" é pedir a conta que
    /// o sistema sabe fazer e a pessoa erra.
    ///
    /// Três regras:
    ///
    /// 1. **O motivo é obrigatório.** Diferença de inventário sem explicação é
    ///    indistinguível de erro de digitação seis meses depois — e é o mesmo raciocínio
    ///    da perda, que já exige motivo desde a parcela 4.
    /// 2. **Não é perda.** Perda afirma que alguém quebrou, venceu ou extraviou; a
    ///    contagem que acha A MAIS não é perda nenhuma, e forçar tudo em `Perda` faria o
    ///    custo médio do insumo parar de valer.
    /// 3. **Contagem igual ao saldo não vira movimento.** Registrar um ajuste de zero
    ///    sujaria o extrato com linhas que não mudam nada.
    /// </summary>
    public Task<MovimentoEstoque?> AjustarInventarioAsync(
        int itemId, decimal quantidadeContada, string motivo,
        string? operador = null, DateOnly? data = null, CancellationToken ct = default,
        string? lote = null, DateOnly? validade = null)
        => _repo.ExecutarGestaoAtomicaAsync<MovimentoEstoque?>(async () =>
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Escreva por que a contagem não bateu. Diferença de inventário sem motivo "
                + "é indistinguível de erro de digitação seis meses depois.");

        if (quantidadeContada < 0m)
            throw new InvalidOperationException("A contagem não pode ser negativa.");

        var item = await _repo.ObterItemEstoqueAsync(itemId, ct)
            ?? throw new InvalidOperationException("Item de estoque não encontrado.");

        var saldos = await _repo.SaldosEstoqueAsync(ct);
        var saldo = saldos.TryGetValue(itemId, out var atual) ? atual : 0m;

        var diferenca = quantidadeContada - saldo;
        if (diferenca == 0m) return null;

        return await MovimentarAsync(new MovimentoEstoque
        {
            ItemEstoqueId = item.Id,
            Tipo = TipoMovimentoEstoque.Ajuste,
            Quantidade = Math.Abs(diferenca),
            AjusteParaCima = diferenca > 0m,
            Lote = lote,
            Validade = validade,
            Data = data ?? DateOnly.FromDateTime(DateTime.Today),
            Observacao = $"Inventário: contado {quantidadeContada:0.###} {item.Unidade}, "
                         + $"sistema tinha {saldo:0.###} — {motivo.Trim()}"
        }, operador, ct);
    }, ct);

    public Task<MovimentoEstoque> MovimentarAsync(
        MovimentoEstoque dados, string? operador = null, CancellationToken ct = default, bool emUnidadeCompra = false)
        => MovimentarInternoAsync(dados, operador, ct, emUnidadeCompra);

    private Task<MovimentoEstoque> MovimentarInternoAsync(MovimentoEstoque dados, string? operador,
        CancellationToken ct, bool emUnidadeCompra = false, bool baixaDaConferencia = false)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
    {
        var item = await _repo.ObterItemEstoqueAsync(dados.ItemEstoqueId, ct)
            ?? throw new InvalidOperationException("Item de estoque não encontrado.");

        if (dados.Quantidade <= 0)
            throw new InvalidOperationException("A quantidade deve ser maior que zero.");
        if (!item.Ativo)
            throw new InvalidOperationException("Reative o item antes de movimentar seu estoque.");
        if (!Enum.IsDefined(dados.Tipo) || dados.CustoUnitario < 0)
            throw new InvalidOperationException("Tipo de movimento ou custo inválido.");
        if (dados.CustoUnitario is { } custo && decimal.Round(custo, 4) != custo)
            throw new InvalidOperationException("Use até quatro casas decimais para o custo unitário.");
        if (decimal.Round(dados.Quantidade, 3) != dados.Quantidade)
            throw new InvalidOperationException("Use até três casas decimais para a quantidade.");
        if (dados.Tipo == TipoMovimentoEstoque.Ajuste &&
            (dados.AjusteParaCima is null || string.IsNullOrWhiteSpace(dados.Observacao)))
            throw new InvalidOperationException("Informe a direção e o motivo do ajuste de inventário.");
        if (dados.Tipo == TipoMovimentoEstoque.Perda && string.IsNullOrWhiteSpace(dados.Observacao))
            throw new InvalidOperationException("Perda sem motivo escrito vira estoque que não bate.");

        ValidarTexto(dados.Lote, 60, "Lote");
        ValidarTexto(dados.Observacao, 300, "Observação");
        ValidarTexto(dados.SetorDestino, 100, "Setor de destino");
        ValidarTexto(dados.Fornecedor, 100, "Fornecedor");
        ValidarTexto(dados.DocumentoEntrada, 80, "Documento da entrada");
        if (!Enum.IsDefined(dados.DestinoConsumo))
            throw new InvalidOperationException("Destino de consumo inválido.");
        if (!baixaDaConferencia && dados.Tipo == TipoMovimentoEstoque.Saida && dados.AtendimentoId is { } idAtendimento
            && await _repo.ConferenciaConsumoAsync(idAtendimento, ct) is not null)
            throw new InvalidOperationException("Os materiais desta sessão já foram conferidos. Consulte o consumo registrado antes de fazer outra baixa.");
        var destinoConsumo = dados.Tipo == TipoMovimentoEstoque.Saida
            ? (dados.AtendimentoId is not null ? DestinoConsumoEstoque.Procedimento : dados.DestinoConsumo)
            : DestinoConsumoEstoque.NaoInformado;
        if (destinoConsumo == DestinoConsumoEstoque.Procedimento && (dados.AtendimentoId is null || item.Uso == UsoEstoque.Rotina))
            throw new InvalidOperationException("Consumo de procedimento exige atendimento e item de uso clínico.");
        if (destinoConsumo == DestinoConsumoEstoque.Rotina &&
            (string.IsNullOrWhiteSpace(dados.SetorDestino) || item.Uso == UsoEstoque.Procedimentos))
            throw new InvalidOperationException("Consumo de rotina exige setor de destino e item liberado para rotina ou ambos os usos.");
        var entrada = dados.Tipo == TipoMovimentoEstoque.Entrada;
        var acrescimo = entrada || dados.Tipo == TipoMovimentoEstoque.Ajuste && dados.AjusteParaCima == true;
        if (acrescimo && (item.ExigirLote || item.Grupo == GrupoEstoque.Medicamento) && string.IsNullOrWhiteSpace(dados.Lote))
            throw new InvalidOperationException("Este produto exige identificação do lote na entrada.");
        if (acrescimo && (item.ExigirValidade || item.Grupo == GrupoEstoque.Medicamento) && dados.Validade is null)
            throw new InvalidOperationException("Este produto exige validade na entrada.");
        if (emUnidadeCompra && (!entrada || string.IsNullOrWhiteSpace(item.UnidadeCompra) || item.FatorCompra <= 0))
            throw new InvalidOperationException("Conversão de embalagem exige entrada e unidade de compra cadastrada.");
        var fator = emUnidadeCompra ? item.FatorCompra : 1m;
        var quantidadeBase = dados.Quantidade * fator;
        if (quantidadeBase >= 100000000000m || decimal.Round(quantidadeBase, 3) != quantidadeBase)
            throw new InvalidOperationException("A conversão excede o limite ou as três casas decimais da quantidade.");
        var custoBase = dados.CustoUnitario is { } custoInformado ? decimal.Round(custoInformado / fator, 4, MidpointRounding.AwayFromZero) : (decimal?)null;
        if (dados.CustoUnitario >= 10000000000m || custoBase >= 10000000000m)
            throw new InvalidOperationException("Custo unitário acima do limite permitido.");

        var data = dados.Data == default ? DateOnly.FromDateTime(DateTime.Today) : dados.Data;
        var anteriores = await _repo.MovimentosDoItemAsync(item.Id, ct);
        if (anteriores.Any(m => m.Data > data))
            throw new InvalidOperationException("Há movimentos posteriores. Registre o acerto na data atual para preservar saldo e custo históricos.");
        var razao = RazaoEstoque.Calcular(anteriores);
        var loteInformado = Limpar(dados.Lote);

        // ⚠️ A recusa da perda sem motivo morava SÓ no wrapper `PerderAsync` — que nenhuma
        // tela chama. A janela genérica de movimento entra por AQUI, e a única barreira
        // dela era a validação da tela: o defeito recorrente vestido de validação (a
        // regra do número da guia — quem valida na tela cobre uma porta e deixa as outras
        // passando). Quem impede é o serviço; a tela usa a mesma regra para avisar antes.
        if (dados.Tipo == TipoMovimentoEstoque.Perda && string.IsNullOrWhiteSpace(dados.Observacao))
            throw new InvalidOperationException("Perda sem motivo escrito vira estoque que não bate.");

        // Ajuste PARA CIMA é entrada de saldo: não se compara com o que existe hoje, é
        // justamente o que está faltando no sistema.
        var tiraSaldo = dados.Tipo != TipoMovimentoEstoque.Entrada
                        && !(dados.Tipo == TipoMovimentoEstoque.Ajuste && dados.AjusteParaCima == true);

        if (tiraSaldo)
        {
            var saldos = await _repo.SaldosEstoqueAsync(ct);
            var saldo = saldos.TryGetValue(item.Id, out var atual) ? atual : 0m;
            if (dados.Quantidade > saldo)
                throw new InvalidOperationException(
                    $"O saldo de {item.Nome} é {saldo:0.###} {item.Unidade} — não dá para baixar "
                    + $"{dados.Quantidade:0.###}.");
            var lotes = razao.Lotes.Where(l => loteInformado is null || l.Entrada.Lote == loteInformado).ToList();
            if (lotes.Sum(l => l.Quantidade) < dados.Quantidade)
                throw new InvalidOperationException("O lote informado não tem saldo suficiente.");
            if (dados.Tipo == TipoMovimentoEstoque.Saida &&
                lotes.Any(l => l.Quantidade > 0 && l.Entrada.Validade < data))
                throw new InvalidOperationException("Há lote vencido nesta baixa. Registre a perda ou informe um lote válido antes de consumir.");
        }

        var movimento = new MovimentoEstoque
        {
            ItemEstoqueId = item.Id,
            Tipo = dados.Tipo,
            Quantidade = quantidadeBase,
            QuantidadeInformada = dados.Quantidade,
            UnidadeInformada = emUnidadeCompra ? item.UnidadeCompra : item.Unidade,
            FatorConversao = fator,
            CustoInformado = entrada ? dados.CustoUnitario : null,
            DestinoConsumo = destinoConsumo,
            SetorDestino = destinoConsumo == DestinoConsumoEstoque.Rotina ? Limpar(dados.SetorDestino) : null,
            Fornecedor = entrada ? Limpar(dados.Fornecedor) : null,
            DocumentoEntrada = entrada ? Limpar(dados.DocumentoEntrada) : null,
            AjusteParaCima = dados.Tipo == TipoMovimentoEstoque.Ajuste
                ? dados.AjusteParaCima ?? false
                : null,
            CustoUnitario = entrada ? custoBase : razao.CustoMedio,
            Data = dados.Data == default ? DateOnly.FromDateTime(DateTime.Today) : dados.Data,
            Validade = acrescimo ? dados.Validade : null,
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
            Detalhe = $"{item.Nome} — {movimento.Quantidade:0.###} {item.Unidade}",
            PacienteId = movimento.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return movimento;
    }, ct);

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
            .GroupBy(m => m.ItemEstoqueId)
            .Select(g => (g.Key, Custo: RazaoEstoque.Calcular(g).CustoMedio))
            .Where(x => x.Custo is not null)
            .ToDictionary(x => x.Key, x => x.Custo!.Value);
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
        var medios = await CustosHistoricosAsync(ct);

        var (total, itens, completo) = Precificar(
            movimentos.Where(m => m.Tipo == TipoMovimentoEstoque.Saida), medios);

        return new CustoDoAtendimento(atendimentoId, total, itens) { Completo = completo };
    }

    /// <summary>
    /// O custo de insumo sessão a sessão no período, da mais recente para a mais antiga.
    ///
    /// Só entra saída COM atendimento: a baixa digitada à mão na tela de movimento não
    /// pertence a sessão nenhuma, e rateá-la entre as sessões do período daria a cada uma
    /// um custo que ela não teve.
    /// </summary>
    public async Task<IReadOnlyList<CustoDeSessao>> CustosDeSessaoAsync(
        DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var movimentos = await _repo.ConsumosDeSessaoNoPeriodoAsync(de, ate, ct);
        if (movimentos.Count == 0) return [];

        var medios = await CustosHistoricosAsync(ct);

        return movimentos
            .GroupBy(m => m.AtendimentoId!.Value)
            .Select(g =>
            {
                var (custo, itens, completo) = Precificar(g, medios);
                return new CustoDeSessao(
                    g.Key,
                    g.Max(m => m.Data),
                    g.Select(m => m.Paciente?.Nome).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                    custo,
                    itens) { Completo = completo };
            })
            .OrderByDescending(c => c.Data)
            .ThenByDescending(c => c.AtendimentoId)
            .ToList();
    }

    /// <summary>Total, média e a sessão mais cara do período.</summary>
    public async Task<ResumoCustoSessoes> ResumoCustoSessoesAsync(
        DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var sessoes = await CustosDeSessaoAsync(de, ate, ct);
        if (sessoes.Count == 0) return new ResumoCustoSessoes(0, 0m, null, null);

        var total = sessoes.Sum(s => s.Custo);
        var incompletas = sessoes.Count(s => !s.Completo);

        return new ResumoCustoSessoes(
            sessoes.Count,
            total,
            incompletas == 0 ? Math.Round(total / sessoes.Count, 2) : null,
            incompletas == 0 ? sessoes.OrderByDescending(s => s.Custo).First() : null)
            { SessoesComCustoIncompleto = incompletas };
    }

    /// <summary>
    /// Dá preço às saídas: usa o custo do próprio movimento quando ele existe e o médio
    /// do item quando não. Item sem preço nenhum NÃO some da lista — entra com custo
    /// zero, para a falta de cadastro ficar visível em vez de baratear a sessão.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, decimal?>> CustosHistoricosAsync(CancellationToken ct)
        => (await _repo.MovimentosNoPeriodoAsync(DateOnly.MinValue, DateOnly.MaxValue, ct))
            .GroupBy(m => m.ItemEstoqueId)
            .SelectMany(g => RazaoEstoque.Calcular(g).CustosDosMovimentos)
            .ToDictionary(x => x.Key, x => x.Value);

    private static (decimal Custo, IReadOnlyList<string> Itens, bool Completo) Precificar(
        IEnumerable<MovimentoEstoque> saidas, IReadOnlyDictionary<int, decimal?> medios)
    {
        decimal total = 0m;
        var itens = new List<string>();
        var completo = true;

        foreach (var m in saidas)
        {
            var unitario = m.CustoUnitario
                           ?? (medios.TryGetValue(m.Id, out var medio) ? medio : null);

            total += (unitario ?? 0m) * m.Quantidade;
            if (unitario is null) completo = false;
            itens.Add(($"{m.Item?.Nome ?? "item"} — {m.Quantidade:0.###} {m.Item?.Unidade ?? string.Empty}"
                + (unitario is null ? " (custo não informado)" : string.Empty)).Trim());
        }

        return (Math.Round(total, 2), itens, completo);
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
