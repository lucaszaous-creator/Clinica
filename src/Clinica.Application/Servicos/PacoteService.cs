using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Saldo de um pacote na tela: o que sobrou e até quando dá para usar.</summary>
public sealed record SaldoPacote(
    int PacoteId,
    int PacienteId,
    string? PacienteNome,
    string Nome,
    TipoPacote Tipo,
    StatusPacote Situacao,
    int? SessoesContratadas,
    int SessoesUsadas,
    int? SaldoSessoes,
    decimal Valor,
    DateOnly DataCompra,
    DateOnly? ValidoAte)
{
    public bool Ativo => Situacao == StatusPacote.Ativo;

    // ---------- Situação de PAGAMENTO (set/2026) ----------
    //
    // Lida dos lançamentos que apontam para o pacote (`LancamentoFinanceiro.PacotePacienteId`).
    // Nula quando quem descreveu não trouxe os lançamentos (a lista de retenção lê pacotes
    // em bloco e não pergunta por dinheiro) — e nulo é "não conferido", nunca "não pago".

    /// <summary>O que já entrou no caixa por este pacote (lançamentos realizados).</summary>
    public decimal? ValorPago { get; init; }

    /// <summary>O que ainda está previsto (parcelas em aberto).</summary>
    public decimal? ValorAReceber { get; init; }

    public int ParcelasEmAberto { get; init; }

    /// <summary>Parcelas previstas cujo vencimento já passou — a cobrança pendente.</summary>
    public int ParcelasVencidas { get; init; }

    /// <summary>
    /// Houve algum lançamento para este pacote. Falso é o pacote vendido ANTES de a venda
    /// mover dinheiro (set/2026) ou registrado sem pagamento — e é o que a tela precisa
    /// dizer, senão "R$ 0,00 pago" se lê como calote.
    /// </summary>
    public bool TemLancamento => ValorPago is { } p && ValorAReceber is { } r && (p > 0m || r > 0m);

    /// <summary>"pago" · "R$ 400,00 a receber (2 parcelas, 1 vencida)" · "sem lançamento no caixa".</summary>
    public string PagamentoRotulo
    {
        get
        {
            if (ValorPago is null || ValorAReceber is null) return "pagamento não conferido";
            if (Valor == 0m) return "sem valor";
            if (!TemLancamento) return "sem lançamento no caixa";
            if (ValorAReceber == 0m) return "pago";

            var vencidas = ParcelasVencidas > 0 ? $", {ParcelasVencidas} vencida(s)" : string.Empty;
            return $"{ValorAReceber:C} a receber ({ParcelasEmAberto} parcela(s){vencidas})";
        }
    }

    /// <summary>"7 de 10 sessões" ou "livre até 31/12".</summary>
    public string SaldoRotulo => SessoesContratadas is { } contratadas
        ? $"{Math.Max(contratadas - SessoesUsadas, 0)} de {contratadas} sessões"
        : ValidoAte is { } ate
            ? $"livre até {ate:dd/MM/yyyy}"
            : "livre, sem prazo";
}

/// <summary>
/// Pacotes, planos e vouchers (feature 08): o que a clínica VENDE, com saldo de sessões.
///
/// Não confundir com <see cref="AutorizacaoSessoes"/>: aquilo é cota do CONVÊNIO, isto é
/// venda da clínica. As duas contam sessões e não têm nada a ver uma com a outra — a
/// cota evita glosa, o pacote evita atender de graça.
///
/// A baixa é sempre um FATO datado: desfazer é cancelar com motivo, nunca apagar. Sem
/// isso, "o paciente diz que sobrou uma sessão" viraria a palavra de um contra a do
/// outro, que é exatamente a conversa que este módulo existe para encerrar.
/// </summary>
public sealed class PacoteService
{
    private readonly IClinicaRepositorio _repo;

    public PacoteService(IClinicaRepositorio repo) => _repo = repo;

    // ==================== Catálogo ====================

    public Task<IReadOnlyList<PacoteCatalogo>> CatalogoAsync(
        bool somenteAtivos = true, CancellationToken ct = default)
        => _repo.PacotesCatalogoAsync(somenteAtivos, ct);

    public async Task<PacoteCatalogo> SalvarCatalogoAsync(
        PacoteCatalogo dados, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
            throw new InvalidOperationException("Dê um nome ao pacote.");
        if (dados.Valor < 0)
            throw new InvalidOperationException("O valor do pacote não pode ser negativo.");
        if (dados.Tipo == TipoPacote.Sessoes && dados.SessoesIncluidas is not > 0)
            throw new InvalidOperationException(
                "Um pacote de sessões precisa dizer quantas sessões inclui.");

        var destino = dados.Id == 0 ? null : await _repo.ObterPacoteCatalogoAsync(dados.Id, ct);
        if (destino is null)
        {
            destino = new PacoteCatalogo { CriadoEm = DateTime.Now, CriadoPor = operador };
            await _repo.AdicionarPacoteCatalogoAsync(destino, ct);
        }

        destino.Nome = dados.Nome.Trim();
        destino.Tipo = dados.Tipo;
        destino.SessoesIncluidas = dados.SessoesIncluidas;
        destino.Valor = dados.Valor;
        destino.ValidadeDias = dados.ValidadeDias;
        destino.Ativo = dados.Ativo;
        destino.Ordem = dados.Ordem;
        destino.Observacoes = Limpar(dados.Observacoes);

        await _repo.SalvarAsync(ct);
        return destino;
    }

    public async Task ExcluirDoCatalogoAsync(int catalogoId, CancellationToken ct = default)
    {
        await _repo.RemoverPacoteCatalogoAsync(catalogoId, ct);
        await _repo.SalvarAsync(ct);
    }

    // ==================== Venda ====================

    /// <summary>
    /// Vende um pacote do catálogo ao paciente. Os dados são COPIADOS: mudar o preço de
    /// tabela em novembro não pode reescrever o que ele comprou em março.
    /// </summary>
    public async Task<PacotePaciente> VenderAsync(
        int pacienteId, int catalogoId, DateOnly? dataCompra = null, decimal? valorCobrado = null,
        string? observacoes = null, string? operador = null, PagamentoDaVenda? pagamento = null,
        CancellationToken ct = default)
    {
        var catalogo = await _repo.ObterPacoteCatalogoAsync(catalogoId, ct)
            ?? throw new InvalidOperationException("Pacote não encontrado no catálogo.");

        var compra = dataCompra ?? DateOnly.FromDateTime(DateTime.Today);

        return await RegistrarVendaAsync(new PacotePaciente
        {
            PacienteId = pacienteId,
            PacoteCatalogoId = catalogo.Id,
            Nome = catalogo.Nome,
            Tipo = catalogo.Tipo,
            SessoesContratadas = catalogo.SessoesIncluidas,
            Valor = valorCobrado ?? catalogo.Valor,
            DataCompra = compra,
            ValidoAte = catalogo.ValidadeDias is { } dias ? compra.AddDays(dias) : null,
            Observacoes = observacoes
        }, operador, pagamento, ct);
    }

    /// <summary>
    /// Venda avulsa, sem passar pelo catálogo (o pacote combinado na hora).
    ///
    /// <paramref name="pagamento"/> é como o paciente paga (set/2026): à vista vira um
    /// lançamento REALIZADO, a prazo vira a entrada realizada mais uma conta a receber
    /// PREVISTA por parcela — tudo apontando para o pacote e gravado no MESMO
    /// <c>SaveChanges</c> da venda (ou existe tudo, ou nada; a regra 7 do compromisso).
    /// Nulo mantém o comportamento anterior — o pacote nasce sem lançamento, e a lista
    /// DIZ isso ("sem lançamento no caixa") em vez de mostrar R$ 0,00 pago com cara de
    /// calote. A tela de venda não oferece o nulo: quem vende decide como recebe.
    /// </summary>
    public Task<PacotePaciente> RegistrarVendaAsync(
        PacotePaciente dados, string? operador = null, PagamentoDaVenda? pagamento = null,
        CancellationToken ct = default)
        => _repo.ExecutarGestaoAtomicaAsync(async () =>
    {
        var paciente = await _repo.ObterPacienteAsync(dados.PacienteId, ct)
            ?? throw new InvalidOperationException("Paciente não encontrado.");

        if (string.IsNullOrWhiteSpace(dados.Nome))
            throw new InvalidOperationException("Dê um nome ao pacote vendido.");
        if (dados.Valor < 0)
            throw new InvalidOperationException("O valor do pacote não pode ser negativo.");
        if (dados.SessoesContratadas is { } sessoes && sessoes <= 0)
            throw new InvalidOperationException("O pacote precisa de ao menos uma sessão.");
        if (dados.Tipo == TipoPacote.Sessoes && dados.SessoesContratadas is null)
            throw new InvalidOperationException(
                "Pacote de sessões sem número de sessões não tem saldo para controlar.");
        if (dados.ValidoAte is { } limite && limite < dados.DataCompra)
            throw new InvalidOperationException("A validade é anterior à data da compra.");

        var pacote = new PacotePaciente
        {
            PacienteId = dados.PacienteId,
            PacoteCatalogoId = dados.PacoteCatalogoId,
            Nome = dados.Nome.Trim(),
            Tipo = dados.Tipo,
            SessoesContratadas = dados.SessoesContratadas,
            Valor = dados.Valor,
            DataCompra = dados.DataCompra == default
                ? DateOnly.FromDateTime(DateTime.Today)
                : dados.DataCompra,
            ValidoAte = dados.ValidoAte,
            LancamentoFinanceiroId = dados.LancamentoFinanceiroId,
            Observacoes = Limpar(dados.Observacoes),
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        await _repo.AdicionarPacotePacienteAsync(pacote, ct);

        // O dinheiro da venda, montado pelo MESMO desenho que a tela mostrou na prévia:
        // a prévia que promete três parcelas e grava duas é pior do que prévia nenhuma.
        // As parcelas são validadas ANTES de qualquer gravação — uma decisão que não
        // fecha (entrada maior que o valor, sem vencimento) recusa a venda inteira.
        var comoPaga = "sem lançamento no caixa";
        var mensais = new List<LancamentoFinanceiro>();
        if (pagamento is not null)
        {
            var desenho = ParcelasDaVenda.Desenhar(pacote.Valor, pacote.DataCompra, pagamento);
            foreach (var lancamento in ParcelasDaVenda.Montar(pacote, paciente.Nome, pagamento, operador))
            {
                if (decimal.Round(lancamento.Valor, 2) != lancamento.Valor)
                    throw new InvalidOperationException("Informe o valor do pacote em reais e centavos.");
                if (lancamento.Status == StatusLancamento.Realizado)
                {
                    var d = await new TaxaService(_repo).CalcularPagamentoPacienteAsync(lancamento.Valor,
                        pacote.DataCompra, pagamento.FormaDoPagoAgora!.Value, pagamento.Adquirente,
                        pagamento.Bandeira, pagamento.ParcelasCartao, ct);
                    await new FechamentoCaixaService(_repo).ExigirDiaAbertoAsync(pacote.DataCompra,
                        lancamento.FormaPagamento, ct);
                    lancamento.Adquirente = TaxaService.ModalidadeDe(lancamento.FormaPagamento) is null ? null : pagamento.Adquirente?.Trim();
                    lancamento.Bandeira = TaxaService.ModalidadeDe(lancamento.FormaPagamento) is null ? null : pagamento.Bandeira?.Trim();
                    lancamento.ModalidadeCartao = TaxaService.ModalidadeDe(lancamento.FormaPagamento, pagamento.ParcelasCartao);
                    lancamento.Parcelas = lancamento.ModalidadeCartao is null ? null : pagamento.ParcelasCartao;
                    lancamento.TaxaPercentual = d.TaxaPercentual;
                    lancamento.ValorTaxa = d.ValorTaxa;
                    lancamento.AliquotaImposto = d.AliquotaImposto;
                    lancamento.ValorImposto = d.ValorImposto;
                    lancamento.DetalheImposto = d.DetalheImposto;
                    lancamento.PrevisaoRecebimento = d.PrevisaoRecebimento;
                    if (d.LiquidacaoMensal) mensais.Add(lancamento);
                }
                await _repo.AdicionarLancamentoAsync(lancamento, ct);
            }
            comoPaga = ParcelasDaVenda.Resumir(desenho);
        }

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "PacoteVendido",
            Detalhe = $"{pacote.Nome} — {paciente.Nome} — {pacote.Valor:C} — {comoPaga}",
            PacienteId = pacote.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        foreach (var l in mensais) await new CalendarioCartaoService(_repo).CriarAsync(l, true, ct);
        return pacote;
    }, ct);

    /// <summary>
    /// Cancela a venda. Não apaga: o histórico e os consumos continuam lá.
    ///
    /// As parcelas ainda PREVISTAS caem junto — venda cancelada não é dívida do paciente,
    /// e deixá-las em aberto poria na inadimplência alguém que não deve nada. O que já foi
    /// RECEBIDO fica: é dinheiro que entrou na conta, e cancelá-lo faria o caixa parar de
    /// bater com o extrato (a regra da glosa, parcela 27). Devolução, se houver, é uma
    /// SAÍDA lançada no Caixa com a data dela — e o aviso devolvido diz isso.
    /// </summary>
    public async Task<IReadOnlyList<string>> CancelarAsync(
        int pacoteId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Diga por que o pacote está sendo cancelado.");

        var pacote = await _repo.ObterPacotePacienteAsync(pacoteId, ct)
            ?? throw new InvalidOperationException("Pacote não encontrado.");

        if (pacote.Cancelado)
            throw new InvalidOperationException("Este pacote já foi cancelado.");

        pacote.CanceladoEm = DateTime.Now;
        pacote.MotivoCancelamento = motivo.Trim();

        var avisos = new List<string>();
        var lancamentos = await _repo.LancamentosDosPacotesAsync([pacote.Id], rastreados: true, ct);
        var previstas = lancamentos.Where(l => l.Status == StatusLancamento.Previsto).ToList();
        foreach (var parcela in previstas)
        {
            parcela.Status = StatusLancamento.Cancelado;
            parcela.Observacoes = string.IsNullOrWhiteSpace(parcela.Observacoes)
                ? $"Cancelado: venda do pacote cancelada — {motivo.Trim()}"
                : $"{parcela.Observacoes} | Cancelado: venda do pacote cancelada — {motivo.Trim()}";
        }

        var recebido = lancamentos
            .Where(l => l.Status == StatusLancamento.Realizado)
            .Sum(l => l.Valor);
        if (recebido > 0m)
            avisos.Add($"{recebido:C} já recebido(s) por este pacote continuam no caixa. "
                       + "Se houver devolução, lance a saída no Caixa com a data dela.");

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "PacoteCancelado",
            Detalhe = $"{pacote.Nome} — {motivo.Trim()}"
                      + (previstas.Count > 0
                          ? $" — {previstas.Count} parcela(s) prevista(s) cancelada(s)"
                          : string.Empty),
            PacienteId = pacote.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return avisos;
    }

    // ==================== Saldo e consumo ====================

    /// <summary>
    /// O pacote que seria DEBITADO nesta data — a MESMA escolha do consumo automático
    /// (<see cref="PacotePaciente.ADebitar"/>), para a tela não prometer um pacote e o
    /// serviço debitar outro.
    ///
    /// ⚠️ Devolve o saldo SEM a situação de pagamento (uma consulta, não duas): quem
    /// pergunta "o que esta sessão debita" não pergunta se o pacote está pago, e
    /// <c>ValorPago</c>/<c>ValorAReceber</c> nulos querem dizer "não conferido" — nunca
    /// "não pago".
    /// </summary>
    public async Task<SaldoPacote?> ADebitarAsync(
        int pacienteId, DateOnly? hoje = null, CancellationToken ct = default)
    {
        var dia = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        var pacotes = await _repo.PacotesDoPacienteAsync(pacienteId, ct);

        return PacotePaciente.ADebitar(pacotes, dia) is { } escolhido
            ? Descrever(escolhido, dia)
            : null;
    }

    public async Task<IReadOnlyList<SaldoPacote>> DoPacienteAsync(
        int pacienteId, DateOnly? hoje = null, CancellationToken ct = default)
    {
        var dia = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        var pacotes = await _repo.PacotesDoPacienteAsync(pacienteId, ct);
        return await DescreverComPagamentoAsync(pacotes, dia, ct);
    }

    /// <summary>Todos os pacotes vendidos (a lista do módulo Financeiro).</summary>
    public async Task<IReadOnlyList<SaldoPacote>> VendidosAsync(
        DateOnly? hoje = null, CancellationToken ct = default)
    {
        var dia = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        var pacotes = await _repo.PacotesVendidosAsync(ct);
        return await DescreverComPagamentoAsync(pacotes, dia, ct);
    }

    /// <summary>
    /// Descreve os pacotes COM a situação de pagamento — os lançamentos vêm em UMA
    /// consulta para a lista inteira, nunca uma por pacote (a lista do Financeiro tem a
    /// carteira toda, e o banco é remoto).
    /// </summary>
    private async Task<IReadOnlyList<SaldoPacote>> DescreverComPagamentoAsync(
        IReadOnlyList<PacotePaciente> pacotes, DateOnly dia, CancellationToken ct)
    {
        if (pacotes.Count == 0) return [];

        var lancamentos = await _repo.LancamentosDosPacotesAsync(
            pacotes.Select(p => p.Id).ToList(), rastreados: false, ct);
        var porPacote = lancamentos
            .Where(l => l.PacotePacienteId is not null)
            .ToLookup(l => l.PacotePacienteId!.Value);

        return pacotes.Select(p => Descrever(p, dia, porPacote[p.Id].ToList())).ToList();
    }

    /// <summary>
    /// Debita uma sessão de um pacote específico. Recusa pacote cancelado, esgotado ou
    /// vencido — deixar passar seria dar sessão que o paciente não comprou.
    /// </summary>
    public async Task<ConsumoPacote> ConsumirAsync(
        int pacoteId, DateOnly? data = null, int? atendimentoId = null, int? agendamentoId = null,
        string? observacao = null, string? operador = null, CancellationToken ct = default)
    {
        var pacote = await _repo.ObterPacotePacienteAsync(pacoteId, ct)
            ?? throw new InvalidOperationException("Pacote não encontrado.");

        var dia = data ?? DateOnly.FromDateTime(DateTime.Today);

        if (!pacote.PodeConsumir(dia))
            throw new InvalidOperationException(pacote.Situacao(dia) switch
            {
                StatusPacote.Cancelado => "Este pacote foi cancelado.",
                StatusPacote.Esgotado => "Este pacote não tem mais sessões.",
                StatusPacote.Expirado => $"Este pacote venceu em {pacote.ValidoAte:dd/MM/yyyy}.",
                _ => "Este pacote não pode ser usado."
            });

        var consumo = new ConsumoPacote
        {
            PacotePacienteId = pacote.Id,
            Data = dia,
            AtendimentoId = atendimentoId,
            AgendamentoId = agendamentoId,
            Observacao = Limpar(observacao),
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        pacote.Consumos.Add(consumo);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "PacoteConsumido",
            Detalhe = $"{pacote.Nome} — sessão de {dia:dd/MM/yyyy}",
            PacienteId = pacote.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return consumo;
    }

    /// <summary>
    /// Baixa automática ao atender: debita do pacote ATIVO que vence primeiro. Devolve
    /// null quando o paciente não tem pacote utilizável — atender sem pacote é o caso
    /// normal (particular avulso, convênio), não um erro.
    ///
    /// Chamado pela Recepção ao concluir a sessão, e não pelo <c>AtendimentoService</c>:
    /// aquele é compartilhado com o faturamento congelado, e dar-lhe um efeito colateral
    /// novo mudaria o comportamento de um app em produção que nada tem com pacotes.
    /// </summary>
    public async Task<ConsumoPacote?> ConsumirPorAtendimentoAsync(
        int pacienteId, int atendimentoId, DateOnly? data = null, int? agendamentoId = null,
        string? operador = null, CancellationToken ct = default)
    {
        // Um atendimento debita UMA vez: concluir duas vezes a mesma sessão (ou reabrir)
        // não pode comer o pacote do paciente.
        if (await _repo.AtendimentoJaConsumiuPacoteAsync(atendimentoId, ct)) return null;

        var dia = data ?? DateOnly.FromDateTime(DateTime.Today);
        var pacotes = await _repo.PacotesDoPacienteAsync(pacienteId, ct);

        // A escolha mora na ENTIDADE (`PacotePaciente.ADebitar`): é a mesma que a proposta
        // do fechamento e a prévia do lançamento leem, e três cópias dela divergiriam.
        var escolhido = PacotePaciente.ADebitar(pacotes, dia);

        if (escolhido is null) return null;

        return await ConsumirAsync(
            escolhido.Id, dia, atendimentoId, agendamentoId,
            observacao: "Baixa automática ao concluir o atendimento",
            operador: operador, ct: ct);
    }

    /// <summary>Devolve a sessão ao saldo. O consumo continua no histórico, cancelado.</summary>
    public async Task CancelarConsumoAsync(
        int consumoId, string motivo, string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Diga por que a sessão está voltando ao saldo.");

        var consumo = await _repo.ObterConsumoPacoteAsync(consumoId, ct)
            ?? throw new InvalidOperationException("Consumo não encontrado.");

        if (consumo.Cancelado)
            throw new InvalidOperationException("Este consumo já foi cancelado.");

        consumo.CanceladoEm = DateTime.Now;
        consumo.MotivoCancelamento = motivo.Trim();

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "PacoteConsumoCancelado",
            Detalhe = $"Sessão de {consumo.Data:dd/MM/yyyy} devolvida ao saldo — {motivo.Trim()}"
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    public Task<PacotePaciente?> ObterAsync(int pacoteId, CancellationToken ct = default)
        => _repo.ObterPacotePacienteAsync(pacoteId, ct);

    /// <summary>
    /// Descreve pacotes já carregados, sem ir ao banco.
    ///
    /// Serve a quem leu os pacotes em bloco (a lista de retenção lê os de dezenas de
    /// pacientes numa consulta só) e ainda precisa da regra de situação e saldo. A regra
    /// continua morando aqui: reimplementá-la do outro lado daria duas respostas para a
    /// mesma pergunta, e a situação do pacote é CALCULADA — divergir nela é divergir
    /// sobre se o paciente ainda tem sessão paga para usar.
    /// </summary>
    public IReadOnlyList<SaldoPacote> Descrever(
        IEnumerable<PacotePaciente> pacotes, DateOnly hoje)
        => pacotes.Select(p => Descrever(p, hoje)).ToList();

    private static SaldoPacote Descrever(PacotePaciente p, DateOnly hoje)
        => new(
            p.Id, p.PacienteId, p.Paciente?.Nome, p.Nome, p.Tipo, p.Situacao(hoje),
            p.SessoesContratadas, p.SessoesUsadas, p.SaldoSessoes,
            p.Valor, p.DataCompra, p.ValidoAte);

    /// <summary>
    /// A situação de pagamento é lida dos lançamentos que apontam para o pacote. Cancelado
    /// não conta em nenhum dos dois lados: parcela cancelada não é devida, e realizado
    /// cancelado não entrou.
    /// </summary>
    private static SaldoPacote Descrever(
        PacotePaciente p, DateOnly hoje, IReadOnlyList<LancamentoFinanceiro> lancamentos)
    {
        var vivos = lancamentos.Where(l => l.Status != StatusLancamento.Cancelado).ToList();
        var previstas = vivos.Where(l => l.Status == StatusLancamento.Previsto).ToList();

        return Descrever(p, hoje) with
        {
            ValorPago = vivos.Where(l => l.Status == StatusLancamento.Realizado).Sum(l => l.Valor),
            ValorAReceber = previstas.Sum(l => l.Valor),
            ParcelasEmAberto = previstas.Count,
            ParcelasVencidas = previstas.Count(l => l.EstaVencido(hoje))
        };
    }

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
