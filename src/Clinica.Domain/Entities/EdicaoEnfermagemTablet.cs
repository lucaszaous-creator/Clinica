namespace Clinica.Domain.Entities;

/// <summary>Reserva temporária de edição, sem conteúdo clínico.</summary>
public sealed class EdicaoEnfermagemTablet
{
    public int AgendamentoId { get; set; }
    public int PacienteId { get; set; }
    public int UsuarioId { get; set; }
    public string SessaoId { get; set; } = "";
    public Guid EditorId { get; set; }
    public long ExpiraEm { get; set; }
}
