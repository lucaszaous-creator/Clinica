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
        _host = await SuiteApp.IniciarAsync(this, "Financeiro", "Financeiro — Clínica SemDor", _modulos);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
