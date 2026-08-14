using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// A frase que o EF põe na tela quando a gravação falha.
///
/// Em 14/08/2026 a clínica assinou um documento com sucesso e levou, no lugar da
/// confirmação, <b>"An error occurred while saving the entity changes. See the inner
/// exception for details."</b> — a mensagem padrão do EF Core. Ela é uma instrução para o
/// programador impressa na cara do usuário: não diz o que houve, não diz o que fazer, e
/// esconde justamente a linha que resolve, que é a do banco (coluna, restrição, valor).
///
/// É a mesma lição que a assinatura em nuvem custou seis rodadas para ensinar: mensagem de
/// erro que carrega a evidência substitui a próxima rodada de adivinhação.
/// </summary>
public class FalhaDeGravacaoDizACausaTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly ClinicaDbContext _db;
    private readonly ClinicaRepositorio _repo;

    public FalhaDeGravacaoDizACausaTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();

        // Sem isto o SQLite não checa chave estrangeira, e o teste não teria o que provocar.
        using (var pragma = _conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON";
            pragma.ExecuteNonQuery();
        }

        var opcoes = new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(_conn).Options;
        _db = new ClinicaDbContext(opcoes);
        _db.Database.EnsureCreated();
        _repo = new ClinicaRepositorio(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Falha_de_gravacao_leva_a_resposta_do_BANCO_para_a_tela()
    {
        // Documento clínico apontando para um paciente que não existe: o banco recusa.
        _db.DocumentosClinicos.Add(new DocumentoClinico
        {
            Numero = "2026/0001",
            CodigoVerificacao = "ABC123",
            Tipo = TipoDocumentoClinico.Receita,
            PacienteId = 999_999,
            Data = new DateOnly(2026, 8, 14)
        });

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.SalvarAsync());

        // A frase do EF não pode ser a única coisa na tela...
        erro.Message.Should().NotBe(
            "An error occurred while saving the entity changes. See the inner exception for details.");

        // ...e o que o BANCO disse tem de estar nela.
        erro.Message.Should().Contain("O banco respondeu");
        erro.Message.Should().ContainEquivalentOf("FOREIGN KEY");

        // "Veja a inner exception" é instrução para programador, não para quem está no balcão.
        erro.Message.Should().NotContainEquivalentOf("inner exception");
    }
}
