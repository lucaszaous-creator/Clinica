namespace Clinica.Domain.Entities;

public sealed class MedicamentoCadastro
{
    public string Codigo { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? PrincipioAtivo { get; set; }
    public string? Apresentacao { get; set; }
    public string? Fabricante { get; set; }
    public string Fonte { get; set; } = "Cadastro da clínica";
    public bool Ativo { get; set; } = true;
    public DateTime AtualizadoEm { get; set; }
    public string? AtualizadoPor { get; set; }
}
