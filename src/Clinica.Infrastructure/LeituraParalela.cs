using Clinica.Application.Abstracoes;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Infrastructure;

/// <summary>Ver <see cref="ILeituraParalela"/>: um escopo — e um DbContext — por leitura.</summary>
public sealed class LeituraParalela(IServiceScopeFactory escopos) : ILeituraParalela
{
    // Teto por app: o painel da direção dispara ~18 leituras ao abrir, e cada uma ocupa uma
    // conexão do banco. Seis ao mesmo tempo já cortam a espera a um terço sem disputar as
    // conexões do servidor com os outros postos da clínica.
    private static readonly SemaphoreSlim Vagas = new(6, 6);

    public Task<T> LerAsync<T>(Func<IClinicaRepositorio, Task<T>> leitura)
        => LerAsync<IClinicaRepositorio, T>(leitura);

    public async Task<T> LerAsync<TServico, T>(Func<TServico, Task<T>> leitura) where TServico : notnull
    {
        await Vagas.WaitAsync();
        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            return await leitura(escopo.ServiceProvider.GetRequiredService<TServico>());
        }
        finally { Vagas.Release(); }
    }
}
