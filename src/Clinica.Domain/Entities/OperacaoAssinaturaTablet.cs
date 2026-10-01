namespace Clinica.Domain.Entities;

/// <summary>Histórico das operações anteriores. Mantém o mapeamento para preservar a tabela nas migrations; não inicia novas assinaturas.</summary>
public sealed class OperacaoAssinaturaTablet
{
    public Guid Id { get; set; }
    public string SessaoId { get; set; } = "";
    public string Tipo { get; set; } = "";
    public int Agendamento { get; set; }
    public int Documento { get; set; }
    public string Situacao { get; set; } = "aguardando";
    public string? ChaveAtiva { get; set; }
    public long ExpiraEm { get; set; }
    public long AtualizadaEm { get; set; }
}
