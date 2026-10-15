using Clinica.Application.Abstracoes;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Infrastructure;

/// <summary>Ver <see cref="ILeituraParalela"/>: um escopo — e um DbContext — por leitura.</summary>
public sealed class LeituraParalela(IServiceScopeFactory escopos) : ILeituraParalela
{
    public Task<T> LerAsync<T>(Func<IClinicaRepositorio, Task<T>> leitura)
        => LerAsync<IClinicaRepositorio, T>(leitura);

    public async Task<T> LerAsync<TServico, T>(Func<TServico, Task<T>> leitura) where TServico : notnull
    {
        await using var escopo = escopos.CreateAsyncScope();
        return await leitura(escopo.ServiceProvider.GetRequiredService<TServico>());
    }
}
