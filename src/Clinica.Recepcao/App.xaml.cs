using System.Windows;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.Hosting;

namespace Clinica.Recepcao;

/// <summary>
/// Executável da Recepção: uma casca fina sobre o shell. Só escolhe quais módulos
/// carregar — a subida (log, conexão, banco, janela) é do <see cref="SuiteApp"/>.
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
        _host = await SuiteApp.IniciarAsync(this, "Recepção", "Recepção — Clínica SemDor", _modulos);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
