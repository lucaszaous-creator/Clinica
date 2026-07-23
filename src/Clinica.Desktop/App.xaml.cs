using System.Windows;
using System.Windows.Threading;
using Clinica.Application.Servicos;
using Clinica.Desktop.Alertas;
using Clinica.Desktop.Configuracao;
using Clinica.Desktop.ViewModels;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Clinica.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private DispatcherTimer? _lembreteTimer;
    private DispatcherTimer? _updateTimer;
    private bool _avisoAberto;
    private bool _rodadaAberta;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Rede/banco podem falhar no meio de um comando assíncrono (Wi-Fi caiu, Neon
        // hibernou). Sem este handler, qualquer exceção não tratada fecha o app com
        // perda do que estava na tela; com ele, avisamos e o app continua de pé.
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            LogErros.Registrar("Exceção não tratada (UI)", args.Exception);
            var snackbar = _host?.Services.GetService<Controls.ISnackbarService>();
            if (snackbar is not null)
                snackbar.Erro($"Ocorreu um erro inesperado: {args.Exception.Message}");
            else
                MessageBox.Show($"Ocorreu um erro inesperado:\n\n{args.Exception.Message}",
                    "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogErros.Registrar("Task não observada", args.Exception);
            args.SetObserved();
        };

        // Evita que o app se encerre ao fechar a janela de setup antes da janela principal abrir.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Atualização NA ABERTURA: havendo versão nova, baixa e reinicia já atualizado
        // (antes mesmo da conexão com o banco). Limite de 30s para nunca travar a
        // abertura com rede lenta — nesse caso o ciclo periódico assume depois.
        if (await UpdateService.AtualizarNaAberturaAsync(TimeSpan.FromSeconds(30)))
            return; // o Velopack encerra este processo e reabre o app atualizado

        // Loop: obter conexão (1º acesso ou salva) → conectar/migrar. Se falhar, oferecer reconfigurar.
        while (true)
        {
            var connectionString = ObterConexao();
            if (connectionString is null)
            {
                Shutdown();
                return;
            }

            try
            {
                _host = ConstruirHost(connectionString);
                using (var scope = _host.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();

                    // Migration pendente num banco que já tem dados: backup local ANTES de
                    // migrar. Se a migration falhar no meio, os dados estão na máquina.
                    var pendentes = await db.Database.GetPendingMigrationsAsync();
                    var aplicadas = await db.Database.GetAppliedMigrationsAsync();
                    if (pendentes.Any() && aplicadas.Any())
                        await BackupLocal.ExecutarPreMigracaoAsync(db);

                    await db.Database.MigrateAsync();

                    // Carrega os catálogos no cache em memória (nomes/famílias/bases).
                    await scope.ServiceProvider.GetRequiredService<ConvenioCatalogoService>().RecarregarCacheAsync();
                    await scope.ServiceProvider.GetRequiredService<ModalidadeCatalogoService>().RecarregarCacheAsync();
                    await scope.ServiceProvider.GetRequiredService<EspecialidadeCatalogoService>().RecarregarCacheAsync();

                    // Migração única: dados do prestador que viviam em JSON local
                    // (%APPDATA%) passam a ser configuração GLOBAL no banco.
                    var parametros = scope.ServiceProvider.GetRequiredService<ParametrosService>();
                    if (!await parametros.PrestadorConfiguradoAsync())
                    {
                        var local = PrestadorStore.Carregar();
                        if (!string.IsNullOrWhiteSpace(local.RazaoSocial) ||
                            !string.IsNullOrWhiteSpace(local.Cnpj) ||
                            local.CodigosTuss.Count > 0)
                            await parametros.SalvarPrestadorAsync(local);
                    }
                }
                break; // sucesso
            }
            catch (Exception ex)
            {
                LogErros.Registrar("Falha ao conectar/migrar o banco na abertura", ex);
                _host?.Dispose();
                _host = null;

                var reconfig = MessageBox.Show(
                    $"Não foi possível conectar ao banco de dados:\n\n{ex.Message}\n\nDeseja reconfigurar a conexão?",
                    "Erro de conexão", MessageBoxButton.YesNo, MessageBoxImage.Error);

                if (reconfig == MessageBoxResult.Yes)
                {
                    ConexaoStore.Limpar(); // força a tela de setup na próxima volta
                    continue;
                }

                // Modo contingência: sem banco (internet caiu / Neon fora do ar), mostra as
                // últimas pendências sincronizadas — a secretária ainda sabe o que faturar hoje.
                MostrarPendenciasOffline();
                Shutdown();
                return;
            }
        }

        var window = _host!.Services.GetRequiredService<MainWindow>();
        window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose; // volta ao comportamento normal
        window.Show();

        // Backup local diário em segundo plano (plano B se o banco na nuvem sumir).
        _ = BackupLocal.ExecutarSeNecessarioAsync(_host.Services);

        // Rodada de pendências vencida (aviso bloqueante) antes do aviso comum: se a rodada zerar
        // as pendências, o aviso a seguir já não tem o que mostrar.
        await MostrarRodadaSeVencidaAsync();

        // Aviso de baixas pendentes ao abrir + lembrete recorrente (a cada 2h).
        await MostrarAvisoPendenciasAsync();
        _lembreteTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(2) };
        _lembreteTimer.Tick += async (_, _) =>
        {
            await MostrarRodadaSeVencidaAsync();
            await MostrarAvisoPendenciasAsync();
        };
        _lembreteTimer.Start();

        // Ciclo periódico (2h): pega versões publicadas DURANTE o expediente, com o app
        // aberto. Baixa em segundo plano, avisa no snackbar e aplica ao fechar — na
        // próxima abertura o fluxo acima já reabre atualizado.
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(2) };
        _updateTimer.Tick += async (_, _) => await VerificarAtualizacaoAsync();
        _updateTimer.Start();
    }

    /// <summary>Checa/baixa atualização e avisa o usuário quando houver versão nova pronta.</summary>
    private async Task VerificarAtualizacaoAsync()
    {
        if (_host is null) return;

        try
        {
            var versao = await UpdateService.VerificarEBaixarAsync();
            if (versao is null) return;

            _updateTimer?.Stop(); // já há versão baixada aguardando; não precisa checar de novo
            var snackbar = _host.Services.GetRequiredService<Controls.ISnackbarService>();
            snackbar.Info($"Atualização {versao} baixada. Feche e reabra o sistema para aplicar.");
        }
        catch (Exception ex)
        {
            // Falha de rede na checagem periódica não pode derrubar o app; tenta no próximo ciclo.
            LogErros.Registrar("Checagem periódica de atualização", ex);
        }
    }

    /// <summary>Mostra a janela de aviso se houver baixas pendentes (usada na abertura e nos lembretes).</summary>
    private async Task MostrarAvisoPendenciasAsync()
    {
        if (_avisoAberto || _host is null) return;

        try
        {
            using var scope = _host.Services.CreateScope();
            var pendencias = scope.ServiceProvider.GetRequiredService<PendenciaService>();
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lista = await pendencias.CodigosPendentesAsync(hoje);

            // Espelho local para o modo contingência (banco/internet fora do ar).
            Configuracao.PendenciasSnapshot.Salvar(lista);

            if (lista.Count == 0) return;

            _avisoAberto = true;
            var aviso = new AvisoPendenciasWindow(lista) { Owner = MainWindow };
            aviso.ShowDialog();
        }
        catch (Exception ex)
        {
            // Um aviso que falha nunca deve derrubar o app.
            LogErros.Registrar("Aviso de baixas pendentes", ex);
        }
        finally
        {
            _avisoAberto = false;
        }
    }

    /// <summary>
    /// Se a rodada de pendências venceu e há guias sem decisão, abre a janela BLOQUEANTE de "rodar as
    /// pendências": a secretária precisa dar baixa ou justificar (não conformidade) antes de seguir.
    /// Ancora o ciclo no 1º uso para não bloquear logo na primeira abertura. Nunca derruba o app.
    /// </summary>
    private async Task MostrarRodadaSeVencidaAsync()
    {
        if (_rodadaAberta || _host is null) return;

        try
        {
            var scopeFactory = _host.Services.GetRequiredService<IServiceScopeFactory>();
            bool exigeDecisao;
            using (var scope = scopeFactory.CreateScope())
            {
                var rodada = scope.ServiceProvider.GetRequiredService<RodadaPendenciasService>();
                var hoje = DateOnly.FromDateTime(DateTime.Today);
                await rodada.GarantirAncoraAsync(hoje); // ancora no 1º uso (não bloqueia de cara)
                exigeDecisao = (await rodada.ObterStatusAsync(hoje)).ExigeDecisao;
            }
            if (!exigeDecisao) return;

            _rodadaAberta = true;
            await Alertas.RodadaPendenciasFluxo.ExecutarAsync(scopeFactory, MainWindow, bloqueante: true);
        }
        catch (Exception ex)
        {
            // Uma falha na rodada nunca deve impedir o uso do sistema.
            LogErros.Registrar("Rodada de pendências (abertura)", ex);
        }
        finally
        {
            _rodadaAberta = false;
        }
    }

    /// <summary>
    /// Sem conexão com o banco: mostra as últimas pendências salvas localmente (somente
    /// leitura), para o dia de trabalho não ficar às cegas. Nunca lança.
    /// </summary>
    private void MostrarPendenciasOffline()
    {
        try
        {
            var snapshot = Configuracao.PendenciasSnapshot.Carregar();
            if (snapshot is null || snapshot.Pendencias.Count == 0) return;

            MessageBox.Show(
                $"Sem conexão com o banco. Exibindo as pendências da última sincronização " +
                $"({snapshot.GeradoEm:dd/MM/yyyy HH:mm}), somente leitura — as baixas devem ser " +
                "registradas quando a conexão voltar.",
                "Modo contingência", MessageBoxButton.OK, MessageBoxImage.Information);

            new AvisoPendenciasWindow(snapshot.Pendencias).ShowDialog();
        }
        catch (Exception ex)
        {
            LogErros.Registrar("Pendências em modo contingência", ex);
        }
    }

    /// <summary>Fonte da conexão: env var → configuração salva → tela de primeiro acesso.</summary>
    private static string? ObterConexao()
    {
        var env = Environment.GetEnvironmentVariable("ConnectionStrings__Clinica");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        var salva = ConexaoStore.Carregar();
        if (!string.IsNullOrWhiteSpace(salva))
            return salva;

        var setup = new SetupWindow();
        return setup.ShowDialog() == true ? ConexaoStore.Carregar() : null;
    }

    private static IHost ConstruirHost(string connectionString) =>
        Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddClinica(connectionString);

                // Snackbar único do shell (instanciado na thread de UI ao resolver o MainViewModel).
                services.AddSingleton<Controls.SnackbarService>();
                services.AddSingleton<Controls.ISnackbarService>(sp => sp.GetRequiredService<Controls.SnackbarService>());
                services.AddSingleton<Controls.IDialogoService, Controls.DialogoService>();

                services.AddSingleton<MainViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<PacientesViewModel>();
                services.AddTransient<NovoAtendimentoViewModel>();
                services.AddTransient<BaixaViewModel>();
                services.AddTransient<RelatoriosViewModel>();
                services.AddTransient<FaturadosViewModel>();
                services.AddTransient<FichaPacienteViewModel>();
                services.AddTransient<AgendaViewModel>();
                services.AddTransient<ConsultasViewModel>();
                services.AddTransient<GlosasViewModel>();
                services.AddTransient<TissViewModel>();
                services.AddTransient<ConsultaGuiasViewModel>();
                services.AddTransient<ParametrosViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
