namespace Clinica.Domain.Entities;

/// <summary>
/// Um pedido de envio ao paciente que a CLÍNICA faz e o CRM executa (out/2026 — documentos
/// pelo WhatsApp oficial). É a metade clínica da fila que o CRM já consome
/// (<c>ClinicalDispatchWorker</c> no repositório semdor-crm, rotas <c>/crm/v1/dispatches</c>
/// da ponte).
///
/// O que a linha guarda e por quanto tempo
/// ---------------------------------------
/// O PDF vai junto (<see cref="Arquivo"/>), porque o CRM não lê o banco da clínica. Mas ele
/// é uma CÓPIA da 2ª via, não o registro clínico — o documento continua em
/// <c>DocumentosClinicos</c>, intocado. Por isso os bytes SAEM da linha assim que o pedido
/// deixa a fila (autorizado, bloqueado ou vencido): a cópia só existe enquanto alguém ainda
/// pode precisar dela para enviar. A linha em si fica — é a trilha de "quem pediu, para
/// onde, quando e o que o provedor respondeu".
///
/// O vocabulário de <see cref="Estado"/> é o do protocolo com o CRM (em inglês, igual nas
/// duas pontas): <c>queued</c>, <c>processing</c>, <c>sending</c>, <c>accepted</c>,
/// <c>delivered</c>, <c>seen</c>, <c>failed</c>, <c>uncertain</c>, <c>blocked</c>. A tela
/// traduz por <c>FilaEnviosCrm.Descrever</c>; a regra das transições mora lá também.
/// </summary>
public class EnvioPacienteCrm
{
    /// <summary>Identificador estável (GUID sem traços). É ele que o CRM usa como id da mensagem.</summary>
    public string Id { get; set; } = string.Empty;

    public int PacienteId { get; set; }
    public Paciente? Paciente { get; set; }

    /// <summary>O documento clínico que originou o pedido. Nulo para pedidos de outra origem.</summary>
    public int? DocumentoClinicoId { get; set; }
    public DocumentoClinico? DocumentoClinico { get; set; }

    /// <summary>Nome do paciente no momento do pedido (o CRM usa o primeiro nome no modelo aprovado).</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Telefone só com dígitos, com DDI (10 a 15 dígitos).</summary>
    public string Telefone { get; set; } = string.Empty;

    /// <summary>E-mail do destinatário. Não há envio por e-mail (out/2026): fica sempre nulo.</summary>
    public string? Email { get; set; }

    /// <summary>"whatsapp" (o único canal que a clínica põe nesta fila hoje).</summary>
    public string Canal { get; set; } = "whatsapp";

    /// <summary>De onde veio o pedido ("DocumentoClinico"). O CRM mostra e escolhe a fila da conversa por ele.</summary>
    public string Origem { get; set; } = string.Empty;

    /// <summary>Quem pediu — o login da sessão (<c>SessaoUsuario.Atual.Operador</c>).</summary>
    public string Operador { get; set; } = string.Empty;

    public string Estado { get; set; } = "queued";

    /// <summary>A frase do último estado (o motivo do bloqueio, a resposta do provedor).</summary>
    public string Detalhe { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    /// <summary>Depois disto o CRM não envia mais (24 h): documento conferido hoje não sai amanhã sem ninguém olhar.</summary>
    public DateTime ExpiraEm { get; set; }

    /// <summary>Última mudança — a fila é entregue ao CRM em lotes, a partir da linha mais antiga nisto.</summary>
    public DateTime AtualizadoEm { get; set; }

    /// <summary>A legenda do PDF (dentro da janela de 24 h do WhatsApp). Sem dado clínico além do nome do papel.</summary>
    public string? Texto { get; set; }

    public string? NomeArquivo { get; set; }

    /// <summary>Os bytes da 2ª via. Nulo depois que o pedido sai da fila (ver o resumo da classe).</summary>
    public byte[]? Arquivo { get; set; }

    public string? Assunto { get; set; }

    /// <summary>O CRM que reservou o pedido (identificador opaco do consumidor).</summary>
    public string? Consumidor { get; set; }

    public DateTime? ReservadoEm { get; set; }

    /// <summary>
    /// Quando a autorização FINAL foi dada. É de uso único: preenchido, nenhum outro pedido
    /// de autorização é aceito — o que garante que o provedor é chamado no máximo uma vez.
    /// </summary>
    public DateTime? SubmetidoEm { get; set; }

    /// <summary>Concorrência otimista: duas reservas ou duas autorizações simultâneas, só uma grava.</summary>
    public int Versao { get; set; }
}
