using Clinica.Application.Abstracoes;
using Clinica.Application.Modelos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Servicos;

/// <summary>
/// O dia de quem ATENDE (parcela 36) — a leitura que faltava para o sistema chegar à
/// máquina do médico e do fisioterapeuta.
///
/// Por que não é a fila da recepção
/// --------------------------------
/// A agenda e a fila do balcão já existem desde a parcela 1, e respondem "quem chegou" e
/// "quem falta chamar". O consultório pergunta outra coisa: **o que eu ainda não
/// escrevi.** É a mesma família do defeito que dá nome ao produto — a guia obtida +24h
/// depois que ninguém lembra —, só que do lado clínico: a sessão acontece, o paciente vai
/// embora e a evolução fica "para depois". Depois é o dia em que já não se lembra o que
/// foi feito, e um prontuário escrito de memória vale menos do que um prontuário vazio,
/// porque parece registro.
///
/// Por que mora fora do <c>AgendaService</c>
/// -----------------------------------------
/// Pela mesma razão de o <c>FechamentoSessaoService</c> morar fora do
/// <c>AtendimentoService</c>: aquele é compartilhado com o faturamento congelado, e dar
/// efeito ou dependência nova a ele muda o comportamento de um app em produção. Este
/// serviço só LÊ, e lê pelo repositório.
///
/// Sem profissional vinculado
/// --------------------------
/// Um usuário do consultório pode não ter <c>Profissional</c> ligado ao login (a clínica
/// ainda não cadastrou a equipe, ou é um residente usando o acesso da casa). Nesse caso o
/// serviço devolve o dia INTEIRO da clínica, com o nome "Todos os profissionais" — a
/// alternativa seria uma tela vazia, que se lê como defeito, e não como "falta cadastro".
/// </summary>
public sealed class ConsultorioService
{
    private readonly IClinicaRepositorio _repo;

    /// <summary>
    /// Até quantos dias para trás procurar sessão sem evolução escrita. Não é limite
    /// técnico: passado disso o registro não se escreve mais de memória, e uma lista que
    /// cresce sem fim vira ruído que a pessoa aprende a fechar sem ler.
    /// </summary>
    public const int JanelaRegistroPendenteDias = 30;

    public ConsultorioService(IClinicaRepositorio repo) => _repo = repo;

    /// <summary>
    /// O dia do profissional: os horários dele, com a marca do que já tem evolução.
    /// </summary>
    public async Task<DiaDoProfissional> DoDiaAsync(
        DateOnly dia, int? profissionalId, CancellationToken ct = default)
    {
        var todos = await _repo.AgendamentosNoPeriodoAsync(
            dia.ToDateTime(TimeOnly.MinValue), dia.ToDateTime(TimeOnly.MaxValue), ct);

        // A disputa pela evolução avulsa é sobre TODAS as sessões do paciente no dia,
        // antes do filtro de profissional — ver <see cref="EvolucaoDoHorario"/>.
        var porPacienteDia = PorPacienteDia(todos);

        var agendamentos = profissionalId is { } id
            ? todos.Where(a => a.ProfissionalId == id).ToList()
            : todos;

        var evolucoes = await _repo.EvolucoesNoPeriodoAsync(dia, dia, ct, profissionalId);

        var sessoes = agendamentos
            .OrderBy(a => a.DataHora)
            .Select(a => Montar(a, evolucoes, porPacienteDia))
            .ToList();

        var nome = profissionalId is null
            ? "Todos os profissionais"
            : (await _repo.ObterProfissionalAsync(profissionalId.Value, ct))?.Rotulo
              ?? "Profissional";

        return new DiaDoProfissional(dia, profissionalId, nome, sessoes);
    }

    /// <summary>
    /// A SEMANA do profissional (parcela 39) — sete dias de uma vez.
    ///
    /// Uma consulta só, não sete: chamar <see cref="DoDiaAsync"/> em laço custaria catorze
    /// idas ao banco (agendamentos + evoluções por dia) para montar uma tela, e o banco é
    /// REMOTO. O agrupamento é feito aqui, em memória, sobre o período inteiro.
    ///
    /// A semana começa na SEGUNDA, como a da recepção: a clínica pensa a semana em bloco,
    /// e começar no dia escolhido daria uma janela diferente a cada clique do calendário.
    /// Dia sem horário nenhum ENTRA na lista, vazio — semana com cinco colunas em vez de
    /// sete faria o olho procurar a quarta-feira que sumiu.
    /// </summary>
    public async Task<SemanaDoProfissional> DaSemanaAsync(
        DateOnly referencia, int? profissionalId, CancellationToken ct = default)
    {
        var inicio = InicioDaSemana(referencia);
        var fim = inicio.AddDays(6);

        var todos = await _repo.AgendamentosNoPeriodoAsync(
            inicio.ToDateTime(TimeOnly.MinValue), fim.ToDateTime(TimeOnly.MaxValue), ct);

        // A disputa pela evolução avulsa é sobre TODAS as sessões do paciente no dia,
        // antes do filtro de profissional — ver <see cref="EvolucaoDoHorario"/>.
        var porPacienteDia = PorPacienteDia(todos);

        var agendamentos = profissionalId is { } id
            ? todos.Where(a => a.ProfissionalId == id).ToList()
            : todos;

        var evolucoes = await _repo.EvolucoesNoPeriodoAsync(inicio, fim, ct, profissionalId);

        var nome = profissionalId is null
            ? "Todos os profissionais"
            : (await _repo.ObterProfissionalAsync(profissionalId.Value, ct))?.Rotulo
              ?? "Profissional";

        var porDia = agendamentos
            .GroupBy(a => DateOnly.FromDateTime(a.DataHora))
            .ToDictionary(g => g.Key, g => g.ToList());

        var dias = Enumerable.Range(0, 7)
            .Select(i => inicio.AddDays(i))
            .Select(dia => new DiaDoProfissional(
                dia, profissionalId, nome,
                porDia.TryGetValue(dia, out var doDia)
                    ? doDia.OrderBy(a => a.DataHora)
                        .Select(a => Montar(a, evolucoes, porPacienteDia)).ToList()
                    : []))
            .ToList();

        return new SemanaDoProfissional(inicio, profissionalId, nome, dias);
    }

    /// <summary>
    /// A segunda-feira da semana da data. <c>DayOfWeek</c> começa no DOMINGO (0), então o
    /// deslocamento tem de tratar o domingo como o SÉTIMO dia — sem isso, olhar um domingo
    /// devolveria a semana que está começando no dia seguinte.
    /// </summary>
    private static DateOnly InicioDaSemana(DateOnly data)
    {
        var desde = ((int)data.DayOfWeek + 6) % 7;
        return data.AddDays(-desde);
    }

    /// <summary>
    /// Sessões de dias ANTERIORES que aconteceram e continuam sem evolução escrita.
    ///
    /// O dia de hoje fica fora de propósito: a sessão das 14h ainda não tem evolução às
    /// 14h05 porque o paciente ainda está na sala, e cobrá-la ali transformaria a lista
    /// numa contagem do trabalho em curso. Quem cuida de hoje é o
    /// <see cref="DiaDoProfissional.RegistrosPendentes"/>.
    /// </summary>
    public async Task<IReadOnlyList<RegistroPendente>> RegistrosPendentesAsync(
        DateOnly hoje, int? profissionalId, CancellationToken ct = default)
    {
        var inicio = hoje.AddDays(-JanelaRegistroPendenteDias);
        var ontem = hoje.AddDays(-1);
        if (ontem < inicio) return [];

        var todos = await _repo.AgendamentosNoPeriodoAsync(
            inicio.ToDateTime(TimeOnly.MinValue), ontem.ToDateTime(TimeOnly.MaxValue), ct);

        // A disputa pela evolução avulsa é sobre TODAS as sessões do paciente no dia,
        // antes do filtro de profissional — ver <see cref="EvolucaoDoHorario"/>.
        var porPacienteDia = PorPacienteDia(todos);

        var agendamentos = profissionalId is { } id
            ? todos.Where(a => a.ProfissionalId == id).ToList()
            : todos;

        var evolucoes = await _repo.EvolucoesNoPeriodoAsync(inicio, ontem, ct, profissionalId);

        return agendamentos
            .Where(a => a.Status == StatusAgendamento.Realizado)
            .Select(a => (Agendamento: a, Sessao: Montar(a, evolucoes, porPacienteDia)))
            .Where(x => x.Sessao.RegistroPendente)
            .OrderBy(x => x.Agendamento.DataHora)
            .Select(x => new RegistroPendente(
                x.Agendamento.Id,
                x.Agendamento.DataHora,
                x.Agendamento.PacienteId,
                x.Agendamento.Paciente?.Nome ?? "Paciente",
                CatalogoModalidades.Nome(x.Agendamento.ModalidadeCodigo),
                x.Agendamento.AtendimentoId)
            {
                Profissional = x.Agendamento.Profissional?.Nome
            })
            .ToList();
    }

    /// <summary>
    /// Os pacientes deste profissional, com a leitura da dor de cada um já resolvida.
    ///
    /// A dor vem do prontuário, uma consulta por paciente — e é por isso que a lista tem
    /// teto. Com <paramref name="comDor"/> desligado a tela abre instantânea e mostra só
    /// quem é e quando veio; ligado, ela responde "como este tratamento está indo?" sem
    /// abrir o prontuário de ninguém, que é a pergunta do profissional antes de chamar o
    /// próximo.
    /// </summary>
    public async Task<IReadOnlyList<PacienteDoProfissional>> MeusPacientesAsync(
        int? profissionalId, int limite = 200, bool comDor = true, CancellationToken ct = default)
    {
        // Sem profissional vinculado, a carteira é a da clínica: o consultório continua
        // servindo (residente, profissional recém-cadastrado), só não filtra por dono.
        var pacientes = profissionalId is { } id
            ? await _repo.PacientesDoProfissionalAsync(id, limite, ct)
            : (await _repo.BuscarPacientesAsync(null, limite, ct))
                .Select(p => new PacienteDoProfissional(p.Id, p.Nome, null, 0))
                .ToList();

        if (!comDor) return pacientes;

        // ⚠️ UMA consulta, não uma por paciente.
        //
        // Isto era um laço com `EvolucoesDoPacienteAsync` dentro: até duzentas idas em
        // fila indiana a um banco REMOTO para desenhar uma tela — e cada uma arrastava o
        // prontuário INTEIRO daquela pessoa (texto da evolução, conduta, orientações) para
        // calcular dois inteiros. "Meus pacientes" é uma das duas portas do Consultório, e
        // ela ficava dezenas de segundos em "Montando a sua carteira…", repetindo a espera
        // a cada volta para a tela.
        //
        // É o mesmo desenho de `DaSemanaAsync` logo acima, e pelo mesmo motivo escrito lá.
        var pares = await _repo.ParesDeEvaDosPacientesAsync(
            [.. pacientes.Select(p => p.PacienteId)], ct);

        return pacientes
            .Select(p => pares.TryGetValue(p.PacienteId, out var par)
                ? p with { DorInicial = par.Inicial, UltimaDor = par.Ultima }
                : p)
            .ToList();
    }

    /// <summary>
    /// A evolução JÁ ESCRITA de um horário, ou nulo.
    ///
    /// O casamento é pelo <c>AgendamentoId</c> da evolução — que é o vínculo real — e cai
    /// para paciente + data quando ele é nulo. O caminho de baixo existe porque a evolução
    /// escrita direto no prontuário (fora da fila) não conhece o agendamento, e sem ele o
    /// consultório cobraria para sempre um registro que já foi escrito.
    ///
    /// ⚠️ A avulsa casa com <b>NO MÁXIMO UMA</b> sessão do dia. Com duas sessões do mesmo
    /// paciente no mesmo dia (manhã e tarde), uma única evolução sem vínculo dava as DUAS
    /// por escritas — a segunda sumia da cobrança, e abrir qualquer uma delas na tela de
    /// Atendimento CONTINUAVA o mesmo texto, fundindo duas sessões num registro só. A
    /// distribuição é cronológica: avulsas na ordem em que foram escritas (Id), sessões na
    /// ordem em que aconteceram — a primeira sem evolução própria fica com a primeira
    /// avulsa. É uma escolha determinística sobre um dado que não diz de quem é; a que
    /// erra, erra para o lado de COBRAR, nunca de calar. Cancelado e falta não disputam:
    /// sessão que não aconteceu não tem o que escrever.
    ///
    /// Daí o parâmetro <paramref name="sessoesDoPacienteNoDia"/>: os agendamentos do
    /// paciente NAQUELE dia (de todos os profissionais — a segunda sessão do dia costuma
    /// ser de outra especialidade). Sem conhecer as irmãs não há como saber a vez de cada
    /// uma na fila da avulsa.
    ///
    /// É <b>público e estático de propósito</b>: quem pergunta "esta sessão já foi
    /// escrita?" são dois — o cartão do Meu dia e a tela de Atendimento, que decide entre
    /// CONTINUAR o registro e começar um novo. Duas definições divergem na primeira
    /// correção, e aqui a que ficasse para trás produziria uma SEGUNDA evolução do mesmo
    /// atendimento, sem erro nenhum na tela.
    /// </summary>
    public static Evolucao? EvolucaoDoHorario(
        IReadOnlyList<Evolucao> evolucoes, int agendamentoId, int pacienteId, DateOnly data,
        IReadOnlyList<Agendamento> sessoesDoPacienteNoDia)
    {
        var porVinculo = evolucoes.FirstOrDefault(e => e.AgendamentoId == agendamentoId);
        if (porVinculo is not null) return porVinculo;

        var avulsas = evolucoes
            .Where(e => e.AgendamentoId is null && e.PacienteId == pacienteId && e.Data == data)
            .OrderBy(e => e.Id)
            .ToList();
        if (avulsas.Count == 0) return null;

        // Quem disputa a avulsa: sessão do MESMO paciente e dia, que aconteceu (ou ainda
        // vai acontecer) e não tem evolução vinculada própria. Vinculada de outro
        // profissional pode estar fora da lista filtrada — nesse caso a sessão dele entra
        // na disputa sem precisar, e o erro cai para o lado de cobrar.
        var concorrentes = sessoesDoPacienteNoDia
            .Where(s => s.PacienteId == pacienteId
                        && DateOnly.FromDateTime(s.DataHora) == data
                        && s.Status is not (StatusAgendamento.Cancelado or StatusAgendamento.Faltou)
                        && evolucoes.All(e => e.AgendamentoId != s.Id))
            .OrderBy(s => s.DataHora).ThenBy(s => s.Id)
            .ToList();

        var vez = concorrentes.FindIndex(s => s.Id == agendamentoId);
        if (vez < 0) return null;
        return vez < avulsas.Count ? avulsas[vez] : null;
    }

    /// <summary>
    /// Os agendamentos do paciente num dia — o universo que disputa a evolução avulsa
    /// (<see cref="EvolucaoDoHorario"/>). É a leitura da tela de Atendimento, que conhece
    /// o horário chamado e não as irmãs dele; sem elas, a avulsa de uma sessão continuaria
    /// abrindo dentro da outra.
    /// </summary>
    public async Task<IReadOnlyList<Agendamento>> SessoesDoPacienteNoDiaAsync(
        int pacienteId, DateOnly dia, CancellationToken ct = default)
        => (await _repo.AgendamentosNoPeriodoAsync(
                dia.ToDateTime(TimeOnly.MinValue), dia.ToDateTime(TimeOnly.MaxValue), ct))
            .Where(a => a.PacienteId == pacienteId)
            .OrderBy(a => a.DataHora)
            .ToList();

    /// <summary>
    /// O CRACHÁ CLÍNICO da pessoa que está na sala (parcela 74).
    ///
    /// Ele responde, de relance, as quatro perguntas que se fazem antes de abrir a boca:
    /// que idade tem, de que convênio é, desde quando se trata aqui e <b>o que não se pode
    /// esquecer</b>. Todos os dados já existiam; nenhum tinha leitor neste lugar.
    ///
    /// ⚠️ As leituras são SEQUENCIAIS, e a primeira versão desta parcela as fazia em
    /// paralelo com <c>Task.WhenAll</c> — o que é um DEFEITO, não uma otimização: as quatro
    /// passam pelo MESMO <c>IClinicaRepositorio</c>, logo pelo mesmo <c>DbContext</c>, e ele
    /// não aceita duas operações ao mesmo tempo. O crachá estouraria com <i>"a second
    /// operation was started on this context instance"</i> em toda troca de paciente.
    ///
    /// ⚠️ E os testes NÃO pegavam: o SQLite em memória completa a consulta quase
    /// sincronamente, então as quatro nunca chegavam a se sobrepor. É a mesma família do
    /// <c>xmin</c> e das datas com fuso — o que só aparece com latência de rede real.
    /// <c>CabecalhoClinicoTests</c> tem o teste com o interceptor de lentidão justamente
    /// para fechar esse buraco.
    ///
    /// ⚠️ E as ALERGIAS entram mesmo dadas por RESOLVIDAS — a regra da parcela 37, que
    /// aqui vale mais ainda: "resolvida" numa alergia é quase sempre "não reagiu da última
    /// vez", e o dia em que reagir é o dia em que o crachá teria valido. Só o DESCARTE a
    /// cala, porque descartar exige motivo escrito e é a afirmação de que o registro
    /// estava errado.
    /// </summary>
    /// <summary>
    /// Os sinais vitais que a enfermagem aferiu NO DIA desta sessão. Nulo quando não houve
    /// aferição naquele dia.
    ///
    /// ⚠️ É o dia da SESSÃO, nunca hoje: a dívida de prontuário e a semana abrem horários
    /// de dias passados, e mostrar a aferição de hoje ao lado da sessão de terça diria que
    /// aquela PA foi medida na consulta que está sendo escrita.
    ///
    /// ⚠️ Só o dia da sessão, e é decisão: aferição antiga tem casa — a curva de PA da tela
    /// de Medidas, que já junta as da enfermagem com a procedência escrita em cada ponto.
    /// Trazê-la para o cabeçalho da consulta seria pôr um número de três semanas atrás onde
    /// se lê "os sinais deste paciente agora".
    ///
    /// A mais TARDIA do dia é a que vale: quando a técnica afere na chegada e de novo depois
    /// da medicação, quem prescreve precisa do estado mais recente.
    /// </summary>
    public async Task<SinaisVitaisDaSessao?> SinaisVitaisDaSessaoAsync(
        int pacienteId, DateOnly data, CancellationToken ct = default)
    {
        var evolucoes = await _repo.EvolucoesEnfermagemDoPacienteAsync(pacienteId, 60, ct);

        var doDia = EvolucaoEnfermagem.Vigentes(evolucoes)
            .Where(e => e.Data == data && e.TemSinaisVitais)
            .OrderByDescending(e => e.Hora)
            .FirstOrDefault();

        return doDia?.SinaisVitaisResumidos is not { } resumo
            ? null
            : new SinaisVitaisDaSessao(
                resumo, doDia.Hora, doDia.AutorNome, doDia.AutorConselho);
    }

    public async Task<CabecalhoClinicoPaciente?> CabecalhoAsync(
        int pacienteId, CancellationToken ct = default)
    {
        var p = await _repo.ObterPacienteAsync(pacienteId, ct);
        if (p is null) return null;

        var (primeira, total) = await _repo.HistoricoDeSessoesAsync(pacienteId, ct);
        var problemas = await _repo.ProblemasDoPacienteAsync(pacienteId, ct: ct);
        var hipoteses = await _repo.HipotesesRecentesAsync(pacienteId, 3, ct);

        var alergias = problemas
            .Where(x => x.Natureza == NaturezaProblema.Alergia
                        && x.Situacao != SituacaoProblema.Descartado)
            .Select(x => x.Descricao)
            .ToList();

        var ativos = problemas
            .Where(x => x.Natureza != NaturezaProblema.Alergia
                        && x.Situacao == SituacaoProblema.Ativo)
            .Select(x => string.IsNullOrWhiteSpace(x.Cid)
                ? x.Descricao
                : $"{x.Descricao} ({x.Cid})")
            .ToList();

        var hoje = DateOnly.FromDateTime(DateTime.Today);

        return new CabecalhoClinicoPaciente(
            p.Id,
            p.Nome,
            p.FotoMiniatura,
            IdadeEm(p.DataNascimento, hoje),
            p.Sexo,
            CatalogoConvenios.Nome(p.ConvenioCodigo, p.Convenio),
            p.Carteirinha,
            p.ValidadeCarteirinha is { } v && v < hoje,
            primeira,
            total,
            alergias,
            ativos,
            hipoteses);
    }

    /// <summary>
    /// Anos COMPLETOS. A conta pelo ano subtraído erra metade do ano de todo mundo, e num
    /// crachá clínico a idade errada muda conduta — a dose pediátrica e a geriátrica não
    /// são a mesma.
    /// </summary>
    private static int? IdadeEm(DateOnly? nascimento, DateOnly hoje)
    {
        if (nascimento is not { } n || n > hoje) return null;
        var anos = hoje.Year - n.Year;
        if (hoje < n.AddYears(anos)) anos--;
        return anos;
    }

    /// <summary>
    /// As sessões de cada paciente por dia — o universo que disputa as evoluções avulsas.
    /// Montado do período INTEIRO, antes do filtro de profissional: a segunda sessão do
    /// dia costuma ser de outro profissional, e ela precisa contar na fila da avulsa.
    /// </summary>
    private static Dictionary<(int PacienteId, DateOnly Dia), List<Agendamento>> PorPacienteDia(
        IReadOnlyList<Agendamento> agendamentos)
        => agendamentos
            .GroupBy(a => (a.PacienteId, DateOnly.FromDateTime(a.DataHora)))
            .ToDictionary(g => g.Key, g => g.ToList());

    /// <summary>A sessão do dia com a evolução casada.</summary>
    private static SessaoDoDia Montar(
        Agendamento a, IReadOnlyList<Evolucao> evolucoes,
        Dictionary<(int PacienteId, DateOnly Dia), List<Agendamento>> porPacienteDia)
    {
        var dia = DateOnly.FromDateTime(a.DataHora);
        var irmas = porPacienteDia.TryGetValue((a.PacienteId, dia), out var lista)
            ? (IReadOnlyList<Agendamento>)lista
            : [a];

        var evolucao = EvolucaoDoHorario(evolucoes, a.Id, a.PacienteId, dia, irmas);

        return new SessaoDoDia(
            a.Id,
            a.DataHora,
            a.PacienteId,
            a.Paciente?.Nome ?? "Paciente",
            CatalogoModalidades.Nome(a.ModalidadeCodigo),
            a.Sala?.Nome,
            a.Status,
            a.Etapa,
            a.Encaixe,
            a.AtendimentoId,
            evolucao?.Id)
        {
            EsperaMinutos = a.EsperaMinutos(DateTime.Now),
            ChamadoHaMinutos = a.ChamadoHaMinutos(DateTime.Now),
            Observacoes = string.IsNullOrWhiteSpace(a.Observacoes) ? null : a.Observacoes.Trim(),
            DuracaoMinutos = a.DuracaoEfetiva
        };
    }
}
