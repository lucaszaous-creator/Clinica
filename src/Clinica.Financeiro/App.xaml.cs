using System.Windows;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
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
        var interfaceWeb = !e.Args.Contains("--financeiro-nativo", StringComparer.OrdinalIgnoreCase);
        _host = await SuiteApp.IniciarAsync(this, "Financeiro", "Financeiro — Clínica SemDor", _modulos,
            criarJanela: interfaceWeb ? CriarJanelaWeb : null);
    }

    private Window CriarJanelaWeb(IServiceProvider servicos)
    {
        if (!SessaoUsuario.Atual.Pode(Permissao.VerFinanceiro))
            return new ShellWindow { DataContext = new ShellViewModel("Financeiro — Clínica SemDor", _modulos, servicos) };
        ShellWindow? ferramentas = null;
        Web.FinanceiroWebWindow? principal = null;
        void AbrirFerramentas(string chave)
        {
            if (ferramentas is null)
            {
                ferramentas = new ShellWindow
                {
                    DataContext = new ShellViewModel("Financeiro — Clínica SemDor", _modulos, servicos),
                    Owner = principal,
                    WindowState = WindowState.Maximized
                };
                ferramentas.Closed += (_, _) => ferramentas = null;
            }
            ferramentas.Show();
            NavegacaoSuite.Ir(chave);
            ferramentas.Activate();
        }
        principal = new Web.FinanceiroWebWindow(servicos, AbrirFerramentas);
        return principal;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
