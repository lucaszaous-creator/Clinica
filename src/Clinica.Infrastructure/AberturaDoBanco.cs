using Clinica.Application.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Infrastructure;

/// <summary>
/// O preparo do banco na abertura dos apps da suíte (out/2026): migration pendente? → catálogos
/// em memória. Mora aqui, e não no shell (WPF), para a medição da abertura
/// (<c>AberturaDosAppsTests</c>) exercitar o MESMO código que a clínica roda.
/// </summary>
public static class AberturaDoBanco
{
    /// <param name="migrar">
    /// Aplica as migrations pendentes — quem chama decide a trava (o shell usa o advisory lock
    /// com prazo). Só é chamado quando HÁ migration pendente.
    /// </param>
    /// <param name="aoConectar">
    /// Recebe a connection string depois da conferência — o shell aquece o pool e liga a escuta
    /// de avisos com ela. A string vem do EF, nunca da conexão aberta: o Npgsql tira a senha dela.
    /// </param>
    public static async Task PrepararAsync(
        IServiceProvider servicos,
        Func<ClinicaDbContext, CancellationToken, Task> migrar,
        Action<string>? aoConectar = null,
        CancellationToken ct = default)
    {
        using var scope = servicos.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();

        // O lock é de sessão: a conexão precisa ficar aberta do lock ao unlock.
        var conexao = db.Database.GetDbConnection();
        await conexao.OpenAsync(ct);
        try
        {
            // Sem migration pendente não há o que serializar, e a abertura do dia a dia não
            // toca a trava (out/2026).
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
                await migrar(db, ct);
        }
        finally
        {
            await conexao.CloseAsync();
        }

        if (db.Database.GetConnectionString() is { Length: > 0 } cadeia)
            aoConectar?.Invoke(cadeia);

        // Catálogos (convênios, modalidades, especialidades) no cache em memória.
        await RecarregarCatalogosAsync(servicos, ct);
    }

    /// <summary>
    /// Os três catálogos JUNTOS, cada um no seu escopo (o mesmo DbContext não aceita duas
    /// consultas ao mesmo tempo). Eram três idas em fila antes do login; o faturamento chama
    /// o mesmo.
    /// </summary>
    public static Task RecarregarCatalogosAsync(IServiceProvider servicos, CancellationToken ct = default)
        => Task.WhenAll(
            NoEscopoAsync<ConvenioCatalogoService>(servicos, s => s.RecarregarCacheAsync(ct)),
            NoEscopoAsync<ModalidadeCatalogoService>(servicos, s => s.RecarregarCacheAsync(ct)),
            NoEscopoAsync<EspecialidadeCatalogoService>(servicos, s => s.RecarregarCacheAsync(ct)));

    private static async Task NoEscopoAsync<T>(IServiceProvider servicos, Func<T, Task> f) where T : notnull
    {
        await using var escopo = servicos.CreateAsyncScope();
        await f(escopo.ServiceProvider.GetRequiredService<T>());
    }
}
