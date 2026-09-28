namespace Clinica.Domain.Entities;

public enum TipoAcompanhamento { NovoBsv, Recall }
public enum EtapaAcompanhamento { AContatar, SemResposta, RetornarNaData, AguardandoPlano, ProntoParaAgendar }

/// <summary>Uma jornada por paciente/modalidade. Agendamento e realização são derivados da agenda.</summary>
public sealed class AcompanhamentoPaciente
{
    public int Id { get; set; }
    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }
    public TipoAcompanhamento Tipo { get; set; }
    public ModalidadeAtendimento Modalidade { get; set; }
    public DateTime ReferenciaEm { get; set; }
    public int? AgendamentoOrigemId { get; set; }
    public int? ProfissionalId { get; set; }
    public int? ResponsavelId { get; set; }
    public UsuarioSistema? Responsavel { get; set; }
    public DateOnly ProximoContato { get; set; }
    public EtapaAcompanhamento Etapa { get; set; }
    public DateTime CriadoEm { get; set; }
    public string CriadoPor { get; set; } = "";
    public DateTime? EncerradoEm { get; set; }
    public string? MotivoEncerramento { get; set; }
    public string? EncerradoPor { get; set; }
    public int? MotivoId { get; set; }
    public MotivoAcompanhamento? Motivo { get; set; }
    public Guid Versao { get; set; } = Guid.NewGuid();
    public List<ContatoAcompanhamento> Contatos { get; set; } = [];
}

/// <summary>Histórico acrescentado, nunca sobrescrito ao virar o mês ou ao remarcar.</summary>
public sealed class ContatoAcompanhamento
{
    public int Id { get; set; }
    public int AcompanhamentoPacienteId { get; set; }
    public AcompanhamentoPaciente? AcompanhamentoPaciente { get; set; }
    public Guid Idempotencia { get; set; }
    public DateTime Em { get; set; }
    public string Operador { get; set; } = "";
    public CanalContato? Canal { get; set; }
    public string Resultado { get; set; } = "";
    public string Observacao { get; set; } = "";
    public DateOnly? ProximoContato { get; set; }
    public int? ResponsavelId { get; set; }
}

public sealed class MotivoAcompanhamento
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
}

public static class RegrasAcompanhamento
{
    public static bool Corresponde(TipoAcompanhamento tipo, ModalidadeAtendimento alvo, ModalidadeAtendimento modalidade)
        => tipo == TipoAcompanhamento.NovoBsv
            ? modalidade is ModalidadeAtendimento.BsvApenas or ModalidadeAtendimento.BsvComAcupuntura
            : modalidade == alvo;

    public static string Rotulo(EtapaAcompanhamento etapa) => etapa switch
    {
        EtapaAcompanhamento.AContatar => "A contatar",
        EtapaAcompanhamento.SemResposta => "Sem resposta",
        EtapaAcompanhamento.RetornarNaData => "Retornar na data combinada",
        EtapaAcompanhamento.AguardandoPlano => "Aguardando plano",
        EtapaAcompanhamento.ProntoParaAgendar => "Pronto para agendar",
        _ => throw new ArgumentOutOfRangeException(nameof(etapa))
    };
}
