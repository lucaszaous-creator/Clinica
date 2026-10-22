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
