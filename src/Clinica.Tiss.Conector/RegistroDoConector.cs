using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Tiss.Conector;

public static class RegistroDoConector
{
    /// <summary>
    /// As peças do conector, menos o transporte (<c>TissWebserviceCliente</c>: o programa registra o
    /// HTTP de verdade; o teste, um manipulador de mentira) e menos o banco (<c>AddClinica</c>).
    /// </summary>
    public static IServiceCollection AddConectorTiss(this IServiceCollection services, OpcoesDoConector opcoes)
    {
        services.AddSingleton(opcoes);
        services.AddScoped<IProcessadorTransacaoTiss>(sp => new ProcessadorDeElegibilidade(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<ParametrosService>(),
            sp.GetRequiredService<CredenciaisTissService>(), HoraDaClinica.Agora));
        services.AddScoped<IProcessadorTransacaoTiss>(sp => new ProcessadorDeEnvioDeGuia(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<ParametrosService>(),
            sp.GetRequiredService<CredenciaisTissService>(), HoraDaClinica.Agora));
        services.AddScoped<IProcessadorTransacaoTiss>(sp => new ProcessadorDeStatusDeProtocolo(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<ParametrosService>(),
            sp.GetRequiredService<CredenciaisTissService>(), HoraDaClinica.Agora));
        services.AddScoped<IProcessadorTransacaoTiss>(sp => new ProcessadorDeCancelamentoDeGuia(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<ParametrosService>(),
            sp.GetRequiredService<CredenciaisTissService>(), HoraDaClinica.Agora));
        services.AddScoped<ExecutorDeTransacoesTiss>();
        return services;
    }
}
