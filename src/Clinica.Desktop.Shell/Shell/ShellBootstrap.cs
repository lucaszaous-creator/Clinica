using Clinica.Desktop.Controls;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

                servicos.AddSingleton<SnackbarService>();
                servicos.AddSingleton<ISnackbarService>(
                    sp => sp.GetRequiredService<SnackbarService>());

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
                await db.Database.MigrateAsync(ct);
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
}
