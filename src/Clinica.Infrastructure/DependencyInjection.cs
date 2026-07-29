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
        services.AddScoped<EquipeService>();
        services.AddScoped<ListaEsperaService>();
        services.AddScoped<PainelRecepcaoService>();
        services.AddScoped<ProntuarioService>();
        services.AddScoped<MapaCorporalService>();
        services.AddScoped<DocumentoClinicoService>();
        services.AddScoped<DocumentosClinicosPdfService>();
        services.AddScoped<ConsentimentoService>();
        services.AddScoped<ElegibilidadeService>();
        services.AddScoped<ConsultaService>();
        services.AddScoped<AutorizacaoService>();
        services.AddScoped<GlosaService>();
        services.AddScoped<FinanceiroService>();
        services.AddScoped<ContasService>();
        services.AddScoped<FluxoCaixaService>();
        services.AddScoped<FechamentoCaixaService>();
        // O TributoService vem ANTES: o TaxaService o recebe para abrir o imposto
        // por tributo em vez de devolver uma aliquota unica e cega.
        services.AddScoped<RecebiveisService>();
        services.AddScoped<TributoService>();
        services.AddScoped<TaxaService>();
        services.AddScoped<PacoteService>();
        services.AddScoped<RepasseService>();
        services.AddScoped<EstoqueService>();
        // A ponte Recepção → Financeiro: depende de quatro serviços acima, e por isso
        // vem depois deles.
        services.AddScoped<FechamentoSessaoService>();
        services.AddScoped<DocumentoFinanceiroService>();
        services.AddScoped<DocumentosFinanceirosPdfService>();
        services.AddScoped<IndicadoresService>();
        services.AddScoped<CampanhaService>();
        services.AddScoped<AcessoService>();
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
