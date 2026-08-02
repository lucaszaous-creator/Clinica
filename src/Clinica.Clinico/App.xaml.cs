using System.Windows;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.Hosting;

namespace Clinica.Clinico;

/// <summary>
/// Executável do Consultório: uma casca fina sobre o shell, como a Recepção e o
/// Financeiro. Só escolhe qual módulo carregar — a subida (log, conexão, banco, login,
/// janela) é do <see cref="SuiteApp"/>.
///
/// Carrega UM módulo de propósito. A máquina do consultório não precisa da agenda do
/// balcão, do caixa nem do cadastro da equipe; instalar tudo em toda máquina é
/// exatamente o que a arquitetura multi-exe existe para evitar.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    private readonly IReadOnlyList<IModuloApp> _modulos =
    [
        new Modulo.ModuloClinico()
    ];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host = await SuiteApp.IniciarAsync(this, "Consultório", "Consultório — Clínica SemDor", _modulos);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
