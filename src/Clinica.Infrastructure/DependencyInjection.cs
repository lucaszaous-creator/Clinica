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
        services.AddDbContext<ClinicaDbContext>(o => o.UseNpgsql(connectionString, ConfigurarNpgsql));
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
        services.AddScoped<BloqueioAgendaService>();
        services.AddScoped<PainelRecepcaoService>();
        services.AddScoped<ProntuarioService>();
        services.AddScoped<MapaCorporalService>();
        services.AddScoped<DocumentoClinicoService>();
        services.AddScoped<DocumentosClinicosPdfService>();
        services.AddScoped<ConsentimentoService>();
        services.AddScoped<TitularDadosService>();
        services.AddScoped<ElegibilidadeService>();
        services.AddScoped<ConsultaService>();
        services.AddScoped<AutorizacaoService>();
        services.AddScoped<GlosaService>();
        services.AddScoped<ReceitaGlosadaService>();
        services.AddScoped<MetaService>();
        services.AddScoped<OrcamentoService>();
        services.AddScoped<ResultadoMensalService>();
        services.AddScoped<RetencaoPacienteService>();
        services.AddScoped<RelacionamentoService>();
        services.AddScoped<AgendaPdfService>();
        services.AddScoped<FinanceiroService>();
        services.AddScoped<ContasService>();
        services.AddScoped<FluxoCaixaService>();
        services.AddScoped<FechamentoCaixaService>();
        services.AddScoped<RecebiveisService>();
        services.AddScoped<CustoTransacaoService>();
        services.AddScoped<RentabilidadeConvenioService>();
        services.AddScoped<PrecoConvenioService>();
        services.AddScoped<AuditoriaService>();
        services.AddScoped<PainelDirecaoService>();
        services.AddScoped<InadimplenciaService>();
        services.AddScoped<CentralDocumentosService>();
        // O TributoService vem ANTES do TaxaService: este o recebe para abrir o imposto
        // por tributo (e a retencao por convenio) em vez de uma aliquota unica e cega.
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
        // Pix: lógica pura, sem estado e sem banco — mas registrado como scoped
        // junto dos outros para as telas o pedirem do mesmo jeito que pedem o resto.
        services.AddScoped<PixService>();
        // Backup e restauração da base inteira (parcela 34).
        services.AddScoped<BackupService>();
        return services;
    }

    /// <summary>
    /// Resiliência de rede — a clínica trabalha contra um Postgres REMOTO (Neon), pela
    /// internet do consultório.
    /// </summary>
    /// <remarks>
    /// Sem isto, uma oscilação de meio segundo no wi-fi vira `NpgsqlException` na cara de
    /// quem está com o paciente na frente, e a secretária refaz o lançamento — quando
    /// refaz. O padrão do EF é não tentar de novo NENHUMA vez.
    ///
    /// Duas decisões:
    ///
    /// - **Só falha transitória é repetida.** `EnableRetryOnFailure` do Npgsql sabe
    ///   distinguir queda de conexão de erro de dados; violação de índice único (guia já
    ///   lançada, recorrência já gerada) continua estourando na primeira vez, como deve.
    /// - **O timeout é generoso, não infinito.** Sem limite, um relatório pesado numa
    ///   conexão ruim deixa a tela pendurada para sempre e o usuário fecha o app no
    ///   gerenciador de tarefas; com limite, ele recebe um erro e tenta de novo.
    ///
    /// O retry é seguro aqui porque **não há transação explícita em lugar nenhum do
    /// projeto** — é a incompatibilidade clássica do `EnableRetryOnFailure`, e a
    /// checagem foi feita antes de ligá-lo. Quem introduzir `BeginTransaction` precisa
    /// passar a usar a estratégia de execução (`Database.CreateExecutionStrategy`).
    /// </remarks>
    private static void ConfigurarNpgsql(Npgsql.EntityFrameworkCore.PostgreSQL
        .Infrastructure.NpgsqlDbContextOptionsBuilder o)
    {
        o.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);

        o.CommandTimeout(60);
    }
}
