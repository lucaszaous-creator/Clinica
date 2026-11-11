using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Tiss.Conector;

public static class RegistroDoConector
{
    /// <summary>
    /// As peças do conector, menos o transporte (o programa registra o HTTP; o teste, a rede de
    /// mentira) e menos o banco (<c>AddClinica</c>).
    /// </summary>
    public static IServiceCollection AddConectorTiss(this IServiceCollection services, OpcoesDoConector opcoes)
    {
        services.AddSingleton(opcoes);
        services.AddScoped<IProcessadorTransacaoTiss>(sp => new ProcessadorDeElegibilidade(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<ParametrosService>(),
            sp.GetRequiredService<CredenciaisTissService>(), HoraDaClinica.Agora));
        services.AddScoped<IProcessadorTransacaoTiss, ProcessadorDeEnvioDeGuia>();
        services.AddScoped<IProcessadorTransacaoTiss, ProcessadorDeStatusDeProtocolo>();
        services.AddScoped<IProcessadorTransacaoTiss, ProcessadorDeCancelamentoDeGuia>();
        services.AddScoped<ExecutorDeTransacoesTiss>();
        return services;
    }
}
