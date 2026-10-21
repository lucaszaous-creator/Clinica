using System.Globalization;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Application.Modelos;

/// <summary>
/// Um horário na GRADE da agenda (out/2026 — era o <c>CartaoAgenda</c> do <c>AgendaViewModel</c>
/// WPF). Tudo o que o cartão, a dica e a janela do horário mostram, já resolvido: a tela só
/// desenha.
/// </summary>
public sealed record CartaoDaGrade
{
    public required int AgendamentoId { get; init; }
    public required int PacienteId { get; init; }

    /// <summary>De quem é a coluna — é o que a lista de espera pergunta ao vagar.</summary>
    public required int? ProfissionalId { get; init; }

    /// <summary>Marcado em série (pacote de dez). Nulo = horário avulso.</summary>
    public required string? SerieId { get; init; }
    public bool EhSerie => !string.IsNullOrWhiteSpace(SerieId);

    public required string Faixa { get; init; }
    public required string Paciente { get; init; }
    public string? Telefone { get; init; }
    public required DateTime DataHora { get; init; }

    /// <summary>Fim previsto — é o que dá ao cartão a ALTURA dele na linha do tempo.</summary>
    public required DateTime Fim { get; init; }

    /// <summary>Meia hora ou menos: o cartão mostra só a primeira linha (~36 px de altura).</summary>
    public bool Compacto => (Fim - DataHora).TotalMinutes <= 30;

    public string TituloPlanejamento => (EhEncaixe ? "Encaixe · " : "") + $"{Faixa} · {Paciente}";

    /// <summary>Nome do CATÁLOGO, com a especialidade quando é consulta — nunca o enum.</summary>
    public required string Modalidade { get; init; }

    /// <summary>A FAMÍLIA da modalidade — dá a cor ao traço do cartão (a variante herda a de quem deriva).</summary>
    public required ModalidadeAtendimento ModalidadeFamilia { get; init; }

    /// <summary>A sala, ou vazio — nunca o travessão (a clínica quase não usa salas).</summary>
    public required string Sala { get; init; }

    /// <summary>Quem atende. No modo SEMANA a coluna é o dia, e sem esta linha o cartão não diz com quem é.</summary>
    public required string Profissional { get; init; }

    /// <summary>O status GRAVADO por extenso (a janela do horário o mostra quando ele saiu do dia).</summary>
    public required string StatusRotulo { get; init; }

    public required StatusAgendamento Situacao { get; init; }
    public required EtapaFila Etapa { get; init; }

    /// <summary>A palavra da situação (o vocabulário das duas listas do dia), vazia enquanto só está marcado.</summary>
    public string SituacaoDaFila => StatusDaFila.SituacaoNaGrade(Situacao, Etapa);

    /// <summary>O grupo da COR — o mesmo do filtro de situação e do traço da lista do dia.</summary>
    public string Grupo => SituacaoVisualAgenda.Grupo(Situacao, Etapa);

    public required bool EhEncaixe { get; init; }
    public required bool EhRetornoDoSegundoCodigo { get; init; }
    public required bool VeioDaListaEspera { get; init; }
    public string? Observacoes { get; init; }

    /// <summary>"Marcado por Ana em 08/08/2026 14:32" — ou a frase que assume a lacuna.</summary>
    public required string Lancamento { get; init; }

    /// <summary>Ainda dá para remarcar/cancelar (não virou atendimento).</summary>
    public required bool EmAberto { get; init; }

    /// <summary>Saiu do fluxo do dia (cancelado, falta, substituído): o vão está livre, mas a linha fica.</summary>
    public required bool ForaDoDia { get; init; }

    /// <summary>"Confirmou" · "Não confirmou" · "Não vem — remarcar" · "Pediu para cancelar" · vazio — só em AGUARDANDO.</summary>
    public string ConfirmacaoRotulo { get; init; } = string.Empty;

    public bool ConfirmouPresenca => ConfirmacaoRotulo == RodadaDeConfirmacao.SeloConfirmou;
    public bool NaoConfirmou => ConfirmacaoRotulo == RodadaDeConfirmacao.SeloSemResposta;
    public bool NaoVem => ConfirmacaoRotulo == RodadaDeConfirmacao.SeloNaoVem;
    public bool PediuCancelar => ConfirmacaoRotulo == RodadaDeConfirmacao.SeloPediuCancelar;

    public bool TemTelefone => !string.IsNullOrWhiteSpace(Telefone);
    public bool TemObservacoes => !string.IsNullOrWhiteSpace(Observacoes);

    /// <summary>
    /// A linha de baixo do cartão: situação · modalidade · profissional · sala. A SITUAÇÃO vem
    /// primeiro, e a ordem é a decisão (set/2026): a linha corta com reticências numa coluna
    /// estreita, e quem vem antes sobrevive ao corte.
    /// </summary>
    public string Contexto => string.Join(" · ",
        new[] { SituacaoDaFila, Modalidade, Profissional, Sala }.Where(p => !string.IsNullOrWhiteSpace(p)));
}

/// <summary>
/// Uma célula da linha do tempo: o cruzamento de uma faixa de horário com uma coluna. Três
/// estados distinguíveis de longe: LIVRE (o clique marca aqui), OCUPADA (tem cartão) e
/// CONTINUAÇÃO (coberta por uma sessão que começou antes — sem ela uma sessão de uma hora
/// pareceria deixar meia hora vaga).
/// </summary>
public sealed record CelulaDaGrade
{
    public required int? ProfissionalId { get; init; }

    /// <summary>Dia + hora desta célula — é o que o formulário recebe já preenchido.</summary>
    public required DateTime Quando { get; init; }

    /// <summary>A sala desta coluna (visão por SALA) ou a do filtro.</summary>
    public int? SalaId { get; init; }

    public required IReadOnlyList<CartaoDaGrade> Cartoes { get; init; }
    public required bool Continuacao { get; init; }
    public bool Livre => Cartoes.Count == 0 && !Continuacao;

    /// <summary>Já passou: marcar para trás é quase sempre engano de clique.</summary>
    public required bool NoPassado { get; init; }

    /// <summary>
    /// O motivo do fechamento (férias, feriado) — ou nulo. Desde set/2026 ele NÃO impede o
    /// clique (o serviço deixou de recusar); fica a hachura com o motivo escrito.
    /// </summary>
    public string? Bloqueio { get; init; }
    public bool Bloqueada => Bloqueio is not null;

    /// <summary>Fora da jornada DECLARADA do profissional da coluna, com a frase da dica. Sem jornada, sempre nulo.</summary>
    public string? ForaDoExpediente { get; init; }
    public bool Expediente => ForaDoExpediente is not null;

    /// <summary>A trava do profissional ("recusar marcação fora do horário…") está ligada.</summary>
    public bool AgendaProtegida { get; init; }

    /// <summary>O vão permite marcar se não houver impedimento na agenda protegida.</summary>
    public bool PodeMarcar => Livre && !NoPassado && !(AgendaProtegida && (Bloqueada || Expediente));

    /// <summary>A sobreposição só existe quando a trava do profissional está desligada.</summary>
    public bool PodeSobrepor => !Livre && !NoPassado && !AgendaProtegida;

    /// <summary>A dica do vão — "Marcar neste horário", com o MOTIVO quando há um fechamento.</summary>
    public string DicaDoVao => ComMotivo("Marcar neste horário");

    /// <summary>A dica do "+" do horário ocupado — mesma composição, outro verbo.</summary>
    public string DicaDeSobrepor => ComMotivo("Marcar mais um paciente neste horário");

    private string ComMotivo(string acao)
        => (Bloqueio ?? ForaDoExpediente) is { } motivo ? $"{acao} — atenção: {motivo}" : acao;
}

/// <summary>Uma faixa de horário da grade — uma LINHA, com uma célula por coluna.</summary>
public sealed record FaixaDaGrade(TimeOnly Hora, string Rotulo, bool HoraCheia, bool Agora, IReadOnlyList<CelulaDaGrade> Celulas);

/// <summary>Uma coluna da grade — um profissional, uma sala, um dia (semana) ou o resíduo.</summary>
public sealed record ColunaDaGrade
{
    public required int? ProfissionalId { get; init; }
    public int? SalaId { get; init; }
    public required string Nome { get; init; }
    public required string Resumo { get; init; }
    public required IReadOnlyList<CartaoDaGrade> Horarios { get; init; }

    /// <summary>O profissional da coluna (é dele a jornada que pinta o fora do expediente).</summary>
    public Profissional? Profissional { get; init; }

    /// <summary>O dia desta coluna — no modo SEMANA cada coluna é um dia.</summary>
    public required DateOnly Data { get; init; }

    /// <summary>O que está FECHADO neste dia (modo semana): "Ana em férias · Sala 2".</summary>
    public string Fechamentos { get; init; } = string.Empty;
    public bool TemFechamentos => !string.IsNullOrEmpty(Fechamentos);
}

/// <summary>
/// A PROJEÇÃO VISUAL de uma coluna: blocos posicionados pela hora (<see cref="Topo"/>) e pela
/// duração (<see cref="Altura"/>), em px, e as sobreposições em raias. Não grava nada.
/// </summary>
public sealed record BlocoDaGrade(double Topo, double Altura, string Rotulo,
    CartaoDaGrade? Cartao = null, CelulaDaGrade? Celula = null,
    string? Detalhe = null, bool Disponivel = false, int Raia = 0, int Raias = 1,
    CelulaDaGrade? Encaixe = null)
{
    public bool TemCartao => Cartao is not null;
    public bool TemVao => Celula is not null;
    public bool Bloqueado => Celula is { Bloqueada: true } or { Expediente: true };
    public bool PodeSobrepor => Encaixe?.PodeSobrepor == true;
}

public sealed record ColunaVisualDaGrade(string Nome, string Resumo, string Fechamentos, DateOnly Data, IReadOnlyList<BlocoDaGrade> Blocos);

public sealed record HoraDaRegua(double Topo, string Rotulo, bool HoraCheia, bool Agora);

/// <summary>O que a tela LEU numa carga — a grade é remontada daqui sem voltar ao banco.</summary>
/// <param name="Horarios">O dia (modo dia) ou a semana (modo semana), SEM o filtro de sala.</param>
/// <param name="Anteriores">Ocupações que começaram antes e atravessam o período (a virada).</param>
public sealed record LeituraDaGrade(
    IReadOnlyList<Agendamento> Horarios,
    IReadOnlyList<Agendamento> Anteriores,
    IReadOnlyList<Profissional> Profissionais,
    IReadOnlyList<Sala> Salas,
    IReadOnlyList<BloqueioAgenda> Bloqueios,
    bool BloqueiosVerificados,
    IReadOnlyDictionary<int, RespostaDeConfirmacao> Confirmacoes);

/// <summary>Os filtros da tela que mudam a montagem.</summary>
public sealed record FiltroDaGrade(
    DateOnly Dia, bool ModoSemana, bool AgruparPorSala, int? ProfissionalId, int? SalaId, string Duracao);

/// <summary>A grade pronta para desenhar.</summary>
public sealed record GradeMontada(
    IReadOnlyList<ColunaDaGrade> Colunas,
    IReadOnlyList<FaixaDaGrade> Faixas,
    IReadOnlyList<ColunaVisualDaGrade> ColunasVisuais,
    IReadOnlyList<HoraDaRegua> Regua,
    double Altura,
    IReadOnlyList<Vaga> Vagas,
    string EstadoVagas,
    string Resumo,
    bool SemProfissionais,
    bool SemSalas,
    bool DisponibilidadeNaoVerificada)
{
    public static GradeMontada Vazia { get; } = new([], [], [], [], 0, [], "Use Próximas vagas para conferir este filtro.",
        string.Empty, false, false, true);
}

/// <summary>O recorte da busca e dos filtros de situação/modalidade sobre os cartões.</summary>
public sealed record RecorteDaGrade(
    IReadOnlySet<int> Visiveis,
    IReadOnlyDictionary<string, int> Contagens,
    IReadOnlyList<string> Modalidades,
    string Resumo,
    bool NenhumCorresponde);

/// <summary>
/// A GRADE da agenda do balcão (out/2026 — migrada do <c>AgendaViewModel</c>,
/// <c>AgendaPlanejamento</c> e <c>FiltroListaAgenda</c> WPF). Tudo o que a grade AFIRMA mora
/// aqui, puro e testado: as colunas (profissional, sala, dia da semana, inativo, "sem
/// profissional", "sem sala"), a linha do tempo (janela padrão ESTICADA pelo que existe,
/// continuação, linha do agora, fechamento e fora do expediente), os blocos com a duração real
/// e as raias das sobreposições, as vagas do período e a busca com as pílulas de situação.
/// </summary>
public static class GradeDaAgendaNaTela
{
    /// <summary>Passo da grade — a duração padrão da clínica.</summary>
    public const int PassoMinutos = Agendamento.DuracaoPadraoMinutos;

    /// <summary>Altura de um minuto na linha do tempo (px). Meia hora = 36 px.</summary>
    public const double PixelsPorMinuto = 1.2;

    public const string NomeDoPeriodo = "no período selecionado";

    private static readonly string[] Dias = ["seg", "ter", "qua", "qui", "sex", "sáb", "dom"];
    private static readonly CompareInfo Comparador = new CultureInfo("pt-BR").CompareInfo;

    /// <summary>A segunda-feira da semana da data (a semana começa na SEGUNDA).</summary>
    public static DateOnly SegundaDa(DateOnly dia) => dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));

    /// <summary>O período que a leitura cobre: o dia, ou a semana (segunda a domingo).</summary>
    public static (DateOnly Inicio, DateOnly Fim) Periodo(DateOnly dia, bool modoSemana)
        => modoSemana ? (SegundaDa(dia), SegundaDa(dia).AddDays(6)) : (dia, dia);

    /// <summary>O status gravado por extenso (a janela do horário).</summary>
    public static string Rotular(StatusAgendamento status) => status switch
    {
        StatusAgendamento.Agendado => "Marcado",
        StatusAgendamento.Realizado => "Atendido",
        StatusAgendamento.Cancelado => "Cancelado",
        StatusAgendamento.Faltou => "Faltou",
        StatusAgendamento.Substituido => "Substituído por uma sessão lançada por fora",
        _ => RotulosEnum.De(status)
    };

    /// <summary>O cartão de um horário — a MESMA montagem da grade e da janela do horário.</summary>
    public static CartaoDaGrade Cartao(Agendamento a, IReadOnlyDictionary<int, RespostaDeConfirmacao> confirmacoes,
        int? profissionalDaColuna = null) => new()
    {
        AgendamentoId = a.Id,
        PacienteId = a.PacienteId,
        ProfissionalId = profissionalDaColuna ?? a.ProfissionalId,
        SerieId = a.SerieId,
        Faixa = $"{a.DataHora:HH\\:mm}–{a.FimPrevisto:HH\\:mm}",
        Paciente = a.Paciente?.Nome ?? "(paciente removido)",
        Telefone = a.Paciente?.Telefone,
        DataHora = a.DataHora,
        Fim = a.FimPrevisto,
        Modalidade = CatalogoModalidades.NomeComEspecialidade(
            a.ModalidadeCodigo, a.ModalidadePrevista, a.EspecialidadeConsultaCodigo),
        ModalidadeFamilia = a.ModalidadePrevista,
        Sala = a.Sala?.Nome ?? string.Empty,
        Profissional = a.Profissional?.Rotulo ?? "sem profissional",
        StatusRotulo = Rotular(a.Status),
        Situacao = a.Status,
        Etapa = a.Etapa,
        EhEncaixe = a.Encaixe,
        EhRetornoDoSegundoCodigo = a.Origem == OrigemAgendamento.RetornoSugerido,
        VeioDaListaEspera = a.Origem == OrigemAgendamento.ListaEspera,
        Observacoes = a.Observacoes,
        Lancamento = AgendaDoDiaNaTela.DescreverLancamento(a),
        EmAberto = a.Status == StatusAgendamento.Agendado,
        ForaDoDia = !a.OcupaAgenda,
        // A MESMA regra da lista do dia: só em AGUARDANDO, e só o que a rodada afirmou.
        ConfirmacaoRotulo = a.Etapa == EtapaFila.Aguardando && confirmacoes.TryGetValue(a.Id, out var r)
            ? RodadaDeConfirmacao.SeloDaLista(r)
            : string.Empty
    };

    /// <summary>
    /// Monta a grade inteira a partir do que foi lido. <paramref name="agora"/> é injetado: a
    /// linha do agora, o "no passado" e as vagas dependem dele, e regra com relógio que não se
    /// testa apodrece sem ninguém notar.
    /// </summary>
    public static GradeMontada Montar(LeituraDaGrade l, FiltroDaGrade f, DateTime agora)
    {
        var naoVerificada = !l.BloqueiosVerificados;
        var horariosPlanejamento = l.Horarios.Concat(l.Anteriores).DistinctBy(a => a.Id).ToList();
        var horarios = f.SalaId is { } salaFiltro ? l.Horarios.Where(a => a.SalaId == salaFiltro).ToList() : l.Horarios.ToList();
        var semProfissionais = l.Profissionais.Count == 0;
        var semSalas = false;
        var profissionalDoFiltro = l.Profissionais.FirstOrDefault(p => p.Id == f.ProfissionalId);

        List<ColunaDaGrade> colunas;
        string resumo;

        if (f.ModoSemana)
        {
            (colunas, resumo) = ColunasDaSemana(horarios, l, f);
        }
        else if (f.AgruparPorSala)
        {
            var doDia = f.ProfissionalId is { } pid ? horarios.Where(a => a.ProfissionalId == pid).ToList() : horarios;
            var salas = f.SalaId is { } sid ? l.Salas.Where(s => s.Id == sid).ToList() : l.Salas.ToList();
            semSalas = salas.Count == 0;
            colunas = [];
            foreach (var s in salas)
                colunas.Add(Coluna(null, s.Nome, f.Dia, doDia.Where(a => a.SalaId == s.Id), l.Confirmacoes, salaId: s.Id));

            // Sala DESATIVADA com horário marcado ganha coluna, marcada como inativa: o horário
            // TEM sala, e sumir com ele faria a grade desenhar menos do que o cabeçalho conta.
            var ativas = salas.Select(s => s.Id).ToHashSet();
            foreach (var grupo in doDia.Where(a => a.SalaId is int id && !ativas.Contains(id) && !(f.SalaId is { } fs && id != fs))
                         .GroupBy(a => a.SalaId!.Value).OrderBy(g => g.First().Sala?.Nome))
                colunas.Add(Coluna(null, $"{grupo.First().Sala?.Nome ?? "Sala removida"} (inativa)",
                    f.Dia, grupo, l.Confirmacoes, salaId: grupo.Key));

            // "Sem sala" é o caso NORMAL nesta clínica, não resíduo — some só quando não há nenhum.
            var semSala = doDia.Where(a => a.SalaId is null).ToList();
            if (semSala.Count > 0) colunas.Add(Coluna(null, "Sem sala", f.Dia, semSala, l.Confirmacoes));

            resumo = $"{doDia.Count(a => a.OcupaAgenda)} horário(s) no dia · {colunas.Count} coluna(s)";
        }
        else
        {
            var meu = f.ProfissionalId;
            var visiveis = meu is not null ? l.Profissionais.Where(p => p.Id == meu).ToList() : l.Profissionais.ToList();
            colunas = [];
            foreach (var p in visiveis)
                colunas.Add(Coluna(p.Id, p.Rotulo, f.Dia, horarios.Where(a => a.ProfissionalId == p.Id), l.Confirmacoes, profissional: p));

            // Profissional DESATIVADO com horário marcado ganha coluna "(inativo)" — não cai em
            // "Sem profissional": o horário TEM dono, e atribuí-lo a ninguém esconderia
            // justamente quem precisa ser remarcado (fila da parcela 69, item 7).
            if (meu is null)
            {
                var ativos = l.Profissionais.Select(p => p.Id).ToHashSet();
                foreach (var grupo in horarios.Where(a => a.ProfissionalId is int id && !ativos.Contains(id))
                             .GroupBy(a => a.ProfissionalId!.Value).OrderBy(g => g.First().Profissional?.Rotulo))
                    colunas.Add(Coluna(grupo.Key,
                        $"{grupo.First().Profissional?.Rotulo ?? "Profissional removido"} (inativo)",
                        f.Dia, grupo, l.Confirmacoes));
            }

            // "Sem profissional" só aparece quando existe (resíduo da agenda antiga e dos encaixes).
            var orfaos = horarios.Where(a => a.ProfissionalId is null).ToList();
            if (orfaos.Count > 0 && meu is null)
                colunas.Add(Coluna(null, "Sem profissional", f.Dia, orfaos, l.Confirmacoes));

            var ocupando = meu is null
                ? horarios.Count(a => a.OcupaAgenda)
                : horarios.Count(a => a.OcupaAgenda && a.ProfissionalId == meu);
            resumo = $"{ocupando} horário(s) no dia · {colunas.Count} coluna(s)";
        }

        var faixas = Faixas(colunas, l.Bloqueios, profissionalDoFiltro, f, agora);
        var (visuais, regua, altura, vagas, estadoVagas) = Planejar(
            colunas, faixas, l, f, profissionalDoFiltro, horariosPlanejamento, naoVerificada, agora);

        return new GradeMontada(colunas, faixas, visuais, regua, altura, vagas, estadoVagas, resumo,
            semProfissionais, semSalas, naoVerificada);
    }

    private static (List<ColunaDaGrade>, string) ColunasDaSemana(
        IReadOnlyList<Agendamento> daSemana, LeituraDaGrade l, FiltroDaGrade f)
    {
        var meu = f.ProfissionalId;
        var soMeu = meu is not null;
        var segunda = SegundaDa(f.Dia);
        var ocupando = 0;
        var colunas = new List<ColunaDaGrade>();
        var porDia = daSemana.GroupBy(a => DateOnly.FromDateTime(a.DataHora))
            .ToDictionary(g => g.Key, g => g.ToList());

        for (var i = 0; i < 7; i++)
        {
            var quando = segunda.AddDays(i);
            var doDia = porDia.TryGetValue(quando, out var achados) ? achados : [];
            var recorte = soMeu ? doDia.Where(a => a.ProfissionalId == meu).ToList() : doDia;
            ocupando += recorte.Count(a => a.OcupaAgenda);

            // A coluna do dia não tem dono — exceto no recorte de UM profissional: aí todo
            // cartão é dele, e amarrá-la é o que faz as FÉRIAS e a JORNADA dele pintarem os vãos.
            var dono = soMeu ? l.Profissionais.FirstOrDefault(p => p.Id == meu) : null;
            colunas.Add(Coluna(dono?.Id, $"{Dias[i]} {quando:dd'/'MM}", quando, recorte, l.Confirmacoes,
                profissional: dono,
                fechamentos: FechamentosDaAgenda.Descrever(l.Bloqueios, quando.ToDateTime(TimeOnly.MinValue), dono?.Id)));
        }

        return (colunas, $"{ocupando} horário(s) na semana de {segunda:dd'/'MM} a {segunda.AddDays(6):dd'/'MM}");
    }

    private static ColunaDaGrade Coluna(
        int? profissionalId, string nome, DateOnly data, IEnumerable<Agendamento> agendamentos,
        IReadOnlyDictionary<int, RespostaDeConfirmacao> confirmacoes,
        int? salaId = null, Profissional? profissional = null, string fechamentos = "")
    {
        var lista = agendamentos.OrderBy(a => a.DataHora).ToList();
        return new ColunaDaGrade
        {
            ProfissionalId = profissionalId,
            SalaId = salaId,
            Profissional = profissional,
            Fechamentos = fechamentos,
            Nome = nome,
            Data = data,
            Resumo = $"{lista.Count(a => a.OcupaAgenda)} horário(s)",
            Horarios = lista.Select(a => Cartao(a, confirmacoes, profissionalId)).ToList()
        };
    }

    /// <summary>
    /// A linha do tempo. A janela é a padrão da clínica (ou a do profissional filtrado)
    /// ESTICADA pelo que existe — nunca 00:00–23:59. Cada sessão ocupa a faixa em que COMEÇA e
    /// marca as seguintes como continuação até o fim previsto; cancelado e falta não cobrem nada.
    /// </summary>
    public static IReadOnlyList<FaixaDaGrade> Faixas(
        IReadOnlyList<ColunaDaGrade> colunas, IReadOnlyList<BloqueioAgenda> bloqueios,
        Profissional? profissionalDoFiltro, FiltroDaGrade f, DateTime agora)
    {
        if (colunas.Count == 0) return [];

        var todos = colunas.SelectMany(c => c.Horarios).ToList();
        var limites = profissionalDoFiltro?.LimitesEm(f.Dia.DayOfWeek);
        var inicio = limites?.Abre ?? Agendamento.AberturaPadraoGrade;
        var fim = limites?.Fecha ?? Agendamento.FechamentoPadraoGrade;
        foreach (var c in todos)
        {
            var comeco = TimeOnly.FromDateTime(c.DataHora);
            var termino = TimeOnly.FromDateTime(c.Fim);
            if (comeco < inicio) inicio = comeco;
            // Sessão que atravessa a meia-noite não estica a grade para trás.
            if (termino > fim && termino > comeco) fim = termino;
        }

        var primeiro = Piso(inicio);
        var ultimo = Piso(fim);
        var horaAgora = TimeOnly.FromDateTime(agora);
        // A linha do agora: só no modo dia e só quando o dia aberto é hoje.
        var marcarAgora = !f.ModoSemana && f.Dia == DateOnly.FromDateTime(agora);
        var faixas = new List<FaixaDaGrade>();

        for (var minuto = primeiro; minuto <= ultimo; minuto += PassoMinutos)
        {
            var hora = new TimeOnly(minuto / 60, minuto % 60);
            var celulas = new List<CelulaDaGrade>();
            foreach (var coluna in colunas)
            {
                var quando = coluna.Data.ToDateTime(hora);
                var naFaixa = coluna.Horarios.Where(h => Piso(TimeOnly.FromDateTime(h.DataHora)) == minuto).ToList();
                var coberta = naFaixa.Count == 0 && coluna.Horarios.Any(h =>
                    !h.ForaDoDia && Piso(TimeOnly.FromDateTime(h.DataHora)) < minuto && h.Fim > quando);
                var dono = coluna.Profissional ?? profissionalDoFiltro;
                var profissionalId = coluna.ProfissionalId ?? f.ProfissionalId;
                var salaId = coluna.SalaId ?? f.SalaId;

                celulas.Add(new CelulaDaGrade
                {
                    ProfissionalId = profissionalId,
                    SalaId = salaId,
                    Quando = quando,
                    Cartoes = naFaixa,
                    Continuacao = coberta,
                    NoPassado = quando < agora,
                    Bloqueio = BloqueioDe(bloqueios, quando, profissionalId, salaId),
                    ForaDoExpediente = ExpedienteDe(dono, quando),
                    AgendaProtegida = dono?.AgendaProtegida == true
                });
            }

            faixas.Add(new FaixaDaGrade(hora, hora.ToString("HH\\:mm", CultureInfo.InvariantCulture), hora.Minute == 0,
                // `AddMinutes` na última faixa do dia dá a volta para 00:00 e a comparação falha — o certo.
                marcarAgora && hora <= horaAgora && horaAgora < hora.AddMinutes(PassoMinutos),
                celulas));
        }

        return faixas;
    }

    /// <summary>O fechamento que alcança este vão — pelo recurso da COLUNA (o da sala 2 não fecha o vão da Ana).</summary>
    private static string? BloqueioDe(IReadOnlyList<BloqueioAgenda> bloqueios, DateTime inicio, int? profissionalId, int? salaId)
    {
        if (bloqueios.Count == 0) return null;
        var fim = inicio.AddMinutes(PassoMinutos);
        return bloqueios.FirstOrDefault(b => b.ColideCom(inicio, fim) && b.AlcancaRecurso(profissionalId, salaId))?.Motivo;
    }

    /// <summary>A jornada declarada contra este vão: nulo dentro (ou sem jornada), a frase da dica fora.</summary>
    private static string? ExpedienteDe(Profissional? profissional, DateTime inicio)
    {
        if (profissional is not { JornadaDeclarada: true }) return null;
        var fim = inicio.AddMinutes(PassoMinutos);
        return profissional.DentroDoExpediente(inicio, fim)
            ? null
            : $"Fora do expediente de {profissional.Rotulo} — atende {profissional.DescricaoJornada}.";
    }

    /// <summary>Minuto do dia arredondado para BAIXO no passo da grade.</summary>
    private static int Piso(TimeOnly hora)
    {
        var minutos = hora.Hour * 60 + hora.Minute;
        return minutos - minutos % PassoMinutos;
    }

    /// <summary>A duração digitada vale de 1 a 720 minutos (a régua da busca de vagas).</summary>
    public static bool DuracaoValida(string? texto, out int duracao)
        => int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out duracao) && duracao is > 0 and <= 720;

    /// <summary>
    /// A projeção visual: blocos contínuos com a duração real (os vãos agrupados), as
    /// sobreposições em raias distintas e as vagas do período aberto.
    /// </summary>
    private static (List<ColunaVisualDaGrade>, List<HoraDaRegua>, double, List<Vaga>, string) Planejar(
        IReadOnlyList<ColunaDaGrade> colunas, IReadOnlyList<FaixaDaGrade> faixas, LeituraDaGrade l, FiltroDaGrade f,
        Profissional? profissionalDoFiltro, IReadOnlyList<Agendamento> horariosPlanejamento, bool naoVerificada, DateTime agora)
    {
        var visuais = new List<ColunaVisualDaGrade>();
        var regua = new List<HoraDaRegua>();
        var vagasDoPeriodo = new List<Vaga>();
        var duracaoValida = DuracaoValida(f.Duracao, out var duracao);

        if (faixas.Count == 0)
            return (visuais, regua, 0, vagasDoPeriodo, EstadoDasVagas(f, naoVerificada, 0));

        var inicioMinuto = faixas[0].Hora.Hour * 60 + faixas[0].Hora.Minute;
        var altura = faixas.Count * PassoMinutos * PixelsPorMinuto;
        foreach (var faixa in faixas)
            regua.Add(new HoraDaRegua((faixa.Hora.Hour * 60 + faixa.Hora.Minute - inicioMinuto) * PixelsPorMinuto,
                faixa.Rotulo, faixa.HoraCheia, faixa.Agora));

        var hoje = DateOnly.FromDateTime(agora);
        for (var indice = 0; indice < colunas.Count; indice++)
        {
            var coluna = colunas[indice];
            var blocos = new List<BlocoDaGrade>();
            var inicio = coluna.Data.ToDateTime(TimeOnly.MinValue).AddMinutes(inicioMinuto);
            var profissional = coluna.Profissional ?? profissionalDoFiltro;
            var salaId = coluna.SalaId ?? f.SalaId;
            var sala = l.Salas.FirstOrDefault(s => s.Id == salaId);
            var vagas = coluna.Data >= hoje && profissional is { } p && duracaoValida && !naoVerificada
                        && (salaId is null || sala is not null)
                ? BuscaDeVagas.Calcular(inicio > agora ? inicio : agora, duracao, p, horariosPlanejamento, l.Bloqueios,
                        quantidade: 100, diasMaximos: 1, sala: sala)
                    .Where(v => DateOnly.FromDateTime(v.Inicio) == coluna.Data).ToList()
                : [];
            var disponiveis = vagas.Select(v => v.Inicio).ToHashSet();
            // O painel lateral segue o profissional do filtro; a grade por sala calcula cada
            // recurso separadamente (juntar repetiria horários e perderia a sala).
            if (f.ProfissionalId is not null && !(f.AgruparPorSala && !f.ModoSemana))
                foreach (var vaga in vagas.Take(Math.Max(0, 10 - vagasDoPeriodo.Count))) vagasDoPeriodo.Add(vaga);

            DateTime? proximoBloco = null;
            foreach (var faixa in faixas)
            {
                var celula = faixa.Celulas[indice];
                var topo = (celula.Quando - inicio).TotalMinutes * PixelsPorMinuto;
                if (celula.Continuacao || celula.Cartoes.Any(c => !c.ForaDoDia) || celula.Quando < proximoBloco) continue;
                var livre = disponiveis.Contains(celula.Quando);
                var minutosBloco = PassoMinutos;
                if (livre) minutosBloco = duracao;
                else if (celula.Bloqueada || celula.Expediente)
                {
                    var seguintes = faixas.Select(x => x.Celulas[indice]).Where(c => c.Quando > celula.Quando)
                        .TakeWhile(c => c.Bloqueio == celula.Bloqueio && c.ForaDoExpediente == celula.ForaDoExpediente
                                        && !c.Continuacao && !c.Cartoes.Any(h => !h.ForaDoDia)).Count();
                    minutosBloco += seguintes * PassoMinutos;
                }
                proximoBloco = celula.Quando.AddMinutes(minutosBloco);
                var salaOcupada = sala is not null && duracaoValida && horariosPlanejamento.Count(a => a.OcupaAgenda && a.SalaId == sala.Id
                    && a.ColideCom(celula.Quando, celula.Quando.AddMinutes(duracao))) >= sala.Capacidade;
                var rotulo = naoVerificada ? "Não verificado"
                    : celula.Bloqueada ? celula.Bloqueio!
                    : celula.Expediente ? "Fora do expediente"
                    : celula.NoPassado ? "Horário passado"
                    : profissional is { } donoDoDia && !BuscaDeVagas.AtendeNoDia(donoDoDia, coluna.Data.DayOfWeek)
                        ? "Sem jornada para este dia"
                    : salaId is not null && sala is null ? "Sala indisponível"
                    : livre ? $"Disponível · {duracao} min"
                    : salaOcupada ? "Sala ocupada"
                    : profissional is null ? "Consultar horário"
                    : duracaoValida ? $"Não cabe {duracao} min" : "Consultar horário";
                blocos.Add(new BlocoDaGrade(topo, Math.Max(2, minutosBloco * PixelsPorMinuto - 2), rotulo, Celula: celula,
                    Detalhe: celula.ForaDoExpediente, Disponivel: livre));
            }

            // As sobreposições em RAIAS: cada cartão vai na primeira raia que já terminou.
            var horarios = coluna.Horarios.OrderBy(h => h.DataHora).ThenBy(h => h.Fim).ToList();
            var finais = new List<DateTime>();
            var cartoes = new List<(CartaoDaGrade Cartao, int Raia)>();
            foreach (var horario in horarios)
            {
                var raia = finais.FindIndex(fim => fim <= horario.DataHora);
                if (raia < 0) { raia = finais.Count; finais.Add(horario.Fim); }
                else finais[raia] = horario.Fim;
                cartoes.Add((horario, raia));
            }
            foreach (var (cartao, raia) in cartoes)
            {
                var dono = l.Profissionais.FirstOrDefault(x => x.Id == cartao.ProfissionalId)
                           ?? horariosPlanejamento.FirstOrDefault(a => a.Id == cartao.AgendamentoId)?.Profissional;
                var encaixe = new CelulaDaGrade
                {
                    ProfissionalId = cartao.ProfissionalId,
                    SalaId = coluna.SalaId ?? f.SalaId,
                    Quando = cartao.DataHora,
                    Cartoes = [cartao],
                    Continuacao = false,
                    NoPassado = cartao.DataHora < agora,
                    AgendaProtegida = dono?.AgendaProtegida == true
                };
                blocos.Add(new BlocoDaGrade((cartao.DataHora - inicio).TotalMinutes * PixelsPorMinuto,
                    Math.Max(2, (cartao.Fim - cartao.DataHora).TotalMinutes * PixelsPorMinuto - 2),
                    cartao.Faixa, Cartao: cartao, Raia: raia, Raias: Math.Max(1, finais.Count), Encaixe: encaixe));
            }
            visuais.Add(new ColunaVisualDaGrade(coluna.Nome, coluna.Resumo, coluna.Fechamentos, coluna.Data, blocos));
        }

        if (f.AgruparPorSala && !f.ModoSemana && !naoVerificada && duracaoValida
            && profissionalDoFiltro is { } selecionado && f.Dia >= hoje)
        {
            var dia = f.Dia.ToDateTime(TimeOnly.MinValue);
            vagasDoPeriodo.AddRange(BuscaDeVagas.Calcular(dia > agora ? dia : agora, duracao, selecionado,
                horariosPlanejamento, l.Bloqueios, diasMaximos: 1, sala: l.Salas.FirstOrDefault(s => s.Id == f.SalaId)));
        }

        return (visuais, regua, altura, vagasDoPeriodo, EstadoDasVagas(f, naoVerificada, vagasDoPeriodo.Count));
    }

    /// <summary>A frase sob a lista de vagas do período.</summary>
    public static string EstadoDasVagas(FiltroDaGrade f, bool naoVerificada, int vagas)
        => f.ProfissionalId is null ? "Escolha um profissional para consultar vagas."
            : naoVerificada ? "Disponibilidade não verificada. Atualize a agenda."
            : vagas == 0 ? "Nenhuma vaga neste período. Consulte mais dias."
            : "Horários do período aberto. Ao agendar, os conflitos serão conferidos novamente.";

    /// <summary>A frase depois da busca de "Próximas vagas" (os próximos 60 dias).</summary>
    public static string EstadoDaBusca(ResultadoBuscaDeVagas r, DateTime agora)
        => r.Vazio ? "Nenhuma vaga encontrada nos próximos 60 dias."
            : $"Conferido às {agora:HH\\:mm}. Ao agendar, os conflitos serão conferidos novamente."
              + (r.JornadaPresumida ? " Jornada presumida: segunda a sábado, 7h às 20h." : "");

    /// <summary>A frase do profissional escolhido sobre a trava dele.</summary>
    public static string Trava(Profissional? p) => p is null ? "Escolha um profissional"
        : p.AgendaProtegida ? "Trava ativa" : "Conflitos como aviso";

    /// <summary>O que a sala escolhida muda na consulta de disponibilidade.</summary>
    public static string SalaDoPlanejamento(int? salaId, string? nome) => salaId is null
        ? "Sem sala selecionada: disponibilidade somente do profissional."
        : $"Profissional e {nome}. O paciente será conferido no agendamento.";

    /// <summary>O rótulo do botão da lista de espera — com a contagem e, quando há foco, o horário.</summary>
    public static string RotuloDaEspera(DateTime? sugestaoPara, int quantos) => sugestaoPara is { } h
        ? $"Quem chamar para {h:HH\\:mm} ({quantos})"
        : $"Lista de espera ({quantos})";

    /// <summary>O título da lista de espera — diz o que ela mostra (filtro invisível engana).</summary>
    public static string TituloDaEspera(DateTime? sugestaoPara) => sugestaoPara is { } h
        ? $"Quem chamar para {h:dd'/'MM HH\\:mm}"
        : "Lista de espera";

    /// <summary>"Ninguém espera" e "ninguém serve para este horário" são respostas diferentes.</summary>
    public static string EsperaVazia(DateTime? sugestaoPara) => sugestaoPara is { } alvo
        ? $"Ninguém da lista serve para {alvo:dd'/'MM HH\\:mm} — turno, janela de datas ou profissional pedido não batem."
        : "Ninguém na lista de espera.";

    /// <summary>As preferências de um pedido da lista de espera, numa linha.</summary>
    public static string PreferenciasDaEspera(ListaEspera p)
    {
        var partes = new List<string>
        {
            p.Profissional?.Rotulo ?? "qualquer profissional",
            p.Periodo switch
            {
                PeriodoPreferido.Manha => "manhã",
                PeriodoPreferido.Tarde => "tarde",
                _ => "qualquer turno"
            }
        };
        if (p.DisponivelDe is not null || p.DisponivelAte is not null)
            partes.Add($"{p.DisponivelDe?.ToString("dd'/'MM", CultureInfo.InvariantCulture) ?? "hoje"}"
                       + $" a {p.DisponivelAte?.ToString("dd'/'MM", CultureInfo.InvariantCulture) ?? "sem limite"}");
        return string.Join(" · ", partes);
    }

    /// <summary>
    /// A busca e os filtros de situação/modalidade (o <c>FiltroListaAgenda</c> WPF): o nome
    /// casa termo a termo, sem acento e sem caixa; as contagens das pílulas são sobre o que a
    /// BUSCA e a MODALIDADE deixaram, nunca sobre o recorte da própria pílula. O recorte esconde
    /// só os PACIENTES: vãos e bloqueios continuam na grade.
    /// </summary>
    public static RecorteDaGrade Recortar(IEnumerable<CartaoDaGrade> cartoes, FiltroAgendaDoDia filtro, bool carregando)
    {
        // Uma linha por horário: na visão por sala o mesmo horário não aparece duas vezes.
        var todos = cartoes.DistinctBy(c => c.AgendamentoId).ToList();
        var modalidadeAtiva = !string.IsNullOrEmpty(filtro.Modalidade) && filtro.Modalidade != AgendaDoDiaNaTela.TodasModalidades;
        var termos = (filtro.Busca ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        bool Passa(CartaoDaGrade c) =>
            (!modalidadeAtiva || c.Modalidade == filtro.Modalidade)
            && termos.All(t => Comparador.IndexOf(c.Paciente, t, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

        var pesquisados = todos.Where(Passa).ToList();
        var contagens = AgendaDoDiaNaTela.Situacoes.ToDictionary(
            s => s.Chave, s => s.Chave == "todos" ? pesquisados.Count : pesquisados.Count(c => c.Grupo == s.Chave));
        var visiveis = pesquisados.Where(c => filtro.Situacao == "todos" || c.Grupo == filtro.Situacao).ToList();

        var modalidades = todos.Select(c => c.Modalidade).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct()
            .OrderBy(m => m, StringComparer.Create(new CultureInfo("pt-BR"), false)).ToList();
        // A escolha atual fica na lista mesmo sem horário dela no período: sumir com a opção
        // escolhida faria o combo trocar o filtro sozinho.
        if (modalidadeAtiva && !modalidades.Contains(filtro.Modalidade!)) modalidades.Add(filtro.Modalidade!);
        modalidades.Insert(0, AgendaDoDiaNaTela.TodasModalidades);

        var temFiltros = termos.Length > 0 || modalidadeAtiva || filtro.Situacao != "todos";
        return new RecorteDaGrade(
            visiveis.Select(c => c.AgendamentoId).ToHashSet(), contagens, modalidades,
            carregando ? "Atualizando agenda…" : $"{visiveis.Count} de {todos.Count} horários {NomeDoPeriodo}",
            NenhumCorresponde: !carregando && todos.Count > 0 && visiveis.Count == 0 && temFiltros);
    }

    /// <summary>A frase da prévia do cancelamento da série — as datas por extenso, até dez.</summary>
    public static string PreviaDaSerie(string paciente, IReadOnlyList<DateTime> emAberto)
    {
        var datas = emAberto.Take(10).Select(d => d.ToString("dd'/'MM'/'yyyy HH\\:mm", CultureInfo.InvariantCulture));
        var lista = string.Join("\n", datas) + (emAberto.Count > 10 ? $"\n… e mais {emAberto.Count - 10}." : string.Empty);
        return $"Cancelar as {emAberto.Count} sessão(ões) ainda marcadas de {paciente}?\n\n{lista}"
               + "\n\nAs que já foram atendidas continuam no histórico.";
    }

    /// <summary>A linha dos pedidos da enfermagem no alto da grade (a caixa inteira, e o dia aberto).</summary>
    public static string ResumoDosPedidos(int novos, int noDia, int abertos)
        => $"{novos} novos · {noDia} neste dia · {abertos} em aberto";

    /// <summary>"Dra. Helena · 30 minutos" — o que a grade e o painel de vagas estão consultando.</summary>
    public static string ResumoDoPlanejamento(string? profissional, string duracao)
        => $"{profissional ?? "Todos os profissionais"} · {duracao} minutos";

    /// <summary>"Disponibilidade consultada às 14:32 · dia 12/10" — o rodapé da grade.</summary>
    public static string LeituraConcluida(DateTime agora, bool modoSemana, DateOnly dia)
        => $"{agora:HH\\:mm} · {(modoSemana ? "semana de" : "dia")} {dia:dd'/'MM}";

    /// <summary>O aviso de quem já estava marcado quando a agenda foi fechada.</summary>
    public static string AvisoDoFechamento(IReadOnlyList<string> marcadosDentro)
        => $"{marcadosDentro.Count} sessão(ões) estão marcadas dentro do período fechado. "
           + "Elas continuam na agenda: remarque com o paciente.\n\n"
           + string.Join("\n", marcadosDentro)
           + "\n\nPara empurrar todas de uma vez, use Profissionais e salas → Empurrar.";
}
