using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Clinica.Tests;

public class ZzSqlProva
{
    private readonly ITestOutputHelper _o;
    public ZzSqlProva(ITestOutputHelper o) => _o = o;

    [Fact]
    public void Sql_do_historico_no_npgsql()
    {
        var opts = new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseNpgsql("Host=localhost;Database=x;Username=u;Password=p").Options;
        using var db = new ClinicaDbContext(opts);
        var sql = db.Atendimentos.AsNoTracking()
            .Where(a => a.PacienteId == 1 && a.RealizadoEm != null)
            .GroupBy(_ => 1)
            .Select(g => new { Primeira = (DateOnly?)g.Min(a => a.Data), Total = g.Count() })
            .ToQueryString();
        throw new Xunit.Sdk.XunitException("SQL>>> " + sql);
        _o.WriteLine(sql);
    }
}
