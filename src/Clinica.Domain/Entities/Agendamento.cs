namespace Clinica.Domain.Entities;

/// <summary>Situação de um agendamento na agenda da recepção.</summary>
public enum StatusAgendamento
{
    Agendado,
    Realizado, // presença confirmada; gerou atendimento
    Cancelado,
    Faltou
}

/// <summary>Como o agendamento nasceu.</summary>
public enum OrigemAgendamento
{
    Manual,          // marcado pela secretária
    /// <summary>
    /// ⚠️ LEGADO — não se cria mais (parcela 58). O sistema materializava a pendência do
    /// 2º código como um horário na agenda, e isso confundia GUIA com ATENDIMENTO: punha
    /// na fila do balcão e na agenda dos médicos um paciente que não tem horário e não vai
    /// aparecer. O valor continua aqui porque é gravado como TEXTO e há linhas assim em
    /// produção — apagá-lo faria o EF quebrar ao lê-las. O selo "Retorno do 2º código"
    /// segue na tela justamente para a clínica reconhecer e limpar as que sobraram.
    /// </summary>
    RetornoSugerido,
    ListaEspera      // chamado da lista de espera para um horário que vagou
}

/// <summary>
/// Coluna do kanban da fila do dia. Não é persistida: é derivada dos carimbos de
/// hora do agendamento — assim o faturamento, que só conhece <see cref="StatusAgendamento"/>,
/// continua funcionando sem saber que o kanban existe.
/// </summary>
public enum EtapaFila
{
    /// <summary>Marcado, ainda não chegou ao balcão.</summary>
    Aguardando,

    /// <summary>Fez check-in na recepção e está esperando ser chamado.</summary>
    Chegou,

    /// <summary>
    /// O profissional avisou que quer este paciente agora, e a recepção ainda não o
    /// levou à sala (parcela 38). É o recado que atravessa os dois módulos: quem chama
    /// pelo nome na sala de espera é o balcão, não o médico.
    /// </summary>
    Chamado,

    /// <summary>Entrou; está na sala com o profissional.</summary>
    EmAtendimento,

    /// <summary>Atendimento encerrado — gerou o atendimento e os códigos.</summary>
    Finalizado,

    /// <summary>Faltou ou foi cancelado; fora do fluxo do dia.</summary>
    ForaDaFila
}

/// <summary>
/// Um horário marcado para o paciente. Ao confirmar a presença, gera o atendimento
/// (e os códigos de faturamento) automaticamente.
/// </summary>
public class Agendamento
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public DateTime DataHora { get; set; }

    public ModalidadeAtendimento ModalidadePrevista { get; set; }

    /// <summary>Código da modalidade no catálogo (identifica a variante/nome). Null = embutida.</summary>
    public string? ModalidadeCodigo { get; set; }

    /// <summary>Especialidade prevista quando a modalidade é Consulta (levada ao atendimento na confirmação).</summary>
    public Especialidade? EspecialidadeConsulta { get; set; }

    /// <summary>Código da especialidade no catálogo. Null = embutida.</summary>
    public string? EspecialidadeConsultaCodigo { get; set; }

    public StatusAgendamento Status { get; set; } = StatusAgendamento.Agendado;

    public OrigemAgendamento Origem { get; set; } = OrigemAgendamento.Manual;

    public string? Observacoes { get; set; }

    /// <summary>Preenchido quando a presença é confirmada e um atendimento é gerado.</summary>
    public int? AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    // ---------- Fundação da recepção (parcela 1) ----------
    // Tudo daqui para baixo é ADITIVO e anulável: o faturamento continua marcando
    // horário sem informar profissional nem sala, e os agendamentos que já existem
    // no banco seguem válidos com estes campos nulos.

    /// <summary>Quem vai atender. Null = agenda antiga/sem profissional definido.</summary>
    public int? ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>Onde vai atender. Null = sala a definir.</summary>
    public int? SalaId { get; set; }
    public Sala? Sala { get; set; }

    /// <summary>
    /// Duração prevista em minutos. Null usa <see cref="DuracaoPadraoMinutos"/> — ou a
    /// duração padrão do profissional, quando ele tiver uma.
    /// </summary>
    public int? DuracaoMinutos { get; set; }

    /// <summary>
    /// Encaixe: marcado por cima de um horário que já estava ocupado, com o choque
    /// aceito de propósito. Fica registrado para a agenda mostrar (e para o BI saber
    /// que a ocupação passou do previsto) em vez de virar um conflito silencioso.
    /// </summary>
    public bool Encaixe { get; set; }

    /// <summary>
    /// Sessões marcadas de uma vez (o pacote de 10) compartilham esta chave.
    ///
    /// É o que permite tratar a série como um bloco depois — cancelar as que sobraram
    /// quando o paciente desiste no meio, por exemplo. Null = horário avulso, que
    /// continua sendo a maioria; nenhum agendamento antigo precisa dela, e o
    /// faturamento congelado nunca a lê.
    ///
    /// Guardada como TEXTO e não como número: ela nasce no cliente (um Guid), e uma
    /// sequência do banco exigiria uma ida a mais só para descobrir o número da série.
    /// </summary>
    public string? SerieId { get; set; }

    /// <summary>Check-in no balcão: o paciente chegou. Base do tempo de espera.</summary>
    public DateTime? ChegadaEm { get; set; }

    /// <summary>
    /// O PROFISSIONAL avisou que quer este paciente agora (parcela 38).
    ///
    /// É diferente de <see cref="InicioAtendimentoEm"/>, e a diferença é a clínica real:
    /// quem chama pelo nome na sala de espera é a RECEPÇÃO, não o médico — ele está na
    /// sala com a porta fechada. Este carimbo é o recado que atravessa: o consultório
    /// marca "pode mandar entrar", o balcão vê e chama a pessoa. O atendimento só começa
    /// quando ela entra, e é aí que o outro carimbo cai.
    ///
    /// Sem os dois separados, o tempo de espera mediria a coisa errada: a espera do
    /// paciente termina quando ele é CHAMADO, e o intervalo entre a chamada e a entrada
    /// na sala é o tempo que ele levou para se levantar — não espera de clínica.
    /// </summary>
    public DateTime? ChamadoEm { get; set; }

    /// <summary>O paciente entrou na sala — começo da sessão.</summary>
    public DateTime? InicioAtendimentoEm { get; set; }

    /// <summary>Duração padrão da clínica quando ninguém informou nada.</summary>
    public const int DuracaoPadraoMinutos = 30;

    /// <summary>Duração efetiva: a do agendamento, a do profissional ou a da clínica.</summary>
    public int DuracaoEfetiva
        => DuracaoMinutos ?? Profissional?.DuracaoPadraoMinutos ?? DuracaoPadraoMinutos;

    /// <summary>Fim previsto do horário — o que faz a grade saber se dois se sobrepõem.</summary>
    public DateTime FimPrevisto => DataHora.AddMinutes(DuracaoEfetiva);

    /// <summary>Coluna do kanban, derivada do status e dos carimbos de hora.</summary>
    public EtapaFila Etapa => Status switch
    {
        StatusAgendamento.Realizado => EtapaFila.Finalizado,
        StatusAgendamento.Cancelado or StatusAgendamento.Faltou => EtapaFila.ForaDaFila,
        _ when InicioAtendimentoEm is not null => EtapaFila.EmAtendimento,
        _ when ChamadoEm is not null => EtapaFila.Chamado,
        _ when ChegadaEm is not null => EtapaFila.Chegou,
        _ => EtapaFila.Aguardando
    };

    /// <summary>
    /// Há quantos minutos o paciente foi chamado e ainda não entrou. Null quando não há
    /// chamada pendente.
    ///
    /// A recepção usa isto para insistir: chamada de dois minutos é a pessoa vindo do
    /// corredor; de dez, é alguém que saiu para o banheiro ou não ouviu — e o profissional
    /// está parado esperando.
    /// </summary>
    public int? ChamadoHaMinutos(DateTime agora)
    {
        if (ChamadoEm is null || InicioAtendimentoEm is not null) return null;
        var minutos = (int)Math.Round((agora - ChamadoEm.Value).TotalMinutes);
        return minutos < 0 ? 0 : minutos;
    }

    /// <summary>
    /// Há quanto tempo o paciente espera, em minutos: da chegada até ser CHAMADO (ou até
    /// <paramref name="agora"/>, se ainda não foi). Null enquanto não fez check-in.
    ///
    /// Termina na chamada, e não na entrada na sala: o que a clínica controla é quanto
    /// tempo alguém ficou sentado sem ser atendido. O minuto entre "pode entrar" e a
    /// pessoa levantar da cadeira não é fila — e contá-lo pioraria o indicador por algo
    /// que a recepção não tem como acelerar.
    /// </summary>
    public int? EsperaMinutos(DateTime agora)
    {
        if (ChegadaEm is null) return null;
        var fim = ChamadoEm ?? InicioAtendimentoEm ?? agora;
        var minutos = (int)Math.Round((fim - ChegadaEm.Value).TotalMinutes);
        return minutos < 0 ? 0 : minutos;
    }

    /// <summary>
    /// Este horário se sobrepõe ao intervalo informado? Comparação por intervalo (e não
    /// por igualdade de horário) — o choque real é a sessão de 30 min que invade a
    /// seguinte, não só o horário batido na mosca.
    /// </summary>
    public bool ColideCom(DateTime inicio, DateTime fim)
        => DataHora < fim && inicio < FimPrevisto;

    /// <summary>Ocupa a agenda (não foi cancelado nem faltou).</summary>
    public bool OcupaAgenda
        => Status is StatusAgendamento.Agendado or StatusAgendamento.Realizado;
}
