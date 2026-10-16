using System.Text.Json;
using Clinica.Domain.Regras;

namespace Clinica.Domain.Entities;

public sealed record AtendimentoHabilitado(string ModalidadeCodigo, string? EspecialidadeCodigo);

/// <summary>
/// Quem atende na clínica. É a entidade de fundação da parcela 1: sem ela não existe
/// agenda por profissional, "quem atendeu" no prontuário, repasse no financeiro,
/// produtividade no BI nem perfil de acesso.
///
/// Não é usuário do sistema (login não existe ainda) — é o profissional que ocupa a
/// agenda e assina o atendimento. Quando os perfis chegarem (parcela 5), o usuário
/// aponta para cá em vez de duplicar o cadastro.
/// </summary>
public class Profissional
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Como aparece na agenda quando o espaço é curto (ex.: "Dra. Ana").</summary>
    public string? NomeCurto { get; set; }

    /// <summary>Conselho e número de registro, como vai nos documentos (ex.: "CRM-SP 123456").</summary>
    public string? RegistroConselho { get; set; }

    /// <summary>
    /// CPF, só dígitos. Existe para AMARRAR o certificado digital à pessoa (parcela 42): o
    /// e-CPF ICP-Brasil carrega o CPF do titular numa extensão do próprio certificado, e é
    /// comparando os dois que o sistema recusa alguém assinar uma prescrição com o
    /// certificado de outra pessoa. Sem essa conferência, "assinatura qualificada" provaria
    /// apenas que ALGUÉM com um token assinou — que é a metade inútil da garantia.
    ///
    /// Opcional: quem não assina digitalmente não precisa dele, e exigi-lo travaria o
    /// cadastro da equipe inteira por causa de uma feature que nem toda clínica usa.
    /// </summary>
    public string? Cpf { get; set; }

    /// <summary>
    /// Especialidade principal, pelo código do catálogo (<c>CatalogoEspecialidades</c>).
    /// Null quando o profissional não se enquadra em nenhuma das cadastradas.
    /// </summary>
    public string? EspecialidadeCodigo { get; set; }

    /// <summary>Nulo conserva as opções antigas; JSON explícito, inclusive [], é a decisão do gerente.</summary>
    public string? HabilitacoesAtendimentoJson { get; set; }

    public IReadOnlyList<AtendimentoHabilitado>? HabilitacoesAtendimento
        => HabilitacoesAtendimentoJson is null ? null
            : JsonSerializer.Deserialize<List<AtendimentoHabilitado>>(HabilitacoesAtendimentoJson) ?? [];

    public bool Atende(string modalidadeCodigo, string? especialidadeCodigo = null)
    {
        var psicologia = string.Equals(EspecialidadeCodigo, "Psicologia", StringComparison.OrdinalIgnoreCase);
        if (psicologia && (CatalogoModalidades.Base(modalidadeCodigo) != ModalidadeAtendimento.Consulta
            || !string.Equals(especialidadeCodigo, "Psicologia", StringComparison.OrdinalIgnoreCase)))
            return false;
        var habilitacoes = HabilitacoesAtendimento;
        if (habilitacoes is null)
            return true;
        return habilitacoes.Any(h =>
            string.Equals(h.ModalidadeCodigo, modalidadeCodigo, StringComparison.OrdinalIgnoreCase)
            && string.Equals(h.EspecialidadeCodigo, especialidadeCodigo, StringComparison.OrdinalIgnoreCase));
    }

    public string? Telefone { get; set; }

    public string? Email { get; set; }

    /// <summary>
    /// Cor da coluna/tarja na agenda, em hexadecimal <c>#RRGGBB</c>. Null usa a cor
    /// padrão do design system — a agenda nunca depende de a cor estar preenchida.
    /// </summary>
    public string? Cor { get; set; }

    /// <summary>
    /// Duração padrão do atendimento deste profissional, em minutos. Null usa o padrão
    /// da clínica (<see cref="Agendamento.DuracaoPadraoMinutos"/>).
    /// </summary>
    public int? DuracaoPadraoMinutos { get; set; }

    /// <summary>Trava compartilhada: impede sobreposição, bloqueios e marcação fora da jornada.</summary>
    public bool AgendaProtegida { get; set; }

    // ==================== Jornada: dias e horário de atendimento (set/2026) ====================
    //
    // Até aqui o cadastro só tinha a duração padrão e o "ativo": quem atende terça e quinta
    // aparecia na grade com coluna vazia e MARCÁVEL nos outros cinco dias, e a única saída
    // era um bloqueio semanal repetido à mão. A ocupação do BI media todo mundo por uma
    // jornada global (ParametrosService.ChaveJornadaDiariaMinutos). Os três campos são
    // opcionais e NULO quer dizer "não declarado": a agenda trata como sempre tratou —
    // qualquer dia, qualquer hora — e nada muda para quem não preencher.

    /// <summary>
    /// Os dias em que atende, como máscara de bits por <see cref="DayOfWeek"/>
    /// (<see cref="BitDe"/>). Nulo = não declarado (todos os dias). Zero não é gravado — o
    /// serviço normaliza para nulo, porque "nenhum dia" não é uma jornada.
    /// </summary>
    public int? DiasDeAtendimento { get; set; }

    /// <summary>Início do horário de atendimento. Anda em par com <see cref="AtendeAte"/>.</summary>
    public TimeOnly? AtendeDas { get; set; }

    /// <summary>Fim do horário de atendimento. Anda em par com <see cref="AtendeDas"/>.</summary>
    public TimeOnly? AtendeAte { get; set; }

    // ==================== Horários de atendimento por dia (out/2026) ====================
    //
    // A jornada de set/2026 tinha UM horário para todos os dias ("seg · qua, das 08:00 às
    // 12:00"): quem atende de manhã na segunda e à tarde na quinta, ou tem almoço, não cabia
    // nela — e a clínica não conseguia dizer o que é "jornada", porque a tela pedia uma coisa
    // que não descrevia a semana de ninguém. Agora cada dia tem as SUAS faixas
    // ("seg 08:00–12:00 e 14:00–18:00 · qui 14:00–19:00"), gravadas em texto compacto.
    //
    // Os três campos antigos continuam gravados (união das faixas) porque o binário anterior e
    // a web de leitura os leem; quem decide é sempre <see cref="FaixasEm"/>.

    /// <summary>
    /// As faixas por dia, em texto: <c>1=08:00-12:00,14:00-18:00;4=14:00-19:00</c> (o número é
    /// o <see cref="DayOfWeek"/>). Nulo = cadastro anterior a out/2026 — vale o legado.
    /// </summary>
    public string? HorariosAtendimento { get; set; }

    /// <summary>Há horário de atendimento declarado (por dia, ou o legado).</summary>
    public bool JornadaDeclarada => HorariosAtendimento is not null
        || DiasDeAtendimento is not null || (AtendeDas is not null && AtendeAte is not null);

    /// <summary>
    /// O horário diz a semana INTEIRA (dias e horas) — o que a ocupação precisa para ter
    /// denominador. Só horas sem dias (legado) não basta: contaria o domingo como vago.
    /// </summary>
    public bool HorarioCompleto => HorariosAtendimento is not null
        || (DiasDeAtendimento is not null && AtendeDas is not null && AtendeAte is not null);

    public static int BitDe(DayOfWeek dia) => 1 << (int)dia;

    /// <summary>Máscara com os sete dias marcados.</summary>
    public const int TodosOsDias = 0b111_1111;

    /// <summary>
    /// As faixas deste dia da semana. Lista VAZIA = não atende nesse dia; NULO = sem horário
    /// declarado para o dia (qualquer hora). São três respostas, e confundir as duas últimas
    /// pintaria de "fechado" o dia de quem nunca preencheu nada.
    /// </summary>
    public IReadOnlyList<FaixaDeAtendimento>? FaixasEm(DayOfWeek dia)
    {
        if (HorariosAtendimento is not null)
            return HorariosDeAtendimento.Ler(HorariosAtendimento).TryGetValue(dia, out var f) ? f : [];
        if (DiasDeAtendimento is { } dias && (dias & BitDe(dia)) == 0) return [];
        return AtendeDas is { } das && AtendeAte is { } ate && ate > das ? [new FaixaDeAtendimento(das, ate)] : null;
    }

    /// <summary>A semana inteira, dia a dia (segunda primeiro), na forma de <see cref="FaixasEm"/>.</summary>
    public IReadOnlyDictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>?> Semana()
        => OrdemDaSemana.ToDictionary(d => d, FaixasEm);

    /// <summary>Atende neste dia da semana? Sem horário declarado, sim.</summary>
    public bool AtendeEm(DayOfWeek dia) => FaixasEm(dia) is not { Count: 0 };

    /// <summary>
    /// O intervalo [início, fim) cabe numa faixa do dia? Sem horário declarado, sempre cabe.
    /// Sessão que atravessa a meia-noite nunca cabe — o horário é do dia.
    /// </summary>
    public bool DentroDoExpediente(DateTime inicio, DateTime fim)
    {
        var faixas = FaixasEm(inicio.DayOfWeek);
        if (faixas is null) return true;
        if (fim.Date > inicio.Date) return false;
        var de = TimeOnly.FromDateTime(inicio);
        var ate = TimeOnly.FromDateTime(fim);
        return faixas.Any(f => de >= f.Inicio && ate <= f.Fim);
    }

    /// <summary>Os minutos de atendimento neste dia — a base da ocupação. Nulo sem horário.</summary>
    public int? MinutosEm(DayOfWeek dia) => FaixasEm(dia) is { } f ? f.Sum(x => x.Minutos) : null;

    /// <summary>Primeira hora e última hora do dia (para abrir a grade). Nulo sem horário.</summary>
    public (TimeOnly Abre, TimeOnly Fecha)? LimitesEm(DayOfWeek dia)
        => FaixasEm(dia) is { Count: > 0 } f ? (f.Min(x => x.Inicio), f.Max(x => x.Fim)) : null;

    /// <summary>
    /// Grava os horários por dia (o editor único — <c>ConfiguracaoAgendaService</c>) e o legado
    /// junto: os dias com faixa e a UNIÃO das horas. Nulo/sem nenhum dia = "não declarado".
    /// </summary>
    public void DefinirHorarios(IReadOnlyDictionary<DayOfWeek, IReadOnlyList<FaixaDeAtendimento>>? semana)
    {
        var limpo = semana?.Where(d => d.Value.Count > 0).ToDictionary(d => d.Key, d => d.Value);
        if (limpo is null || limpo.Count == 0)
        {
            HorariosAtendimento = null; DiasDeAtendimento = null; AtendeDas = null; AtendeAte = null;
            return;
        }
        HorariosAtendimento = HorariosDeAtendimento.Escrever(limpo);
        DiasDeAtendimento = limpo.Keys.Aggregate(0, (m, d) => m | BitDe(d));
        AtendeDas = limpo.Values.SelectMany(f => f).Min(f => f.Inicio);
        AtendeAte = limpo.Values.SelectMany(f => f).Max(f => f.Fim);
    }

    /// <summary>
    /// "seg · qua · sex 08:00–12:00 e 14:00–18:00 · qui 14:00–19:00" — como o horário se lê na
    /// equipe, na grade e no aviso. Dias com as mesmas faixas se juntam. Nulo sem horário.
    /// </summary>
    public string? DescricaoJornada
    {
        get
        {
            if (!JornadaDeclarada) return null;
            var grupos = new List<(List<DayOfWeek> Dias, string Faixas)>();
            foreach (var d in OrdemDaSemana)
            {
                var f = FaixasEm(d);
                if (f is { Count: 0 }) continue;
                var texto = f is null ? "qualquer hora" : string.Join(" e ", f.Select(x => x.ToString()));
                var g = grupos.FindIndex(x => x.Faixas == texto);
                if (g >= 0) grupos[g].Dias.Add(d); else grupos.Add(([d], texto));
            }
            if (grupos.Count == 0) return "nenhum dia";
            if (grupos.Count == 1 && grupos[0].Dias.Count == 7) return "todos os dias, " + grupos[0].Faixas;
            return string.Join(" · ", grupos.Select(g => string.Join(" · ", g.Dias.Select(Abreviar)) + " " + g.Faixas));
        }
    }

    /// <summary>Segunda primeiro — a semana como a clínica a lê.</summary>
    public static IReadOnlyList<DayOfWeek> DiasDaSemana => OrdemDaSemana;

    /// <summary>"seg", "ter"... — o rótulo curto de cada dia, igual em toda tela.</summary>
    public static string AbreviarDia(DayOfWeek dia) => Abreviar(dia);

    private static readonly DayOfWeek[] OrdemDaSemana =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    private static string Abreviar(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => "seg",
        DayOfWeek.Tuesday => "ter",
        DayOfWeek.Wednesday => "qua",
        DayOfWeek.Thursday => "qui",
        DayOfWeek.Friday => "sex",
        DayOfWeek.Saturday => "sáb",
        _ => "dom"
    };

    /// <summary>Desligado/afastado sai das listas de agendamento sem perder o histórico.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Ordem de exibição nas listas e nas colunas da agenda.</summary>
    public int Ordem { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>O que a agenda mostra: o nome curto quando existe, senão o nome.</summary>
    public string Rotulo => string.IsNullOrWhiteSpace(NomeCurto) ? Nome : NomeCurto!;
}

/// <summary>
/// Sala/consultório onde o atendimento acontece. É o segundo recurso disputado da
/// agenda: dois profissionais podem atender no mesmo horário, mas não na mesma sala.
/// </summary>
public class Sala
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Quantos atendimentos simultâneos a sala comporta (padrão 1).</summary>
    public int Capacidade { get; set; } = 1;

    public bool Ativa { get; set; } = true;

    public int Ordem { get; set; }

    public string? Observacoes { get; set; }
}

/// <summary>Turno preferido por quem está na lista de espera.</summary>
public enum PeriodoPreferido
{
    Qualquer,
    Manha,
    Tarde
}

/// <summary>Situação de um pedido na lista de espera.</summary>
public enum StatusListaEspera
{
    /// <summary>Ainda procurando horário.</summary>
    Aguardando,

    /// <summary>Virou agendamento (o horário vagou e o paciente foi encaixado).</summary>
    Agendado,

    /// <summary>Saiu da lista sem agendar (desistiu, resolveu em outro lugar…).</summary>
    Removido
}

/// <summary>
/// Paciente que quer atendimento e não achou horário. É o outro lado do encaixe: quando
/// alguém cancela ou falta, a recepção precisa saber, na hora, quem chamar para o buraco
/// — perguntar "quem estava querendo?" de cabeça é como a vaga se perde.
/// </summary>
public class ListaEspera
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Profissional desejado. Null = qualquer um (aumenta a chance de encaixe).</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Modalidade pretendida, pelo código do catálogo. Null = a definir.</summary>
    public string? ModalidadeCodigo { get; set; }

    public PeriodoPreferido Periodo { get; set; } = PeriodoPreferido.Qualquer;

    /// <summary>A partir de quando o paciente pode vir. Null = qualquer dia.</summary>
    public DateOnly? DisponivelDe { get; set; }

    /// <summary>Até quando faz sentido chamar (viagem, fim do tratamento…). Null = sem limite.</summary>
    public DateOnly? DisponivelAte { get; set; }

    /// <summary>Urgente vem antes na fila, independentemente da data de entrada.</summary>
    public bool Prioritario { get; set; }

    public string? Observacoes { get; set; }

    public StatusListaEspera Status { get; set; } = StatusListaEspera.Aguardando;

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    /// <summary>Quando saiu da lista (agendado ou removido).</summary>
    public DateTime? ResolvidoEm { get; set; }

    /// <summary>Agendamento gerado ao chamar o paciente da lista.</summary>
    public int? AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }

    /// <summary>Serve para o dia informado, considerando janela e período.</summary>
    public bool ServeParaDia(DateOnly dia)
        => Status == StatusListaEspera.Aguardando
           && (DisponivelDe is null || dia >= DisponivelDe)
           && (DisponivelAte is null || dia <= DisponivelAte);

    /// <summary>Serve para o horário informado (dia + turno).</summary>
    public bool ServeParaHorario(DateTime dataHora)
    {
        if (!ServeParaDia(DateOnly.FromDateTime(dataHora))) return false;
        return Periodo switch
        {
            PeriodoPreferido.Manha => dataHora.Hour < 12,
            PeriodoPreferido.Tarde => dataHora.Hour >= 12,
            _ => true
        };
    }
}

/// <summary>
/// Um período em que a agenda NÃO aceita marcação: férias, feriado, congresso, folga.
///
/// Até existir isto, a única coisa que segurava a marcação em cima da folga do
/// profissional era a memória de quem estava no balcão — e memória de balcão falha
/// justamente no dia cheio. O choque de horário já era detectado entre agendamentos; o
/// que faltava era a agenda saber que aquele intervalo não existe.
///
/// **Sem profissional e sem sala = a clínica inteira** (feriado). Com profissional, é a
/// agenda dele; com sala, é a sala em manutenção. Um bloqueio pode ter os dois, e aí vale
/// para aquele profissional naquela sala — o caso raro, mas que não custa nada permitir.
///
/// Ele NÃO apaga o que já estava marcado: bloquear o Natal depois de alguém ter marcado
/// no dia 25 não pode fazer a sessão sumir do sistema sem ninguém avisar o paciente. A
/// tela mostra quem já está marcado dentro do bloqueio, para a recepção remarcar.
/// </summary>
public class BloqueioAgenda
{
    public int Id { get; set; }

    /// <summary>De quem é a indisponibilidade. Null = de todo mundo (feriado).</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Sala indisponível (manutenção, reforma). Null = qualquer sala.</summary>
    public int? SalaId { get; set; }
    public Sala? Sala { get; set; }

    public DateTime Inicio { get; set; }

    public DateTime Fim { get; set; }

    /// <summary>Por que a agenda está fechada. Obrigatório: bloqueio sem motivo vira mistério.</summary>
    public string Motivo { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public string? CriadoPor { get; set; }

    /// <summary>Vale para a clínica inteira — não é de um profissional nem de uma sala.</summary>
    public bool DaClinica => ProfissionalId is null && SalaId is null;

    /// <summary>
    /// O intervalo informado esbarra neste bloqueio?
    ///
    /// Fim exclusivo dos dois lados: uma sessão que termina exatamente quando o bloqueio
    /// começa não colide — senão marcar até as 12h com bloqueio a partir das 12h seria
    /// recusado, e ninguém entenderia por quê.
    /// </summary>
    public bool ColideCom(DateTime inicio, DateTime fim) => inicio < Fim && fim > Inicio;

    /// <summary>
    /// Este bloqueio se aplica ao recurso que está sendo marcado?
    ///
    /// O da clínica se aplica a todo mundo. O do profissional só a ele — inclusive quando
    /// a marcação não informa profissional, caso em que não há como saber de quem é o
    /// horário e o bloqueio não impede.
    /// </summary>
    public bool AlcancaRecurso(int? profissionalId, int? salaId)
    {
        if (DaClinica) return true;
        if (ProfissionalId is { } p && profissionalId == p) return true;
        return SalaId is { } s && salaId == s;
    }

    /// <summary>Descrição pronta para a tela e para a mensagem de recusa.</summary>
    public string Descricao
    {
        get
        {
            var alvo = DaClinica
                ? "A clínica"
                : Profissional?.Rotulo ?? (Sala?.Nome is { } sala ? $"A sala {sala}" : "O recurso");

            var quando = Inicio.Date == Fim.Date
                ? $"{Inicio:dd/MM} das {Inicio:HH:mm} às {Fim:HH:mm}"
                : $"de {Inicio:dd/MM HH:mm} a {Fim:dd/MM HH:mm}";

            return $"{alvo} está indisponível {quando} — {Motivo}.";
        }
    }
}
