namespace Clinica.Domain.Entities;

/// <summary>Como o paciente disse que está depois da sessão — a resposta da Escuta pós-sessão.</summary>
public enum ComoEstaPaciente
{
    /// <summary>Disse que melhorou.</summary>
    Melhor,

    /// <summary>Disse que está igual.</summary>
    Igual,

    /// <summary>Disse que piorou.</summary>
    Pior,

    /// <summary>A clínica perguntou e o paciente não respondeu. Fecha a escuta sem dado de saúde nenhum.</summary>
    NaoRespondeu
}

/// <summary>Por que a sessão pede escuta: são os dois momentos em que um tratamento costuma se perder.</summary>
public enum MotivoDaEscuta
{
    /// <summary>A primeira sessão do tratamento (BSV ou acupuntura): é quando o paciente decide se continua.</summary>
    PrimeiraSessao,

    /// <summary>A sessão de volta de quem a retenção trouxe de volta (Fila 360): é quando se vê se ele fica.</summary>
    VoltouPelaRetencao
}

/// <summary>
/// A ESCUTA PÓS-SESSÃO (nov/2026, Fila 360): o que o paciente respondeu à pergunta "como você está depois da
/// sessão?", registrado por quem cuida da retenção.
///
/// ⚠️ É DADO DE SAÚDE (LGPD art. 5º, II) e REGISTRO CLÍNICO (Lei 13.787/2018). Daí as decisões:
/// <list type="number">
/// <item><b>Não se apaga nem se edita.</b> Registrou errado: CANCELA com motivo escrito e registra de novo. A linha
/// cancelada fica, marcada — por isso não há versão (nada é sobrescrito).</item>
/// <item><b>Ler e escrever pedem <see cref="Permissao.VerProntuario"/></b>, além do acesso do acompanhamento. Quem
/// só tem o cadastro vê a fila de sessões a escutar (dado de agenda) e não vê como o paciente está.</item>
/// <item><b>É raiz clínica</b>: entra em <c>PacienteTemRegistroClinicoAsync</c>, na exportação, na guarda e no
/// documento do titular (<see cref="Clinica.Domain.Prontuario.NaturezaRegistroClinico.EscutaPosSessao"/>).</item>
/// </list>
/// </summary>
public class EscutaPosSessao
{
    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>A sessão escutada (o atendimento realizado). Uma escuta VIGENTE por sessão.</summary>
    public int AtendimentoId { get; set; }

    /// <summary>O dia da sessão (cópia: a escuta se lê sem buscar o atendimento).</summary>
    public DateOnly SessaoEm { get; set; }

    public ModalidadeAtendimento Modalidade { get; set; }

    /// <summary>O profissional da sessão (contexto da escuta). Nulo: sessão sem profissional.</summary>
    public int? ProfissionalId { get; set; }

    public MotivoDaEscuta Motivo { get; set; }

    public ComoEstaPaciente Como { get; set; }

    /// <summary>O que o paciente disse, nas palavras dele (obrigatório quando piorou).</summary>
    public string? Relato { get; set; }

    /// <summary>Por onde a resposta veio.</summary>
    public CanalContato Canal { get; set; }

    public DateTime RegistradoEm { get; set; }

    /// <summary>O login de quem registrou (SessaoUsuario, nunca o usuário do Windows).</summary>
    public string RegistradoPor { get; set; } = string.Empty;

    public DateTime? CanceladaEm { get; set; }
    public string? CanceladaPor { get; set; }
    public string? MotivoCancelamento { get; set; }

    /// <summary>Derivada — nunca use em consulta traduzida (use a coluna <see cref="CanceladaEm"/>).</summary>
    public bool Cancelada => CanceladaEm is not null;
}
