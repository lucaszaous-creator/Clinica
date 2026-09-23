using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Totais do caixa num período (só lançamentos não cancelados).</summary>
public sealed record ResumoCaixa(
    decimal EntradasRealizadas,
    decimal SaidasRealizadas,
    decimal EntradasPrevistas,
    decimal SaidasPrevistas,
    decimal TaxasDescontadas = 0m,
    decimal ImpostosRetidos = 0m)
{
    /// <summary>O que efetivamente entrou menos o que saiu. BRUTO — o que o paciente pagou.</summary>
    public decimal SaldoRealizado => EntradasRealizadas - SaidasRealizadas;

    /// <summary>Saldo considerando também o que está previsto.</summary>
    public decimal SaldoPrevisto =>
        (EntradasRealizadas + EntradasPrevistas) - (SaidasRealizadas + SaidasPrevistas);

    /// <summary>
    /// O que a clínica de fato recebe das entradas realizadas: bruto menos a taxa da
    /// maquininha e o imposto retido. É o número que bate com o extrato da adquirente —
    /// o bruto nunca bate, e era ele que a tela mostrava sozinho até a parcela 9.
    /// </summary>
    public decimal EntradasLiquidas => EntradasRealizadas - TaxasDescontadas - ImpostosRetidos;
    public decimal SaldoLiquido => EntradasLiquidas - SaidasRealizadas;

    /// <summary>Tudo o que foi descontado do bruto no período.</summary>
    public decimal TotalDeducoes => TaxasDescontadas + ImpostosRetidos;

    /// <summary>Houve dedução no período — a tela só mostra a linha do líquido quando há.</summary>
    public bool TemDeducao => TotalDeducoes > 0m;
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
    TipoCodigo Tipo)
{
    /// <summary>
    /// Código do convênio no catálogo (parcela 18). É por ele que a RETENÇÃO na fonte é
    /// resolvida: <see cref="Convenio"/> é a família de REGRA de faturamento, e duas
    /// operadoras diferentes podem compartilhar a mesma família e reter percentuais
    /// diferentes.
    /// </summary>
    public string? ConvenioCodigo { get; init; }

    /// <summary>
    /// Situação da glosa desta guia (parcela 27). A guia glosada NÃO some da conciliação
    /// — aparece MARCADA, como o documento cancelado na central: sumir faria a linha
    /// desaparecer sem explicação, e o balcão gastaria a tarde procurando a guia que ele
    /// viu ontem. Marcada, ela diz por que não deve virar receita.
    /// </summary>
    public StatusGlosa Glosa { get; init; } = StatusGlosa.SemGlosa;

    public DateOnly? DataGlosa { get; init; }

    public string? MotivoGlosa { get; init; }

    /// <summary>
    /// O convênio recusou e ainda não aceitou de volta: lançar receita daqui é contar
    /// dinheiro que foi negado. É AVISO, não impedimento — a clínica pode estar certa de
    /// que vai recuperar no recurso, e quem decide é ela.
    /// </summary>
    public bool GlosaEmAberto => Glosa is StatusGlosa.Glosada or StatusGlosa.Reapresentada;
}

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
    public Task<LancamentoFinanceiro> LancarAsync(
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
        DeducoesRecebimento? deducoes = null,
        string? adquirente = null,
        string? bandeira = null,
        ModalidadeCartao? modalidadeCartao = null,
        int? parcelas = null,
        // Quando a entrada é PREVISTA e tem dia para acontecer (a sessão particular que
        // "fica a receber", set/2026). Sem vencimento a inadimplência não a enxerga —
        // ela só conta o que VENCEU.
        DateOnly? dataVencimento = null,
        CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("A descrição do lançamento é obrigatória.", nameof(descricao));
        if (valor <= 0)
            throw new ArgumentException("O valor deve ser maior que zero — o sinal vem do tipo.", nameof(valor));
        if (decimal.Round(valor, 2) != valor)
            throw new ArgumentException("Informe o valor em reais e centavos.", nameof(valor));
        if (deducoes is { } negativa && (negativa.ValorTaxa < 0 || negativa.ValorImposto < 0))
            throw new InvalidOperationException("Taxas e impostos não podem ser negativos.");
        if (deducoes is { } d && d.Total > valor)
            throw new InvalidOperationException(
                "As deduções não podem passar do valor bruto — isso deixaria a clínica "
                + "recebendo menos que zero por um atendimento.");

        if (status == StatusLancamento.Realizado)
            await new FechamentoCaixaService(_repo).ExigirDiaAbertoAsync(dataPagamento ?? data, formaPagamento, ct);

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
            DataVencimento = dataVencimento,
            CriadoPor = operador,

            // As deduções vêm COPIADAS do que o TaxaService calculou na hora da venda —
            // o catálogo de taxas é renegociado, e o que vale neste recebimento é o
            // percentual de hoje. O líquido não é gravado: é calculado do bruto.
            Adquirente = adquirente,
            Bandeira = bandeira,
            ModalidadeCartao = modalidadeCartao,
            Parcelas = parcelas,
            TaxaPercentual = deducoes?.TaxaPercentual,
            ValorTaxa = deducoes?.ValorTaxa,
            AliquotaImposto = deducoes?.AliquotaImposto,
            ValorImposto = deducoes?.ValorImposto,
            DetalheImposto = deducoes?.DetalheImposto,
            PrevisaoRecebimento = deducoes?.PrevisaoRecebimento
        };

        await _repo.AdicionarLancamentoAsync(lancamento, ct);
        await RegistrarAsync("LancamentoCriado",
            $"{tipo} de {valor:C} — {lancamento.Descricao}", lancamento, operador, ct);
        await _repo.SalvarAsync(ct);
        await new CalendarioCartaoService(_repo).CriarAsync(lancamento, deducoes?.LiquidacaoMensal == true, ct);
        return lancamento;
    }, ct);

    /// <summary>Marca um lançamento previsto como efetivamente pago/recebido.</summary>
    public Task RealizarAsync(int lancamentoId, DateOnly? dataPagamento = null,
        FormaPagamento? formaPagamento = null, string? operador = null, CancellationToken ct = default,
        string? adquirente = null, string? bandeira = null, int? parcelas = null, decimal? valorConferido = null,
        decimal? valorPago = null, DateOnly? vencimentoSaldo = null)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
    {
        var lancamento = await _repo.ObterLancamentoAsync(lancamentoId, ct)
            ?? throw new InvalidOperationException($"Lançamento {lancamentoId} não encontrado.");

        if (lancamento.Status == StatusLancamento.Cancelado)
            throw new InvalidOperationException("Lançamento cancelado não pode ser realizado.");
        if (lancamento.Status == StatusLancamento.Realizado)
            throw new InvalidOperationException("Este lançamento já foi realizado.");
        if (valorConferido is not null && valorConferido != lancamento.Valor)
            throw new InvalidOperationException("O valor mudou. Atualize a cobrança antes de confirmar o pagamento.");

        var dia = dataPagamento ?? DateOnly.FromDateTime(DateTime.Today);
        if (dia > DateOnly.FromDateTime(DateTime.Today))
            throw new InvalidOperationException("Pagamento futuro deve permanecer previsto.");
        await new FechamentoCaixaService(_repo).ExigirDiaAbertoAsync(dia, formaPagamento ?? lancamento.FormaPagamento, ct);
        var forma = formaPagamento ?? lancamento.FormaPagamento;
        if (forma is { } formaInformada && !Enum.IsDefined(formaInformada))
            throw new InvalidOperationException("Forma de pagamento inválida.");
        if (valorPago is { } parcial)
            await new ContasService(_repo).PrepararBaixaParcialAsync(lancamento, parcial, vencimentoSaldo, operador, ct);
        var liquidacaoMensal = false;
        if (lancamento.Tipo == TipoLancamento.Entrada && TaxaService.ModalidadeDe(forma) is not null)
        {
            var d = await new TaxaService(_repo).CalcularPagamentoPacienteAsync(lancamento.Valor, dia, forma!.Value,
                adquirente ?? lancamento.Adquirente, bandeira ?? lancamento.Bandeira, parcelas ?? lancamento.Parcelas ?? 1, ct);
            liquidacaoMensal = d.LiquidacaoMensal;
            lancamento.Adquirente = (adquirente ?? lancamento.Adquirente)?.Trim();
            lancamento.Bandeira = (bandeira ?? lancamento.Bandeira)?.Trim();
            lancamento.Parcelas = parcelas ?? lancamento.Parcelas ?? 1;
            lancamento.ModalidadeCartao = TaxaService.ModalidadeDe(forma, lancamento.Parcelas.Value);
            lancamento.ValorTaxa = d.ValorTaxa;
            lancamento.TaxaPercentual = d.TaxaPercentual;
            lancamento.PrevisaoRecebimento = d.PrevisaoRecebimento;
        }
        else if (forma is not null && forma != FormaPagamento.Convenio)
        {
            lancamento.ValorTaxa = null;
            lancamento.TaxaPercentual = null;
            lancamento.PrevisaoRecebimento = null;
            lancamento.Adquirente = null;
            lancamento.Bandeira = null;
            lancamento.Parcelas = null;
            lancamento.ModalidadeCartao = null;
        }
        lancamento.Status = StatusLancamento.Realizado;
        lancamento.DataPagamento = dataPagamento ?? DateOnly.FromDateTime(DateTime.Today);
        if (formaPagamento is not null) lancamento.FormaPagamento = formaPagamento;

        await RegistrarAsync("LancamentoRealizado",
            $"{lancamento.Valor:C} — {lancamento.Descricao}", lancamento, operador, ct);
        await _repo.SalvarAsync(ct);
        await new CalendarioCartaoService(_repo).CriarAsync(lancamento, liquidacaoMensal, ct);
        return true;
    }, ct);

    /// <summary>
    /// Cancela um lançamento. Nunca apaga: o registro sai dos totais mas permanece no
    /// histórico, junto com o motivo.
    /// </summary>
    public Task CancelarAsync(int lancamentoId, string motivo,
        string? operador = null, CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > 300)
            throw new ArgumentException("Informe o motivo do cancelamento (até 300 caracteres).", nameof(motivo));

        var lancamento = await _repo.ObterLancamentoAsync(lancamentoId, ct)
            ?? throw new InvalidOperationException($"Lançamento {lancamentoId} não encontrado.");

        if (lancamento.Status == StatusLancamento.Cancelado)
            throw new InvalidOperationException("Este lançamento já está cancelado.");
        if (lancamento.Conciliado || lancamento.RecebimentoConfirmadoEm is not null
            || (await _repo.ParcelasCartaoDoLancamentoAsync(lancamento.Id, ct)).Any(p => p.RecebidoEm != null || p.ConciliadoEm != null))
            throw new InvalidOperationException("Desfaça a conciliação e a confirmação de depósito antes de cancelar o lançamento.");

        if (lancamento.Status == StatusLancamento.Realizado)
            await new FechamentoCaixaService(_repo).ExigirDiaAbertoAsync(lancamento.DataPagamento ?? lancamento.Data, lancamento.FormaPagamento, ct);
        await new ContasService(_repo).ReabrirBaixaDesdobradaAsync(lancamento, operador, ct);
        var recibo = await _repo.ReciboVigenteDoLancamentoAsync(lancamento.Id, ct);
        if (recibo is not null)
            await new DocumentoFinanceiroService(_repo).CancelarAsync(recibo.Id, "Pagamento cancelado: " + motivo.Trim(), operador, ct);
        lancamento.Status = StatusLancamento.Cancelado;
        var observacaoCancelamento = string.IsNullOrWhiteSpace(lancamento.Observacoes)
            ? $"Cancelado: {motivo}" : $"{lancamento.Observacoes} | Cancelado: {motivo}";
        if (observacaoCancelamento.Length <= 500) lancamento.Observacoes = observacaoCancelamento;

        await RegistrarAsync("LancamentoCancelado",
            $"{lancamento.Valor:C} — {motivo}", lancamento, operador, ct);
        await _repo.SalvarAsync(ct);
        return true;
    }, ct);

    /// <summary>
    /// Lançamentos do período, do mais recente para o mais antigo.
    /// <paramref name="limite"/> corta no BANCO — a tela mostra as primeiras linhas, e
    /// trazer o período inteiro para descartar em memória é desperdício de rede (mesma
    /// convenção da busca de pacientes). Null = sem corte, para quem precisa de todos.
    /// </summary>
    public Task<IReadOnlyList<LancamentoFinanceiro>> DoPeriodoAsync(
        DateOnly inicio, DateOnly fim, int? limite = null, CancellationToken ct = default)
        => _repo.LancamentosNoPeriodoAsync(inicio, fim, limite, ct);

    /// <summary>
    /// Totais do período, ignorando cancelados.
    ///
    /// Usa a PROJEÇÃO (tipo, situação, valor) em vez da lista completa: antes esta
    /// chamada trazia o mês inteiro do banco — com categoria e paciente carregados
    /// junto — só para somar quatro números, e a tela de Caixa já tinha carregado a
    /// mesma lista uma linha antes. Eram duas viagens pesadas por atualização, num
    /// banco remoto.
    ///
    /// A soma continua em memória de propósito: o SQLite, onde os testes rodam, não
    /// traduz <c>Sum</c> sobre <c>decimal</c> (mesmo motivo já documentado no estoque).
    /// O que muda é o TAMANHO do que vem — três colunas, sem join.
    /// </summary>
    public async Task<ResumoCaixa> ResumoAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        var lancamentos = await _repo.ValoresDeLancamentoNoPeriodoAsync(inicio, fim, ct);

        decimal Somar(TipoLancamento tipo, StatusLancamento status) =>
            lancamentos.Where(l => l.Tipo == tipo && l.Status == status).Sum(l => l.Valor);

        // A dedução só conta sobre ENTRADA REALIZADA: taxa de maquininha em dinheiro que
        // ainda não entrou é desconto de receita que não existe, e inflaria o "descontado"
        // do mês com o que a adquirente ainda nem processou.
        var entradasRealizadas = lancamentos
            .Where(l => l.Tipo == TipoLancamento.Entrada && l.Status == StatusLancamento.Realizado)
            .ToList();

        return new ResumoCaixa(
            Somar(TipoLancamento.Entrada, StatusLancamento.Realizado),
            Somar(TipoLancamento.Saida, StatusLancamento.Realizado),
            Somar(TipoLancamento.Entrada, StatusLancamento.Previsto),
            Somar(TipoLancamento.Saida, StatusLancamento.Previsto),
            entradasRealizadas.Sum(l => l.ValorTaxa ?? 0m),
            entradasRealizadas.Sum(l => l.ValorImposto ?? 0m));
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
                c.Tipo)
            {
                // Sem código no catálogo (paciente antigo), cai na família — é o que o
                // resto do sistema já faz para resolver nome e regra do convênio.
                ConvenioCodigo = c.Atendimento.Paciente.ConvenioCodigo
                                 ?? c.Atendimento.Paciente.Convenio.ToString(),

                // A glosa vem junto (parcela 27): sem ela, a conciliação convidava o
                // balcão a lançar receita de uma guia que o convênio já tinha recusado —
                // e a linha era idêntica à de uma guia paga.
                Glosa = c.Glosa,
                DataGlosa = c.DataGlosa,
                MotivoGlosa = c.MotivoGlosa
            })
            .OrderBy(g => g.DataBaixa)
            .ToList();
    }

    /// <summary>
    /// Cria a receita a partir de uma guia já efetivada, deixando o vínculo gravado
    /// para que ela não apareça de novo na conciliação.
    ///
    /// <paramref name="deducoes"/> carrega a RETENÇÃO NA FONTE do convênio (parcela 18).
    /// A operadora que paga serviço de PJ retém IRRF, CSLL/PIS/COFINS e às vezes o ISS
    /// antes de depositar: a guia vale R$ 1.000 e caem R$ 943,50. Até a parcela 18 o
    /// sistema gravava os R$ 1.000 e não sabia da diferença — o mesmo defeito que a
    /// parcela 9 corrigiu para a maquininha, intocado justamente no convênio, que é onde
    /// esta clínica fatura mais.
    ///
    /// O valor do lançamento continua sendo o BRUTO da guia, como em todo o resto do
    /// módulo: a retenção é dedução ao lado, e o líquido é calculado.
    /// </summary>
    public async Task<LancamentoFinanceiro> LancarReceitaDaGuiaAsync(
        GuiaSemLancamento guia,
        decimal valor,
        StatusLancamento status = StatusLancamento.Previsto,
        int? categoriaId = null,
        string? operador = null,
        DeducoesRecebimento? deducoes = null,
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
            // O CÓDIGO vai junto da família (parcela 18): a análise por convênio precisa
            // separar duas operadoras que compartilham a mesma regra de faturamento.
            convenioCodigo: guia.ConvenioCodigo,
            operador: operador,
            deducoes: deducoes,
            ct: ct);

    // ==================== A conciliação do PARTICULAR (set/2026) ====================

    /// <summary>
    /// Sessões particulares realizadas no período que ainda não têm dinheiro registrado —
    /// nem recebido, nem a receber, nem sessão de pacote. É a pergunta que o financeiro
    /// faz à Recepção: "o que aconteceu e ninguém registrou como foi pago?".
    ///
    /// O par de <see cref="GuiasSemLancamentoAsync"/> para quem não tem convênio: aquela
    /// lista é inacessível ao particular por construção (<c>CodigosNoPeriodoAsync</c> exclui
    /// o <c>NaoAplicavel</c>), e até aqui a sessão particular que saía do balcão sem
    /// lançamento não aparecia em lugar nenhum — nem no dia, nem depois.
    /// </summary>
    public Task<IReadOnlyList<SessaoSemReceita>> SessoesParticularesSemReceitaAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
        => _repo.SessoesParticularesSemReceitaAsync(inicio, fim, ct);

    /// <summary>
    /// Registra o dinheiro de uma sessão particular, deixando o vínculo gravado para ela
    /// sair da conciliação. <paramref name="vencimento"/> nulo = RECEBIDO (na data
    /// informada, ou na da sessão); com vencimento = fica A RECEBER, previsto, com dono —
    /// e a inadimplência passa a enxergá-lo quando vencer.
    /// </summary>
    public Task<LancamentoFinanceiro> LancarReceitaDaSessaoAsync(
        SessaoSemReceita sessao,
        decimal valor,
        FormaPagamento? forma = null,
        DateOnly? vencimento = null,
        DateOnly? dataRecebimento = null,
        int? categoriaId = null,
        string? operador = null,
        DeducoesRecebimento? deducoes = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        var aReceber = vencimento is not null;

        return LancarAsync(
            data: sessao.Data,
            tipo: TipoLancamento.Entrada,
            descricao: DescricaoDaSessao(sessao.Data, sessao.ModalidadeNome, sessao.Paciente),
            valor: valor,
            status: aReceber ? StatusLancamento.Previsto : StatusLancamento.Realizado,
            // A parcela prevista não tem forma — ela é decidida no dia em que o paciente
            // paga, e é o `RealizarAsync` que a grava.
            formaPagamento: aReceber ? null : forma,
            categoriaId: categoriaId,
            pacienteId: sessao.PacienteId,
            atendimentoId: sessao.AtendimentoId,
            convenio: sessao.Convenio,
            convenioCodigo: sessao.CodigoParaTabela,
            dataPagamento: aReceber ? null : dataRecebimento,
            operador: operador,
            deducoes: aReceber ? null : deducoes,
            dataVencimento: vencimento,
            ct: ct);
    }

    /// <summary>
    /// A descrição da receita de uma sessão — a MESMA para o fechamento no balcão e para a
    /// conciliação depois. Até set/2026 ela era "Sessão de dd/MM/yyyy", e dez sessões do
    /// mesmo dia eram dez linhas idênticas no extrato do Caixa. Cabe nos 200 da coluna.
    /// </summary>
    public static string DescricaoDaSessao(DateOnly data, string modalidade, string paciente)
    {
        var texto = $"Sessão de {data:dd/MM/yyyy} — {modalidade} ({paciente})";
        return texto.Length <= 200 ? texto : texto[..200];
    }

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
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            CodigoId = lancamento.CodigoFaturamentoId,
            PacienteId = lancamento.PacienteId
        }, ct);
}
