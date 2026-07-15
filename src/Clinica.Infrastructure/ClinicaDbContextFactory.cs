using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Clinica.Infrastructure;

/// <summary>
/// Usada pelo comando `dotnet ef` para gerar migrations em tempo de projeto.
/// Não precisa de banco vivo. Em produção a connection string vem da configuração do app.
/// </summary>
public sealed class ClinicaDbContextFactory : IDesignTimeDbContextFactory<ClinicaDbContext>
{
    public ClinicaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Clinica;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new ClinicaDbContext(options);
    }
}
