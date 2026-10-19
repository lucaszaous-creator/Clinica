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
/// A conferência de elegibilidade em paralelo (out/2026) devolve EXATAMENTE os mesmos alertas,
/// na mesma ordem, que a conferência em fila — só muda o tempo de espera. Cada bloco roda numa
/// instância do serviço com o seu próprio DbContext, como no app.
/// </summary>
public class ElegibilidadeParalelaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private static readonly DateOnly Hoje = new(2026, 8, 3);

    public ElegibilidadeParalelaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _db = Contexto();
        _db.Database.EnsureCreated();
    }

    private ClinicaDbContext Contexto()
        => new(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options);

    private static ElegibilidadeService Servico(ClinicaRepositorio repo, ILeituraParalela? paralela = null)
        => new(repo, new AutorizacaoService(repo), new ConsentimentoService(repo), new ConsultaService(repo),
            new InadimplenciaService(repo, new FinanceiroService(repo)), new PacoteService(repo), null, paralela);

    /// <summary>Um contexto NOVO por leitura, como o escopo do app. Serializado: o SQLite em memória
    /// é UMA conexão, e o que se prova aqui é a independência dos blocos e a ordem, não o relógio.</summary>
    private sealed class PorEscopo(Func<ClinicaDbContext> novo) : ILeituraParalela
    {
        private readonly SemaphoreSlim _fila = new(1, 1);
        public Task<T> LerAsync<T>(Func<IClinicaRepositorio, Task<T>> leitura)
            => LerAsync<IClinicaRepositorio, T>(leitura);
        public async Task<T> LerAsync<TServico, T>(Func<TServico, Task<T>> leitura) where TServico : notnull
        {
            await _fila.WaitAsync();
            try
            {
                using var db = novo();
                var repo = new ClinicaRepositorio(db);
                object servico = typeof(TServico) == typeof(ElegibilidadeService) ? Servico(repo) : repo;
                return await leitura((TServico)servico);
            }
            finally { _fila.Release(); }
        }
    }

    [Fact]
    public async Task Paralela_devolve_os_mesmos_alertas_na_mesma_ordem()
    {
        var p = new Paciente { Nome = "Paciente fictícia", Convenio = Convenio.UnimedIntercambio, Sexo = Sexo.Feminino,
            Carteirinha = "123", ValidadeCarteirinha = Hoje.AddDays(-3) };
        _db.Pacientes.Add(p);
        await _db.SaveChangesAsync();
        var repo = new ClinicaRepositorio(_db);
        // Carteirinha vencida e dívida vencida: dois blocos diferentes produzem alerta (o de
        // consentimento LGPD saiu do balcão em out/2026).
        await new ContasService(repo).LancarContaAsync(TipoLancamento.Entrada, "Sessão avulsa", 120m,
            vencimento: Hoje.AddDays(-20), pacienteId: p.Id);

        var emFila = await Servico(repo).ConferirAsync(p.Id, Hoje);
        var emParalelo = await Servico(repo, new PorEscopo(Contexto)).ConferirAsync(p.Id, Hoje);

        emFila.Alertas.Count.Should().BeGreaterThanOrEqualTo(2);
        emParalelo.Alertas.Select(a => (a.Motivo, a.Descricao, a.Urgencia))
            .Should().Equal(emFila.Alertas.Select(a => (a.Motivo, a.Descricao, a.Urgencia)));
        emParalelo.PacienteNome.Should().Be(emFila.PacienteNome);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }
}
