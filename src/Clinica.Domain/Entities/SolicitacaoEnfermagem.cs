namespace Clinica.Domain.Entities;

/// <summary>Orientação operacional da equipe BSV da clínica; nunca reserva ou modifica a agenda.</summary>
public sealed class SolicitacaoEnfermagem
{
    public int Id { get; set; }
    public Guid Idempotencia { get; set; }
    public string PedidoHash { get; set; } = "";
    public int AutoraId { get; set; }
    public UsuarioSistema? Autora { get; set; }
    public DateTime? VistaEm { get; set; }
    public string VistaPor { get; set; } = "";
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public DateTime Validade { get; set; }
    public DateTime CriadaEm { get; set; }
    public ModalidadeAtendimento Modalidade { get; set; }
    public int Quantidade { get; set; }
    public string Observacao { get; set; } = "";
    public string Situacao { get; set; } = "Novo";
    public string AgendaReferencia { get; set; } = "";
    public int TotalReferencia { get; set; }
    public Guid Versao { get; set; } = Guid.NewGuid();
    public List<EventoSolicitacaoEnfermagem> Eventos { get; set; } = [];
}

public sealed class EventoSolicitacaoEnfermagem
{
    public int Id { get; set; }
    public int SolicitacaoEnfermagemId { get; set; }
    public SolicitacaoEnfermagem? Solicitacao { get; set; }
    public Guid Idempotencia { get; set; }
    public string PedidoHash { get; set; } = "";
    public int UsuarioId { get; set; }
    public string Autor { get; set; } = "";
    public DateTime Em { get; set; }
    public string Acao { get; set; } = "";
    public string Observacao { get; set; } = "";
}
