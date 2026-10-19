using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PreviaBlazor.Sementes;

namespace Clinica.Tests.Desempenho;

/// <summary>
/// O banco da medição da abertura: um SQLite em ARQUIVO (não em memória) para que cada escopo
/// tenha a SUA conexão, como no pool do Npgsql — é isso que deixa duas leituras correrem de
/// verdade ao mesmo tempo, e a rede simulada contar quantas conexões físicas elas pediriam.
///
/// Semeado com as MESMAS sementes fictícias da prévia e das telas, mais um usuário de cada
/// perfil com senha de verdade (o login passa pelo mesmo PBKDF2 da clínica), e com TODAS as
/// migrations marcadas como aplicadas: a abertura do dia a dia não tem migration pendente, e
/// a conferência dela é o que se mede.
/// </summary>
public sealed class BancoDaAbertura : IDisposable
{
    public const string Senha = "medicao-da-abertura";
    public string Arquivo { get; } = Path.Combine(Path.GetTempPath(), $"abertura-{Guid.NewGuid():N}.db");
    public string Cadeia => $"Data Source={Arquivo};Default Timeout=60";

    public Dictionary<PerfilAcesso, UsuarioSistema> Usuarios { get; } = [];

    public BancoDaAbertura()
    {
        using (var c = new SqliteConnection(Cadeia))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }

        using var db = new ClinicaDbContext(new DbContextOptionsBuilder<ClinicaDbContext>().UseSqlite(Cadeia).Options);
        db.Database.EnsureCreated();
        foreach (var semente in typeof(ISemente).Assembly.GetTypes()
                     .Where(t => typeof(ISemente).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                     .Select(t => (ISemente)Activator.CreateInstance(t)!)
                     .OrderBy(s => s.Ordem))
        {
            semente.Semear(db);
            db.ChangeTracker.Clear();
        }

        var helena = db.Profissionais.OrderBy(p => p.Id).First(p => p.Nome.StartsWith("Dra. Helena"));
        var joana = db.Profissionais.OrderBy(p => p.Id).FirstOrDefault(p => p.RegistroConselho != null && p.RegistroConselho.StartsWith("COREN"));
        foreach (var perfil in new[] { PerfilAcesso.Gerente, PerfilAcesso.Recepcao, PerfilAcesso.Financeiro,
                     PerfilAcesso.Faturista, PerfilAcesso.Profissional })
        {
            var (hash, sal) = HashSenha.Gerar(Senha);
            var u = new UsuarioSistema
            {
                Nome = $"Medição {perfil}", Login = $"medicao.{perfil}".ToLowerInvariant(), Perfil = perfil,
                SenhaHash = hash, SenhaSalt = sal, Ativo = true,
                ProfissionalId = perfil == PerfilAcesso.Profissional ? helena.Id : null
            };
            db.Add(u);
            Usuarios[perfil] = u;
        }
        db.SaveChanges();

        // A abertura do dia a dia: nada pendente. A conferência ("há migration?") é o que custa.
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL)");
        foreach (var m in db.Database.GetMigrations())
            db.Database.ExecuteSqlRaw("INSERT OR IGNORE INTO \"__EFMigrationsHistory\" VALUES ({0}, '8.0.11')", m);
        _ = joana;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var sufixo in new[] { "", "-wal", "-shm" })
            try { File.Delete(Arquivo + sufixo); } catch (IOException) { }
    }
}
