using Clinica.Application.Abstracoes;
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
        string? observacoes = null, string? operador = null, CancellationToken ct = default)
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
        }, operador, ct);
    }

    /// <summary>Venda avulsa, sem passar pelo catálogo (o pacote combinado na hora).</summary>
    public async Task<PacotePaciente> RegistrarVendaAsync(
        PacotePaciente dados, string? operador = null, CancellationToken ct = default)
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
        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "PacoteVendido",
            Detalhe = $"{pacote.Nome} — {paciente.Nome} — {pacote.Valor:C}",
            PacienteId = pacote.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return pacote;
    }

    /// <summary>Amarra o pagamento já lançado no caixa ao pacote vendido.</summary>
    public async Task VincularPagamentoAsync(
        int pacoteId, int lancamentoId, CancellationToken ct = default)
    {
        var pacote = await _repo.ObterPacotePacienteAsync(pacoteId, ct)
            ?? throw new InvalidOperationException("Pacote não encontrado.");

        pacote.LancamentoFinanceiroId = lancamentoId;
        await _repo.SalvarAsync(ct);
    }

    /// <summary>Cancela a venda. Não apaga: o histórico e os consumos continuam lá.</summary>
    public async Task CancelarAsync(
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

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "PacoteCancelado",
            Detalhe = $"{pacote.Nome} — {motivo.Trim()}",
            PacienteId = pacote.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
    }

    // ==================== Saldo e consumo ====================

    public async Task<IReadOnlyList<SaldoPacote>> DoPacienteAsync(
        int pacienteId, DateOnly? hoje = null, CancellationToken ct = default)
    {
        var dia = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        var pacotes = await _repo.PacotesDoPacienteAsync(pacienteId, ct);
        return pacotes.Select(p => Descrever(p, dia)).ToList();
    }

    /// <summary>Todos os pacotes vendidos (a lista do módulo Financeiro).</summary>
    public async Task<IReadOnlyList<SaldoPacote>> VendidosAsync(
        DateOnly? hoje = null, CancellationToken ct = default)
    {
        var dia = hoje ?? DateOnly.FromDateTime(DateTime.Today);
        var pacotes = await _repo.PacotesVendidosAsync(ct);
        return pacotes.Select(p => Descrever(p, dia)).ToList();
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

        var escolhido = pacotes
            .Where(p => p.PodeConsumir(dia))
            // O que vence primeiro sai na frente; sem validade, o mais antigo.
            .OrderBy(p => p.ValidoAte ?? DateOnly.MaxValue)
            .ThenBy(p => p.DataCompra)
            .ThenBy(p => p.Id)
            .FirstOrDefault();

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

    private static string? Limpar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
