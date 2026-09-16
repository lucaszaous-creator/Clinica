namespace Clinica.Domain.Entities;

/// <summary>Recibo de escrita: uma repetição de rede não cria outro registro clínico.</summary>
public sealed class OperacaoClinicaTablet
{
    public Guid Id { get; set; }
    public int UsuarioId { get; set; }
    public int AgendamentoId { get; set; }
    public string PedidoHash { get; set; } = "";
    public string ResultadoJson { get; set; } = "";
    public long CriadaEm { get; set; }
}
