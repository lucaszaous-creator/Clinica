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
        // A connection string vem da env var CLINICA_DB (nunca hardcoded/commitada).
        // Fallback local apenas para gerar migrations offline.
        var cs = Environment.GetEnvironmentVariable("CLINICA_DB")
                 ?? "Host=localhost;Database=clinica;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseNpgsql(cs)
            .Options;
        return new ClinicaDbContext(options);
    }
}
