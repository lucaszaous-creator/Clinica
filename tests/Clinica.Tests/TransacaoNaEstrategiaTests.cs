using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// nov/2026, produção: o "Lançar" da Conciliação → Particulares respondia "The configured execution strategy
/// 'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions". Os testes rodam em SQLite, SEM a
/// estratégia que repete — por isso ficavam verdes. Aqui o SQLite ganha uma estratégia que repete (como o
/// EnableRetryOnFailure do Npgsql), e o EF recusa transação aberta fora dela do mesmo jeito que em produção.
/// </summary>
public class TransacaoNaEstrategiaTests : IDisposable
{
    private sealed class EstrategiaQueRepete(ExecutionStrategyDependencies d) : ExecutionStrategy(d, 3, TimeSpan.FromMilliseconds(1))
    {
        protected override bool ShouldRetryOn(Exception exception) => false;
    }

    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public TransacaoNaEstrategiaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:"); _conn.Open();
        _db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>()
            .UseSqlite(_conn, o => o.ExecutionStrategy(d => new EstrategiaQueRepete(d))).Options);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    public void Dispose() { _db.Dispose(); _conn.Dispose(); }

    [Fact]
    public async Task A_estrategia_que_repete_recusa_transacao_aberta_a_mao()
    {
        // A prova de que o teste enxerga o defeito: é exatamente o erro de produção — o EF deixa abrir e recusa no
        // primeiro comando dentro da transação.
        await using var tx = await _db.Database.BeginTransactionAsync();
        var ler = () => _db.Pacientes.CountAsync();
        await ler.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not support user-initiated transactions*");
    }

    [Fact]
    public async Task Gestao_atomica_etapa_do_fechamento_e_alergia_rodam_sob_a_estrategia()
    {
        (await _repo.ExecutarGestaoAtomicaAsync(() => Task.FromResult(1))).Should().Be(1);
        (await _repo.ExecutarRegistroAlergiaAtomicoAsync(7, () => Task.FromResult(2))).Should().Be(2);
        (await _repo.ExecutarEtapaFechamentoAsync(0, "teste", "{}", () => Task.FromResult(0))).Should().Be(0);
    }

    [Fact]
    public async Task Dentro_de_transacao_de_quem_chamou_nao_abre_outra()
    {
        var resultado = await TransacaoResiliente.ExecutarAsync(_db, async c =>
            await _repo.ExecutarGestaoAtomicaAsync(() => Task.FromResult(3), c), default);
        resultado.Should().Be(3);
    }

    /// <summary>
    /// A rede para a PRÓXIMA transação: todo arquivo que abre transação tem de passar pela estratégia
    /// (TransacaoResiliente, TransacaoDaPonte ou CreateExecutionStrategy). As exceções são a assinatura SafeID/A1 —
    /// área congelada, e repetir lá seria cobrar a assinatura de novo.
    /// </summary>
    [Fact]
    public void Nenhum_BeginTransaction_fora_da_estrategia_de_execucao()
    {
        var raiz = AcharRaiz();
        var excecoes = new[] { "SafeIdTabletService.cs", "A1TabletService.cs" };
        var fora = Directory.EnumerateFiles(Path.Combine(raiz, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f).Contains("BeginTransaction(") || File.ReadAllText(f).Contains("BeginTransactionAsync("))
            .Where(f => !excecoes.Contains(Path.GetFileName(f)))
            .Where(f => { var t = File.ReadAllText(f); return !(t.Contains("TransacaoResiliente.ExecutarAsync") || t.Contains("TransacaoDaPonte.Executar") || t.Contains("CreateExecutionStrategy()")); })
            .Select(Path.GetFileName).ToList();
        fora.Should().BeEmpty("transação aberta fora da estratégia quebra em produção (EnableRetryOnFailure) e passa nos testes em SQLite");
    }

    private static string AcharRaiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Clinica.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
