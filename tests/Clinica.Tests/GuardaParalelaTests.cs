using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O retrato da guarda lê vários pacientes ao mesmo tempo (out/2026) — e o número tem de ser o
/// MESMO da leitura um a um, que é a que a tela de conformidade sempre mostrou.
/// </summary>
public sealed class GuardaParalelaTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");

    public GuardaParalelaTests()
    {
        _conn.Open();
        using var db = Novo();
        db.Database.EnsureCreated();
    }

    private ClinicaDbContext Novo() => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);

    /// <summary>Um contexto novo por leitura, como o escopo do app; serializado (SQLite é uma conexão).</summary>
    private sealed class PorEscopo(Func<ClinicaDbContext> novo) : ILeituraParalela
    {
        private readonly SemaphoreSlim _fila = new(1, 1);
        public int Leituras;
        public Task<T> LerAsync<T>(Func<IClinicaRepositorio, Task<T>> leitura) => LerAsync<IClinicaRepositorio, T>(leitura);
        public async Task<T> LerAsync<TServico, T>(Func<TServico, Task<T>> leitura) where TServico : notnull
        {
            await _fila.WaitAsync();
            try
            {
                Interlocked.Increment(ref Leituras);
                using var db = novo();
                var repo = new ClinicaRepositorio(db);
                object servico = typeof(TServico) == typeof(GuardaProntuarioService) ? new GuardaProntuarioService(repo) : repo;
                return await leitura((TServico)servico);
            }
            finally { _fila.Release(); }
        }
    }

    [Fact]
    public async Task Resumo_em_paralelo_e_igual_ao_resumo_um_a_um()
    {
        await using (var db = Novo())
        {
            for (var i = 0; i < 9; i++)
            {
                var p = new Paciente { Nome = $"Paciente {i}", Convenio = Convenio.UnimedIntercambio };
                db.Pacientes.Add(p);
                await db.SaveChangesAsync();
                // Um sem registro, os outros com datas espalhadas — inclusive mais de 20 anos atrás.
                for (var s = 0; s < i % 4; s++)
                    db.Evolucoes.Add(new Evolucao { PacienteId = p.Id, Data = new DateOnly(2000 + i * 3, 1 + s, 10), TextoEvolucao = "Sessão" });
            }
            await db.SaveChangesAsync();
        }

        var hoje = new DateOnly(2026, 10, 15);
        await using var dbUm = Novo();
        var umAUm = await new GuardaProntuarioService(new ClinicaRepositorio(dbUm)).ResumoAsync(hoje);

        var paralela = new PorEscopo(Novo);
        await using var dbPar = Novo();
        var emParalelo = await new GuardaProntuarioService(new ClinicaRepositorio(dbPar), paralela).ResumoAsync(hoje);

        emParalelo.Should().Be(umAUm);
        umAUm.ComRegistro.Should().BeGreaterThan(0);
        umAUm.JaCumpriramPrazo.Should().BeGreaterThan(0);
        paralela.Leituras.Should().Be(9);
    }

    public void Dispose() => _conn.Dispose();
}
