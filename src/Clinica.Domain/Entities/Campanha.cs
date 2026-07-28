namespace Clinica.Domain.Entities;

/// <summary>Por que a clínica está falando com o paciente.</summary>
public enum TipoContato
{
    /// <summary>Confirmar a sessão marcada (transacional — não é marketing).</summary>
    ConfirmacaoSessao,

    /// <summary>Pesquisa de satisfação depois do atendimento.</summary>
    Nps,

    /// <summary>Chamar de volta quem parou de vir.</summary>
    Recall
}

/// <summary>Em que pé está o contato.</summary>
public enum StatusContato
{
    /// <summary>Gerado pela campanha, ainda não falaram com o paciente.</summary>
    Pendente,

    /// <summary>Mensagem enviada; esperando (ou não) resposta.</summary>
    Enviado,

    /// <summary>O paciente respondeu — no NPS, a nota está em <see cref="ContatoCampanha.Nota"/>.</summary>
    Respondido,

    /// <summary>Saiu da campanha sem envio (sem telefone, pediu para não receber, veio antes…).</summary>
    Dispensado
}

/// <summary>Por onde a clínica falou com o paciente.</summary>
public enum CanalContato
{
    WhatsApp,
    Telefone,
    Email,
    Presencial
}

/// <summary>Faixa do respondente na metodologia NPS.</summary>
public enum ClasseNps
{
    /// <summary>0 a 6 — insatisfeito.</summary>
    Detrator,

    /// <summary>7 e 8 — satisfeito, mas não recomenda.</summary>
    Neutro,

    /// <summary>9 e 10 — recomenda.</summary>
    Promotor
}

/// <summary>
/// Um contato da clínica com o paciente dentro de uma campanha: a confirmação da
/// sessão de amanhã, o NPS depois do atendimento, o recall de quem sumiu.
///
/// Os três são UMA entidade de propósito: o fato registrado é o mesmo — "falamos (ou
/// vamos falar) com este paciente, por este motivo, e ele respondeu isto". Três
/// tabelas quase idênticas dariam três telas, três consultas e três lugares para
/// esquecer de checar o consentimento. O que varia entre eles é o <see cref="Tipo"/>
/// e, no NPS, a <see cref="Nota"/>.
///
/// <see cref="Origem"/> é a chave de idempotência: rodar a campanha duas vezes no
/// mesmo dia não pode gerar dois contatos para a mesma sessão — numa clínica pequena
/// isso vira mensagem duplicada no WhatsApp do paciente.
/// </summary>
public class ContatoCampanha
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    public TipoContato Tipo { get; set; }

    public StatusContato Status { get; set; } = StatusContato.Pendente;

    public CanalContato Canal { get; set; } = CanalContato.WhatsApp;

    /// <summary>
    /// Identifica o FATO que gerou o contato (ex.: <c>AGD:1234</c>, <c>ATD:987</c>,
    /// <c>REC:55:2026-07</c>). Único junto com <see cref="Tipo"/> — é o que impede a
    /// campanha de duplicar quando roda de novo.
    /// </summary>
    public string Origem { get; set; } = string.Empty;

    /// <summary>Sessão a confirmar, quando o contato nasceu da agenda.</summary>
    public int? AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }

    /// <summary>Atendimento avaliado, quando o contato é NPS.</summary>
    public int? AtendimentoId { get; set; }
    public Atendimento? Atendimento { get; set; }

    /// <summary>
    /// Dia a que o contato se refere: a data da sessão (confirmação/NPS) ou a data de
    /// corte da rodada (recall). É por ele que a tela filtra o período.
    /// </summary>
    public DateOnly Referencia { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public DateTime? EnviadoEm { get; set; }

    /// <summary>Quem disparou a mensagem (usuário da suíte).</summary>
    public string? EnviadoPor { get; set; }

    public DateTime? RespondidoEm { get; set; }

    /// <summary>Nota do NPS, de 0 a 10. Null nos outros tipos e enquanto não respondeu.</summary>
    public int? Nota { get; set; }

    /// <summary>O que o paciente respondeu (ou o motivo da dispensa).</summary>
    public string? Comentario { get; set; }

    public const int NotaMinima = 0;
    public const int NotaMaxima = 10;

    /// <summary>Faixa NPS da nota registrada. Null enquanto não há nota.</summary>
    public ClasseNps? Classe => Classificar(Nota);

    /// <summary>Regra da metodologia NPS: 0–6 detrator, 7–8 neutro, 9–10 promotor.</summary>
    public static ClasseNps? Classificar(int? nota) => nota switch
    {
        null => null,
        <= 6 => ClasseNps.Detrator,
        <= 8 => ClasseNps.Neutro,
        _ => ClasseNps.Promotor
    };

    public static bool NotaValida(int? nota)
        => nota is null || (nota >= NotaMinima && nota <= NotaMaxima);

    // ---- Chaves de origem (um lugar só, para gerador e conferência não divergirem) ----

    public static string OrigemDeAgendamento(int agendamentoId) => $"AGD:{agendamentoId}";

    public static string OrigemDeAtendimento(int atendimentoId) => $"ATD:{atendimentoId}";

    /// <summary>
    /// Recall é por paciente e por MÊS: quem continua sumido entra de novo na rodada do
    /// mês seguinte, mas nunca duas vezes na mesma.
    /// </summary>
    public static string OrigemDeRecall(int pacienteId, DateOnly referencia)
        => $"REC:{pacienteId}:{referencia:yyyy-MM}";

    /// <summary>Já saiu do fluxo (respondido ou dispensado)?</summary>
    public bool Encerrado => Status is StatusContato.Respondido or StatusContato.Dispensado;
}
