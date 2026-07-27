namespace Clinica.Domain.Entities;

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
    /// Especialidade principal, pelo código do catálogo (<c>CatalogoEspecialidades</c>).
    /// Null quando o profissional não se enquadra em nenhuma das cadastradas.
    /// </summary>
    public string? EspecialidadeCodigo { get; set; }

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
