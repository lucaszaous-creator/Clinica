namespace Clinica.Domain.Entities;

/// <summary>Texto de apoio compartilhado pela equipe; a evolução registrada guarda sua própria cópia.</summary>
public sealed class ModeloEvolucaoEnfermagem
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string NomeChave { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public Guid Versao { get; set; } = Guid.NewGuid();
    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public DateTime AtualizadoEm { get; set; } = DateTime.Now;
    public string CriadoPor { get; set; } = string.Empty;
    public string AtualizadoPor { get; set; } = string.Empty;
}
