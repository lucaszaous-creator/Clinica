using System.Windows;
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

                services.AddSingleton<MainViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<PacientesViewModel>();
                services.AddTransient<NovoAtendimentoViewModel>();
                services.AddTransient<BaixaViewModel>();
                services.AddTransient<RelatoriosViewModel>();
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
