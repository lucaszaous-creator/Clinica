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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Evita que o app se encerre ao fechar a janela de setup antes da janela principal abrir.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
                    await db.Database.MigrateAsync();
                }
                break; // sucesso
            }
            catch (Exception ex)
            {
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

                Shutdown();
                return;
            }
        }

        var window = _host!.Services.GetRequiredService<MainWindow>();
        window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose; // volta ao comportamento normal
        window.Show();

        // Aviso de baixas pendentes ao abrir + lembrete recorrente (a cada 2h).
        await MostrarAvisoPendenciasAsync();
        _lembreteTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(2) };
        _lembreteTimer.Tick += async (_, _) => await MostrarAvisoPendenciasAsync();
        _lembreteTimer.Start();

        // Auto-update: verifica ao abrir e a cada 2h (o app fica aberto o expediente
        // inteiro; sem a checagem periódica, uma versão nova só chegaria após dois
        // reinícios). Quando baixar, avisa no snackbar — aplica ao fechar o app.
        _ = VerificarAtualizacaoAsync();
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(2) };
        _updateTimer.Tick += async (_, _) => await VerificarAtualizacaoAsync();
        _updateTimer.Start();
    }

    /// <summary>Checa/baixa atualização e avisa o usuário quando houver versão nova pronta.</summary>
    private async Task VerificarAtualizacaoAsync()
    {
        if (_host is null) return;

        var versao = await UpdateService.VerificarEBaixarAsync();
        if (versao is null) return;

        _updateTimer?.Stop(); // já há versão baixada aguardando; não precisa checar de novo
        var snackbar = _host.Services.GetRequiredService<Controls.ISnackbarService>();
        snackbar.Info($"Atualização {versao} baixada. Feche e reabra o sistema para aplicar.");
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
            if (lista.Count == 0) return;

            _avisoAberto = true;
            var aviso = new AvisoPendenciasWindow(lista) { Owner = MainWindow };
            aviso.ShowDialog();
        }
        catch
        {
            // Um aviso que falha nunca deve derrubar o app.
        }
        finally
        {
            _avisoAberto = false;
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
