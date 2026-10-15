using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O banco avisa quando a agenda ou um pedido da enfermagem muda (migration
/// <c>AvisosInstantaneos</c>, out/2026). Só o Postgres executa o gatilho; no SQLite o teste
/// passa sem fazer nada, como os outros testes de migration real.
/// </summary>
public sealed class AvisosInstantaneosTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly ClinicaDbContext _db;

    public AvisosInstantaneosTests()
    {
        _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Postgres_gravar_um_horario_avisa_o_canal_com_o_nome_da_tabela_e_so_no_commit()
    {
        if (!BancoDosTestes.NoPostgres) return;

        // A string do EF guarda a senha; a da conexão já aberta, não.
        await using var ouvinte = new NpgsqlConnection(_db.Database.GetConnectionString());
        var recebidos = new List<string>();
        ouvinte.Notification += (_, e) => { lock (recebidos) recebidos.Add(e.Payload); };
        await ouvinte.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"LISTEN {OuvinteDeMudancas.Canal}", ouvinte))
            await cmd.ExecuteNonQueryAsync();

        var paciente = new Paciente { Nome = "Paciente fictício", Convenio = Convenio.UnimedIntercambio };
        _db.Add(paciente);
        await _db.SaveChangesAsync();

        // Dentro de uma transação aberta o aviso ainda não sai.
        await using (var transacao = await _db.Database.BeginTransactionAsync())
        {
            _db.Add(new Agendamento { PacienteId = paciente.Id, DataHora = new DateTime(2026, 10, 20, 9, 0, 0),
                Status = StatusAgendamento.Agendado });
            await _db.SaveChangesAsync();
            (await ouvinte.WaitAsync(TimeSpan.FromMilliseconds(300))).Should().BeFalse();
            await transacao.CommitAsync();
        }

        (await ouvinte.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeTrue();
        lock (recebidos) recebidos.Should().Contain("Agendamentos");
    }

    [Fact]
    public void Aviso_repassado_chega_a_quem_escuta_e_some_de_quem_saiu()
    {
        var vistos = new List<string>();
        void Ouvir(string t) => vistos.Add(t);
        OuvinteDeMudancas.Mudou += Ouvir;
        OuvinteDeMudancas.Avisar("Agendamentos");
        OuvinteDeMudancas.Mudou -= Ouvir;
        OuvinteDeMudancas.Avisar("Agendamentos");
        vistos.Should().Equal("Agendamentos");
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }
}
