namespace Clinica.Domain.Entities;

/// <summary>
/// O dia do CRM visto pela direção (out/2026, etapa "CRM no Gerente", caminho A). O CRM
/// calcula estes números a partir das conversas dele e a ponte grava UMA linha por dia; o
/// Gerente só lê. Nenhum dado de paciente entra aqui — só contagens.
///
/// A linha do dia é SUBSTITUÍDA a cada envio mais novo: o CRM manda o dia de hoje (parcial)
/// várias vezes e o de ontem (fechado) mais uma, e o que vale é o último que ele calculou
/// (<see cref="GeradoEm"/>). Envio mais velho que o gravado é ignorado — chegou fora de ordem.
///
/// Nulo quer dizer "não medido", nunca zero: dia sem resposta humana não tem tempo de
/// resposta, e escrever 0 diria que a recepção respondeu na hora.
/// </summary>
public class ResumoCrmDiario
{
    public int Id { get; set; }

    /// <summary>O dia, no fuso da clínica. Único.</summary>
    public DateOnly Dia { get; set; }

    /// <summary>Quando o CRM calculou estes números (hora da clínica).</summary>
    public DateTime GeradoEm { get; set; }

    /// <summary>Quando a ponte gravou (hora da clínica). Separado de <see cref="GeradoEm"/>: a diferença é atraso de rede.</summary>
    public DateTime RecebidoEm { get; set; }

    public int ConversasIniciadas { get; set; }
    public int ConversasEncerradas { get; set; }
    /// <summary>Conversas abertas no fim do dia (no dia de hoje: agora).</summary>
    public int ConversasAbertas { get; set; }

    public int MensagensRecebidas { get; set; }
    public int MensagensEnviadas { get; set; }

    /// <summary>Mediana do tempo até a primeira resposta da recepção, em segundos. Nula sem resposta medida.</summary>
    public int? RespostaMedianaSegundos { get; set; }
    /// <summary>Quantas respostas entraram na mediana — a base dela.</summary>
    public int RespostasMedidas { get; set; }

    public int SmsEnviados { get; set; }
    /// <summary>Segmentos cobrados pelo provedor (um SMS longo ou com acento vira vários).</summary>
    public int SmsSegmentos { get; set; }
    public int SmsFalhas { get; set; }

    public int ConfirmacoesPedidas { get; set; }
    public int ConfirmacoesRespondidas { get; set; }

    /// <summary>Atendimentos encerrados como resolvidos, agendados ou sem resposta do paciente.</summary>
    public int Resolvidas { get; set; }
    public int Agendadas { get; set; }
    public int SemResposta { get; set; }
}
