using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Clinica.Recepcao;

/// <summary>
/// Executável da Recepção: uma casca fina sobre o shell. Só escolhe quais módulos
/// carregar — a subida (log, conexão, banco, janela) é do <see cref="SuiteApp"/>.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    /// <summary>
    /// Módulos carregados por este executável. Instância única: o shell monta a sidebar
    /// a partir destes mesmos objetos que registraram os serviços no DI.
    /// </summary>
    private readonly IReadOnlyList<IModuloApp> _modulos =
    [
        new Modulo.ModuloRecepcao(),
        new ModuloContextual(new Clinica.Clinico.Modulo.ModuloClinico())
    ];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host = await SuiteApp.IniciarAsync(this, "Recepção", "Recepção — Clínica SemDor", _modulos);

        if (_host is not null)
        {
            // Nada disto é o que a pessoa veio ver: espera a tela inicial ter os dados
            // (AberturaDoApp, out/2026 — disputava as conexões da abertura com ela).
            await Clinica.Application.AberturaDoApp.DepoisDaPrimeiraTelaAsync();
            RelogioDoSms.Iniciar(_host, "Recepção");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
