using Clinica.Domain.Entities;
using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Clinica.Desktop.Shell;

/// <summary>
/// Subida padrão de um app da suíte: connection string → host DI → migrations → janela.
/// Cada executável só informa o título e os módulos que quer carregar.
/// </summary>
public static class ShellBootstrap
{
    /// <summary>
    /// Chave do advisory lock que serializa a aplicação de migrations. Com vários apps
    /// da suíte instalados, dois podem abrir ao mesmo tempo pela manhã; sem o lock, os
    /// dois tentariam migrar em paralelo.
    /// </summary>
    private const long ChaveLockMigration = 727411;

    /// <summary>
    /// Obtém a connection string: variável de ambiente primeiro, depois a configuração
    /// local (pasta da suíte e, como fallback, a do faturamento já instalado).
    /// </summary>
    public static string? ObterConnectionString()
    {
        var env = Environment.GetEnvironmentVariable("ConnectionStrings__Clinica");
        if (!string.IsNullOrWhiteSpace(env)) return ConexaoStore.Normalizar(env);

        return ConexaoStore.Carregar();
    }

    /// <summary>Monta o host com as camadas compartilhadas e os módulos informados.</summary>
    public static IHost ConstruirHost(string connectionString, IEnumerable<IModuloApp> modulos)
    {
        var lista = modulos.ToList();

        return Host.CreateDefaultBuilder()
            .ConfigureServices(servicos =>
            {
                // Domínio + aplicação + infraestrutura: idêntico ao faturamento.
                servicos.AddClinica(connectionString);

                // Quem está usando o app neste processo (parcela 5). Singleton: a sessão
                // é do processo, não de um scope — e é dela que as telas tiram o
                // "operador" que vai para a auditoria.
                servicos.AddSingleton<SessaoUsuario>();

                servicos.AddSingleton<SnackbarService>();
                servicos.AddSingleton<ISnackbarService>(
                    sp => sp.GetRequiredService<SnackbarService>());
                servicos.AddSingleton<IDialogoService, DialogoService>();

                foreach (var modulo in lista) modulo.Registrar(servicos);
            })
            .Build();
    }

    /// <summary>
    /// Aplica migrations pendentes sob advisory lock (só um app migra por vez) e
    /// recarrega os catálogos em memória.
    /// </summary>
    public static Task PrepararBancoAsync(IServiceProvider servicos, CancellationToken ct = default)
        // O corpo mora na Infrastructure (AberturaDoBanco) para a medição da abertura rodar o
        // MESMO código; aqui fica o que só o shell sabe: a trava das migrations e o pool.
        => AberturaDoBanco.PrepararAsync(servicos,
            // Sem migration pendente não há o que serializar, e a abertura do dia a dia não
            // toca a trava (out/2026). Antes, TODA abertura esperava por ela com
            // pg_advisory_lock, sem prazo: uma conexão que caiu com a trava na mão (internet
            // instável no posto) ficava "idle" no servidor segurando-a até o keepalive de 2 h,
            // e todos os apps da clínica paravam na abertura com "o banco não respondeu".
            migrar: async (db, c) =>
            {
                var travou = await TentarTravarMigrationAsync(db, c);
                try
                {
                    await MigrarTolerandoCorridaAsync(db, c);
                }
                finally
                {
                    if (travou) await DestravarMigrationAsync(db);
                }
            },
            // As conexões mínimas do pool abrem em segundo plano enquanto os catálogos carregam:
            // é a primeira troca de tela que deixaria de pagar os segundos da abertura.
            aoConectar: cadeia =>
            {
                _ = Task.Run(() => ConexaoRemota.AquecerAsync(cadeia), CancellationToken.None);
                // Escuta os avisos do banco (agenda e pedidos da enfermagem): as telas abertas
                // relêem na hora em que outra máquina grava, sem esperar o relógio.
                OuvinteDeMudancas.Iniciar(cadeia);
            },
            ct);

    /// <summary>Quanto a abertura espera pela trava das migrations antes de seguir sem ela.</summary>
    private static readonly TimeSpan PrazoDaTravaDeMigration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Pega a trava das migrations com PRAZO (<c>pg_try_advisory_lock</c> de 2 em 2 s). Quem
    /// a segura há mais que o prazo não está migrando — é uma sessão morta que o servidor
    /// ainda não derrubou —, e esperar por ela travaria a clínica inteira. Passado o prazo,
    /// a abertura segue SEM a trava: a corrida de DDL com outro app já é tolerada por
    /// <see cref="MigrarTolerandoCorridaAsync"/>, e isso fica no log.
    /// </summary>
    private static async Task<bool> TentarTravarMigrationAsync(ClinicaDbContext db, CancellationToken ct)
    {
        var limite = DateTime.UtcNow + PrazoDaTravaDeMigration;
        while (true)
        {
            // Na MESMA conexão já aberta: a trava é de sessão.
            await using var comando = db.Database.GetDbConnection().CreateCommand();
            comando.CommandText = $"SELECT pg_try_advisory_lock({ChaveLockMigration})";
            var conseguiu = await comando.ExecuteScalarAsync(ct) is true;
            if (conseguiu) return true;
            if (DateTime.UtcNow >= limite)
            {
                LogSuite.Registrar(
                    $"Suíte — a trava das migrations está presa há mais de {PrazoDaTravaDeMigration.TotalSeconds:0} s; seguindo sem ela",
                    new TimeoutException("pg_advisory_lock " + ChaveLockMigration));
                return false;
            }
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }

    /// <summary>
    /// Solta a trava. Falhar aqui não impede a abertura: fechar a conexão devolve-a ao pool,
    /// que a reinicia (DISCARD ALL) e solta a trava de sessão do mesmo jeito.
    /// </summary>
    private static async Task DestravarMigrationAsync(ClinicaDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({ChaveLockMigration})");
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Suíte — não foi possível soltar a trava das migrations", ex);
        }
    }

    /// <summary>
    /// Aplica as migrations tolerando a corrida de DDL com outro app.
    ///
    /// O advisory lock acima serializa os apps DA SUÍTE, mas o Faturamento — que está
    /// congelado — chama <c>MigrateAsync</c> direto, sem participar dele. Numa clínica
    /// com vários postos abrindo os apps às 8h, dois processos podem rodar o mesmo
    /// CREATE TABLE ao mesmo tempo: o Postgres deixa um passar e devolve ao outro um
    /// erro de chave duplicada no catálogo (<c>pg_type_typname_nsp_index</c>, 23505) ou
    /// "relação já existe" (42P07).
    ///
    /// Perder essa corrida NÃO é falha de conexão, mas era assim que aparecia: a tela
    /// dizia "não foi possível conectar ao banco" e oferecia trocar a connection
    /// string — mandando a clínica caçar um problema que não existe. Aqui a gente
    /// confere quem ganhou: se o outro processo já aplicou tudo, seguimos; se ainda
    /// falta, tentamos de novo uma vez.
    /// </summary>
    private static async Task MigrarTolerandoCorridaAsync(
        ClinicaDbContext db, CancellationToken ct)
    {
        try
        {
            await db.Database.MigrateAsync(ct);
            return;
        }
        catch (Exception ex) when (EhCorridaDeSchema(ex))
        {
            LogSuite.Registrar(
                "Suíte — outro app criava o schema ao mesmo tempo; conferindo o resultado", ex);
        }

        // Dá tempo de o vencedor da corrida terminar a migration dele.
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        var pendentes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pendentes.Count == 0) return; // o outro processo fez o serviço: nada a fazer

        // Ainda falta: segunda e última tentativa. Falhando aqui, o erro sobe — a
        // clínica precisa saber que o banco não ficou no estado esperado.
        await db.Database.MigrateAsync(ct);
    }

    /// <summary>
    /// O erro é "outro processo criou este objeto primeiro"? Só estes códigos entram:
    /// qualquer outra falha de banco continua subindo como sempre.
    /// </summary>
    private static bool EhCorridaDeSchema(Exception excecao)
    {
        // SQLSTATE em texto: são códigos do padrão SQL/Postgres, estáveis há décadas.
        //   23505 unique_violation  — o índice do catálogo (pg_type_typname_nsp_index)
        //   42P07 duplicate_table   42710 duplicate_object   42P06 duplicate_schema
        //   42723 duplicate_function                         42701 duplicate_column
        for (Exception? ex = excecao; ex is not null; ex = ex.InnerException)
            if (ex is PostgresException pg &&
                pg.SqlState is "23505" or "42P07" or "42710" or "42P06" or "42723" or "42701")
                return true;

        return false;
    }
}
