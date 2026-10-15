using System.Windows;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.Hosting;

namespace Clinica.Financeiro;

/// <summary>
/// Executável do Financeiro: casca fina sobre o shell, igual à Recepção —
/// só muda a lista de módulos carregados.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    private readonly IReadOnlyList<IModuloApp> _modulos =
    [
        new Modulo.ModuloFinanceiro(),
        new ModuloContextual(new Clinica.Recepcao.Modulo.ModuloRecepcao()),
        new ModuloContextual(new Clinica.Clinico.Modulo.ModuloClinico())
    ];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // A janela é a do shell, com as telas WPF do Financeiro (out/2026): a interface web
        // (WebView2) saiu do produto — a área preta ao abrir cada tela era o navegador antes
        // de pintar, e as telas nativas são as mesmas que o Gerente sempre usou.
        _host = await SuiteApp.IniciarAsync(this, "Financeiro", "Financeiro — Clínica SemDor", _modulos);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
