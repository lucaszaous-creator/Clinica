using Clinica.Application.Abstracoes;
using Clinica.Application.Servicos;
using Clinica.Domain.Regras;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra DbContext, repositório, motor de regras e serviços de aplicação.</summary>
    public static IServiceCollection AddClinica(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ClinicaDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IClinicaRepositorio, ClinicaRepositorio>();
        services.AddSingleton(new RegistroRegras());
        services.AddScoped<AtendimentoService>();
        services.AddScoped<FaturamentoService>();
        services.AddScoped<PendenciaService>();
        services.AddScoped<RodadaPendenciasService>();
        services.AddScoped<RelatorioService>();
        services.AddScoped<PacienteService>();
        services.AddScoped<AgendaService>();
        services.AddScoped<ConsultaService>();
        services.AddScoped<GlosaService>();
        services.AddScoped<PrevencaoGlosaService>();
        services.AddScoped<TissExportService>();
        services.AddScoped<LoteTissService>();
        services.AddScoped<GuiaTissPdfService>();
        services.AddScoped<CapaFaturamentoService>();
        services.AddScoped<FechamentoPdfService>();
        services.AddScoped<ParametrosService>();
        services.AddScoped<ConvenioCatalogoService>();
        services.AddScoped<ModalidadeCatalogoService>();
        services.AddScoped<EspecialidadeCatalogoService>();
        return services;
    }
}
