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
        // UM por processo: a memória da última ida a cada operadora atravessa as voltas do laço.
        services.AddSingleton(new IntervaloEntreChamadasTiss(TimeSpan.FromSeconds(opcoes.IntervaloMinimoSegundos)));
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
        services.AddScoped<IProcessadorTransacaoTiss>(sp => new ProcessadorDeAutorizacao(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<ParametrosService>(),
            sp.GetRequiredService<CredenciaisTissService>(), HoraDaClinica.Agora));
        services.AddScoped<IProcessadorTransacaoTiss>(sp => new ProcessadorDeCancelamentoDeAutorizacao(
            sp.GetRequiredService<IClinicaRepositorio>(), sp.GetRequiredService<ParametrosService>(),
            sp.GetRequiredService<CredenciaisTissService>(), HoraDaClinica.Agora));
        services.AddScoped<ExecutorDeTransacoesTiss>();
        return services;
    }
}
