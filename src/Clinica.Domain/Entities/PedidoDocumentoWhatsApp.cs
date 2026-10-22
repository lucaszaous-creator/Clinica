namespace Clinica.Domain.Entities;

// O documento pedido pelo WhatsApp (@SemDor fase 4, out/2026 — docs/semdor-assistente.md).
// O médico escreve ao número da clínica "atestado Maria Silva 2 dias"; a ponte acha a paciente
// na agenda DELE e grava este RASCUNHO — nunca o documento. O documento só nasce no portal,
// depois de o médico abrir o link de uso único, revisar o texto e emitir (com ou sem A1),
// pelos MESMOS serviços do atendimento no tablet. A senha do A1 nunca passa pelo WhatsApp.
// Nada se apaga: o link vencido ou usado continua aqui, como trilha do pedido.

/// <summary>O rascunho de um documento pedido pelo WhatsApp, à espera da revisão no portal.</summary>
public sealed class PedidoDocumentoWhatsApp
{
    public int Id { get; set; }

    /// <summary>SHA-256 do token do link (o token em si só existe na mensagem ao médico).</summary>
    public string TokenHash { get; set; } = "";

    /// <summary>O id da mensagem da Meta: a mesma mensagem recebida duas vezes não gera dois links.</summary>
    public string IdMensagem { get; set; } = "";

    /// <summary>Quem pediu — e o único login que o link abre.</summary>
    public int UsuarioId { get; set; }

    public int PacienteId { get; set; }

    /// <summary>O horário da agenda do médico pelo qual a paciente foi achada (procedência).</summary>
    public int? AgendamentoId { get; set; }

    /// <summary>O tipo do tablet: atestado, comparecimento, receita, exame ou relatorio.</summary>
    public string Tipo { get; set; } = "";

    /// <summary>O texto proposto. O médico revisa e pode mudar antes de emitir.</summary>
    public string Texto { get; set; } = "";

    public int? DiasAfastamento { get; set; }

    public DateTime CriadoEm { get; set; }

    /// <summary>Depois disso o link não abre mais (15 minutos).</summary>
    public DateTime ExpiraEm { get; set; }

    /// <summary>Quando o link foi aberto. Preenchido uma vez só: é o "uso único".</summary>
    public DateTime? AbertoEm { get; set; }

    /// <summary>O documento emitido a partir deste rascunho.</summary>
    public int? DocumentoClinicoId { get; set; }

    /// <summary>Quando o médico terminou (assinado com A1 ou emitido sem assinatura digital).</summary>
    public DateTime? ConcluidoEm { get; set; }

    public bool AssinadoComA1 { get; set; }

    /// <summary>Quando o envio ao paciente foi posto na fila do CRM.</summary>
    public DateTime? EnviadoAoPacienteEm { get; set; }
}
