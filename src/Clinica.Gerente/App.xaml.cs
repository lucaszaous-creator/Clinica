using System.Windows;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.Hosting;
using ModuloFinanceiro = Clinica.Financeiro.Modulo.ModuloFinanceiro;
using ModuloGerente = Clinica.Gerente.Modulo.ModuloGerente;
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
        new ModuloFinanceiro(),
        // Último de propósito: as telas da direção são as menos usadas no dia a dia, e
        // a sidebar é lida de cima para baixo na ordem do trabalho.
        new ModuloGerente()
        // A Fase 4 (faturamento como módulo) foi CANCELADA: o Gerente lê o faturamento
        // pela tela própria do ModuloGerente, sem encostar no app em produção.
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
