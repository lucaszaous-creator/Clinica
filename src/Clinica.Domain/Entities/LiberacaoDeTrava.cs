namespace Clinica.Domain.Entities;

/// <summary>
/// PEDIDO DE LIBERAÇÃO DA TRAVA DE CONSULTA (nov/2026, pedido da gerência).
///
/// O convênio só paga uma consulta a cada N dias (a "validade da consulta" do cadastro:
/// Unimed 22, Amil e Petrobras 30…). Com a trava ligada, marcar outra consulta pelo convênio
/// dentro desse prazo é recusado — e a saída, quando a consulta PRECISA acontecer, é a
/// gerência autorizar. Esta linha é o pedido: quem pediu, por quê, o horário pretendido
/// (inteiro, para o sistema marcar sozinho quando a resposta chegar) e quem decidiu.
///
/// ⚠️ Não se apaga: é a trilha de quem liberou uma consulta que o convênio pode glosar.
/// Os estados são texto (<see cref="LiberacaoDeTrava.Pendente"/>…), pela lição da parcela
/// 67 — valor novo de enum numérico é a única coisa que o app antigo não lê.
/// </summary>
public class LiberacaoDeTrava
{
    public const string Pendente = "pendente";
    public const string Liberada = "liberada";
    public const string Negada = "negada";
    public const string Expirada = "expirada";

    public int Id { get; set; }

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>O nome do paciente e do convênio COMO ERAM no pedido — a mensagem à gerência os cita.</summary>
    public string PacienteNome { get; set; } = string.Empty;
    public string ConvenioNome { get; set; } = string.Empty;

    // ---------- o horário pretendido (o que o sistema marca quando a gerência libera) ----------

    public DateTime DataHora { get; set; }
    public int? ProfissionalId { get; set; }
    public string ProfissionalNome { get; set; } = string.Empty;
    public int? SalaId { get; set; }
    public int? DuracaoMinutos { get; set; }
    public string ModalidadeCodigo { get; set; } = string.Empty;
    public string? EspecialidadeConsultaCodigo { get; set; }
    public bool Encaixe { get; set; }
    public bool ColetaPreventivo { get; set; }
    public string? Observacoes { get; set; }

    /// <summary>
    /// O pedido nasceu de uma REMARCAÇÃO: liberado, é ESTE horário que muda de dia — marcar um
    /// novo deixaria o antigo de pé e o paciente com dois. Nulo = horário novo.
    /// </summary>
    public int? AgendamentoARemarcarId { get; set; }

    // ---------- a trava que o pedido quer atravessar ----------

    /// <summary>O prazo do convênio, em dias, no momento do pedido.</summary>
    public int PrazoDias { get; set; }

    /// <summary>A consulta que acende a trava (a mais próxima do horário pretendido).</summary>
    public DateTime ConsultaAnteriorEm { get; set; }

    /// <summary>O número do atendimento dela, quando já existe (nulo = só marcada).</summary>
    public string? AtendimentoAnteriorNumero { get; set; }

    /// <summary>Por que a consulta precisa acontecer antes do prazo — obrigatório.</summary>
    public string Motivo { get; set; } = string.Empty;

    // ---------- quem pediu, quem decidiu ----------

    public int PedidoPorId { get; set; }
    public string PedidoPorNome { get; set; } = string.Empty;
    public DateTime PedidoEm { get; set; }

    /// <summary>Sem resposta até aqui, o pedido expira e nada é marcado.</summary>
    public DateTime ExpiraEm { get; set; }

    public string Estado { get; set; } = Pendente;

    public int? DecididoPorId { get; set; }
    public string? DecididoPorNome { get; set; }
    public DateTime? DecididoEm { get; set; }

    /// <summary>Por onde a decisão chegou: "whatsapp" ou "app" (o chat do sistema).</summary>
    public string? DecididoPor { get; set; }

    /// <summary>O horário que a liberação marcou. Nulo = negada, expirada, ou a marcação falhou.</summary>
    public int? AgendamentoId { get; set; }

    /// <summary>O que aconteceu ao marcar depois de liberado ("marcado", ou a recusa da agenda).</summary>
    public string? Desfecho { get; set; }
}

/// <summary>
/// Uma MENSAGEM de WhatsApp que o pedido de liberação manda à EQUIPE: à gerência (o pedido) e a
/// quem pediu (o desfecho). Quem envia é o CRM, pela fila de avisos da equipe que ele já lê na
/// ponte — esta linha é só a fila.
///
/// O pedido à gerência vai como MODELO aprovado pela Meta (<see cref="Modelo"/>), com os dados
/// da consulta nos parâmetros e os botões Liberar/Negar: modelo é o que chega FORA da janela
/// de 24 h, e a gerência não escreve ao número da clínica todo dia. Sem o modelo aprovado, o
/// CRM manda o <see cref="Texto"/> — que só chega dentro da janela. Entregue ou não, a linha é
/// marcada como tratada (repetir não muda a resposta da Meta); o pedido continua na conversa
/// com o sistema, no app.
///
/// A confirmação ao PACIENTE não passa por aqui: o horário liberado é marcado pela agenda, e
/// quem avisa é a mensagem de agendamento que o CRM já manda para todo horário novo.
/// </summary>
public class AvisoDeLiberacao
{
    public int Id { get; set; }
    public int LiberacaoDeTravaId { get; set; }
    public LiberacaoDeTrava? Liberacao { get; set; }

    /// <summary>O nome do modelo da Meta; nulo = mensagem livre (o desfecho a quem pediu).</summary>
    public string? Modelo { get; set; }

    /// <summary>Os valores dos campos {{1}}…{{n}} do modelo, em JSON (lista de textos).</summary>
    public string? Parametros { get; set; }

    /// <summary>O que cada botão do modelo devolve quando tocado ("liberar 12", "negar 12"), em JSON.</summary>
    public string? Botoes { get; set; }

    /// <summary>Chave canônica (55 + DDD + número).</summary>
    public string Telefone { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
    public DateTime? TratadoEm { get; set; }
    public bool Entregue { get; set; }
    public string? Motivo { get; set; }
}
