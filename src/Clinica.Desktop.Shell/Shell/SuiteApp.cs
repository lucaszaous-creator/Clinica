using System.Windows;
using Clinica.Application.Servicos;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Microsoft.Extensions.DependencyInjection;
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
    /// <param name="app">A instância de <see cref="System.Windows.Application"/> do executável.</param>
    /// <param name="nomeApp">Nome curto, usado nos títulos das caixas de mensagem ("Recepção").</param>
    /// <param name="titulo">Título da janela ("Recepção — Clínica SemDor").</param>
    /// <param name="modulos">
    /// Módulos a carregar, na ordem em que aparecem na sidebar. As MESMAS instâncias
    /// registram no DI e montam o menu.
    /// </param>
    public static async Task<IHost?> IniciarAsync(
        System.Windows.Application app, string nomeApp, string titulo, IReadOnlyList<IModuloApp> modulos)
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

        // Fechar a janela de setup não pode encerrar o app antes de a janela principal
        // existir — sem isto, o WPF derruba tudo ao fechar a última janela aberta.
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Laço: obter conexão (1º acesso ou salva) → conectar e migrar. Falhando, a
        // clínica pode reconfigurar sem reabrir o app — a senha do banco muda, o
        // servidor troca de endereço, e sair só com "erro" deixaria o posto parado.
        IHost host;
        var forcarSetup = false;
        while (true)
        {
            // Depois de uma falha, a tela é obrigatória: a conexão ruim pode ser a
            // herdada do Faturamento ou a da variável de ambiente — nenhuma das duas é
            // nossa para apagar, e reler a mesma daria um laço infinito de erro.
            var connectionString = forcarSetup ? null : ShellBootstrap.ObterConnectionString();

            // 1º acesso: sem conexão salva (nem própria nem herdada do Faturamento), a
            // clínica configura aqui mesmo. Antes desta tela, os apps da suíte só subiam
            // com o Faturamento instalado na mesma máquina.
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                if (new SetupWindow(nomeApp).ShowDialog() != true)
                {
                    app.Shutdown();
                    return null;
                }
                // Depois de gravar, a leitura normal vale de novo — o que a clínica
                // acabou de salvar tem precedência sobre a herança do Faturamento.
                forcarSetup = false;
                connectionString = ConexaoStore.Carregar();
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    // Salvou e sumiu: só acontece se a leitura de volta falhar, e o
                    // LogSuite já registrou o motivo na gravação.
                    MessageBox.Show(
                        "A conexão foi configurada, mas não pôde ser lida de volta.\n\n" +
                        "Veja a pasta de logs do aplicativo.",
                        nomeApp, MessageBoxButton.OK, MessageBoxImage.Error);
                    app.Shutdown();
                    return null;
                }
            }

            IHost? tentativa = null;
            try
            {
                tentativa = ShellBootstrap.ConstruirHost(connectionString, modulos);
                await ShellBootstrap.PrepararBancoAsync(tentativa.Services);
                host = tentativa;
                break;
            }
            catch (Exception ex)
            {
                // O host da tentativa que falhou segura conexões: descartar antes de
                // tentar de novo, senão cada erro deixa um pool para trás.
                tentativa?.Dispose();
                LogSuite.Registrar($"{nomeApp} — não foi possível preparar o banco", ex);

                var reconfigurar = MessageBox.Show(
                    $"Não foi possível conectar ao banco de dados:\n\n{ex.Message}\n\n" +
                    "Deseja informar outra conexão?",
                    nomeApp, MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes;

                if (!reconfigurar)
                {
                    app.Shutdown();
                    return null;
                }

                // Apaga a configuração DA SUÍTE (a do faturamento não é nossa para
                // apagar) e força a tela na volta do laço.
                ConexaoStore.Limpar();
                forcarSetup = true;
            }
        }

        // Login (parcela 5). Base sem nenhum usuário abre o PRIMEIRO ACESSO em vez de
        // pedir credencial que ninguém tem — a versão que introduz login não pode
        // trancar a clínica do lado de fora.
        if (!await AutenticarAsync(app, host, nomeApp))
            return null;

        var janela = new ShellWindow
        {
            DataContext = new ShellViewModel(titulo, modulos, host.Services)
        };
        app.MainWindow = janela;
        janela.Show();

        // A janela principal existe: fechar o app volta a ser fechar a janela. Sem isto
        // o processo ficaria vivo depois de a clínica fechar a janela.
        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        return host;
    }

    /// <summary>
    /// Pede o login e grava a sessão. Devolve false quando o usuário desistiu (e o
    /// encerramento já foi pedido).
    ///
    /// Falha ao CONSULTAR os usuários não tranca ninguém: o banco acabou de migrar com
    /// sucesso, então o problema é outro, e derrubar a abertura por causa dele deixaria
    /// o posto parado. A tela de login aparece assim mesmo — errar a senha é um
    /// problema menor do que não conseguir abrir o app.
    /// </summary>
    private static async Task<bool> AutenticarAsync(
        System.Windows.Application app, IHost host, string nomeApp)
    {
        var escopos = host.Services.GetRequiredService<IServiceScopeFactory>();

        bool primeiroAcesso;
        try
        {
            using var scope = escopos.CreateScope();
            var acesso = scope.ServiceProvider.GetRequiredService<AcessoService>();
            primeiroAcesso = !await acesso.ExisteUsuarioAtivoAsync();
        }
        catch (Exception ex)
        {
            LogSuite.Registrar($"{nomeApp} — não foi possível verificar os usuários cadastrados", ex);
            primeiroAcesso = false;
        }

        var login = new LoginWindow(escopos, nomeApp, primeiroAcesso);
        if (login.ShowDialog() != true || login.Usuario is null)
        {
            host.Dispose();
            app.Shutdown();
            return false;
        }

        host.Services.GetRequiredService<SessaoUsuario>().Entrar(login.Usuario);
        return true;
    }
}
