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

/// <summary>O que a remarcação em lote conseguiu fazer, e o que ela deixou para a recepção.</summary>
public sealed record ResultadoRemarcacaoEmLote(
    IReadOnlyList<Agendamento> Remarcados,
    IReadOnlyList<(Agendamento Sessao, string Motivo)> Recusados)
{
    public bool TudoRemarcado => Recusados.Count == 0;

    /// <summary>Nada a fazer: ninguém estava marcado dentro do período.</summary>
    public bool Vazio => Remarcados.Count == 0 && Recusados.Count == 0;
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
    private readonly AgendaService _agenda;

    /// <summary>
    /// Não há ciclo com o <see cref="AgendaService"/>: ele consulta os bloqueios pelo
    /// REPOSITÓRIO, não por este serviço. A dependência é de mão única, e a remarcação em
    /// lote passa por ele de propósito — quem valida choque de horário é um só.
    /// </summary>
    public BloqueioAgendaService(IClinicaRepositorio repo, AgendaService agenda)
    {
        _repo = repo;
        _agenda = agenda;
    }

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

    /// <summary>
    /// Empurra em bloco as sessões que caíram dentro do período fechado (parcela 28).
    ///
    /// Bloquear NÃO desmarca ninguém — essa regra continua de pé e é a razão de este
    /// método existir: até aqui o serviço DEVOLVIA a lista de quem estava marcado e a
    /// recepção remarcava um por um, à mão, num dia de férias com trinta sessões. Quem
    /// tem trinta para remarcar não remarca; empurra o problema e descobre no dia.
    ///
    /// Três regras:
    ///
    /// 1. **O mesmo horário, N dias depois.** Deslocar mantém a hora do paciente, que é
    ///    o que ele organizou a vida em torno. Reorganizar horários é decisão de quem
    ///    fala com ele, não do sistema.
    /// 2. **Sessão já realizada não se toca.** É fato: o atendimento aconteceu, mesmo que
    ///    o bloqueio tenha sido cadastrado depois.
    /// 3. **Data nova com choque é PULADA e dita**, como no agendamento em série — a
    ///    recepção resolve as poucas que sobraram, em vez de conferir as trinta.
    /// </summary>
    public async Task<ResultadoRemarcacaoEmLote> RemarcarEmLoteAsync(
        int bloqueioId, int deslocamentoDias, string? operador = null,
        CancellationToken ct = default)
    {
        if (deslocamentoDias == 0)
            throw new InvalidOperationException(
                "Informe em quantos dias as sessões devem ser empurradas.");

        var bloqueio = await _repo.ObterBloqueioAsync(bloqueioId, ct)
            ?? throw new InvalidOperationException("Bloqueio não encontrado.");

        var marcados = await MarcadosDentroAsync(bloqueio, ct);

        var remarcados = new List<Agendamento>();
        var recusados = new List<(Agendamento Sessao, string Motivo)>();

        foreach (var ag in marcados)
        {
            if (ag.Status == StatusAgendamento.Realizado) continue;

            try
            {
                var novo = await _agenda.RemarcarAsync(
                    ag.Id, ag.DataHora.AddDays(deslocamentoDias), ag.Observacoes,
                    operador: operador, ct: ct);
                remarcados.Add(novo);
            }
            catch (InvalidOperationException ex)
            {
                // Pula e DIZ qual: abortar o lote inteiro por causa de uma data devolveria
                // a recepção ao trabalho manual que este método existe para evitar.
                recusados.Add((ag, ex.Message));
            }
        }

        if (remarcados.Count > 0)
        {
            await _repo.RegistrarAuditoriaAsync(new EventoAuditoria
            {
                Operador = string.IsNullOrWhiteSpace(operador) ? "?" : operador,
                Acao = "AgendaRemarcadaEmLote",
                Detalhe = $"{remarcados.Count} sessão(ões) empurradas em {deslocamentoDias} dia(s) "
                          + $"por causa de: {bloqueio.Motivo}"
            }, ct);
            await _repo.SalvarAsync(ct);
        }

        return new ResultadoRemarcacaoEmLote(remarcados, recusados);
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
