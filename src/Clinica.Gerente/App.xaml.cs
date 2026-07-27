using System.Windows;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.Hosting;
using ModuloFinanceiro = Clinica.Financeiro.Modulo.ModuloFinanceiro;
using ModuloRecepcao = Clinica.Recepcao.Modulo.ModuloRecepcao;

namespace Clinica.Gerente;

/// <summary>
/// Executável do Gerente Geral: a mesma casca da Recepção e do Financeiro, com a
/// lista de módulos completa. É o que a arquitetura de casca fina prometia — o app
/// que "engloba tudo" não tem nenhuma tela própria nem nenhuma cópia.
///
/// A ordem da lista é a ordem dos grupos na sidebar.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    private readonly IReadOnlyList<IModuloApp> _modulos =
    [
        new ModuloRecepcao(),
        new ModuloFinanceiro()
        // Fase 4: o módulo de Faturamento entra aqui.
    ];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host = await SuiteApp.IniciarAsync(this, "Gerente Geral", "Gerente Geral — Clínica SemDor", _modulos);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
