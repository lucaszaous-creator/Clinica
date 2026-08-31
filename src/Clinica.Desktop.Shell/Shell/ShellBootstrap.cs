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

                // O bilhete da pesquisa global: quem acha o paciente é o shell, quem abre
                // a ficha é o módulo, e a navegação entre os dois é por CHAVE — que não
                // carrega parâmetro. Singleton porque é do PROCESSO, como a sessão; e é um
                // pedido só, consumido na chegada (ver FichaPedida).
                servicos.AddSingleton<FichaPedida>();

                foreach (var modulo in lista) modulo.Registrar(servicos);
            })
            .Build();
    }

    /// <summary>
    /// Aplica migrations pendentes sob advisory lock (só um app migra por vez) e
    /// recarrega os catálogos em memória.
    /// </summary>
    public static async Task PrepararBancoAsync(IServiceProvider servicos, CancellationToken ct = default)
    {
        using var scope = servicos.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();

        // O lock é de sessão: a conexão precisa ficar aberta do lock ao unlock.
        var conexao = db.Database.GetDbConnection();
        await conexao.OpenAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                $"SELECT pg_advisory_lock({ChaveLockMigration})", ct);
            try
            {
                await MigrarTolerandoCorridaAsync(db, ct);
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_unlock({ChaveLockMigration})", ct);
            }
        }
        finally
        {
            await conexao.CloseAsync();
        }

        // Catálogos (convênios, modalidades, especialidades) no cache em memória.
        await scope.ServiceProvider
            .GetRequiredService<Clinica.Application.Servicos.ConvenioCatalogoService>()
            .RecarregarCacheAsync(ct);
        await scope.ServiceProvider
            .GetRequiredService<Clinica.Application.Servicos.ModalidadeCatalogoService>()
            .RecarregarCacheAsync(ct);
        await scope.ServiceProvider
            .GetRequiredService<Clinica.Application.Servicos.EspecialidadeCatalogoService>()
            .RecarregarCacheAsync(ct);
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
