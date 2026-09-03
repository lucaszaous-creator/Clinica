using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>Uma sessão que existe no dia do horário parado — o que faz a pergunta ter três respostas.</summary>
public sealed record SessaoLancadaNoDia(int AtendimentoId, string Numero, string Modalidade, int GuiasFaturaveis)
{
    public string Resumo => GuiasFaturaveis == 0
        ? $"nº {Numero} · {Modalidade} · sem guia"
        : $"nº {Numero} · {Modalidade} · {(GuiasFaturaveis == 1 ? "1 guia" : $"{GuiasFaturaveis} guias")}";
}

/// <summary>
/// Um horário que a data já passou e ninguém resolveu: continua <see cref="StatusAgendamento.Agendado"/>.
/// </summary>
public sealed record HorarioParado(
    int AgendamentoId, int PacienteId, string Paciente, DateTime DataHora,
    bool Importado, string? Profissional, int DiasParado,
    IReadOnlyList<SessaoLancadaNoDia> SessoesDoDia)
{
    /// <summary>
    /// O paciente TEM sessão lançada naquele dia — quase sempre um encaixe criado por
    /// fora da agenda. É o que separa "faltou" de "veio, e o horário é que ficou para
    /// trás", e é o motivo de esta conciliação existir.
    /// </summary>
    public bool TemSessaoNoDia => SessoesDoDia.Count > 0;

    /// <summary>A frase que a tela mostra — montada aqui para não divergir entre leitores.</summary>
    public string Situacao => TemSessaoNoDia
        ? $"Há {SessoesDoDia.Count} sessão(ões) lançada(s) neste dia: {string.Join("; ", SessoesDoDia.Select(s => s.Resumo))}."
        : "Nenhuma sessão lançada neste dia para este paciente.";
}

/// <summary>Um horário REALIZADO que não aponta para atendimento nenhum.</summary>
public sealed record HorarioOrfao(
    int AgendamentoId, string Paciente, DateTime DataHora, string? Profissional, int DiasParado);

/// <summary>O levantamento inteiro, com os números que a tela mostra no cabeçalho.</summary>
public sealed record ConciliacaoDaAgenda(
    IReadOnlyList<HorarioParado> Parados,
    IReadOnlyList<HorarioOrfao> Orfaos,
    DateOnly Desde,
    DateOnly Ate)
{
    /// <summary>Parados que têm sessão no dia — o caso de AMARRAR, não de lançar.</summary>
    public int ComSessaoNoDia => Parados.Count(p => p.TemSessaoNoDia);

    /// <summary>Parados sem sessão nenhuma — o caso de FALTA, ou de lançar retroativo.</summary>
    public int SemSessaoNoDia => Parados.Count - ComSessaoNoDia;

    public bool Vazio => Parados.Count == 0 && Orfaos.Count == 0;
}

/// <summary>
/// A CONCILIAÇÃO DA AGENDA (parcela 93): o que ficou pendurado entre o horário e a sessão.
///
/// Por que ela existe
/// ------------------
/// A clínica ainda não trabalha o check-in pela agenda: a recepcionista vai direto ao Novo
/// atendimento e lança. Desde a parcela 91 o lançamento RECONHECE o horário do dia e nasce
/// pendurado nele (<c>AgendaService.LancarNoHorarioAsync</c>) — mas isso vale dali para a
/// frente, e só quando a data bate. Antes disso, e sempre que o paciente aparece num dia
/// que não é o marcado, o horário fica em aberto para sempre.
///
/// Horário parado não é ruído de tela. Ele infla a ocupação da agenda, põe no "Meu dia" do
/// médico um paciente que não vem, e — o pior — a evolução importada é distribuída pela
/// ORDEM DA HORA MARCADA: o horário fantasma, mais cedo, fica com a evolução da sessão de
/// verdade, que continua aparecendo em "Sessões sem evolução".
///
/// O que ela NÃO faz, e é decisão
/// ------------------------------
/// Não decide nada sozinha. É LEITURA: monta a pergunta e mostra os fatos que permitem
/// respondê-la — inclusive o fato que mais importa, <see cref="HorarioParado.TemSessaoNoDia"/>.
/// Um serviço que fechasse horário sozinho estaria adivinhando falta de paciente a partir
/// da ausência de um clique que a clínica sabidamente não dá.
///
/// ⚠️ A pergunta tem TRÊS respostas, não duas, e é por isso que a linha carrega as sessões
/// do dia:
/// <list type="number">
/// <item><b>Faltou</b> — não há sessão no dia. <c>AgendaService.MarcarFaltaAsync</c>.</item>
/// <item><b>Aconteceu e NÃO foi lançada</b> — também sem sessão no dia. Lançar retroativo
/// pelo horário (<c>LancarNoHorarioAsync</c>), que já data o atendimento pela data DELE.</item>
/// <item><b>Aconteceu e JÁ foi lançada por fora</b> — há sessão no dia. Aqui lançar de novo
/// criaria um SEGUNDO jogo de guias para a mesma sessão: exatamente a duplicata que esta
/// tela existe para acabar. O horário precisa ser ENCERRADO apontando para a sessão que já
/// existe — e isso pede um <see cref="StatusAgendamento"/> novo, porque nenhum dos quatro
/// serve: <c>Cancelado</c> contaria como cancelamento no <c>IndicadoresService</c> e no
/// <c>RelacionamentoService</c> (uma sessão que ACONTECEU inflando o indicador de
/// cancelamento), e <c>Faltou</c> culparia o paciente por uma falta que não houve.
/// Fica para a parcela seguinte; aqui a linha é MOSTRADA e a ação de lançar é recusada,
/// que é o que impede o estrago enquanto o status não existe.</item>
/// </list>
/// </summary>
public sealed class ConciliacaoAgendaService
{
    private readonly IClinicaRepositorio _repo;

    public ConciliacaoAgendaService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// Dias de carência antes de perguntar. Não é zero de propósito: o horário de hoje
    /// ainda vai acontecer, e o de ontem pode estar esperando o fechamento do dia. Dois
    /// dias é o pedido da direção — e casa com o 2º código, que nasce com +24h: passados
    /// dois dias, as guias da sessão já existem e já se sabe se foram baixadas.
    /// </summary>
    public const int CarenciaPadraoDias = 2;

    /// <summary>
    /// Até quanto tempo para trás olhar. Sem teto, a agenda importada inteira apareceria
    /// de uma vez e a tela viraria um arquivo morto em vez de uma fila de trabalho.
    /// </summary>
    public const int JanelaPadraoDias = 120;

    public async Task<ConciliacaoDaAgenda> LevantarAsync(
        DateOnly hoje, int carenciaDias = CarenciaPadraoDias, int janelaDias = JanelaPadraoDias,
        CancellationToken ct = default)
    {
        if (carenciaDias < 0) throw new ArgumentOutOfRangeException(nameof(carenciaDias));
        if (janelaDias <= 0) throw new ArgumentOutOfRangeException(nameof(janelaDias));

        var desde = hoje.AddDays(-janelaDias);
        var ate = hoje.AddDays(-carenciaDias);

        var inicio = desde.ToDateTime(TimeOnly.MinValue);
        // Exclusivo: o horário do próprio dia-limite ainda está dentro da carência.
        var fim = ate.ToDateTime(TimeOnly.MinValue);

        // ⚠️ SEQUENCIAL, nunca Task.WhenAll: é o mesmo DbContext, e ele não aceita duas
        // operações ao mesmo tempo. O SQLite dos testes esconderia.
        var parados = await _repo.HorariosEmAbertoVencidosAsync(inicio, fim, ct);

        // O órfão olha até HOJE, e não até a carência: ele não é uma pergunta que amadurece
        // — é um horário que diz "Finalizado" sobre uma sessão sem guia, e isso é errado
        // desde o primeiro minuto.
        var orfaos = await _repo.HorariosRealizadosSemAtendimentoAsync(
            inicio, hoje.AddDays(1).ToDateTime(TimeOnly.MinValue), ct);

        var sessoesPorPacienteEDia = await SessoesDoPeriodoAsync(parados, ct);

        var linhas = parados.Select(a =>
        {
            var dia = DateOnly.FromDateTime(a.DataHora);
            var chave = (a.PacienteId, dia);
            var sessoes = sessoesPorPacienteEDia.TryGetValue(chave, out var s) ? s : [];

            return new HorarioParado(
                a.Id, a.PacienteId,
                a.Paciente?.Nome ?? $"Paciente {a.PacienteId}",
                a.DataHora,
                // O importado é o grosso da fila e merece o selo: ele veio da agenda de
                // outro sistema e nunca teve check-in aqui.
                Importado: a.ChaveImportacao is not null,
                a.Profissional?.Rotulo,
                DiasParado: hoje.DayNumber - dia.DayNumber,
                sessoes);
        }).ToList();

        var orfaosLinhas = orfaos.Select(a => new HorarioOrfao(
            a.Id,
            a.Paciente?.Nome ?? $"Paciente {a.PacienteId}",
            a.DataHora,
            a.Profissional?.Rotulo,
            DiasParado: hoje.DayNumber - DateOnly.FromDateTime(a.DataHora).DayNumber)).ToList();

        return new ConciliacaoDaAgenda(linhas, orfaosLinhas, desde, ate);
    }

    /// <summary>
    /// As sessões dos pacientes da fila, indexadas por (paciente, dia). Uma consulta só
    /// para a lista inteira: perguntar por horário parado seria uma ida ao banco por
    /// linha, e a fila da migração tem centenas.
    /// </summary>
    private async Task<Dictionary<(int, DateOnly), List<SessaoLancadaNoDia>>> SessoesDoPeriodoAsync(
        IReadOnlyList<Agendamento> parados, CancellationToken ct)
    {
        var vazio = new Dictionary<(int, DateOnly), List<SessaoLancadaNoDia>>();
        if (parados.Count == 0) return vazio;

        var pacienteIds = parados.Select(a => a.PacienteId).Distinct().ToList();
        var dias = parados.Select(a => DateOnly.FromDateTime(a.DataHora)).ToList();

        var atendimentos = await _repo.AtendimentosDosPacientesNoPeriodoAsync(
            pacienteIds, dias.Min(), dias.Max(), ct);

        var mapa = new Dictionary<(int, DateOnly), List<SessaoLancadaNoDia>>();
        foreach (var at in atendimentos)
        {
            var chave = (at.PacienteId, at.Data);
            if (!mapa.TryGetValue(chave, out var lista))
                mapa[chave] = lista = [];

            lista.Add(new SessaoLancadaNoDia(
                at.Id,
                at.Numero ?? $"#{at.Id}",
                at.ModalidadeCodigo is { } cod
                    ? CatalogoModalidades.Nome(cod)
                    : ModalidadeInfo.NomeExibicao(at.Modalidade),
                at.Codigos.Count(c => c.Status != StatusCodigo.NaoAplicavel)));
        }

        return mapa;
    }
}
