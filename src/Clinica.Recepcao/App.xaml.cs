using System.Windows;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Clinica.Recepcao;

/// <summary>
/// Executável da Recepção: uma casca fina sobre o shell. Escolhe os módulos,
/// obtém a conexão, prepara o banco e mostra a janela.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>
    /// Módulos carregados por este executável. Instância única: o shell monta a sidebar
    /// a partir destes mesmos objetos que registraram os serviços no DI.
    /// </summary>
    private readonly IReadOnlyList<IModuloApp> _modulos =
    [
        new Modulo.ModuloRecepcao()
    ];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Erro não tratado nunca derruba o app (mesmo padrão do faturamento).
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"Ocorreu um erro inesperado:\n\n{args.Exception.Message}",
                "Recepção", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        var connectionString = ShellBootstrap.ObterConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fase 1: a Recepção ainda não tem tela de setup própria — ela reaproveita a
            // conexão já configurada pelo app de Faturamento na mesma máquina.
            MessageBox.Show(
                "Nenhuma conexão com o banco foi encontrada.\n\n" +
                "Abra o app de Faturamento nesta máquina e configure a conexão uma vez; " +
                "a Recepção passa a usá-la automaticamente.",
                "Recepção", MessageBoxButton.OK, MessageBoxImage.Information);
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
                "Recepção", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var janela = new ShellWindow
        {
            DataContext = new ShellViewModel("Recepção — Clínica SemDor", _modulos, _host.Services)
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
