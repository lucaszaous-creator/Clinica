using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// O que a geração de contas recorrentes fez numa rodada.
///
/// <paramref name="JaExistiam"/> é contado, não escondido: a rodada que "não fez nada"
/// e a rodada que "não rodou" precisam ser distinguíveis na tela, pela mesma razão que
/// o painel de pendências mostra um terceiro estado quando a checagem não rodou.
/// </summary>
public sealed record ResultadoGeracaoContas(
    int Geradas,
    int JaExistiam,
    IReadOnlyList<string> Avisos)
{
    public bool FezAlgo => Geradas > 0;
}

/// <summary>Totais do que está por vencer e do que já venceu.</summary>
public sealed record ResumoContas(
    decimal APagarVencido,
    decimal APagarAVencer,
    decimal AReceberVencido,
    decimal AReceberAVencer,
    int QuantidadeVencida)
{
    /// <summary>Tudo o que já passou do prazo, dos dois lados.</summary>
    public decimal TotalVencido => APagarVencido + AReceberVencido;

    /// <summary>Sobra prevista do que ainda vai vencer no horizonte consultado.</summary>
    public decimal SaldoPrevisto =>
        (AReceberVencido + AReceberAVencer) - (APagarVencido + APagarAVencer);

    public bool TemVencido => QuantidadeVencida > 0;
}

/// <summary>
/// Contas a pagar e a receber (parcela 12).
///
/// Até aqui o caixa sabia o que JÁ aconteceu e o que estava "previsto", mas previsto sem
/// data de vencimento não responde à pergunta que se faz toda segunda-feira: "o que vence
/// esta semana?". <see cref="LancamentoFinanceiro.DataVencimento"/> é o que faltava, e
/// este serviço é a leitura dela.
///
/// A segunda metade é a RECORRÊNCIA. Aluguel, luz, contador e mensalidade de software
/// eram redigitados todo mês, e conta redigitada é conta esquecida. O molde
/// (<see cref="LancamentoRecorrente"/>) gera lançamentos PREVISTOS, nunca realizados: o
/// sistema sabe que o aluguel vence dia 10, não que ele foi pago — quem paga é a clínica,
/// e dar baixa sozinho encheria o caixa de saída que talvez não tenha saído.
///
/// A geração é IDEMPOTENTE pela <see cref="LancamentoFinanceiro.OrigemRecorrencia"/>,
/// com índice único no banco. Dois postos abrindo o app na mesma manhã não podem criar
/// o aluguel de agosto duas vezes.
/// </summary>
public sealed class ContasService
{
    private readonly IClinicaRepositorio _repo;

    public ContasService(IClinicaRepositorio repo) => _repo = repo;

    // ---------------- Leitura das contas ----------------

    /// <summary>
    /// Contas em aberto (previstas) com vencimento até <paramref name="ate"/>, incluindo
    /// as que já venceram — vencida é justamente a que mais importa, e escondê-la porque
    /// "está fora do período consultado" seria esconder o problema.
    /// </summary>
    public Task<IReadOnlyList<LancamentoFinanceiro>> EmAbertoAsync(
        DateOnly ate, TipoLancamento? tipo = null, CancellationToken ct = default)
        => _repo.LancamentosComVencimentoAteAsync(ate, tipo, ct);

    /// <summary>Contas em aberto cujo vencimento já passou.</summary>
    public async Task<IReadOnlyList<LancamentoFinanceiro>> VencidasAsync(
        DateOnly hoje, TipoLancamento? tipo = null, CancellationToken ct = default)
    {
        var abertas = await _repo.LancamentosComVencimentoAteAsync(hoje, tipo, ct);
        return abertas.Where(l => l.EstaVencido(hoje)).ToList();
    }

    /// <summary>
    /// Totais do que vence até <paramref name="ate"/>, separando o que já passou do prazo
    /// do que ainda vai vencer. Soma em memória porque o SQLite dos testes não traduz
    /// <c>Sum</c> sobre <c>decimal</c> — mesma razão já registrada no estoque e no caixa.
    /// </summary>
    public async Task<ResumoContas> ResumoAsync(
        DateOnly hoje, DateOnly ate, CancellationToken ct = default)
    {
        var abertas = await _repo.LancamentosComVencimentoAteAsync(ate, null, ct);

        decimal Somar(TipoLancamento tipo, bool vencido) => abertas
            .Where(l => l.Tipo == tipo && l.EstaVencido(hoje) == vencido)
            .Sum(l => l.Valor);

        return new ResumoContas(
            Somar(TipoLancamento.Saida, true),
            Somar(TipoLancamento.Saida, false),
            Somar(TipoLancamento.Entrada, true),
            Somar(TipoLancamento.Entrada, false),
            abertas.Count(l => l.EstaVencido(hoje)));
    }

    /// <summary>
    /// Registra uma conta a pagar ou a receber: é um lançamento PREVISTO com vencimento.
    /// Reaproveita o <see cref="FinanceiroService"/>? Não — a conta precisa do vencimento,
    /// que o lançar genérico não conhece, e duplicar a validação aqui sairia mais caro
    /// que gravá-la direto com a auditoria junto.
    /// </summary>
    public async Task<LancamentoFinanceiro> LancarContaAsync(
        TipoLancamento tipo,
        string descricao,
        decimal valor,
        DateOnly vencimento,
        DateOnly? competencia = null,
        int? categoriaId = null,
        FormaPagamento? formaPagamento = null,
        string? observacoes = null,
        string? operador = null,
        string? origemRecorrencia = null,
        // De QUEM é a conta a receber (parcela 23). Sem isto a dívida existia mas não
        // tinha dono, e "quem me deve?" não tinha como ser respondido: a conta vencida
        // aparecia dissolvida na lista, sem ligação com o paciente que a contraiu.
        // Opcional porque conta a PAGAR não tem paciente nenhum.
        int? pacienteId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("A descrição da conta é obrigatória.", nameof(descricao));
        if (valor <= 0)
            throw new ArgumentException("O valor deve ser maior que zero — o sinal vem do tipo.", nameof(valor));

        var conta = new LancamentoFinanceiro
        {
            // A competência PADRÃO é o próprio vencimento. A clínica não faz regime de
            // competência separado, e pedir duas datas para quem só quer registrar o
            // aluguel transformaria a tarefa mais comum na mais chata.
            Data = competencia ?? vencimento,
            DataVencimento = vencimento,
            Tipo = tipo,
            Descricao = descricao.Trim(),
            Valor = valor,
            Status = StatusLancamento.Previsto,
            CategoriaFinanceiraId = categoriaId,
            FormaPagamento = formaPagamento,
            Observacoes = observacoes,
            OrigemRecorrencia = origemRecorrencia,
            PacienteId = pacienteId,
            CriadoPor = operador
        };

        await _repo.AdicionarLancamentoAsync(conta, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = "ContaCriada",
            Detalhe = $"{tipo} de {valor:C} vencendo em {vencimento:dd/MM/yyyy} — {conta.Descricao}",
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador
        }, ct);
        await _repo.SalvarAsync(ct);
        return conta;
    }

    /// <summary>
    /// Adia o vencimento de uma conta em aberto. Renegociar prazo é rotina; o que não se
    /// faz é mexer no vencimento de conta já paga — a data em que ela venceu é fato, e
    /// reescrevê-la apagaria o atraso que de fato houve.
    /// </summary>
    public async Task ReagendarAsync(
        int lancamentoId, DateOnly novoVencimento, string? operador = null, CancellationToken ct = default)
    {
        var conta = await _repo.ObterLancamentoAsync(lancamentoId, ct)
            ?? throw new InvalidOperationException($"Lançamento {lancamentoId} não encontrado.");

        if (conta.Status != StatusLancamento.Previsto)
            throw new InvalidOperationException(
                "Só conta em aberto pode ser reagendada — a data em que uma conta já paga "
                + "venceu é fato, e reescrevê-la apagaria o atraso que de fato houve.");

        var antes = conta.DataVencimento;
        conta.DataVencimento = novoVencimento;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = "ContaReagendada",
            Detalhe = $"{conta.Descricao}: {antes?.ToString("dd/MM/yyyy") ?? "sem vencimento"} "
                      + $"→ {novoVencimento:dd/MM/yyyy}",
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador
        }, ct);
        await _repo.SalvarAsync(ct);
    }

    // ---------------- Contas recorrentes ----------------

    public Task<IReadOnlyList<LancamentoRecorrente>> RecorrentesAsync(
        bool somenteAtivas = false, CancellationToken ct = default)
        => _repo.RecorrentesAsync(somenteAtivas, ct);

    public async Task<LancamentoRecorrente> CriarRecorrenteAsync(
        string descricao,
        TipoLancamento tipo,
        decimal valor,
        PeriodicidadeRecorrencia periodicidade,
        DateOnly primeiroVencimento,
        DateOnly? ultimoVencimento = null,
        int? categoriaId = null,
        FormaPagamento? formaPagamento = null,
        string? observacoes = null,
        string? operador = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("A descrição da conta é obrigatória.", nameof(descricao));
        if (valor <= 0)
            throw new ArgumentException("O valor deve ser maior que zero.", nameof(valor));
        if (ultimoVencimento is { } fim && fim < primeiroVencimento)
            throw new InvalidOperationException(
                "O último vencimento não pode ser anterior ao primeiro — a série nasceria vazia.");

        var recorrente = new LancamentoRecorrente
        {
            Descricao = descricao.Trim(),
            Tipo = tipo,
            Valor = valor,
            Periodicidade = periodicidade,
            PrimeiroVencimento = primeiroVencimento,
            UltimoVencimento = ultimoVencimento,
            CategoriaFinanceiraId = categoriaId,
            FormaPagamento = formaPagamento,
            Observacoes = observacoes,
            CriadoPor = operador
        };

        await _repo.AdicionarRecorrenteAsync(recorrente, ct);
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = "RecorrenteCriada",
            Detalhe = $"{tipo} de {valor:C} {recorrente.PeriodicidadeTexto} — {recorrente.Descricao}",
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador
        }, ct);
        await _repo.SalvarAsync(ct);
        return recorrente;
    }

    /// <summary>
    /// Muda o molde. Vale só para o que ainda NÃO foi gerado: o lançamento de agosto que
    /// já nasceu não é reescrito, porque ele já pode ter sido pago, conciliado e conferido
    /// no extrato. Mesma regra da vigência do repasse e do preço do pacote.
    /// </summary>
    public async Task<LancamentoRecorrente> AtualizarRecorrenteAsync(
        int recorrenteId,
        string descricao,
        decimal valor,
        DateOnly? ultimoVencimento,
        bool ativa,
        int? categoriaId = null,
        string? observacoes = null,
        string? operador = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("A descrição da conta é obrigatória.", nameof(descricao));
        if (valor <= 0)
            throw new ArgumentException("O valor deve ser maior que zero.", nameof(valor));

        var recorrente = await _repo.ObterRecorrenteAsync(recorrenteId, ct)
            ?? throw new InvalidOperationException("Conta recorrente não encontrada.");

        if (ultimoVencimento is { } fim && fim < recorrente.PrimeiroVencimento)
            throw new InvalidOperationException(
                "O último vencimento não pode ser anterior ao primeiro.");

        recorrente.Descricao = descricao.Trim();
        recorrente.Valor = valor;
        recorrente.UltimoVencimento = ultimoVencimento;
        recorrente.Ativa = ativa;
        recorrente.CategoriaFinanceiraId = categoriaId;
        recorrente.Observacoes = observacoes;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Acao = ativa ? "RecorrenteAtualizada" : "RecorrenteDesativada",
            Detalhe = $"{recorrente.Descricao} — {valor:C} {recorrente.PeriodicidadeTexto}",
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador
        }, ct);
        await _repo.SalvarAsync(ct);
        return recorrente;
    }

    /// <summary>
    /// Gera os lançamentos previstos de todas as contas recorrentes ativas com vencimento
    /// até <paramref name="ate"/>.
    ///
    /// Duas decisões que sustentam o resto:
    ///
    /// 1. Gera o PASSADO também. Uma recorrência cadastrada hoje com primeiro vencimento
    ///    em janeiro produz as contas de janeiro em diante — quem cadastra o aluguel no
    ///    meio do ano quer justamente registrar o que já vinha vencendo. Por isso a
    ///    janela é "até", não "de hoje até".
    /// 2. NÃO dá baixa. Tudo nasce <see cref="StatusLancamento.Previsto"/>: o sistema sabe
    ///    que a conta vence, não que ela foi paga.
    ///
    /// A idempotência é por <see cref="LancamentoRecorrente.OrigemDe"/> consultada em
    /// bloco antes de gravar, e reforçada pelo índice único — a consulta evita o trabalho,
    /// o índice evita a corrida.
    /// </summary>
    public async Task<ResultadoGeracaoContas> GerarAsync(
        DateOnly ate, string? operador = null, CancellationToken ct = default)
    {
        var recorrentes = await _repo.RecorrentesAsync(somenteAtivas: true, ct);
        if (recorrentes.Count == 0) return new ResultadoGeracaoContas(0, 0, []);

        // Monta todas as ocorrências candidatas antes de ir ao banco: uma consulta só
        // para saber o que já existe, em vez de uma por conta por mês.
        var candidatas = new List<(LancamentoRecorrente Recorrente, DateOnly Vencimento, string Origem)>();
        foreach (var r in recorrentes)
            foreach (var v in r.VencimentosAte(ate))
                candidatas.Add((r, v, r.OrigemDe(v)));

        if (candidatas.Count == 0) return new ResultadoGeracaoContas(0, 0, []);

        var existentes = (await _repo.OrigensRecorrenciaExistentesAsync(
            candidatas.Select(c => c.Origem).Distinct().ToList(), ct)).ToHashSet();

        var avisos = new List<string>();
        var geradas = 0;
        var jaExistiam = 0;

        foreach (var (r, vencimento, origem) in candidatas)
        {
            if (existentes.Contains(origem)) { jaExistiam++; continue; }

            try
            {
                var conta = new LancamentoFinanceiro
                {
                    Data = vencimento,
                    DataVencimento = vencimento,
                    Tipo = r.Tipo,
                    Descricao = r.Descricao,
                    Valor = r.Valor,
                    Status = StatusLancamento.Previsto,
                    CategoriaFinanceiraId = r.CategoriaFinanceiraId,
                    FormaPagamento = r.FormaPagamento,
                    Observacoes = r.Observacoes,
                    OrigemRecorrencia = origem,
                    CriadoPor = operador
                };
                await _repo.AdicionarLancamentoAsync(conta, ct);
                // Marca em memória: a mesma origem não pode ser gerada duas vezes dentro
                // da própria rodada (duas recorrências não colidem, mas a guarda é grátis).
                existentes.Add(origem);
                geradas++;
            }
            catch (Exception ex)
            {
                // Uma conta que falha não pode derrubar a geração das outras — mesma
                // regra do fechamento da sessão: o que falhou vira aviso, com nome.
                Diagnostico.Registrar($"Geração da conta recorrente '{r.Descricao}' ({vencimento:dd/MM/yyyy})", ex);
                avisos.Add($"{r.Descricao} ({vencimento:dd/MM/yyyy}): {ex.Message}");
            }
        }

        if (geradas > 0)
        {
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Acao = "ContasRecorrentesGeradas",
                Detalhe = $"{geradas} conta(s) prevista(s) até {ate:dd/MM/yyyy}",
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador
            }, ct);
            await _repo.SalvarAsync(ct);
        }

        return new ResultadoGeracaoContas(geradas, jaExistiam, avisos);
    }
}
