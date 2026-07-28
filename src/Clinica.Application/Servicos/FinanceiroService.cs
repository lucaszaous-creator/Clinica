using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Totais do caixa num período (só lançamentos não cancelados).</summary>
public sealed record ResumoCaixa(
    decimal EntradasRealizadas,
    decimal SaidasRealizadas,
    decimal EntradasPrevistas,
    decimal SaidasPrevistas)
{
    /// <summary>O que efetivamente entrou menos o que saiu.</summary>
    public decimal SaldoRealizado => EntradasRealizadas - SaidasRealizadas;

    /// <summary>Saldo considerando também o que está previsto.</summary>
    public decimal SaldoPrevisto =>
        (EntradasRealizadas + EntradasPrevistas) - (SaidasRealizadas + SaidasPrevistas);
}

/// <summary>
/// Guia já efetivada no convênio que ainda não tem dinheiro lançado no caixa.
/// É o elo entre os dois módulos: o faturamento sabe que a guia foi baixada, o
/// financeiro sabe se ela virou receita.
/// </summary>
public sealed record GuiaSemLancamento(
    int CodigoId,
    int AtendimentoId,
    int PacienteId,
    string Paciente,
    DateOnly DataBaixa,
    string? NumeroGuiaReal,
    Convenio Convenio,
    TipoCodigo Tipo);

/// <summary>
/// Caixa da clínica: entradas, saídas e a conciliação com o faturamento.
///
/// Regra de separação: o dinheiro vive apenas em <see cref="LancamentoFinanceiro"/>.
/// As entidades de faturamento não ganham nenhum campo de valor — o lançamento é que
/// aponta para a guia. A dependência tem um sentido só, então o faturamento continua
/// funcionando sem saber que o financeiro existe.
/// </summary>
public sealed class FinanceiroService
{
    private readonly IClinicaRepositorio _repo;

    public FinanceiroService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Registra uma entrada ou saída. Lançamento já realizado recebe a data de
    /// pagamento automaticamente quando ela não é informada.
    /// </summary>
    public async Task<LancamentoFinanceiro> LancarAsync(
        DateOnly data,
        TipoLancamento tipo,
        string descricao,
        decimal valor,
        StatusLancamento status = StatusLancamento.Realizado,
        FormaPagamento? formaPagamento = null,
        int? categoriaId = null,
        int? pacienteId = null,
        int? atendimentoId = null,
        int? codigoFaturamentoId = null,
        Convenio? convenio = null,
        string? convenioCodigo = null,
        string? observacoes = null,
        DateOnly? dataPagamento = null,
        string? operador = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("A descrição do lançamento é obrigatória.", nameof(descricao));
        if (valor <= 0)
            throw new ArgumentException("O valor deve ser maior que zero — o sinal vem do tipo.", nameof(valor));

        var lancamento = new LancamentoFinanceiro
        {
            Data = data,
            Tipo = tipo,
            Descricao = descricao.Trim(),
            Valor = valor,
            Status = status,
            FormaPagamento = formaPagamento,
            CategoriaFinanceiraId = categoriaId,
            PacienteId = pacienteId,
            AtendimentoId = atendimentoId,
            CodigoFaturamentoId = codigoFaturamentoId,
            Convenio = convenio,
            ConvenioCodigo = convenioCodigo,
            Observacoes = observacoes,
            DataPagamento = status == StatusLancamento.Realizado ? (dataPagamento ?? data) : dataPagamento,
            CriadoPor = operador
        };

        await _repo.AdicionarLancamentoAsync(lancamento, ct);
        await RegistrarAsync("LancamentoCriado",
            $"{tipo} de {valor:C} — {lancamento.Descricao}", lancamento, operador, ct);
        await _repo.SalvarAsync(ct);
        return lancamento;
    }

    /// <summary>Marca um lançamento previsto como efetivamente pago/recebido.</summary>
    public async Task RealizarAsync(int lancamentoId, DateOnly? dataPagamento = null,
        FormaPagamento? formaPagamento = null, string? operador = null, CancellationToken ct = default)
    {
        var lancamento = await _repo.ObterLancamentoAsync(lancamentoId, ct)
            ?? throw new InvalidOperationException($"Lançamento {lancamentoId} não encontrado.");

        if (lancamento.Status == StatusLancamento.Cancelado)
            throw new InvalidOperationException("Lançamento cancelado não pode ser realizado.");
        if (lancamento.Status == StatusLancamento.Realizado)
            throw new InvalidOperationException("Este lançamento já foi realizado.");

        lancamento.Status = StatusLancamento.Realizado;
        lancamento.DataPagamento = dataPagamento ?? DateOnly.FromDateTime(DateTime.Today);
        if (formaPagamento is not null) lancamento.FormaPagamento = formaPagamento;

        await RegistrarAsync("LancamentoRealizado",
            $"{lancamento.Valor:C} — {lancamento.Descricao}", lancamento, operador, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Cancela um lançamento. Nunca apaga: o registro sai dos totais mas permanece no
    /// histórico, junto com o motivo.
    /// </summary>
    public async Task CancelarAsync(int lancamentoId, string motivo,
        string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Informe o motivo do cancelamento.", nameof(motivo));

        var lancamento = await _repo.ObterLancamentoAsync(lancamentoId, ct)
            ?? throw new InvalidOperationException($"Lançamento {lancamentoId} não encontrado.");

        if (lancamento.Status == StatusLancamento.Cancelado)
            throw new InvalidOperationException("Este lançamento já está cancelado.");

        lancamento.Status = StatusLancamento.Cancelado;
        lancamento.Observacoes = string.IsNullOrWhiteSpace(lancamento.Observacoes)
            ? $"Cancelado: {motivo}"
            : $"{lancamento.Observacoes} | Cancelado: {motivo}";

        await RegistrarAsync("LancamentoCancelado",
            $"{lancamento.Valor:C} — {motivo}", lancamento, operador, ct);
        await _repo.SalvarAsync(ct);
    }

    public Task<IReadOnlyList<LancamentoFinanceiro>> DoPeriodoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => _repo.LancamentosNoPeriodoAsync(inicio, fim, ct);

    /// <summary>Totais do período, ignorando cancelados.</summary>
    public async Task<ResumoCaixa> ResumoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var lancamentos = await _repo.LancamentosNoPeriodoAsync(inicio, fim, ct);

        decimal Somar(TipoLancamento tipo, StatusLancamento status) =>
            lancamentos.Where(l => l.Tipo == tipo && l.Status == status).Sum(l => l.Valor);

        return new ResumoCaixa(
            Somar(TipoLancamento.Entrada, StatusLancamento.Realizado),
            Somar(TipoLancamento.Saida, StatusLancamento.Realizado),
            Somar(TipoLancamento.Entrada, StatusLancamento.Previsto),
            Somar(TipoLancamento.Saida, StatusLancamento.Previsto));
    }

    /// <summary>
    /// Conciliação: guias baixadas no período que ainda não viraram lançamento.
    /// É a pergunta que o financeiro faz ao faturamento — "o que já foi efetivado
    /// no convênio e ainda não entrou no caixa?".
    /// </summary>
    public async Task<IReadOnlyList<GuiaSemLancamento>> GuiasSemLancamentoAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var codigos = await _repo.CodigosNoPeriodoAsync(inicio, fim, ct);

        var baixados = codigos
            .Where(c => c.DataBaixa is not null && c.Atendimento?.Paciente is not null)
            .ToList();
        if (baixados.Count == 0) return [];

        var jaLancados = (await _repo.CodigosComLancamentoAsync(
            baixados.Select(c => c.Id).ToList(), ct)).ToHashSet();

        return baixados
            .Where(c => !jaLancados.Contains(c.Id))
            .Select(c => new GuiaSemLancamento(
                c.Id,
                c.AtendimentoId,
                c.Atendimento!.PacienteId,
                c.Atendimento.Paciente!.Nome,
                c.DataBaixa!.Value,
                c.NumeroGuiaReal,
                c.Atendimento.Paciente.Convenio,
                c.Tipo))
            .OrderBy(g => g.DataBaixa)
            .ToList();
    }

    /// <summary>
    /// Cria a receita a partir de uma guia já efetivada, deixando o vínculo gravado
    /// para que ela não apareça de novo na conciliação.
    /// </summary>
    public async Task<LancamentoFinanceiro> LancarReceitaDaGuiaAsync(
        GuiaSemLancamento guia,
        decimal valor,
        StatusLancamento status = StatusLancamento.Previsto,
        int? categoriaId = null,
        string? operador = null,
        CancellationToken ct = default)
        => await LancarAsync(
            data: guia.DataBaixa,
            tipo: TipoLancamento.Entrada,
            descricao: $"{guia.Convenio} — guia {guia.NumeroGuiaReal ?? guia.CodigoId.ToString()} ({guia.Paciente})",
            valor: valor,
            status: status,
            formaPagamento: Domain.Entities.FormaPagamento.Convenio,
            categoriaId: categoriaId,
            pacienteId: guia.PacienteId,
            atendimentoId: guia.AtendimentoId,
            codigoFaturamentoId: guia.CodigoId,
            convenio: guia.Convenio,
            operador: operador,
            ct: ct);

    public Task<IReadOnlyList<CategoriaFinanceira>> CategoriasAsync(CancellationToken ct = default)
        => _repo.CategoriasFinanceirasAsync(ct);

    /// <summary>
    /// Categorias ativas de um tipo, na ordem de exibição — o que o combo do lançamento
    /// oferece. Categoria de despesa não faz sentido numa entrada, e vice-versa.
    /// </summary>
    public async Task<IReadOnlyList<CategoriaFinanceira>> CategoriasAsync(
        TipoLancamento tipo, CancellationToken ct = default)
    {
        var todas = await _repo.CategoriasFinanceirasAsync(ct);
        return todas
            .Where(c => c.Ativa && c.Tipo == tipo)
            .OrderBy(c => c.Ordem)
            .ThenBy(c => c.Nome)
            .ToList();
    }

    /// <summary>Cria uma categoria do plano de contas.</summary>
    public async Task<CategoriaFinanceira> CriarCategoriaAsync(
        string codigo, string nome, TipoLancamento tipo, int ordem = 0, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo)) throw new ArgumentException("Código obrigatório.", nameof(codigo));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Nome obrigatório.", nameof(nome));

        var existentes = await _repo.CategoriasFinanceirasAsync(ct);
        if (existentes.Any(c => string.Equals(c.Codigo, codigo, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Já existe uma categoria com o código '{codigo}'.");

        var categoria = new CategoriaFinanceira
        {
            Codigo = codigo.Trim(),
            Nome = nome.Trim(),
            Tipo = tipo,
            Ordem = ordem
        };
        await _repo.AdicionarCategoriaFinanceiraAsync(categoria, ct);
        await _repo.SalvarAsync(ct);
        return categoria;
    }

    /// <summary>
    /// Edita uma categoria do plano de contas. O CÓDIGO não muda: ele é a referência
    /// estável, e trocá-lo desligaria os lançamentos que já apontam para ela.
    /// </summary>
    public async Task<CategoriaFinanceira> AtualizarCategoriaAsync(
        int categoriaId, string nome, bool ativa, int ordem, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Nome obrigatório.", nameof(nome));

        var categoria = await _repo.ObterCategoriaFinanceiraAsync(categoriaId, ct)
            ?? throw new InvalidOperationException("Categoria não encontrada.");

        categoria.Nome = nome.Trim();
        categoria.Ativa = ativa;
        categoria.Ordem = ordem;

        await _repo.SalvarAsync(ct);
        return categoria;
    }

    /// <summary>Auditoria no MESMO SaveChanges da ação (atomicidade).</summary>
    private Task RegistrarAsync(string acao, string detalhe,
        LancamentoFinanceiro lancamento, string? operador, CancellationToken ct)
        => _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = acao,
            Detalhe = detalhe,
            Operador = operador ?? Environment.UserName,
            CodigoId = lancamento.CodigoFaturamentoId,
            PacienteId = lancamento.PacienteId
        }, ct);
}
