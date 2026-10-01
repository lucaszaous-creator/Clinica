namespace Clinica.Domain.Entities;

/// <summary>Credencial removível, separada dos documentos clínicos já assinados. Nunca guarda senha.</summary>
public sealed class CertificadoA1Profissional
{
    public int UsuarioId { get; set; }
    public int ProfissionalId { get; set; }
    public byte[] ArquivoProtegido { get; set; } = [];
    public string ImpressaoDigital { get; set; } = "";
    public string Titular { get; set; } = "";
    public DateTime ValidoAte { get; set; }
    public Guid Versao { get; set; }
}
