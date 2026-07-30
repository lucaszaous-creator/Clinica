using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// Um bloqueio com o que já está marcado dentro dele — o que a tela precisa mostrar
/// junto, porque bloquear não desmarca ninguém.
/// </summary>
public sealed record BloqueioComAgenda(
    BloqueioAgenda Bloqueio,
    IReadOnlyList<Agendamento> JaMarcados)
{
    /// <summary>Há sessão marcada dentro do período fechado — alguém precisa remarcar.</summary>
    public bool TemConflito => JaMarcados.Count > 0;
}

/// <summary>
/// Agenda fechada: férias, feriado, congresso, folga, sala em manutenção.
///
/// Até aqui a agenda só sabia recusar choque ENTRE agendamentos. O que segurava a
/// marcação em cima da folga do profissional era a memória de quem está no balcão — e
/// memória de balcão falha justamente no dia cheio, que é quando o erro custa caro.
///
/// Três decisões:
///
/// 1. **Bloquear não desmarca ninguém.** Fechar o Natal depois de alguém já ter marcado
///    no dia 25 não pode fazer a sessão sumir do sistema: o paciente combinou aquele
///    horário com uma pessoa, e quem desmarca avisa. O serviço DEVOLVE quem já está
///    marcado dentro do período, para a recepção remarcar com o telefone na mão.
/// 2. **O bloqueio impede como um recurso disputado impede** — e o ENCAIXE continua
///    furando, igual ao resto da agenda. Quem assume atender no feriado assume por
///    escrito, e fica registrado como encaixe.
/// 3. **Motivo é obrigatório.** Bloqueio sem motivo vira mistério na agenda de dezembro:
///    ninguém lembra por que aquela terça está fechada, e o horário se perde por medo de
///    mexer.
/// </summary>
public sealed class BloqueioAgendaService
{
    private readonly IClinicaRepositorio _repo;

    public BloqueioAgendaService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Bloqueios daqui para a frente (o padrão) ou todos, quando <paramref name="incluirPassado"/>.
    /// </summary>
    public Task<IReadOnlyList<BloqueioAgenda>> ListarAsync(
        bool incluirPassado = false, CancellationToken ct = default)
        => _repo.BloqueiosAsync(incluirPassado ? null : DateTime.Now.Date, ct);

    public Task<BloqueioAgenda?> ObterAsync(int bloqueioId, CancellationToken ct = default)
        => _repo.ObterBloqueioAsync(bloqueioId, ct);

    /// <summary>
    /// Cria o bloqueio e devolve, junto, quem já está marcado dentro dele.
    ///
    /// A lista vem no RESULTADO, e não como recusa: a clínica pode ter decidido tirar
    /// férias depois de a agenda estar montada, e travar o cadastro obrigaria a desmarcar
    /// um a um antes de registrar a decisão que já foi tomada.
    /// </summary>
    public async Task<BloqueioComAgenda> CriarAsync(
        DateTime inicio, DateTime fim, string motivo,
        int? profissionalId = null, int? salaId = null,
        string? operador = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException(
                "Diga por que a agenda está fechada (férias, feriado, congresso…). "
                + "Bloqueio sem motivo vira mistério na agenda do mês que vem.");

        if (fim <= inicio)
            throw new InvalidOperationException("O fim do bloqueio precisa ser depois do início.");

        var bloqueio = new BloqueioAgenda
        {
            ProfissionalId = profissionalId,
            SalaId = salaId,
            Inicio = inicio,
            Fim = fim,
            Motivo = motivo.Trim(),
            CriadoEm = DateTime.Now,
            CriadoPor = operador
        };

        await _repo.AdicionarBloqueioAsync(bloqueio, ct);

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AgendaBloqueada",
            Detalhe = $"{inicio:dd/MM/yyyy HH:mm} a {fim:dd/MM/yyyy HH:mm} — {motivo.Trim()}"
        }, ct);

        await _repo.SalvarAsync(ct);

        return new BloqueioComAgenda(bloqueio, await MarcadosDentroAsync(bloqueio, ct));
    }

    /// <summary>Reabre a agenda: apaga o bloqueio. O que estava marcado continua marcado.</summary>
    public async Task ExcluirAsync(int bloqueioId, string? operador = null, CancellationToken ct = default)
    {
        var bloqueio = await _repo.ObterBloqueioAsync(bloqueioId, ct)
            ?? throw new InvalidOperationException("Bloqueio não encontrado.");

        await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
        {
            Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
            Acao = "AgendaReaberta",
            Detalhe = $"{bloqueio.Inicio:dd/MM/yyyy HH:mm} a {bloqueio.Fim:dd/MM/yyyy HH:mm} — {bloqueio.Motivo}"
        }, ct);

        await _repo.RemoverBloqueioAsync(bloqueioId, ct);
        await _repo.SalvarAsync(ct);
    }

    /// <summary>
    /// Quem já está marcado dentro do período fechado. É a pergunta que a recepção faz
    /// no segundo seguinte ao cadastro: "e quem já estava marcado?".
    /// </summary>
    public async Task<IReadOnlyList<Agendamento>> MarcadosDentroAsync(
        BloqueioAgenda bloqueio, CancellationToken ct = default)
    {
        var doPeriodo = await _repo.AgendamentosNoPeriodoAsync(bloqueio.Inicio, bloqueio.Fim, ct);

        return doPeriodo
            .Where(a => a.OcupaAgenda)
            .Where(a => a.ColideCom(bloqueio.Inicio, bloqueio.Fim))
            .Where(a => bloqueio.AlcancaRecurso(a.ProfissionalId, a.SalaId))
            .OrderBy(a => a.DataHora)
            .ToList();
    }

    /// <summary>
    /// A agenda está fechada neste horário para este recurso? Devolve o bloqueio que
    /// alcança, ou null. A marcação já é recusada pela própria agenda — isto serve para
    /// a tela AVISAR antes do clique.
    /// </summary>
    public async Task<BloqueioAgenda?> BloqueioDoHorarioAsync(
        DateTime inicio, DateTime fim, int? profissionalId = null, int? salaId = null,
        CancellationToken ct = default)
        => (await _repo.BloqueiosNoPeriodoAsync(inicio, fim, ct))
            .FirstOrDefault(b => b.AlcancaRecurso(profissionalId, salaId));
}
