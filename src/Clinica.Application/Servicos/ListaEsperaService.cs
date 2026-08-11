using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Lista de espera: quem quer atendimento e não achou horário.
///
/// Ela existe pelo mesmo motivo do dashboard de pendências — o que a recepção guarda
/// só na cabeça, se perde. Quando um horário vaga (cancelamento, falta), a secretária
/// precisa saber NA HORA quem chamar; sem lista, a vaga vira buraco na agenda.
/// </summary>
public sealed class ListaEsperaService
{
    private readonly IClinicaRepositorio _repo;
    private readonly AgendaService _agenda;

    public ListaEsperaService(IClinicaRepositorio repo, AgendaService agenda)
    {
        _repo = repo;
        _agenda = agenda;
    }

    /// <summary>Pedidos que ainda procuram horário — prioritários primeiro, depois por ordem de chegada.</summary>
    public Task<IReadOnlyList<ListaEspera>> AguardandoAsync(CancellationToken ct = default)
        => _repo.ListaEsperaAsync(somenteAguardando: true, ct);

    /// <summary>Tudo, inclusive já agendado/removido (histórico da lista).</summary>
    public Task<IReadOnlyList<ListaEspera>> TodosAsync(CancellationToken ct = default)
        => _repo.ListaEsperaAsync(somenteAguardando: false, ct);

    public async Task<ListaEspera> AdicionarAsync(
        int pacienteId, int? profissionalId = null, string? modalidadeCodigo = null,
        PeriodoPreferido periodo = PeriodoPreferido.Qualquer,
        DateOnly? disponivelDe = null, DateOnly? disponivelAte = null,
        bool prioritario = false, string? observacoes = null, CancellationToken ct = default)
    {
        if (await _repo.ObterPacienteAsync(pacienteId, ct) is null)
            throw new InvalidOperationException("Paciente não encontrado.");

        if (disponivelDe is not null && disponivelAte is not null && disponivelAte < disponivelDe)
            throw new InvalidOperationException(
                "A data final da janela não pode ser anterior à inicial.");

        // Mesmo paciente pedido duas vezes é erro de digitação, não urgência dobrada.
        var aguardando = await _repo.ListaEsperaAsync(somenteAguardando: true, ct);
        if (aguardando.Any(l => l.PacienteId == pacienteId && l.ProfissionalId == profissionalId))
            throw new InvalidOperationException(
                "Este paciente já está na lista de espera para este profissional.");

        var pedido = new ListaEspera
        {
            PacienteId = pacienteId,
            ProfissionalId = profissionalId,
            ModalidadeCodigo = modalidadeCodigo,
            Periodo = periodo,
            DisponivelDe = disponivelDe,
            DisponivelAte = disponivelAte,
            Prioritario = prioritario,
            Observacoes = observacoes,
            Status = StatusListaEspera.Aguardando,
            CriadoEm = DateTime.Now
        };

        await _repo.AdicionarListaEsperaAsync(pedido, ct);
        await _repo.SalvarAsync(ct);
        return pedido;
    }

    /// <summary>
    /// Quem serve para um horário que vagou: respeita a janela de datas, o turno e o
    /// profissional pedido (quem não escolheu profissional serve para qualquer um).
    /// Sai na ordem de chamada — o primeiro da lista é o primeiro a ligar.
    /// </summary>
    public async Task<IReadOnlyList<ListaEspera>> CandidatosParaAsync(
        DateTime dataHora, int? profissionalId = null, CancellationToken ct = default)
    {
        var aguardando = await _repo.ListaEsperaAsync(somenteAguardando: true, ct);

        return aguardando
            .Where(l => l.ServeParaHorario(dataHora))
            .Where(l => l.ProfissionalId is null
                        || profissionalId is null
                        || l.ProfissionalId == profissionalId)
            .ToList();
    }

    /// <summary>
    /// Chama o paciente da lista para um horário: cria o agendamento e fecha o pedido.
    /// O agendamento nasce com origem <see cref="OrigemAgendamento.ListaEspera"/>, para
    /// a agenda mostrar de onde veio.
    /// </summary>
    public async Task<Agendamento> ChamarAsync(
        int pedidoId, DateTime dataHora, ModalidadeAtendimento modalidade,
        int? profissionalId = null, int? salaId = null, int? duracaoMinutos = null,
        bool encaixe = false, string? modalidadeCodigo = null, CancellationToken ct = default,
        string? operador = null)
    {
        var pedido = await _repo.ObterListaEsperaAsync(pedidoId, ct)
            ?? throw new InvalidOperationException("Pedido da lista de espera não encontrado.");

        // Duas secretárias chamando o mesmo paciente ao mesmo tempo: a segunda para aqui.
        if (pedido.Status != StatusListaEspera.Aguardando)
            throw new InvalidOperationException(
                pedido.Status == StatusListaEspera.Agendado
                    ? "Este paciente já foi chamado da lista de espera."
                    : "Este pedido já saiu da lista de espera.");

        var agendamento = await _agenda.AgendarAsync(
            pedido.PacienteId, dataHora, modalidade,
            observacoes: pedido.Observacoes,
            origem: OrigemAgendamento.ListaEspera, ct: ct,
            modalidadeCodigo: modalidadeCodigo ?? pedido.ModalidadeCodigo,
            profissionalId: profissionalId ?? pedido.ProfissionalId,
            salaId: salaId,
            duracaoMinutos: duracaoMinutos,
            encaixe: encaixe,
            operador: operador);

        pedido.Status = StatusListaEspera.Agendado;
        pedido.ResolvidoEm = DateTime.Now;
        pedido.AgendamentoId = agendamento.Id;

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "ListaEsperaChamada",
            Detalhe = $"Chamado para {dataHora:dd/MM/yyyy HH:mm}",
            PacienteId = pedido.PacienteId
        }, ct);

        await _repo.SalvarAsync(ct);
        return agendamento;
    }

    /// <summary>Tira o pedido da lista sem agendar (desistiu, resolveu em outro lugar…).</summary>
    public async Task RemoverAsync(int pedidoId, string? motivo = null, CancellationToken ct = default)
    {
        var pedido = await _repo.ObterListaEsperaAsync(pedidoId, ct)
            ?? throw new InvalidOperationException("Pedido da lista de espera não encontrado.");

        if (pedido.Status != StatusListaEspera.Aguardando)
            throw new InvalidOperationException("Este pedido já saiu da lista de espera.");

        pedido.Status = StatusListaEspera.Removido;
        pedido.ResolvidoEm = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(motivo))
            pedido.Observacoes = string.IsNullOrWhiteSpace(pedido.Observacoes)
                ? motivo
                : $"{pedido.Observacoes} — saiu da lista: {motivo}";

        await _repo.SalvarAsync(ct);
    }

    /// <summary>Devolve um pedido para a fila (chamada desfeita, paciente voltou atrás).</summary>
    public async Task ReabrirAsync(int pedidoId, CancellationToken ct = default)
    {
        var pedido = await _repo.ObterListaEsperaAsync(pedidoId, ct)
            ?? throw new InvalidOperationException("Pedido da lista de espera não encontrado.");

        if (pedido.Status == StatusListaEspera.Aguardando) return;

        pedido.Status = StatusListaEspera.Aguardando;
        pedido.ResolvidoEm = null;
        pedido.AgendamentoId = null;
        await _repo.SalvarAsync(ct);
    }
}
