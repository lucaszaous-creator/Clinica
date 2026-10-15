using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// Uma semente de dados FICTÍCIOS da prévia. Cada tela escreve a sua num arquivo próprio;
/// a prévia acha todas por reflexão e roda na ordem de <see cref="Ordem"/> — zero é a base.
/// Nenhum arquivo comum precisa ser editado para acrescentar uma.
/// </summary>
public interface ISemente
{
    int Ordem { get; }
    void Semear(ClinicaDbContext db);
}
