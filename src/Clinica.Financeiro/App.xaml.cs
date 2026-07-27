using System.Windows;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.Hosting;

namespace Clinica.Financeiro;

/// <summary>
/// Executável do Financeiro: casca fina sobre o shell, igual à Recepção —
/// só muda a lista de módulos carregados.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    private readonly IReadOnlyList<IModuloApp> _modulos =
    [
        new Modulo.ModuloFinanceiro()
    ];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Ocorreu um erro inesperado:\n\n{args.Exception.Message}",
                "Financeiro", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        var connectionString = ShellBootstrap.ObterConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show(
                "Nenhuma conexão com o banco foi encontrada.\n\n" +
                "Abra o app de Faturamento nesta máquina e configure a conexão uma vez; " +
                "o Financeiro passa a usá-la automaticamente.",
                "Financeiro", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            _host = ShellBootstrap.ConstruirHost(connectionString, _modulos);
            await ShellBootstrap.PrepararBancoAsync(_host.Services);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível conectar ao banco de dados:\n\n{ex.Message}",
                "Financeiro", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var janela = new ShellWindow
        {
            DataContext = new ShellViewModel("Financeiro — Clínica SemDor", _modulos, _host.Services)
        };
        MainWindow = janela;
        janela.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
