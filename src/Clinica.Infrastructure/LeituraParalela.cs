using Clinica.Application.Abstracoes;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Infrastructure;

/// <summary>Ver <see cref="ILeituraParalela"/>: um escopo — e um DbContext — por leitura.</summary>
public sealed class LeituraParalela(IServiceScopeFactory escopos) : ILeituraParalela
{
    // Teto por app: o painel da direção dispara ~20 leituras ao abrir, e cada uma ocupa uma
    // conexão do banco. Seis ao mesmo tempo já cortam a espera a um terço sem disputar as
    // conexões do servidor com os outros postos da clínica — e é o número de conexões que a
    // abertura deixa prontas (ConexaoRemota.AquecidasNaAbertura).
    public const int Vagas = 4;
    private static readonly SemaphoreSlim Semaforo = new(Vagas, Vagas);

    // A leitura de DENTRO (uma leitura paralela que dispara outras) tem vagas PRÓPRIAS: esperar
    // pelas de fora seria o app parado para sempre (as de fora estão seguras por quem espera a de
    // dentro), e não ter teto nenhum abria até 13 consultas ao mesmo tempo no Início da Recepção —
    // cada uma acima das conexões aquecidas é uma conexão FÍSICA nova, de 2,7–4,7 s (medido em
    // AberturaDosAppsTests). Fora + dentro = ConexaoRemota.AquecidasNaAbertura.
    public const int VagasDeDentro = 2;
    private static readonly SemaphoreSlim SemaforoDeDentro = new(VagasDeDentro, VagasDeDentro);

    // Leitura que roda DENTRO de uma vaga e pede outra (o painel da direção lê o Consultório,
    // que também lê em paralelo, out/2026) não espera vaga: seis leituras segurando as seis
    // vagas e esperando uma sétima seria o app parado para sempre. A de dentro corre no seu
    // escopo, como as outras — só não entra na fila.
    private static readonly AsyncLocal<bool> DentroDeUmaVaga = new();

    public Task<T> LerAsync<T>(Func<IClinicaRepositorio, Task<T>> leitura)
        => LerAsync<IClinicaRepositorio, T>(leitura);

    public async Task<T> LerAsync<TServico, T>(Func<TServico, Task<T>> leitura) where TServico : notnull
    {
        if (DentroDeUmaVaga.Value)
        {
            await SemaforoDeDentro.WaitAsync();
            try { return await LerNoEscopoAsync(leitura); }
            finally { SemaforoDeDentro.Release(); }
        }
        await Semaforo.WaitAsync();
        try
        {
            DentroDeUmaVaga.Value = true;
            return await LerNoEscopoAsync(leitura);
        }
        finally { Semaforo.Release(); }
    }

    private async Task<T> LerNoEscopoAsync<TServico, T>(Func<TServico, Task<T>> leitura) where TServico : notnull
    {
        await using var escopo = escopos.CreateAsyncScope();
        return await leitura(escopo.ServiceProvider.GetRequiredService<TServico>());
    }
}
