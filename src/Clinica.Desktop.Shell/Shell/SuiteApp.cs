using System.Windows;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.Hosting;

namespace Clinica.Desktop.Shell;

/// <summary>
/// A abertura de um app da suíte, inteira: log → rede de segurança de exceções →
/// conexão → host DI → migrations → janela.
///
/// Existe para que o <c>App.xaml.cs</c> de cada executável seja só a escolha do nome
/// e dos módulos. Com três cascas (Recepção, Financeiro, Gerente Geral) rodando a
/// mesma sequência, mantê-la copiada seria três lugares para corrigir a mesma coisa.
/// </summary>
public static class SuiteApp
{
    /// <summary>
    /// Sobe o app e devolve o host (para o <c>OnExit</c> descartar). Devolve
    /// <c>null</c> quando não há o que abrir: ou o Velopack está reiniciando o app
    /// numa versão nova, ou a abertura falhou — e aí a mensagem já foi mostrada e o
    /// encerramento já foi pedido.
    /// </summary>
    /// <param name="app">A instância de <see cref="Application"/> do executável.</param>
    /// <param name="nomeApp">Nome curto, usado nos títulos das caixas de mensagem ("Recepção").</param>
    /// <param name="titulo">Título da janela ("Recepção — Clínica SemDor").</param>
    /// <param name="modulos">
    /// Módulos a carregar, na ordem em que aparecem na sidebar. As MESMAS instâncias
    /// registram no DI e montam o menu.
    /// </param>
    public static async Task<IHost?> IniciarAsync(
        Application app, string nomeApp, string titulo, IReadOnlyList<IModuloApp> modulos)
    {
        // Degradação deliberada nas camadas sem UI também deixa rastro em arquivo.
        LogSuite.Instalar();

        // Erro não tratado nunca derruba o app (mesma rede de segurança do faturamento).
        app.DispatcherUnhandledException += (_, args) =>
        {
            LogSuite.Registrar($"{nomeApp} — erro não tratado", args.Exception);
            MessageBox.Show($"Ocorreu um erro inesperado:\n\n{args.Exception.Message}",
                nomeApp, MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        // Atualização na abertura: havendo versão nova, o app reinicia já atualizado
        // (antes de tocar no banco). O limite evita travar a abertura com rede lenta.
        if (await AtualizadorSuite.AtualizarNaAberturaAsync(TimeSpan.FromSeconds(30)))
            return null; // o Velopack encerra este processo e reabre o app atualizado

        var connectionString = ShellBootstrap.ObterConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // A suíte ainda não tem tela de setup própria: reaproveita a conexão já
            // configurada pelo Faturamento na mesma máquina (ver ConexaoStore).
            MessageBox.Show(
                "Nenhuma conexão com o banco foi encontrada.\n\n" +
                "Abra o app de Faturamento nesta máquina e configure a conexão uma vez; " +
                $"o {nomeApp} passa a usá-la automaticamente.",
                nomeApp, MessageBoxButton.OK, MessageBoxImage.Information);
            app.Shutdown();
            return null;
        }

        IHost host;
        try
        {
            host = ShellBootstrap.ConstruirHost(connectionString, modulos);
            await ShellBootstrap.PrepararBancoAsync(host.Services);
        }
        catch (Exception ex)
        {
            LogSuite.Registrar($"{nomeApp} — não foi possível preparar o banco", ex);
            MessageBox.Show($"Não foi possível conectar ao banco de dados:\n\n{ex.Message}",
                nomeApp, MessageBoxButton.OK, MessageBoxImage.Error);
            app.Shutdown();
            return null;
        }

        var janela = new ShellWindow
        {
            DataContext = new ShellViewModel(titulo, modulos, host.Services)
        };
        app.MainWindow = janela;
        janela.Show();
        return host;
    }
}
