namespace Clinica.Domain.Entities;

// O PEDIDO À RECEPÇÃO (out/2026 — chat da equipe integrado ao CRM).
//
// Quem atende (o médico, a enfermagem) pede à recepção um gesto sobre a agenda de um paciente:
// remarcar, cancelar, encaixar, avisar algo ao paciente. O pedido NÃO mexe na agenda — quem
// remarca é a recepção, com as regras da agenda (AgendaService), pelo balcão ou pelo CRM. O
// pedido é o RECADO com dono e desfecho: nasce Aberto, alguém o assume, e termina Concluído
// (feito) ou Não feito (com o motivo). Quem pediu recebe o desfecho no chat, sem perguntar.
//
// Nada se apaga: o pedido muda de situação e cada mudança fica em EventoPedidoRecepcao.

/// <summary>Um pedido de quem atende à recepção, sobre a agenda de um paciente.</summary>
public sealed class PedidoRecepcao
{
    public int Id { get; set; }

    /// <summary>A chave do comando que criou o pedido: o segundo clique (ou a resposta perdida) não cria outro.</summary>
    public Guid Idempotencia { get; set; }

    /// <summary>"remarcar", "cancelar", "encaixar", "avisar" ou "outro" (<c>PedidosRecepcaoRegras.Tipos</c>).</summary>
    public string Tipo { get; set; } = string.Empty;

    public int? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>Nome do paciente COPIADO no pedido: o cartão não precisa ler a ficha para se desenhar.</summary>
    public string PacienteNome { get; set; } = string.Empty;

    /// <summary>O horário de que se trata, quando o pedido é sobre um horário marcado.</summary>
    public int? AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }

    /// <summary>A hora do horário quando o pedido foi feito — remarcado depois, o cartão ainda diz de qual se falava.</summary>
    public DateTime? HorarioEm { get; set; }

    /// <summary>O que quem pediu escreveu ("para a semana que vem, de manhã").</summary>
    public string Detalhe { get; set; } = string.Empty;

    public int CriadoPorId { get; set; }
    public UsuarioSistema? CriadoPor { get; set; }
    public string CriadoPorNome { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }

    /// <summary>"Aberto", "EmAndamento", "Concluido" ou "NaoFeito" (<c>PedidosRecepcaoRegras</c>).</summary>
    public string Situacao { get; set; } = "Aberto";

    public int? ResponsavelId { get; set; }
    public string ResponsavelNome { get; set; } = string.Empty;
    public DateTime? AssumidoEm { get; set; }

    /// <summary>O que a recepção fez (ou por que não fez) — vai a quem pediu.</summary>
    public string Desfecho { get; set; } = string.Empty;
    public DateTime? EncerradoEm { get; set; }

    /// <summary>O cartão do pedido no # Recepção (o chat o atualiza quando a situação muda).</summary>
    public int? MensagemRecepcaoId { get; set; }

    /// <summary>O cartão do pedido na conversa de quem pediu com o Sistema SemDor.</summary>
    public int? MensagemAutorId { get; set; }

    /// <summary>Concorrência otimista: dois cliques de pessoas diferentes não assumem o mesmo pedido.</summary>
    public Guid Versao { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Quando o CRM tratou o aviso do desfecho pelo WhatsApp de quem pediu (entregue ou não — o
    /// resultado fica nos eventos). Nulo com o pedido encerrado = o aviso ainda está na fila.
    /// </summary>
    public DateTime? AvisoWhatsAppEm { get; set; }

    // ------------------------------------------------ o pedido que vem do PACIENTE (@SemDor, out/2026)
    // O paciente toca "Remarcar" no lembrete do WhatsApp e o pedido nasce sem pessoa da equipe por
    // trás: o autor é o usuário técnico do SemDor (UsuarioTecnico), e estes campos guardam a conversa
    // com o paciente — os horários oferecidos, o que ele escolheu e a mensagem que falta mandar.

    /// <summary>"equipe" (alguém pediu no chat ou no WhatsApp da equipe) ou "paciente" (pelo lembrete).</summary>
    public string Origem { get; set; } = "equipe";

    /// <summary>O WhatsApp do paciente quando o pedido veio dele (55 + DDD + número): é para onde a oferta vai.</summary>
    public string TelefonePaciente { get; set; } = string.Empty;

    /// <summary>Os horários que a recepção ofereceu, "yyyy-MM-ddTHH:mm" separados por ";" (até 3, o teto dos botões).</summary>
    public string HorariosOferecidos { get; set; } = string.Empty;

    /// <summary>A chave da oferta que vai nos botões: a resposta de uma oferta velha não vale para a nova.</summary>
    public string? OfertaToken { get; set; }
    public DateTime? OferecidoEm { get; set; }

    /// <summary>Quando o CRM tratou a oferta (entregue ou não). Nulo com oferta feita = ainda na fila.</summary>
    public DateTime? OfertaTratadaEm { get; set; }

    /// <summary>Por que a oferta não chegou ao paciente ("janela de 24 h fechada"); vazio quando chegou.</summary>
    public string OfertaFalha { get; set; } = string.Empty;

    public DateTime? HorarioEscolhido { get; set; }
    public DateTime? EscolhidoEm { get; set; }

    /// <summary>A mensagem ao paciente que falta sair ("sua sessão foi remarcada para…"); vazio = nada.</summary>
    public string RespostaAoPaciente { get; set; } = string.Empty;
    public DateTime? RespostaTratadaEm { get; set; }

    public List<EventoPedidoRecepcao> Eventos { get; set; } = [];
}

/// <summary>Uma mudança de situação do pedido: quem, quando e o que fez.</summary>
public sealed class EventoPedidoRecepcao
{
    public int Id { get; set; }
    public int PedidoRecepcaoId { get; set; }
    public PedidoRecepcao? Pedido { get; set; }
    public int UsuarioId { get; set; }
    public string Autor { get; set; } = string.Empty;
    public DateTime Em { get; set; }

    /// <summary>"criou", "assumiu", "concluiu", "nao-feito", "reabriu".</summary>
    public string Acao { get; set; } = string.Empty;
    public string Observacao { get; set; } = string.Empty;
}
