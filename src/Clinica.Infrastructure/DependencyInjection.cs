using Clinica.Application.Assinatura;
using Clinica.Application.Assinatura.SafeID;
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
        // Consultório (parcela 36): o dia de quem atende e as escalas por especialidade.
        services.AddScoped<ConsultorioService>();
        services.AddScoped<AvaliacaoClinicaService>();
        // Parcela 37: as medidas seriadas e a lista de problemas do prontuário.
        services.AddScoped<MedidaClinicaService>();
        services.AddScoped<ProblemaPacienteService>();
        // Conferência clínica da prescrição (parcela 40): alergia registrada × item escrito.
        services.AddScoped<PrescricaoService>();
        // Prescrição de execução interna, checagem de enfermagem e assinatura ICP-Brasil
        // (parcela 42). O assinador é SINGLETON e sem estado — ele não toca no banco, e
        // exige cadeia confiável porque assinatura que não encadeia abre como inválida em
        // qualquer leitor de PDF (só os testes o constroem com a exigência desligada).
        services.AddScoped<PrescricaoInternaService>();
        services.AddScoped<ChecagemPrescricaoService>();
        services.AddScoped<PrescricaoInternaPdfService>();
        services.AddScoped<AssinaturaDePrescricaoService>();
        services.AddScoped<AssinaturaDeDocumentoClinicoService>();
        services.AddSingleton(new AssinaturaDigitalService(exigirCadeiaConfiavel: true));
        RegistrarSafeID(services);
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

        // Parcela 52 — a metade de LEITURA da trilha e a guarda de 20 anos.
        services.AddScoped<AcessoProntuarioService>();
        services.AddScoped<GuardaProntuarioService>();
        services.AddScoped<ExportacaoProntuarioService>();

        // Parcela 53 — publicação do documento assinado. O serviço é registrado SEMPRE; a
        // implementação de armazenamento é que só existe quando a clínica contrata um
        // provedor. Sem ela, o AssinaturaDeDocumentoClinicoService recebe null e o sistema
        // funciona exatamente como antes (QR para o validador do ITI).
        services.AddScoped<PublicacaoDocumentoService>();
        // Objeto-nulo enquanto não há provedor contratado. Trocar por uma implementação
        // S3-compatível é a ÚNICA coisa que a escolha do fornecedor exige do código.
        services.AddScoped<IArmazenamentoPublico, ArmazenamentoIndisponivel>();
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

        // Parcela 52 — a POLÍTICA em volta da ferramenta: prazo, destino e rotação.
        services.AddScoped<PoliticaBackupService>();
        return services;
    }

    /// <summary>
    /// Assinatura em nuvem pelo SafeID (parcela 44), quando a clínica a configurou.
    ///
    /// <b>Silencioso quando não há configuração, e isso é decisão.</b> Este método roda no
    /// arranque de todos os aplicativos, inclusive o faturamento CONGELADO, que não assina
    /// nada — lançar aqui por falta de uma variável de ambiente derrubaria a abertura de um
    /// app em produção por causa de uma funcionalidade que ele não usa.
    ///
    /// O <see cref="HttpClient"/> é registrado com <c>AddHttpClient</c> por causa do
    /// esgotamento de portas que <c>new HttpClient()</c> por chamada produz — e com um
    /// tempo-limite curto, porque quem está assinando tem um paciente na frente: falhar em
    /// 30 s dizendo o motivo é melhor que travar a tela por dois minutos.
    /// </summary>
    private static void RegistrarSafeID(IServiceCollection services)
    {
        // Registra SEMPRE, e resolve as credenciais só quando alguém for assinar. A versão
        // anterior lia a configuração aqui e registrava condicionalmente — o que obrigava a
        // resposta a existir antes de haver banco, e por tabela obrigava a clínica a definir
        // variável de ambiente em cada máquina.
        services.AddScoped<ProvedorOpcoesSafeID>();

        // Tempo-limite curto: quem está assinando tem um paciente na frente, e falhar em
        // 30 s dizendo o motivo é melhor que travar a tela por dois minutos.
        services.AddHttpClient(NomeHttpSafeID, c => c.Timeout = TimeSpan.FromSeconds(30));
    }

    /// <summary>Nome do <c>HttpClient</c> configurado para falar com o PSC.</summary>
    public const string NomeHttpSafeID = "SafeID";

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
