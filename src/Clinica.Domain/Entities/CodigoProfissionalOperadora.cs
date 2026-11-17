namespace Clinica.Domain.Entities;

/// <summary>
/// O código de um profissional numa operadora (nov/2026, Amil): o "código de credenciado" que o portal
/// pede na equipe da guia SP/SADT. Opcional; quando existe, vai no lugar do CPF do profissional.
/// Um por profissional e operadora (a família do convênio no catálogo, ex.: "Amil").
/// </summary>
public class CodigoProfissionalOperadora
{
    public int ProfissionalId { get; set; }
    public Profissional? Profissional { get; set; }

    /// <summary>A operadora pela família do convênio (<c>Convenio</c>, ex.: "Amil").</summary>
    public string Operadora { get; set; } = string.Empty;

    /// <summary>O código (até 14 caracteres, <c>st_texto14</c>).</summary>
    public string Codigo { get; set; } = string.Empty;
}
