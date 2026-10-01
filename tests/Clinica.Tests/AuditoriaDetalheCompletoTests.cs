using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Clinica.Tests;

public class AuditoriaDetalheCompletoTests
{
    [Fact]
    public async Task Migracao_preserva_historico_e_recusa_reversao_que_truncaria_detalhes()
    {
        if (!BancoDosTestes.NoPostgres) return; // Executado pelo job PostgreSQL, com migrations reais.
        using var conexao = new SqliteConnection("DataSource=:memory:");
        conexao.Open();
        using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(conexao).Options);
        var migrador = db.GetService<IMigrator>();
        const string anterior = "20261001015315_ConclusaoAutomaticaSafeId";
        const string atual = "20261001180000_AuditoriaPreservaDetalheCompleto";

        var antigo = new EventoAuditoria { Acao = "TesteHistorico", Detalhe = "Registro anterior preservado." };
        db.Auditoria.Add(antigo);
        await db.SaveChangesAsync();
        await migrador.MigrateAsync(anterior);
        await migrador.MigrateAsync(atual);
        (await db.Auditoria.AsNoTracking().SingleAsync(e => e.Id == antigo.Id)).Detalhe.Should().Be(antigo.Detalhe);

        var detalhe = "Contexto da execução · " + new string('á', 1000);
        var novo = new EventoAuditoria { Acao = "TesteJustificativa", Detalhe = detalhe };
        db.Auditoria.Add(novo);
        await db.SaveChangesAsync();
        var reverter = () => migrador.MigrateAsync(anterior);
        await reverter.Should().ThrowAsync<PostgresException>().Where(e => e.SqlState == "P0001");
        (await db.Auditoria.AsNoTracking().SingleAsync(e => e.Id == novo.Id)).Detalhe.Should().Be(detalhe);
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(atual);
    }
}
